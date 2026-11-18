using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The <c>capacity</c> of a bag item (I03b): read from the data, allowed only on an item tagged <c>bag</c> and worn on the <c>back</c>, and the largest one sizes the inventory's array.</summary>
public sealed class ItemCapacityTests
{
    private static ItemCatalogue Parse(string row)
    {
        var shipped = File.ReadAllText(Repo.Path("content", "game-v1", "game", "items.json"));
        var at = shipped.LastIndexOf(']');
        return ItemCatalogue.Parse(shipped[..at].TrimEnd() + "," + row + "]}");
    }

    [Fact]
    public void Given_TheShippedItems_When_TheInventoryIsBuilt_Then_ItIsSizedToTheBaseAndTheLargestShippedBag()
    {
        Assert.Equal(TestItems.Catalogue.All.Max(static item => item.Capacity ?? 0), TestItems.Catalogue.LargestCapacity);
        Assert.Equal(30 + TestItems.Catalogue.LargestCapacity, new Inventory(TestItems.Catalogue).MaxSlots);
        Assert.All(TestItems.Catalogue.All.Where(static item => item.Capacity is not null), static item => Assert.True(item.HasTag("bag") && item.Wear == "back"));
    }

    [Fact]
    public void Given_TwoBagsInTheCatalogue_When_ItIsBuilt_Then_TheLargestCapacitySizesTheInventory()
    {
        Assert.Equal(GearItems.RucksackCapacity, GearItems.Catalogue.LargestCapacity);
        Assert.Equal(GearItems.Catalogue.Get(GearItems.Sack).Capacity, GearItems.SackCapacity);
        Assert.Equal(50, new Inventory(GearItems.Catalogue).MaxSlots);
    }

    [Fact]
    public void Given_ARowWithACapacity_When_ItIsParsed_Then_TheDefinitionHasIt()
    {
        var catalogue = Parse("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["bag"], "wear": "back", "capacity": 6 }""");

        Assert.Equal(6, catalogue.Find("pouch")!.Capacity);
        Assert.Equal(20, catalogue.LargestCapacity); // the shipped backpack (I08b) is still the largest bag
    }

    [Theory]
    [InlineData("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["tool"], "wear": "back", "capacity": 6 }""")] // not a bag
    [InlineData("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["bag"], "wear": "head", "capacity": 6 }""")] // not worn on the back
    [InlineData("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["bag"], "capacity": 6 }""")] // not worn at all
    [InlineData("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["bag"], "wear": "back", "capacity": 0 }""")]
    [InlineData("""{ "name": "pouch", "id": 300, "stack": 1, "icon": "pouch", "tags": ["bag"], "wear": "back", "capacity": 101 }""")]
    public void Given_ACapacityOnTheWrongItemOrOutOfRange_When_ItIsParsed_Then_TheLoaderRefusesItByName(string row)
    {
        var error = Assert.Throws<InvalidDataException>(() => Parse(row));

        Assert.Contains("pouch", error.Message, StringComparison.Ordinal);
        Assert.Contains("capacity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ACatalogueWithNoCache_When_TheLargestCapacityIsAsked_Then_TheInterfaceDefaultReadsTheRows()
    {
        IItemCatalogue custom = new RowsOnly(GearItems.Catalogue.All);

        Assert.Equal(GearItems.RucksackCapacity, custom.LargestCapacity);
    }

    private sealed class RowsOnly(IReadOnlyList<ItemDefinition> rows) : IItemCatalogue
    {
        public IReadOnlyList<ItemDefinition> All => rows;

        public ItemDefinition Get(ItemId id) => rows.First(d => d.Id == id);

        public ItemDefinition? Find(ItemId id) => rows.FirstOrDefault(d => d.Id == id);

        public ItemDefinition? Find(string name) => rows.FirstOrDefault(d => d.Name == name);
    }
}
