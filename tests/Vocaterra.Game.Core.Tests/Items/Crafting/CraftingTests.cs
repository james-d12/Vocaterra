using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Crafting;

/// <summary>The crafting rule as plain values: ingredients, the station in reach, and the room for the output.</summary>
public sealed class CraftingTests
{
    private static readonly Recipe Torch = new(ItemId.Torch, 1, [new Ingredient(ItemId.Wood, 2)], BlockId.Table);
    private static readonly Recipe ByHand = new(ItemId.Wood, 1, [new Ingredient(ItemId.Berry, 1)], null);

    [Fact]
    public void TheTorchNeedsTwoWoodAndTheWorkbenchInReach()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 2);

        Assert.Equal(CraftResult.MissingStation, CraftingRules.Check(Torch, inventory, stationInReach: false));
        Assert.Equal(CraftResult.Done, CraftingRules.Check(Torch, inventory, stationInReach: true));
    }

    [Fact]
    public void AMissingIngredientIsSaidBeforeTheStation()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 1);

        Assert.Equal(CraftResult.MissingIngredients, CraftingRules.Check(Torch, inventory, stationInReach: false));
    }

    [Fact]
    public void ACraftTakesTheIngredientsAndGivesTheOutputOnlyWhenItIsDone()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 3);

        Assert.Equal(CraftResult.MissingStation, CraftingRules.Craft(Torch, inventory, stationInReach: false));
        Assert.Equal(3, inventory.Count(ItemId.Wood));

        Assert.Equal(CraftResult.Done, CraftingRules.Craft(Torch, inventory, stationInReach: true));
        Assert.Equal(1, inventory.Count(ItemId.Wood));
        Assert.Equal(1, inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void AFullInventoryCraftsWhenTheIngredientsFreeTheSlot()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 2);
        inventory.Add(ItemId.Berry, ItemDefinition.DefaultStackLimit * 39);
        Assert.False(inventory.CanAdd(ItemId.Torch, 1));

        Assert.Equal(CraftResult.Done, CraftingRules.Check(Torch, inventory, stationInReach: true));
    }

    [Fact]
    public void AFullInventoryRefusesWhenNothingIsFreed()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Berry, ItemDefinition.DefaultStackLimit * 40);

        Assert.Equal(CraftResult.InventoryFull, CraftingRules.Check(ByHand, inventory, stationInReach: true));
    }
}
