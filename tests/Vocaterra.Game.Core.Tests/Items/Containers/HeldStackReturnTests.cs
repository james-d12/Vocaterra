using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>Returning the held stack, as closing the panel does (I02): to the slot it came from, else the bag; with no room it stays held and is never lost.</summary>
public sealed class HeldStackReturnTests
{
    [Fact]
    public void Given_AHeldStack_When_Returned_Then_ItGoesBackToItsSlot()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);

        var result = rig.Ops.ReturnHeld();

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Bag.Slots[12]);
        Assert.True(rig.Sim.Held.IsEmpty);
    }

    [Fact]
    public void Given_AHeldStackWhoseSlotWasTaken_When_Returned_Then_ItMergesIntoTheBagOrTakesTheFirstFreeSlot()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5).With(20, MoveRig.Apple, 2);
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);
        rig.Bag.SetSlot(12, new ItemStack(MoveRig.Bread, 1)); // the origin is taken now

        var result = rig.Ops.ReturnHeld();

        Assert.True(result.IsDone);
        Assert.Equal(new ItemStack(MoveRig.Apple, 7), rig.Bag.Slots[20]);
        Assert.True(rig.Sim.Held.IsEmpty);
    }

    [Fact]
    public void Given_AHeldStackFromAWornSlot_When_Returned_Then_ItIsWornAgain()
    {
        var rig = new MoveRig().With(12, Vocaterra.Game.Core.Tests.TestSupport.GearItems.Shirt, 1);
        _ = rig.Ops.QuickMove(ContainerIds.Bag, 12);
        _ = rig.Ops.LeftClick(ContainerIds.Equipment, rig.Flat(MoveRig.Body));

        var result = rig.Ops.ReturnHeld();

        Assert.True(result.IsDone);
        Assert.Equal(Vocaterra.Game.Core.Tests.TestSupport.GearItems.Shirt, rig.Sim.Equipment.Get(MoveRig.Body).Item);
    }

    [Fact]
    public void Given_AFullBagAndATakenOrigin_When_Returned_Then_TheStackStaysHeldAndNothingIsLost()
    {
        var rig = new MoveRig();
        for (var i = 0; i < rig.Bag.SlotCount; i++)
        {
            rig.Bag.SetSlot(i, new ItemStack(MoveRig.Bread, rig.Limit(MoveRig.Bread)));
        }

        rig.Sim.Held.Put(0, new ItemStack(MoveRig.Apple, 5));
        rig.Sim.Held.SetOrigin(ContainerIds.Bag, 12);
        var before = rig.Snapshot();

        var result = rig.Ops.ReturnHeld();

        Assert.Equal(MoveReason.NoRoom, result.Reason);
        Assert.Equal(before, rig.Snapshot());
        Assert.False(rig.Sim.ReturnHeld());
    }

    [Fact]
    public void Given_NothingHeld_When_Returned_Then_ItIsDoneAndNothingChanges()
    {
        var rig = new MoveRig().With(3, MoveRig.Apple, 2);
        var before = rig.Snapshot();

        Assert.True(rig.Ops.ReturnHeld().IsDone);
        Assert.True(rig.Sim.ReturnHeld());
        Assert.Equal(before, rig.Snapshot());
    }
}
