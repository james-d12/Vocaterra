using Vocaterra.Game;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>The noun catalogue (<c>game/nouns.json</c>): every tile is a noun or colourless, every noun has a lexicon reading and a gender.</summary>
public sealed class NounCatalogTests
{
    [Fact]
    public void EveryTileKindIsANounOrColourlessAndEveryNounHasANounReadingAndAGender()
    {
        var catalogue = TestGame.Nouns;
        foreach (var definition in TileDefinition.All)
        {
            var noun = catalogue.ForTile(definition.Id);
            var colourless = catalogue.IsColourless(definition.Id);
            Assert.True(noun is not null || colourless, $"Tile {definition.Name} is neither a catalogue noun nor colourless.");
            Assert.False(noun is not null && colourless, $"Tile {definition.Name} is both a noun and colourless.");
        }

        foreach (var noun in catalogue.Nouns)
        {
            var readings = TestGame.Bundle.Lexicon.EntriesForLemma(noun.Lemma);
            Assert.Contains(readings, e => e.Pos == "noun");
            Assert.Contains(noun.Gender, new[] { "m", "f" });
        }
    }

    [Fact]
    public void EveryItemKindIsNamedByANounWithAGenderAndANounReading()
    {
        foreach (var definition in ItemDefinition.All)
        {
            var noun = TestGame.Nouns.ForItem(definition.Id);
            Assert.True(noun is not null, $"Item {definition.Name} has no catalogue noun.");
            Assert.Contains(noun.Gender, new[] { "m", "f" });
            Assert.Contains(TestGame.Bundle.Lexicon.EntriesForLemma(noun.Lemma), e => e.Pos == "noun");
        }

        Assert.All(TestGame.Nouns.Nouns.SelectMany(static n => n.Items), item => Assert.NotNull(TestGame.Nouns.ForItem(item)));
    }

    [Theory]
    [InlineData("madera", true)]
    [InlineData("piedra", true)]
    [InlineData("tierra", true)]
    [InlineData("arena", true)]
    [InlineData("flor", false)]
    [InlineData("árbol", false)]
    public void MassNounsAreMarkedAsSuch(string lemma, bool mass)
    {
        Assert.Equal(mass, TestGame.Nouns.ForLemma(lemma)!.Mass);
    }

    [Fact]
    public void WoodComesFromTrees()
    {
        Assert.Equal(["árbol"], TestGame.Nouns.ForLemma("madera")!.Sources);
        Assert.Empty(TestGame.Nouns.ForLemma("piedra")!.Sources);
    }

    [Fact]
    public void AnItemNamedTwiceOrUnknownIsRefused()
    {
        var twice = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","tiles":["stone"],"items":["stone"]},{"lemma":"madera","tiles":["planks"],"items":["stone"]}],"colourless":["air"]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, twice));

        var unknown = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","tiles":["stone"],"items":["rock"]}],"colourless":["air"]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, unknown));
    }

    [Fact]
    public void ASourceThatIsNotACatalogueNounIsRefused()
    {
        var json = """{"schemaVersion":1,"nouns":[{"lemma":"madera","tiles":["planks"],"items":["wood"],"sources":["cueva"]}],"colourless":["air"]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, json));
    }

    [Fact]
    public void EveryTileNamedInTheFileIsTheTileKindTheSimulationDefines()
    {
        foreach (var noun in TestGame.Nouns.Nouns)
        {
            foreach (var tile in noun.Tiles)
            {
                Assert.Equal(tile, TileDefinition.Get(tile).Id);
            }
        }

        Assert.Equal([TileId.Air], TestGame.Nouns.Colourless);
    }

    [Theory]
    [InlineData(ItemId.Wood, "madera")]
    [InlineData(ItemId.Sand, "arena")]
    [InlineData(ItemId.Mushroom, "seta")]
    [InlineData(ItemId.Axe, "hacha")]
    [InlineData(ItemId.Pickaxe, "pico")]
    [InlineData(ItemId.Chest, "caja")]
    public void EachItemIsNamedByItsNoun(ItemId item, string lemma)
    {
        Assert.Equal(lemma, TestGame.Nouns.ForItem(item)?.Lemma);
    }

    [Theory]
    [InlineData("arena", TileId.Sand)]
    [InlineData("flor", TileId.Flower)]
    [InlineData("seta", TileId.Mushroom)]
    [InlineData("madera", TileId.Planks)]
    [InlineData("puerta", TileId.Door)]
    [InlineData("mesa", TileId.Table)]
    [InlineData("silla", TileId.Chair)]
    [InlineData("cama", TileId.Bed)]
    [InlineData("caja", TileId.Chest)]
    public void TheNewNounsAndTheirTilesAgreeInBothDirections(string lemma, TileId tile)
    {
        Assert.Equal([tile], WorldFacts.TilesOfNoun(lemma));
        Assert.Equal(lemma, WorldFacts.NounOfTile(tile));
    }

    [Fact]
    public void MaderaComesFromTheTreesAndTheMassNounsAreCountedInBlocks()
    {
        Assert.True(TestGame.Nouns.ForLemma("arena")!.Mass);
        Assert.True(TestGame.Nouns.ForLemma("madera")!.Mass);
        Assert.False(TestGame.Nouns.ForLemma("flor")!.Mass);
        Assert.Equal(["árbol"], TestGame.Nouns.ForLemma("madera")!.Sources);
    }

    [Theory]
    [InlineData("árbol", "m", "el", "un", "árboles")]
    [InlineData("piedra", "f", "la", "una", "piedras")]
    [InlineData("tierra", "f", "la", "una", "tierras")]
    [InlineData("hierba", "f", "la", "una", "hierbas")]
    public void EachNounHasItsGenderArticlesAndPluralFromTheCore(string lemma, string gender, string definite, string indefinite, string plural)
    {
        var noun = TestGame.Nouns.ForLemma(lemma);

        Assert.NotNull(noun);
        Assert.Equal(gender, noun.Gender);
        Assert.Equal(definite, noun.Definite);
        Assert.Equal(indefinite, noun.Indefinite);
        Assert.Equal(plural, noun.Plural);
        Assert.Equal($"{definite} {lemma}", noun.WithDefinite);
        Assert.Equal($"{indefinite} {lemma}", noun.WithIndefinite);
    }

    [Fact]
    public void TheWorldFactsNounOfEachTileIsTheCataloguesNounOfThatTile()
    {
        foreach (var definition in TileDefinition.All.Where(static d => d.Id != TileId.Air))
        {
            var fromFacts = WorldFacts.NounOfTile(definition.Id);
            Assert.Equal(fromFacts, TestGame.Nouns.ForTile(definition.Id)?.Lemma);
        }

        Assert.Null(WorldFacts.NounOfTile(TileId.Air));
    }

    [Fact]
    public void AnAccentlessOrPluralFormReadsAsTheCatalogueNoun()
    {
        Assert.Contains("árbol", TestGame.Nouns.CatalogueNounsOf("arbol"));
        Assert.Contains("árbol", TestGame.Nouns.CatalogueNounsOf("árboles"));
        Assert.Empty(TestGame.Nouns.CatalogueNounsOf("es"));
        Assert.Empty(TestGame.Nouns.CatalogueNounsOf("la"));
        Assert.Empty(TestGame.Nouns.CatalogueNounsOf(string.Empty));
    }

    [Fact]
    public void AnUnknownTileNameIsRefused()
    {
        var json = """{"schemaVersion":1,"nouns":[{"lemma":"árbol","tiles":["oak"],"items":[]}],"colourless":["air"]}""";

        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, json));
    }

    [Fact]
    public void ANounWithoutANounReadingIsRefused()
    {
        var json = """{"schemaVersion":1,"nouns":[{"lemma":"correr","tiles":["dirt"],"items":[]}],"colourless":["air"]}""";

        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, json));
    }

    [Fact]
    public void ATileListedTwiceIsRefused()
    {
        var json = """{"schemaVersion":1,"nouns":[{"lemma":"árbol","tiles":["trunk"],"items":[]},{"lemma":"piedra","tiles":["trunk"],"items":[]}],"colourless":["air"]}""";

        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, json));
    }

    [Fact]
    public void TheCatalogueIsReadFromTheBundleFolderAfterTheLoad()
    {
        // CR-007 workaround: the 0.1.0 loader does not parse game/nouns.json, so the game reads it beside the loaded bundle.
        var directory = GameBundle.ResolveDirectory();
        var catalogue = NounCatalog.Load(TestGame.Bundle, directory);

        Assert.Equal(TestGame.Nouns.Nouns.Select(static n => n.Lemma), catalogue.Nouns.Select(static n => n.Lemma));
        Assert.True(File.Exists(Path.Combine(directory, NounCatalog.RelativePath)));
    }
}
