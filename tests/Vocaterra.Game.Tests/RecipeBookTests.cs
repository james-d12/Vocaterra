using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>The bundle's <c>game/recipes.json</c>: it parses from the manifest-verified bytes, names only catalogue nouns and placeable stations, and feeds the simulation.</summary>
public sealed class RecipeBookTests
{
    private static RecipeBook Book => RecipeBook.Load(TestGame.Bundle, TestGame.Nouns);

    [Fact]
    public void TheFileIsInTheManifestAndReadsThroughTheHostFileCall()
    {
        Assert.True(TestGame.Bundle.TryReadHostFile(RecipeBook.RelativePath, out var bytes));
        Assert.True(bytes.Length > 0);
        Assert.Equal(9, Book.Entries.Count);
    }

    [Fact]
    public void EveryOutputAndIngredientIsACatalogueNounWithAnItem()
    {
        foreach (var entry in Book.Entries)
        {
            Assert.Same(TestGame.Nouns.ForLemma(entry.Output.Lemma), entry.Output);
            Assert.NotEmpty(entry.Output.Items);
            Assert.NotEmpty(entry.Ingredients);
            foreach (var ingredient in entry.Ingredients)
            {
                Assert.Same(TestGame.Nouns.ForLemma(ingredient.Noun.Lemma), ingredient.Noun);
                Assert.Contains(ingredient.Item, ingredient.Noun.Items);
                Assert.True(ingredient.Count > 0);
            }
        }
    }

    [Fact]
    public void EveryStationIsAPlaceableTileAndAStationIsItselfMadeBySomeRecipeOrHand()
    {
        foreach (var entry in Book.Entries.Where(static e => e.Station is not null))
        {
            var tile = entry.Rule.Station!.Value;
            Assert.Contains(entry.Station!.Tiles, t => t == tile);
            Assert.Contains(ItemDefinition.All, i => i.PlacesTile == tile);
        }

        // The mesa is made by hand, the horno at the mesa: a station is reachable from nothing.
        Assert.Null(Book.ForOutput("mesa")!.Station);
        Assert.Equal("mesa", Book.ForOutput("horno")!.Station!.Lemma);
    }

    [Fact]
    public void TheTableOfSection43IsInTheFile()
    {
        string Describe(string lemma)
        {
            var entry = Book.ForOutput(lemma)!;
            return $"{lemma}<-{string.Join('+', entry.Ingredients.Select(i => $"{i.Count} {i.Noun.Lemma}"))}@{entry.Station?.Lemma ?? "mano"}";
        }

        Assert.Equal("mesa<-10 madera@mano", Describe("mesa"));
        Assert.Equal("puerta<-6 madera@mesa", Describe("puerta"));
        Assert.Equal("silla<-4 madera@mesa", Describe("silla"));
        Assert.Equal("cama<-15 madera@mesa", Describe("cama"));
        Assert.Equal("caja<-8 madera@mesa", Describe("caja"));
        Assert.Equal("espada<-2 madera+6 piedra@mesa", Describe("espada"));
        Assert.Equal("horno<-20 piedra+4 madera@mesa", Describe("horno"));
        Assert.Equal("cristal<-2 arena@horno", Describe("cristal"));
        Assert.Equal("ladrillo<-2 piedra@horno", Describe("ladrillo"));
    }

    [Fact]
    public void TheRulesAreThePlainValuesTheSimulationCraftsWith()
    {
        var book = Book;
        Assert.Equal(book.Entries.Count, book.Rules.Count);
        var session = TestGame.NewSession();
        Assert.Equal(book.Rules.Count, session.Simulation.Recipes.Count);
        for (var i = 0; i < book.Rules.Count; i++)
        {
            Assert.Equal(book.Rules[i].Output, session.Simulation.Recipes[i].Output);
            Assert.Equal(book.Rules[i].Station, session.Simulation.Recipes[i].Station);
            Assert.Equal(book.Rules[i].Ingredients, session.Simulation.Recipes[i].Ingredients);
        }

        session.Simulation.GrantItems(ItemId.Wood, 10);
        Assert.Equal(CraftResult.Done, session.Simulation.Craft(book.IndexOf("mesa")));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Table));
    }

    [Fact]
    public void EveryMaterialHasAtLeastOneSourceThatIsACatalogueNounWithATileOrTheTreeForWood()
    {
        foreach (var lemma in new[] { "madera", "piedra", "arena", "tierra", "flor", "seta" })
        {
            var noun = TestGame.Nouns.ForLemma(lemma)!;
            Assert.NotEmpty(noun.Sources);
            foreach (var source in noun.Sources)
            {
                Assert.NotEmpty(WorldFacts.TilesOfNoun(source));
            }
        }

        Assert.Equal(["árbol"], TestGame.Nouns.ForLemma("madera")!.Sources);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"nube","count":1,"ingredients":[{"item":"madera","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[{"item":"nube","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[{"item":"madera","count":1}],"station":"árbol"}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[{"item":"madera","count":1}],"station":"hierba"}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[{"item":"madera","count":0}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":0,"ingredients":[{"item":"madera","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"mesa","count":1,"ingredients":[{"item":"madera","count":1}],"station":null},{"output":"mesa","count":1,"ingredients":[{"item":"madera","count":2}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":2,"recipes":[]}""")]
    public void AMalformedFileIsRefused(string json)
    {
        Assert.Throws<InvalidDataException>(() => RecipeBook.Parse(json, TestGame.Nouns));
    }

    [Fact]
    public void ANounWithNoRecipeHasNoIndex()
    {
        Assert.Equal(-1, Book.IndexOf("árbol"));
        Assert.Null(Book.ForOutput("hacha"));
        Assert.Equal(0, Book.IndexOf("mesa"));
    }
}
