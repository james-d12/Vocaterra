using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The durability data (I07): how a row is read and refused, what the bundle ships, and what the validator says.</summary>
public sealed class DurabilityDataTests
{
    private static string Row(string durability, int stack = 1) =>
        $$"""{ "schemaVersion": 1, "items": [ { "name": "wood", "id": 0, "stack": 99, "icon": "wood", "tags": [] }, { "name": "torch", "id": 21, "stack": 99, "icon": "torch", "tags": [] }, { "name": "berry", "id": 25, "stack": 99, "icon": "b", "tags": [] }, { "name": "bottle", "id": 26, "stack": 99, "icon": "b", "tags": [] }, { "name": "water-bottle", "id": 27, "stack": 99, "icon": "b", "tags": [] }, { "name": "book", "id": 29, "stack": 1, "icon": "b", "tags": [] }, { "name": "coin", "id": 30, "stack": 99, "icon": "b", "tags": [] }, { "name": "stake", "id": 35, "stack": 1, "icon": "b", "tags": [] }, { "name": "camp-kit", "id": 36, "stack": 1, "icon": "b", "tags": [] }, { "name": "pick", "id": 150, "stack": {{stack}}, "icon": "axe", "tags": ["tool"], "durability": {{durability}} } ] }""";

    [Fact]
    public void Given_ADurabilityRow_When_ItIsParsed_Then_ItHasItsMaxItsSourceAndItsRate()
    {
        var pick = ItemCatalogue.Parse(Row("""{ "max": 40, "source": "use", "rate": 2 }""")).Find("pick")!;

        Assert.Equal(new DurabilitySpec(40, "use", 2), pick.Durability);
        Assert.False(pick.IsWornOut(39));
        Assert.True(pick.IsWornOut(40));
    }

    [Fact]
    public void Given_ADurabilityWithNoRate_When_ItIsParsed_Then_TheRateIsOne() =>
        Assert.Equal(1, ItemCatalogue.Parse(Row("""{ "max": 40, "source": "time" }""")).Find("pick")!.Durability!.Rate);

    [Theory]
    [InlineData("""{ "max": 0, "source": "use" }""")]
    [InlineData("""{ "max": 200000, "source": "use" }""")]
    [InlineData("""{ "max": 10, "source": "use", "rate": 0 }""")]
    [InlineData("""{ "max": 10, "source": "use", "rate": 11 }""")]
    [InlineData("""{ "max": 10, "source": "Use" }""")]
    [InlineData("""{ "source": "use" }""")]
    [InlineData("""{ "max": 10 }""")]
    public void Given_ABadDurability_When_ItIsParsed_Then_TheCatalogueRefusesIt(string durability) =>
        _ = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(Row(durability)));

    [Fact]
    public void Given_ADurableItemThatStacks_When_ItIsParsed_Then_TheCatalogueRefusesIt() =>
        _ = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(Row("""{ "max": 10, "source": "use" }""", stack: 5)));

    [Fact]
    public void Given_AnItemWithNoDurability_When_ItIsAsked_Then_ItNeverWearsOut()
    {
        var apple = TestItems.Catalogue.Get(TestItems.Apple);

        Assert.Null(apple.Durability);
        Assert.False(apple.IsWornOut(int.MaxValue));
    }

    [Fact]
    public void Given_TheShippedBundle_When_TheWearSourcesAreChecked_Then_NoProblemIsFound() =>
        Assert.Empty(WearValidator.Validate(TestGame.Data.Items, TestGame.Data.Recipes, WearSources.Standard()));

    [Fact]
    public void Given_ASourceTheCodeLacks_When_TheValidatorRuns_Then_ItNamesTheItem()
    {
        var sources = new WearSources();
        sources.Add(new UseWearSource());

        var problems = WearValidator.Validate(TestGame.Data.Items, TestGame.Data.Recipes, sources);

        Assert.Contains(problems, static p => p.Contains("'shirt' wears by the source 'time'", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, static p => p.Contains("'axe'", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_TheShippedGear_When_TheNumbersAreRead_Then_TheyAreTheOnesProposed()
    {
        var shipped = TestItems.Catalogue;
        (string Source, int Max) Of(string name) => (shipped.Find(name)!.Durability!.Source, shipped.Find(name)!.Durability!.Max);

        Assert.Equal(("use", 200), Of("axe"));
        foreach (var clothes in new[] { "hat", "cap", "shirt", "jacket", "coat", "trousers", "shoes" })
        {
            Assert.Equal(("time", 240), Of(clothes)); // ten in-game days worn
        }

        foreach (var accessory in new[] { "ring", "bracelet", "belt", "necklace" })
        {
            Assert.Equal(("time", 480), Of(accessory));
        }

        Assert.Equal(("time", 720), Of("amulet"));
        Assert.Equal(("time", 480), Of("sack"));
        Assert.Equal(("time", 720), Of("backpack"));
        Assert.Equal(("time", 240), Of("lantern"));
        Assert.Equal(("damage", 150), Of("helmet"));
        Assert.Equal(("damage", 300), Of("cuirass"));
        Assert.Equal(("damage", 200), Of("greaves"));
        Assert.Equal(("damage", 150), Of("boots"));
        Assert.Equal(("damage", 250), Of("shield"));
    }

    [Fact]
    public void Given_TheTorch_When_ItsRowIsRead_Then_ItHasNoDurabilityBecauseItStacks() =>
        Assert.Null(TestItems.Catalogue.Find("torch")!.Durability);

    [Fact]
    public void Given_EveryDurableItemOfTheBundle_When_ItsStackIsRead_Then_ItIsOne() =>
        Assert.All(TestItems.Catalogue.All.Where(static d => d.Durability is not null), static d => Assert.Equal(1, d.MaxStack));
}
