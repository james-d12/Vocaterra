using Vocaterra.Simulation.Items;

namespace Vocaterra.Simulation.Tests;

/// <summary>The inventory as plain values: forty slots, a ten-slot hotbar, stacks with one limit per kind, and what does not fit.</summary>
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
    public void AStackHoldsNinetyNineAndTheRestGoesToTheNextSlot()
    {
        var inventory = new Inventory();

        Assert.Equal(0, inventory.Add(ItemId.Wood, 99));
        Assert.Equal(0, inventory.Add(ItemId.Wood, 1));
        Assert.Equal(new ItemStack(ItemId.Wood, 99), inventory.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Wood, 1), inventory.Slots[1]);
        Assert.Equal(100, inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void WhatDoesNotFitIsReturnedAndNothingIsLost()
    {
        var inventory = new Inventory();
        Assert.Equal(0, inventory.Add(ItemId.Berry, 99 * 40));
        Assert.False(inventory.CanAdd(ItemId.Berry, 1));
        Assert.Equal(5, inventory.Add(ItemId.Berry, 5));
        Assert.Equal(99 * 40, inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void RemovingTakesFromTheKindAndFailsWhenThereIsNotEnough()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Wood, 3);

        Assert.False(inventory.Remove(ItemId.Wood, 4));
        Assert.Equal(3, inventory.Count(ItemId.Wood));
        Assert.True(inventory.Remove(ItemId.Wood, 2));
        Assert.Equal(1, inventory.Count(ItemId.Wood));
        Assert.True(inventory.Remove(ItemId.Wood, 1));
        Assert.True(inventory.Slots[0].IsEmpty);
    }

    [Fact]
    public void CopyIsIndependent()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Torch, 2);

        var copy = inventory.Copy();
        inventory.Remove(ItemId.Torch, 2);

        Assert.Equal(2, copy.Count(ItemId.Torch));
        Assert.Equal(0, inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void EveryItemHasItsStackLimitInTheDefinitionsAndNothingElse()
    {
        Assert.Equal(new[] { ItemId.Wood, ItemId.Torch, ItemId.Berry, ItemId.Bottle, ItemId.WaterBottle, ItemId.Bread }, ItemDefinition.All.Select(static d => d.Id).ToArray());
        Assert.All(ItemDefinition.All, static d => Assert.Equal(ItemDefinition.DefaultStackLimit, d.MaxStack));
    }
}
