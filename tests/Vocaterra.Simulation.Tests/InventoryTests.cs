using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Stacks, limits, the hotbar and the removals of <see cref="Inventory"/>.</summary>
public sealed class InventoryTests
{
    [Fact]
    public void ThereAreFortySlotsWithATenSlotHotbarAndNothingInThemAtFirst()
    {
        var inventory = new Inventory();
        Assert.Equal(40, Inventory.SlotCount);
        Assert.Equal(10, Inventory.HotbarSize);
        Assert.Equal(40, inventory.Slots.Count);
        Assert.All(inventory.Slots, slot => Assert.True(slot.IsEmpty));
        Assert.True(Inventory.IsHotbarSlot(0));
        Assert.True(Inventory.IsHotbarSlot(9));
        Assert.False(Inventory.IsHotbarSlot(10));
        Assert.False(Inventory.IsHotbarSlot(-1));
    }

    [Fact]
    public void ANewGameStartsWithAnAxeAndAPickaxeOnTheHotbar()
    {
        var sim = GameSimulation.Create(42);
        Assert.Equal(new ItemStack(ItemId.Axe, 1), sim.Inventory.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Pickaxe, 1), sim.Inventory.Slots[1]);
        Assert.Equal(0, sim.SelectedSlot);
        Assert.Equal(new ItemStack(ItemId.Axe, 1), sim.SelectedStack);
    }

    [Fact]
    public void AddingFillsTheExistingStackBeforeAnEmptySlotAndStopsAtTheLimit()
    {
        var inventory = new Inventory();
        Assert.Equal(0, inventory.Add(ItemId.Dirt, 60));
        Assert.Equal(0, inventory.Add(ItemId.Stone, 5));
        Assert.Equal(0, inventory.Add(ItemId.Dirt, 50));

        Assert.Equal(new ItemStack(ItemId.Dirt, 99), inventory.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Stone, 5), inventory.Slots[1]);
        Assert.Equal(new ItemStack(ItemId.Dirt, 11), inventory.Slots[2]);
        Assert.Equal(110, inventory.Count(ItemId.Dirt));
        Assert.Equal(5, inventory.Count(ItemId.Stone));
    }

    [Fact]
    public void AStackNeverExceedsTheLimitOfItsKind()
    {
        Assert.Equal(ItemDefinition.DefaultStackLimit, ItemDefinition.Get(ItemId.Wood).MaxStack);
        Assert.Equal(ItemDefinition.ToolStackLimit, ItemDefinition.Get(ItemId.Axe).MaxStack);
        Assert.Equal(1, ItemDefinition.ToolStackLimit);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory().SetSlot(0, new ItemStack(ItemId.Axe, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory().SetSlot(0, new ItemStack(ItemId.Wood, 100)));
    }

    [Fact]
    public void AddingReturnsWhatDoesNotFitAndCanAddSaysSoFirst()
    {
        var inventory = new Inventory();
        Assert.Equal(0, inventory.Add(ItemId.Dirt, 40 * 99));
        Assert.False(inventory.CanAdd(ItemId.Dirt, 1));
        Assert.False(inventory.CanAdd(ItemId.Stone, 1));
        Assert.Equal(1, inventory.Add(ItemId.Stone, 1));
        Assert.Equal(40 * 99, inventory.Count(ItemId.Dirt));
        Assert.Equal(0, inventory.Count(ItemId.Stone));
    }

    [Fact]
    public void CanAddChangesNothing()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 7);
        var before = inventory.Slots.ToArray();
        Assert.True(inventory.CanAdd(ItemId.Wood, 50));
        Assert.Equal(before, inventory.Slots);
    }

    [Fact]
    public void ToolsTakeOneSlotEach()
    {
        var inventory = new Inventory();
        Assert.Equal(0, inventory.Add(ItemId.Axe, 2));
        Assert.Equal(new ItemStack(ItemId.Axe, 1), inventory.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Axe, 1), inventory.Slots[1]);
        Assert.True(inventory.Slots[2].IsEmpty);
    }

    [Fact]
    public void RemoveIsAllOrNothingAndTakesFromTheLowestSlotFirst()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 99);
        inventory.Add(ItemId.Wood, 20);
        Assert.False(inventory.Remove(ItemId.Wood, 200));
        Assert.Equal(119, inventory.Count(ItemId.Wood));
        Assert.True(inventory.Remove(ItemId.Wood, 100));
        Assert.Equal(19, inventory.Count(ItemId.Wood));
        Assert.Equal(new ItemStack(ItemId.Wood, 19), inventory.Slots[1]);
        Assert.True(inventory.Slots[0].IsEmpty);
    }

    [Fact]
    public void RemoveFromSlotEmptiesTheSlotWhenItRunsOut()
    {
        var inventory = new Inventory();
        inventory.SetSlot(3, new ItemStack(ItemId.Flower, 2));
        Assert.False(inventory.RemoveFromSlot(3, 3));
        Assert.True(inventory.RemoveFromSlot(3, 1));
        Assert.Equal(new ItemStack(ItemId.Flower, 1), inventory.Slots[3]);
        Assert.True(inventory.RemoveFromSlot(3, 1));
        Assert.True(inventory.Slots[3].IsEmpty);
        Assert.False(inventory.RemoveFromSlot(3, 1));
    }

    [Fact]
    public void SettingAnEmptyStackClearsTheSlot()
    {
        var inventory = new Inventory();
        inventory.SetSlot(5, new ItemStack(ItemId.Sand, 4));
        inventory.SetSlot(5, ItemStack.Empty);
        Assert.True(inventory.Slots[5].IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSlot(40, ItemStack.Empty));
    }

    [Fact]
    public void EveryItemKindHasADefinitionWithItsOwnId()
    {
        for (var i = 0; i < ItemDefinition.All.Count; i++)
        {
            Assert.Equal((ItemId)i, ItemDefinition.All[i].Id);
            Assert.Equal(ItemDefinition.All[i], ItemDefinition.Get((ItemId)i));
        }

        Assert.Equal(13, ItemDefinition.All.Count);
        Assert.Equal(ToolKind.Axe, ItemDefinition.Get(ItemId.Axe).Tool);
        Assert.Equal(ToolKind.Pickaxe, ItemDefinition.Get(ItemId.Pickaxe).Tool);
        Assert.Null(ItemDefinition.Get(ItemId.Axe).PlacesTile);
        Assert.Equal(TileId.Planks, ItemDefinition.Get(ItemId.Wood).PlacesTile);
    }
}
