using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Figures;

namespace Vocaterra.Game.Tests.Graphics.Figures;

/// <summary>How the hand and the hotbar draw an item from its row (I01): the icon key names the picture, the held kind the model, and a key without art draws the fallback tile.</summary>
public sealed class HeldItemsTests
{
    [Fact]
    public void Given_TheShippedItems_When_TheContentIsCheckedWithTheAtlasKeys_Then_EveryIconHasArt()
    {
        Assert.Empty(GameDataValidator.Validate(TestGame.Data, BlockAtlasLayout.IconKeys));
        Assert.All(TestItems.Catalogue.All, static d => Assert.Contains(d.Icon, BlockAtlasLayout.IconKeys));
        Assert.All(BlockAtlasLayout.IconKeys, static key => Assert.NotEqual(BlockCell.IconPlain, BlockAtlasLayout.IconForKey(key)));
    }

    [Fact]
    public void Given_ADataOnlyItemWithAKeyThatHasArt_When_ItIsHeld_Then_TheHandShowsThatIconAsASprite()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)900, "pearl", 20, "coin", ItemHeld.Sprite, ["material"]));

        var held = HeldItems.For(items, new ItemStack((ItemId)900, 3));

        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconCoin), held);
    }

    [Fact]
    public void Given_ADataOnlyItemWithAKeyWithoutArt_When_ItIsHeld_Then_TheHandShowsTheFallbackTileNotNothing()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)900, "pearl", 20, "pearl", ItemHeld.Sprite, ["material"]));

        var held = HeldItems.For(items, new ItemStack((ItemId)900, 1));

        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconPlain), held);
        Assert.Equal(BlockCell.IconPlain, BlockAtlasLayout.IconFor(items.Get((ItemId)900)));
    }

    [Fact]
    public void Given_TheHeldKindsOfTheData_When_ItemsAreHeld_Then_TheLogTheTorchAndTheBottleKeepTheirModels()
    {
        var items = TestItems.With(
            new ItemDefinition((ItemId)901, "branch", 99, "wood", ItemHeld.Log, []),
            new ItemDefinition((ItemId)902, "candle", 99, "torch", ItemHeld.Torch, []),
            new ItemDefinition((ItemId)903, "flask", 99, "bottle", ItemHeld.Bottle, []));

        Assert.Equal(HeldKind.Log, HeldItems.For(items, new ItemStack((ItemId)901, 1)).Kind);
        Assert.Equal(HeldKind.Torch, HeldItems.For(items, new ItemStack((ItemId)902, 1)).Kind);
        Assert.Equal(new HeldChoice(HeldKind.Bottle, BlockCell.IconBottle), HeldItems.For(items, new ItemStack((ItemId)903, 1)));
        Assert.Equal(HeldChoice.Nothing, HeldItems.For(items, new ItemStack((ItemId)999, 1))); // an item the catalogue lacks
    }
}
