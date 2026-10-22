using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>The noun catalogue of the 3D world: which block and item kinds name which Spanish noun, and each noun's gender and articles.</summary>
public sealed class NounCatalogTests
{
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
    [InlineData("árbol", "el", "un")]
    [InlineData("madera", "la", "una")]
    [InlineData("fruta", "la", "una")]
    [InlineData("antorcha", "la", "una")]
    [InlineData("mesa", "la", "una")]
    [InlineData("río", "el", "un")]
    [InlineData("agua", "el", "un")]
    public void TheArticlesComeFromTheLexicon(string lemma, string definite, string indefinite)
    {
        var noun = TestGame.Nouns.ForLemma(lemma);
        Assert.NotNull(noun);
        Assert.Equal(definite, noun.Definite);
        Assert.Equal(indefinite, noun.Indefinite);
    }

    [Theory]
    [InlineData("madera", true)]
    [InlineData("piedra", true)]
    [InlineData("tierra", true)]
    [InlineData("arena", true)]
    [InlineData("agua", true)]
    [InlineData("fruta", false)]
    [InlineData("árbol", false)]
    public void MassNounsAreMarkedAsSuch(string lemma, bool mass)
    {
        Assert.Equal(mass, TestGame.Nouns.ForLemma(lemma)!.Mass);
    }

    [Fact]
    public void WoodComesFromTreesAndAMaterialComesFromItself()
    {
        Assert.Equal(["árbol"], TestGame.Nouns.ForLemma("madera")!.Sources);
        Assert.Equal(["piedra"], TestGame.Nouns.ForLemma("piedra")!.Sources);
    }

    [Fact]
    public void TheRouteNounsNameTheirBlocksAndItems()
    {
        Assert.Equal([BlockId.Trunk, BlockId.Leaves], TestGame.Nouns.ForLemma("árbol")!.Blocks);
        Assert.Equal([ItemId.Wood], TestGame.Nouns.ForLemma("madera")!.Items);
        Assert.Equal([BlockId.BerryBush], TestGame.Nouns.ForLemma("fruta")!.Blocks);
        Assert.Equal([ItemId.Berry], TestGame.Nouns.ForLemma("fruta")!.Items);
        Assert.Equal([BlockId.Table], TestGame.Nouns.ForLemma("mesa")!.Blocks);
        Assert.Equal([BlockId.Torch], TestGame.Nouns.ForLemma("antorcha")!.Blocks);
        Assert.Equal([ItemId.Torch], TestGame.Nouns.ForLemma("antorcha")!.Items);
        Assert.Equal([BlockId.Water], TestGame.Nouns.ForLemma("agua")!.Blocks);
        Assert.Equal([BlockId.Water], TestGame.Nouns.ForLemma("río")!.Blocks);
        Assert.Equal([BlockId.Bridge], TestGame.Nouns.ForLemma("puente")!.Blocks);
        Assert.Equal([BlockId.Stall, BlockId.Roof], TestGame.Nouns.ForLemma("tienda")!.Blocks);
    }

    [Fact]
    public void ABlockWithTwoNounsIsNamedByTheFirstInLemmaOrder()
    {
        // The water is agua and río; agua comes first in ordinal order, so the guide's name for a water block is agua.
        Assert.Equal("agua", TestGame.Nouns.ForBlock(BlockId.Water)!.Lemma);
        Assert.Equal(["agua", "río"], TestGame.Nouns.NounsOfBlock(BlockId.Water).Select(static n => n.Lemma).ToArray());
    }

    [Fact]
    public void AnItemNamedTwiceOrAnUnknownItemOrBlockIsRefused()
    {
        var twice = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","items":["wood"]},{"lemma":"madera","items":["wood"]}]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, twice));

        var unknownItem = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","items":["rock"]}]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, unknownItem));

        var unknownBlock = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","items":[],"blocks":["rock"]}]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, unknownBlock));
    }

    [Fact]
    public void ASourceThatIsNotACatalogueNounIsRefused()
    {
        var json = """{"schemaVersion":1,"nouns":[{"lemma":"madera","items":["wood"],"sources":["cueva"]}]}""";
        Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, json));
    }

    [Fact]
    public void TheCatalogueHoldsOnlyTheNounsOfThe3DWorld()
    {
        // The 2D nouns (the ores, the tools, the furniture and the flowers) are gone with the 2D slice (S4).
        foreach (var lemma in new[] { "carbón", "hierro", "hacha", "silla", "horno", "flor", "seta" })
        {
            Assert.Null(TestGame.Nouns.ForLemma(lemma));
        }

        Assert.Equal(16, TestGame.Nouns.Nouns.Count);
    }
}
