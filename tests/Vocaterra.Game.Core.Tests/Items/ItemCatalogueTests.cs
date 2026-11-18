using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The item catalogue (I01): <c>game/items.json</c> read once, checked against the code's <see cref="ItemId"/> members, and asked by id and by name.</summary>
public sealed class ItemCatalogueTests
{
    private static readonly string Shipped = File.ReadAllText(Repo.Path("content", "game-v1", "game", "items.json"));

    [Fact]
    public void Given_TheShippedBundle_When_TheCatalogueLoads_Then_ItHasTheTwentyFourRowsInTheFilesOrderAndEveryEnumMember()
    {
        var catalogue = ItemCatalogue.Load(TestGame.Bundle);

        Assert.Equal(24, catalogue.All.Count); // I08a: the three materials and the seven clothes, ids 100 to 109
        Assert.Equal(["wood", "torch", "berry", "bottle", "water-bottle", "bread", "book", "coin", "apple", "axe", "roast-apple", "flour", "stake", "camp-kit", "cloth", "leather", "iron", "hat", "cap", "shirt", "jacket", "coat", "trousers", "shoes"], catalogue.All.Select(static d => d.Name));
        foreach (var member in Enum.GetValues<ItemId>())
        {
            Assert.Equal(ItemCatalogue.KeyOf(member), catalogue.Get(member).Name);
            Assert.Same(catalogue.Get(member), catalogue.Find(catalogue.Get(member).Name));
        }

        Assert.Equal(["Wood", "Torch", "Berry", "Bottle", "WaterBottle", "Book", "Coin", "Stake", "CampKit"], Enum.GetNames<ItemId>());
    }

    [Fact]
    public void Given_TheShippedRows_When_TheyAreRead_Then_TheStackLimitsTheToolAndTheTagsAreTheOnesTheTableHad()
    {
        var catalogue = TestItems.Catalogue;

        Assert.Equal(1, catalogue.Get(ItemId.Book).MaxStack);
        Assert.Equal(1, catalogue.Get(ItemId.Stake).MaxStack);
        Assert.Equal(1, catalogue.Get(ItemId.CampKit).MaxStack);
        Assert.Equal(1, catalogue.Get(TestItems.Axe).MaxStack);
        Assert.Equal(99, catalogue.Get(ItemId.Wood).MaxStack);
        Assert.Equal(new ToolSpec(Material.Wood, 4), catalogue.Get(TestItems.Axe).Tool);
        Assert.Null(catalogue.Get(ItemId.Wood).Tool);
        Assert.True(catalogue.Get(TestItems.Axe).HasTag("tool"));
        Assert.True(catalogue.Get(ItemId.Berry).HasTag("food"));
        Assert.True(catalogue.Get(ItemId.Bottle).HasTag("container"));
        Assert.False(catalogue.Get(ItemId.Berry).HasTag("tool"));
        Assert.Equal(ItemHeld.Log, catalogue.Get(ItemId.Wood).Held);
        Assert.Equal(ItemHeld.Sprite, catalogue.Get(ItemId.Coin).Held);
    }

    [Fact]
    public void Given_AnUnknownIdOrName_When_ItIsAsked_Then_FindGivesNullAndGetThrows()
    {
        var catalogue = TestItems.Catalogue;

        Assert.Null(catalogue.Find((ItemId)999));
        Assert.Null(catalogue.Find("no-such-item"));
        Assert.Null(catalogue.Find("WOOD"));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalogue.Get((ItemId)999));
    }

    [Fact]
    public void Given_ARowOfAnEnumMemberIsMissing_When_TheCatalogueLoads_Then_ItFailsNamingTheMember()
    {
        var without = Regex.Replace(Shipped, "^\\s*\\{ \"name\": \"coin\".*\\},?\\r?\\n", string.Empty, RegexOptions.Multiline);

        var error = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(without));

        Assert.Contains("no row for the item Coin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheRowOfAMembersIdHasAnotherName_When_TheCatalogueLoads_Then_ItFailsNamingBoth()
    {
        var renamed = Shipped.Replace("\"name\": \"coin\", \"id\": 30", "\"name\": \"money\", \"id\": 30", StringComparison.Ordinal).Replace("\"icon\": \"coin\"", "\"icon\": \"money\"", StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(renamed));

        Assert.Contains("'money'", error.Message, StringComparison.Ordinal);
        Assert.Contains("Coin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AMemberHasARowOfAnotherId_When_TheCatalogueLoads_Then_ItFails()
    {
        var moved = Shipped.Replace("\"name\": \"coin\", \"id\": 30", "\"name\": \"coin\", \"id\": 130", StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(moved));

        Assert.Contains("no row for the item Coin", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"name\": \"torch\", \"id\": 21", "\"name\": \"wood\", \"id\": 21", "name 'wood' is used twice")]
    [InlineData("\"name\": \"flour\", \"id\": 34", "\"name\": \"flour\", \"id\": 33", "id 33 of 'flour' is used twice")]
    [InlineData("\"name\": \"flour\", \"id\": 34, \"stack\": 99", "\"name\": \"flour\", \"id\": 34, \"stack\": 0", "stack limit of 0")]
    [InlineData("\"name\": \"flour\", \"id\": 34, \"stack\": 99, \"icon\": \"flour\"", "\"name\": \"flour\", \"id\": 34, \"stack\": 99, \"icon\": \"\"", "no icon key")]
    [InlineData("\"name\": \"flour\"", "\"name\": \"Flour Bag\"", "not a lowercase key")]
    [InlineData("\"icon\": \"flour\"", "\"icon\": \"flour\", \"held\": \"wand\"", "held as 'wand'")]
    [InlineData("\"tool\": { \"class\": \"wood\"", "\"tool\": { \"class\": \"granite\"", "unknown material 'granite'")]
    public void Given_ARowThatIsNotValid_When_TheCatalogueLoads_Then_ItFailsNamingTheRow(string from, string to, string expected)
    {
        var broken = Shipped.Replace(from, to, StringComparison.Ordinal);
        Assert.NotEqual(Shipped, broken);

        var error = Assert.Throws<InvalidDataException>(() => ItemCatalogue.Parse(broken));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"schemaVersion\": 2, \"items\": [] }")]
    [InlineData("{ \"schemaVersion\": 1 }")]
    [InlineData("{ \"schemaVersion\": 1, \"items\": [ { \"name\": \"x\" } ] }")]
    public void Given_TextThatIsNotTheFile_When_TheCatalogueLoads_Then_ItIsAnInvalidDataException(string text) =>
        Assert.ThrowsAny<InvalidDataException>(() => ItemCatalogue.Parse(text));

    [Fact]
    public void Given_ARowThatNoEnumMemberNames_When_ItIsAdded_Then_TheCatalogueHasItByIdAndByName()
    {
        var gem = new ItemDefinition((ItemId)900, "gem", 7, "gem", ItemHeld.Sprite, ["material"]);

        var catalogue = TestItems.With(gem);

        Assert.Equal(25, catalogue.All.Count);
        Assert.Same(gem, catalogue.Get((ItemId)900));
        Assert.Same(gem, catalogue.Find("gem"));
        Assert.Equal("gem", catalogue.All[^1].Name);
    }

    [Fact]
    public void Given_AnEnumMemberKey_When_ItIsDerived_Then_ItIsTheLowercaseHyphenatedName()
    {
        Assert.Equal("water-bottle", ItemCatalogue.KeyOf(ItemId.WaterBottle));
        Assert.Equal("camp-kit", ItemCatalogue.KeyOf(ItemId.CampKit));
        Assert.Equal("wood", ItemCatalogue.KeyOf(ItemId.Wood));
    }
}
