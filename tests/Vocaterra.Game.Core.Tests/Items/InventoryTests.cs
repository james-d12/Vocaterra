using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The inventory as plain values: thirty slots with no bag, a ten-slot hotbar, stacks with one limit per kind, and what does not fit.</summary>
public sealed class InventoryTests
{
    [Fact]
    public void ThereAreThirtySlotsWithATenSlotHotbarAndNothingInThemAtFirst()
    {
        var inventory = new Inventory(TestItems.Catalogue);
        Assert.Equal(30, inventory.SlotCount);
        Assert.Equal(30, inventory.BaseSlots);
        Assert.Equal(10, Inventory.HotbarSize);
        Assert.Equal(30, inventory.Slots.Count);
        Assert.All(inventory.Slots, slot => Assert.True(slot.IsEmpty));
        Assert.True(Inventory.IsHotbarSlot(0));
        Assert.True(Inventory.IsHotbarSlot(9));
        Assert.False(Inventory.IsHotbarSlot(10));
        Assert.False(Inventory.IsHotbarSlot(-1));
    }

    [Fact]
    public void AStackHoldsNinetyNineAndTheRestGoesToTheNextSlot()
    {
        var inventory = new Inventory(TestItems.Catalogue);

        Assert.Equal(0, inventory.Add(ItemId.Wood, 99));
        Assert.Equal(0, inventory.Add(ItemId.Wood, 1));
        Assert.Equal(new ItemStack(ItemId.Wood, 99), inventory.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Wood, 1), inventory.Slots[1]);
        Assert.Equal(100, inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void WhatDoesNotFitIsReturnedAndNothingIsLost()
    {
        var inventory = new Inventory(TestItems.Catalogue);
        Assert.Equal(0, inventory.Add(ItemId.Berry, 99 * 30));
        Assert.False(inventory.CanAdd(ItemId.Berry, 1));
        Assert.Equal(5, inventory.Add(ItemId.Berry, 5));
        Assert.Equal(99 * 30, inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void RemovingTakesFromTheKindAndFailsWhenThereIsNotEnough()
    {
        var inventory = new Inventory(TestItems.Catalogue);
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
        var inventory = new Inventory(TestItems.Catalogue);
        inventory.Add(ItemId.Torch, 2);

        var copy = inventory.Copy();
        inventory.Remove(ItemId.Torch, 2);

        Assert.Equal(2, copy.Count(ItemId.Torch));
        Assert.Equal(0, inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void EveryItemHasItsStackLimitInTheDefinitionsAndNothingElse()
    {
        Assert.Equal(new[] { ItemId.Wood, ItemId.Torch, ItemId.Berry, ItemId.Bottle, ItemId.WaterBottle, TestItems.Bread, ItemId.Book, ItemId.Coin, TestItems.Apple, TestItems.Axe, TestItems.RoastApple, TestItems.Flour, ItemId.Stake, ItemId.CampKit, (ItemId)100, (ItemId)101, (ItemId)102, (ItemId)103, (ItemId)104, (ItemId)105, (ItemId)106, (ItemId)107, (ItemId)108, (ItemId)109 }, TestItems.Catalogue.All.Select(static d => d.Id).ToArray());
        Assert.Equal((30, 31, 32), ((int)ItemId.Coin, (int)TestItems.Apple, (int)TestItems.Axe)); // V03: after the book; a value is never reused
        Assert.All(TestItems.Catalogue.All.Where(static d => d.Id is not (ItemId.Book or TestItems.Axe or ItemId.Stake or ItemId.CampKit) && !d.Tags.Contains("clothing")), static d => Assert.Equal(ItemDefinition.DefaultStackLimit, d.MaxStack));
        Assert.Equal(1, TestItems.Catalogue.Get(TestItems.Axe).MaxStack); // a tool, like the book
        Assert.Equal(1, TestItems.Catalogue.Get(ItemId.Stake).MaxStack); // Y10: one stake marks the one plot
        Assert.All(TestItems.Catalogue.All.Where(static d => d.Tags.Contains("clothing")), static d => Assert.Equal(1, d.MaxStack)); // I08a: a worn piece is one to a slot
        Assert.Equal(1, TestItems.Catalogue.Get(ItemId.CampKit).MaxStack); // Y11: one kit comes with the one plot
        Assert.Equal(1, TestItems.Catalogue.Get(ItemId.Book).MaxStack); // the learner has one book
    }

    [Fact]
    public void Given_ARowsStackLimitIsChangedInATestCatalogue_When_ItemsAreAdded_Then_TheBagFollowsTheDataNotTheCode()
    {
        var wood = TestItems.Catalogue.Get(ItemId.Wood);
        var small = new Inventory(TestItems.With(wood with { MaxStack = 5 }));

        Assert.Equal(0, small.Add(ItemId.Wood, 12));
        Assert.Equal(new ItemStack(ItemId.Wood, 5), small.Slots[0]);
        Assert.Equal(new ItemStack(ItemId.Wood, 5), small.Slots[1]);
        Assert.Equal(new ItemStack(ItemId.Wood, 2), small.Slots[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => small.SetSlot(3, new ItemStack(ItemId.Wood, 6)));

        var big = new Inventory(TestItems.With(wood with { MaxStack = 300 }));
        Assert.Equal(0, big.Add(ItemId.Wood, 250));
        Assert.Equal(new ItemStack(ItemId.Wood, 250), big.Slots[0]);
        Assert.Equal(ItemId.Wood, big.Copy().Slots[0].Item);
        Assert.Equal(300, big.Copy().Items.Get(ItemId.Wood).MaxStack); // a copy keeps the catalogue
    }

    [Fact]
    public void Given_AnItemOnlyTheDataKnows_When_ItIsAddedToTheBag_Then_ItStacksToItsOwnLimitLikeAnyOther()
    {
        var gem = (ItemId)900;
        var bag = new Inventory(TestItems.With(new ItemDefinition(gem, "gem", 7, "gem", ItemHeld.Sprite, ["material"])));

        Assert.Equal(0, bag.Add(gem, 15));
        Assert.Equal(new ItemStack(gem, 7), bag.Slots[0]);
        Assert.Equal(new ItemStack(gem, 7), bag.Slots[1]);
        Assert.Equal(new ItemStack(gem, 1), bag.Slots[2]);
        Assert.Equal(15, bag.Count(gem));
        Assert.True(bag.CanExchange(gem, 15, ItemId.Wood, 99));
        Assert.True(bag.Remove(gem, 9));
        Assert.Equal(6, bag.Count(gem));
    }

    [Fact]
    public void Given_AnItemTheCatalogueDoesNotHave_When_ItIsAdded_Then_TheBagRefusesItLoudly()
    {
        var bag = new Inventory(TestItems.Catalogue);

        Assert.Throws<ArgumentOutOfRangeException>(() => bag.Add((ItemId)999, 1));
    }
}
