using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>What the content check says about items (I01): every item another file names is a row of <c>game/items.json</c>, names and ids are unique, and an icon key without art is reported.</summary>
public sealed class ItemValidationTests
{
    private static readonly string[] Keys = [.. TestItems.Catalogue.All.Select(static d => d.Icon)];

    [Fact]
    public void Given_TheShippedContent_When_ItIsChecked_Then_NoItemProblemIsReportedWithOrWithoutTheArt()
    {
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
        Assert.Empty(GameDataValidator.Validate(TestGame.Data, new HashSet<string>(Keys, StringComparer.Ordinal)));
    }

    [Fact]
    public void Given_ACatalogueWithoutItemsOtherFilesName_When_TheContentIsChecked_Then_EachFileReportsTheItemItNames()
    {
        var items = ItemCatalogue.Create(TestItems.Catalogue.All.Where(static d => d.Name is not ("axe" or "bread" or "apple" or "roast-apple" or "flour")));

        var problems = GameDataValidator.Validate(TestGame.Data.WithItems(items));

        Assert.Contains(problems, static p => p.StartsWith("game/nouns.json: the noun 'hacha' names the item 32, which is not in game/items.json", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("game/nouns.json: the noun 'pan' names the item 28", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("game/recipes.json: the recipe for 'manzana' makes it and", StringComparison.Ordinal) && p.Contains("33", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("game/recipes.json: the recipe for 'manzana' uses 'manzana'", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("game/host-events.json: shopkeeper's", StringComparison.Ordinal) && p.Contains("28", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("game/needs.json:", StringComparison.Ordinal) && p.Contains("bread", StringComparison.Ordinal));
        Assert.All(problems.Where(static p => p.Contains("is not in game/items.json", StringComparison.Ordinal)), static p => Assert.Matches("names the item \\d+", p));
    }

    [Fact]
    public void Given_ACatalogueThatListsANameOrAnIdTwice_When_TheContentIsChecked_Then_BothAreReported()
    {
        var wood = TestItems.Catalogue.Get(ItemId.Wood);
        var torch = TestItems.Catalogue.Get(ItemId.Torch);
        var twice = new ListCatalogue([.. TestItems.Catalogue.All, wood with { Id = (ItemId)150 }, torch with { Name = "torch-two" }]);

        var problems = GameDataValidator.Validate(TestGame.Data.WithItems(twice));

        Assert.Contains("game/items.json: the item name 'wood' is used twice.", problems);
        Assert.Contains("game/items.json: the id 21 of 'torch-two' is used twice.", problems);
    }

    [Fact]
    public void Given_AnIconKeyTheFrontEndHasNoArtFor_When_TheContentIsChecked_Then_TheItemIsReported()
    {
        var withoutCoin = new HashSet<string>(Keys.Where(static k => k != "coin"), StringComparer.Ordinal);

        var problems = GameDataValidator.Validate(TestGame.Data, withoutCoin);

        var problem = Assert.Single(problems);
        Assert.Equal("game/items.json: the icon 'coin' of 'coin' has no art in the atlas (it would draw the fallback tile).", problem);
    }

    [Fact]
    public void Given_ADataOnlyItemWithAnIconWithoutArt_When_TheContentIsChecked_Then_OnlyThatItemIsReported()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)100, "gem", 7, "sparkle", ItemHeld.Sprite, ["material"]));

        var problems = GameDataValidator.Validate(TestGame.Data.WithItems(items), new HashSet<string>(Keys, StringComparer.Ordinal));

        Assert.Equal(["game/items.json: the icon 'sparkle' of 'gem' has no art in the atlas (it would draw the fallback tile)."], problems);
    }

    private sealed class ListCatalogue(IReadOnlyList<ItemDefinition> rows) : IItemCatalogue
    {
        public IReadOnlyList<ItemDefinition> All => rows;

        public ItemDefinition Get(ItemId id) => Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id));

        public ItemDefinition? Find(ItemId id) => rows.FirstOrDefault(d => d.Id == id);

        public ItemDefinition? Find(string name) => rows.FirstOrDefault(d => d.Name == name);
    }
}
