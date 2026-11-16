using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>The moves by hand (I02): left click, right click, shift-click, sort and the trash, each all or nothing.</summary>
public sealed class InventoryOpsTests
{
    private const int Bag = ContainerIds.Bag;
    private const int Worn = ContainerIds.Equipment;
    private const int Trash = ContainerIds.Trash;

    [Fact]
    public void Given_AStack_When_LeftClicked_Then_TheWholeStackIsHeldAndTheSlotIsEmpty()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);

        var result = rig.Ops.LeftClick(Bag, 12);

        Assert.True(result.IsDone);
        Assert.True(rig.Bag.Slots[12].IsEmpty);
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Sim.Held.Stack);
        Assert.Equal((Bag, 12), (rig.Sim.Held.OriginContainer, rig.Sim.Held.OriginSlot));
    }

    [Fact]
    public void Given_AHeldStackAndAnEmptySlot_When_LeftClicked_Then_ItIsPutDown()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);
        _ = rig.Ops.LeftClick(Bag, 12);

        var result = rig.Ops.LeftClick(Bag, 20);

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Bag.Slots[20]);
        Assert.True(rig.Sim.Held.IsEmpty);
    }

    [Fact]
    public void Given_AHeldStackAndADifferentItem_When_LeftClicked_Then_TheyAreSwapped()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(13, MoveRig.Bread, 2);
        _ = rig.Ops.LeftClick(Bag, 12);

        var result = rig.Ops.LeftClick(Bag, 13);

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Bag.Slots[13]);
        Assert.Equal(new ItemStack(MoveRig.Bread, 2), rig.Sim.Held.Stack);
        Assert.Equal((Bag, 13), (rig.Sim.Held.OriginContainer, rig.Sim.Held.OriginSlot));
    }

    [Fact]
    public void Given_AHeldStackAndTheSameItem_When_LeftClicked_Then_ItMergesUpToTheLimitAndTheRestStaysHeld()
    {
        var rig = new MoveRig();
        var limit = rig.Limit(MoveRig.Apple);
        _ = rig.With(12, MoveRig.Apple, 5).With(13, MoveRig.Apple, limit - 2);
        _ = rig.Ops.LeftClick(Bag, 12);

        var result = rig.Ops.LeftClick(Bag, 13);

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, limit), rig.Bag.Slots[13]);
        Assert.Equal(new ItemStack(MoveRig.Apple, 3), rig.Sim.Held.Stack);
    }

    [Fact]
    public void Given_AHeldStackThatFitsAStackOfTheSameItem_When_LeftClicked_Then_AllOfItMergesAndNothingIsHeld()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(13, MoveRig.Apple, 4);
        _ = rig.Ops.LeftClick(Bag, 12);

        _ = rig.Ops.LeftClick(Bag, 13);

        Assert.Equal(new ItemStack(MoveRig.Apple, 9), rig.Bag.Slots[13]);
        Assert.True(rig.Sim.Held.IsEmpty);
    }

    [Fact]
    public void Given_NothingToMove_When_LeftOrRightClicked_Then_ItIsRefusedAndNothingChanges()
    {
        var rig = new MoveRig();
        var before = rig.Snapshot();

        Assert.Equal(MoveReason.NothingToMove, rig.Ops.LeftClick(Bag, 12).Reason);
        Assert.Equal(MoveReason.NothingToMove, rig.Ops.RightClick(Bag, 12).Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Theory]
    [InlineData(99, 0)]
    [InlineData(-1, 0)]
    public void Given_NoSuchSlotOrContainer_When_Clicked_Then_ItIsRefused(int slot, int unused)
    {
        var rig = new MoveRig();
        var before = rig.Snapshot();

        Assert.Equal(MoveReason.NoSuchSlot, rig.Ops.LeftClick(Bag, slot).Reason);
        Assert.Equal(MoveReason.NoSuchContainer, rig.Ops.LeftClick(77 + unused, 0).Reason);
        Assert.Equal(MoveReason.NoSuchContainer, rig.Ops.LeftClick(ContainerIds.Held, 0).Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(4, 2, 2)]
    [InlineData(1, 1, 0)]
    public void Given_AStack_When_RightClicked_Then_TheLargerHalfIsHeld(int count, int held, int left)
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, count);

        var result = rig.Ops.RightClick(Bag, 12);

        Assert.True(result.IsDone);
        Assert.Equal(held, rig.Sim.Held.Stack.Count);
        Assert.Equal(left, rig.Bag.Slots[12].Count);
    }

    [Fact]
    public void Given_AHeldStack_When_RightClickedOnEmptyAndSameItemSlots_Then_OneIsPutDownEachTime()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(14, MoveRig.Apple, 2);
        _ = rig.Ops.LeftClick(Bag, 12);

        _ = rig.Ops.RightClick(Bag, 13);
        _ = rig.Ops.RightClick(Bag, 14);

        Assert.Equal(new ItemStack(MoveRig.Apple, 1), rig.Bag.Slots[13]);
        Assert.Equal(new ItemStack(MoveRig.Apple, 3), rig.Bag.Slots[14]);
        Assert.Equal(new ItemStack(MoveRig.Apple, 3), rig.Sim.Held.Stack);
    }

    [Fact]
    public void Given_AHeldStack_When_RightClickedOnAnotherItem_Then_ItIsRefusedAndNothingChanges()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(13, MoveRig.Bread, 2);
        _ = rig.Ops.LeftClick(Bag, 12);
        var before = rig.Snapshot();

        var result = rig.Ops.RightClick(Bag, 13);

        Assert.Equal(MoveReason.DifferentItem, result.Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AFullStackOfTheSameItem_When_RightClickedWithOneHeld_Then_ItIsRefusedAsFull()
    {
        var rig = new MoveRig();
        _ = rig.With(12, MoveRig.Apple, 5).With(13, MoveRig.Apple, rig.Limit(MoveRig.Apple));
        _ = rig.Ops.LeftClick(Bag, 12);
        var before = rig.Snapshot();

        Assert.Equal(MoveReason.SlotFull, rig.Ops.RightClick(Bag, 13).Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AWearableInTheBag_When_ShiftClicked_Then_ItIsWornAndTheBagSlotIsFree()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1);

        var result = rig.Ops.QuickMove(Bag, 12);

        Assert.True(result.IsDone);
        Assert.Equal(GearItems.Shirt, rig.Sim.Equipment.Get(MoveRig.Body).Item);
        Assert.True(rig.Bag.Slots[12].IsEmpty);
    }

    [Fact]
    public void Given_AShirtWorn_When_AScarfIsShiftClicked_Then_ItTakesTheEmptyArmourSlotAndTheShirtStays()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1).With(13, GearItems.Scarf, 1);
        _ = rig.Ops.QuickMove(Bag, 12);

        var result = rig.Ops.QuickMove(Bag, 13);

        Assert.True(result.IsDone);
        Assert.Equal(GearItems.Shirt, rig.Sim.Equipment.Get(MoveRig.Body).Item);
        Assert.Equal(GearItems.Scarf, rig.Sim.Equipment.Get(GearItems.Slot("armour", "body")).Item);
    }

    [Fact]
    public void Given_AHeldScarfAndAWornShirt_When_LeftClickedOnTheShirtSlot_Then_TheyAreSwapped()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1).With(13, GearItems.Scarf, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        _ = rig.Ops.LeftClick(Bag, 13);

        var result = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Body));

        Assert.True(result.IsDone);
        Assert.Equal(GearItems.Scarf, rig.Sim.Equipment.Get(MoveRig.Body).Item);
        Assert.Equal(new ItemStack(GearItems.Shirt, 1), rig.Sim.Held.Stack);
    }

    [Fact]
    public void Given_TwoAccessories_When_ShiftClicked_Then_EachTakesTheFirstFreeAccessorySlot()
    {
        var rig = new MoveRig().With(12, GearItems.Ring, 1).With(13, GearItems.Charm, 1);

        _ = rig.Ops.QuickMove(Bag, 12);
        _ = rig.Ops.QuickMove(Bag, 13);

        Assert.Equal(GearItems.Ring, rig.Sim.Equipment.Get(GearItems.Nth("accessories", 0)).Item);
        Assert.Equal(GearItems.Charm, rig.Sim.Equipment.Get(GearItems.Nth("accessories", 1)).Item);
    }

    [Fact]
    public void Given_AWearableThatNoSlotTakes_When_ShiftClicked_Then_ItIsRefusedWithTheEquipReasonAndNothingChanges()
    {
        var rig = new MoveRig().With(12, GearItems.Rucksack, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        rig.Bag.SetSlot(13, new ItemStack(GearItems.Sack, 1));
        rig.Bag.SetSlot(45, new ItemStack(MoveRig.Bread, 1));
        var before = rig.Snapshot();

        var result = rig.Ops.QuickMove(Bag, 13); // a smaller bag while slot 45 holds an item

        Assert.Equal(MoveReason.SlotRefused, result.Reason);
        Assert.Equal(EquipResult.BagNotEmpty, result.Equip);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AWornItem_When_ShiftClicked_Then_ItGoesToTheFirstFreeBagSlot()
    {
        var rig = new MoveRig().With(0, MoveRig.Apple, 1).With(12, GearItems.Shirt, 1);
        _ = rig.Ops.QuickMove(Bag, 12);

        var result = rig.Ops.QuickMove(Worn, rig.Flat(MoveRig.Body));

        Assert.True(result.IsDone);
        Assert.True(rig.Sim.Equipment.IsEmpty);
        Assert.Equal(GearItems.Shirt, rig.Bag.Slots[1].Item);
    }

    [Fact]
    public void Given_AFullBag_When_AWornItemIsShiftClicked_Then_ItIsRefusedAndStaysWorn()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        for (var i = 0; i < rig.Bag.SlotCount; i++)
        {
            rig.Bag.SetSlot(i, new ItemStack(i % 2 == 0 ? MoveRig.Bread : MoveRig.Flour, rig.Limit(i % 2 == 0 ? MoveRig.Bread : MoveRig.Flour)));
        }

        var before = rig.Snapshot();

        var result = rig.Ops.QuickMove(Worn, rig.Flat(MoveRig.Body));

        Assert.Equal(MoveReason.NoRoom, result.Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AWornBagWithItemsInItsSlots_When_ItIsShiftClicked_Then_ItIsRefusedAndStaysWorn()
    {
        var rig = new MoveRig().With(12, GearItems.Sack, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        rig.Bag.SetSlot(35, new ItemStack(MoveRig.Apple, 2));
        var before = rig.Snapshot();

        var result = rig.Ops.QuickMove(Worn, rig.Flat(MoveRig.Back));

        Assert.Equal(EquipResult.BagNotEmpty, result.Equip);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AFullBaseAndAnEmptyWornBag_When_ItIsShiftClicked_Then_ItCannotGoIntoItsOwnSlotsAndIsRefused()
    {
        var rig = new MoveRig().With(12, GearItems.Sack, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        for (var i = 0; i < 30; i++)
        {
            rig.Bag.SetSlot(i, new ItemStack(i % 2 == 0 ? MoveRig.Bread : MoveRig.Flour, rig.Limit(i % 2 == 0 ? MoveRig.Bread : MoveRig.Flour)));
        }

        var before = rig.Snapshot(); // the 30 base slots are full: the bag's own 10 are empty, and it cannot go into them

        var result = rig.Ops.QuickMove(Worn, rig.Flat(MoveRig.Back));

        Assert.Equal(MoveReason.NoRoom, result.Reason);
        Assert.Equal(before, rig.Snapshot());
        Assert.Equal(40, rig.Bag.SlotCount);
    }

    [Fact]
    public void Given_AHotbarStack_When_ShiftClicked_Then_ItMergesIntoThePackThenGoesToTheFirstFreePackSlot()
    {
        var rig = new MoveRig().With(3, MoveRig.Apple, 4).With(15, MoveRig.Apple, 2).With(4, MoveRig.Bread, 1);

        _ = rig.Ops.QuickMove(Bag, 3);
        _ = rig.Ops.QuickMove(Bag, 4);

        Assert.True(rig.Bag.Slots[3].IsEmpty);
        Assert.Equal(new ItemStack(MoveRig.Apple, 6), rig.Bag.Slots[15]);
        Assert.Equal(new ItemStack(MoveRig.Bread, 1), rig.Bag.Slots[10]);
    }

    [Fact]
    public void Given_APackStack_When_ShiftClicked_Then_ItGoesToTheHotbarAndMergesFirst()
    {
        var rig = new MoveRig().With(15, MoveRig.Apple, 4).With(2, MoveRig.Apple, 3);

        _ = rig.Ops.QuickMove(Bag, 15);

        Assert.True(rig.Bag.Slots[15].IsEmpty);
        Assert.Equal(new ItemStack(MoveRig.Apple, 7), rig.Bag.Slots[2]);
    }

    [Fact]
    public void Given_AStackThatOnlyPartlyFits_When_ShiftClicked_Then_WhatFitsMovesAndTheRestStays()
    {
        var rig = new MoveRig();
        var limit = rig.Limit(MoveRig.Apple);
        _ = rig.With(15, MoveRig.Apple, 10);
        for (var i = 0; i < Inventory.HotbarSize; i++)
        {
            rig.Bag.SetSlot(i, new ItemStack(MoveRig.Bread, 1));
        }

        rig.Bag.SetSlot(7, new ItemStack(MoveRig.Apple, limit - 4));
        var result = rig.Ops.QuickMove(Bag, 15);

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, limit), rig.Bag.Slots[7]);
        Assert.Equal(new ItemStack(MoveRig.Apple, 6), rig.Bag.Slots[15]);
    }

    [Fact]
    public void Given_NoRoomInTheHotbar_When_APackStackIsShiftClicked_Then_ItIsRefusedAndNothingChanges()
    {
        var rig = new MoveRig().With(15, MoveRig.Apple, 10);
        for (var i = 0; i < Inventory.HotbarSize; i++)
        {
            rig.Bag.SetSlot(i, new ItemStack(MoveRig.Bread, 1));
        }

        var before = rig.Snapshot();

        Assert.Equal(MoveReason.NoRoom, rig.Ops.QuickMove(Bag, 15).Reason);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AMixedPack_When_Sorted_Then_StacksMergeInCatalogueOrderThenCountDescending_AndTheHotbarIsLeftAlone()
    {
        var rig = new MoveRig().With(2, MoveRig.Flour, 3).With(11, MoveRig.Flour, 5).With(12, MoveRig.Apple, 7).With(20, MoveRig.Flour, 4).With(25, MoveRig.Apple, 1).With(21, MoveRig.Bread, 2);
        var hotbar = rig.Bag.Slots.Take(Inventory.HotbarSize).ToList();

        var result = rig.Ops.Sort();

        Assert.True(result.IsDone);
        Assert.Equal(hotbar, rig.Bag.Slots.Take(Inventory.HotbarSize).ToList());
        var pack = rig.Bag.Slots.Skip(Inventory.HotbarSize).Where(static s => !s.IsEmpty).ToList();
        Assert.Equal(19, pack.Sum(static s => s.Count));
        Assert.Equal(3, pack.Count); // Bread 2, Apple 8, Flour 9 (each one stack)
        var catalogue = rig.Bag.Items.All.Select(static d => d.Id).ToList();
        Assert.Equal(pack.Select(static s => s.Item).OrderBy(i => catalogue.IndexOf(i)), pack.Select(static s => s.Item));
        Assert.All(rig.Bag.Slots.Skip(Inventory.HotbarSize + pack.Count), static s => Assert.True(s.IsEmpty));
    }

    [Fact]
    public void Given_StacksAboveTheLimitWhenMerged_When_Sorted_Then_FullStacksComeFirstThenTheRemainder()
    {
        var rig = new MoveRig();
        var limit = rig.Limit(MoveRig.Apple);
        _ = rig.With(11, MoveRig.Apple, limit - 1).With(12, MoveRig.Apple, 5).With(13, MoveRig.Apple, limit - 2);

        _ = rig.Ops.Sort();

        Assert.Equal(limit, rig.Bag.Slots[10].Count);
        Assert.Equal(limit, rig.Bag.Slots[11].Count);
        Assert.Equal(2, rig.Bag.Slots[12].Count);
    }

    [Fact]
    public void Given_TwoOrdersOfTheSameStacks_When_Sorted_Then_TheResultIsTheSame()
    {
        var one = new MoveRig().With(11, MoveRig.Flour, 5).With(12, MoveRig.Apple, 7).With(25, MoveRig.Bread, 2);
        var two = new MoveRig().With(29, MoveRig.Bread, 2).With(18, MoveRig.Apple, 7).With(10, MoveRig.Flour, 5);

        _ = one.Ops.Sort();
        _ = two.Ops.Sort();

        Assert.Equal(one.Snapshot(), two.Snapshot());
        var again = one.Snapshot();
        _ = one.Ops.Sort();
        Assert.Equal(again, one.Snapshot());
    }

    [Fact]
    public void Given_AHeldStack_When_PutInTheTrash_Then_ItReplacesWhatWasThereAndTheOldOneIsGone()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(13, MoveRig.Bread, 2);
        _ = rig.Ops.LeftClick(Bag, 12);
        _ = rig.Ops.LeftClick(Trash, 0);
        _ = rig.Ops.LeftClick(Bag, 13);

        var result = rig.Ops.LeftClick(Trash, 0);

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Bread, 2), rig.Sim.Trash.Stack);
        Assert.True(rig.Sim.Held.IsEmpty);
        Assert.Equal(0, rig.Bag.Count(MoveRig.Apple));
    }

    [Fact]
    public void Given_AStackInTheTrash_When_LeftClickedWithNothingHeld_Then_ItIsPickedUpAsUsual()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);
        _ = rig.Ops.LeftClick(Bag, 12);
        _ = rig.Ops.LeftClick(Trash, 0);

        _ = rig.Ops.LeftClick(Trash, 0);

        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Sim.Held.Stack);
        Assert.True(rig.Sim.Trash.Stack.IsEmpty);
    }

    [Fact]
    public void Given_AStackInTheTrash_When_ShiftClicked_Then_ItGoesToTheBag()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);
        _ = rig.Ops.LeftClick(Bag, 12);
        _ = rig.Ops.LeftClick(Trash, 0);

        var result = rig.Ops.QuickMove(Trash, 0);

        Assert.True(result.IsDone);
        Assert.True(rig.Sim.Trash.Stack.IsEmpty);
        Assert.Equal(5, rig.Bag.Count(MoveRig.Apple));
    }

    [Fact]
    public void Given_AWornShirt_When_PickedUpAndPutBack_Then_ItIsWornAgain()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1);
        _ = rig.Ops.QuickMove(Bag, 12);

        _ = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Body));
        Assert.True(rig.Sim.Equipment.IsEmpty);
        Assert.Equal(GearItems.Shirt, rig.Sim.Held.Stack.Item);
        var result = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Body));

        Assert.True(result.IsDone);
        Assert.Equal(GearItems.Shirt, rig.Sim.Equipment.Get(MoveRig.Body).Item);
        Assert.True(rig.Sim.Held.IsEmpty);
    }

    [Fact]
    public void Given_AHeldHatAndTheBodySlot_When_LeftClicked_Then_ItIsRefusedWithTheEquipReasonAndStaysHeld()
    {
        var rig = new MoveRig().With(12, GearItems.Hat, 1);
        _ = rig.Ops.LeftClick(Bag, 12);
        var before = rig.Snapshot();

        var result = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Body));

        Assert.Equal(MoveReason.SlotRefused, result.Reason);
        Assert.Equal(EquipResult.WrongSlot, result.Equip);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AHeldStackOfTwoShirts_When_LeftClickedOnAWornSlot_Then_ItIsRefusedAsTooLarge()
    {
        var rig = new MoveRig();
        rig.Sim.Held.Put(0, new ItemStack(GearItems.Shirt, 2));
        var before = rig.Snapshot();

        var result = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Body));

        Assert.Equal(EquipResult.StackTooLarge, result.Equip);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_AHeldBagAndAWornBag_When_SwappedForASmallerOneWithItemsInItsSlots_Then_ItIsRefusedAndNothingChanges()
    {
        var rig = new MoveRig().With(12, GearItems.Rucksack, 1);
        _ = rig.Ops.QuickMove(Bag, 12);
        rig.Bag.SetSlot(45, new ItemStack(MoveRig.Apple, 1));
        rig.Bag.SetSlot(13, new ItemStack(GearItems.Sack, 1));
        _ = rig.Ops.LeftClick(Bag, 13);
        var before = rig.Snapshot();

        var result = rig.Ops.LeftClick(Worn, rig.Flat(MoveRig.Back));

        Assert.Equal(EquipResult.BagNotEmpty, result.Equip);
        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void Given_ASlotOfTheLargeBagNoLongerThere_When_Clicked_Then_ItIsNoSuchSlot()
    {
        var rig = new MoveRig();

        Assert.Equal(MoveReason.NoSuchSlot, rig.Ops.LeftClick(Bag, 30).Reason);
    }

    [Fact]
    public void Given_AnUnknownAction_When_Applied_Then_ItIsRefused()
    {
        var rig = new MoveRig();

        Assert.Equal(MoveReason.NoSuchAction, rig.Ops.Apply((MoveAction)99, Bag, 0).Reason);
    }
}
