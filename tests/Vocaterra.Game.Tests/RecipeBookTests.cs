using Vocaterra.Game.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>The bundle's <c>game/recipes.json</c>: it parses from the manifest-verified bytes, names only catalogue nouns and blocks for the stations, and feeds the simulation.</summary>
public sealed class RecipeBookTests
{
    private static RecipeBook Book => RecipeBook.Load(TestGame.Bundle, TestGame.Nouns);

    [Fact]
    public void TheFileIsInTheManifestAndHoldsTheWorkbenchRecipeOfThePrototype()
    {
        Assert.True(TestGame.Bundle.TryReadHostFile(RecipeBook.RelativePath, out var bytes));
        Assert.True(bytes.Length > 0);
        Assert.Single(Book.Entries);
        Assert.Equal("antorcha", Book.Entries[0].Output.Lemma);
        Assert.Equal(1, Book.Entries[0].OutputCount);
        Assert.Equal("mesa", Book.Entries[0].Station!.Lemma);
        Assert.Equal([("madera", 2)], Book.Entries[0].Ingredients.Select(static i => (i.Noun.Lemma, i.Count)).ToArray());
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
    public void TheStationIsTheWorkbenchBlockAndTheRulesAreTheSimulationsValues()
    {
        var book = Book;
        Assert.Equal(BlockId.Table, book.Rules[0].Station);
        Assert.Equal(book.Entries.Count, book.Rules.Count);

        var session = TestGame.NewSession();
        Assert.Equal(book.Rules.Count, session.Simulation.Recipes.Count);
        Assert.Equal(book.Rules[0].Station, session.Simulation.Recipes[0].Station);
        Assert.Equal(book.Rules[0].Ingredients, session.Simulation.Recipes[0].Ingredients);
        Assert.Equal(0, book.IndexOf("antorcha"));
    }

    [Fact]
    public void TheMaterialsOfTheRecipesHaveTheirSources()
    {
        Assert.Equal(["árbol"], TestGame.Nouns.ForLemma("madera")!.Sources);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"nube","count":1,"ingredients":[{"item":"madera","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"antorcha","count":1,"ingredients":[{"item":"nube","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"antorcha","count":1,"ingredients":[],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"antorcha","count":1,"ingredients":[{"item":"madera","count":0}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"antorcha","count":0,"ingredients":[{"item":"madera","count":1}],"station":null}]}""")]
    [InlineData("""{"schemaVersion":1,"recipes":[{"output":"antorcha","count":1,"ingredients":[{"item":"madera","count":1}],"station":null},{"output":"antorcha","count":1,"ingredients":[{"item":"madera","count":2}],"station":null}]}""")]
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
        Assert.Equal(0, Book.IndexOf("antorcha"));
    }
}
