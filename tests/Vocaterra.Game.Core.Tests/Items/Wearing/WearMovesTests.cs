using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Tests.Items.Containers;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The wear stays with the item through every move by hand (I07): pick up, swap, put down, trash, wear, take off, sort and the return of a held stack; and a new item never joins a worn stack.</summary>
public sealed class WearMovesTests
{
    private const int Worn = 7;
    private static readonly ItemId Axe = TestItems.Axe;

    private static MoveRig RigWithAWornAxe(int slot = 12) => new MoveRig().With(slot, Axe, 1).Worn(slot, Worn);

    [Fact]
    public void Given_AWornAxe_When_ItIsPickedUpAndPutInAnotherSlot_Then_ItKeepsItsWear()
    {
        var rig = RigWithAWornAxe();

        Assert.True(rig.Ops.LeftClick(ContainerIds.Bag, 12).IsDone);
        Assert.Equal(Worn, rig.Sim.Held.Stack.Wear);
        Assert.True(rig.Ops.LeftClick(ContainerIds.Bag, 14).IsDone);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Bag.Slots[14]);
    }

    [Fact]
    public void Given_AWornAxeHeldOverANewOne_When_TheyAreSwapped_Then_EachKeepsItsOwnWearAndNothingMerges()
    {
        var rig = new MoveRig().With(12, Axe, 1).Worn(12, Worn).With(13, Axe, 1);
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);

        Assert.True(rig.Ops.LeftClick(ContainerIds.Bag, 13).IsDone);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Bag.Slots[13]);
        Assert.Equal(new ItemStack(Axe, 1, 0), rig.Sim.Held.Stack);
    }

    [Fact]
    public void Given_AWornAxe_When_ItIsHalvedByARightClick_Then_TheWearIsKept()
    {
        var rig = RigWithAWornAxe();

        Assert.True(rig.Ops.RightClick(ContainerIds.Bag, 12).IsDone);

        Assert.Equal(Worn, rig.Sim.Held.Stack.Wear);
    }

    [Fact]
    public void Given_AWornAxeInTheHotbar_When_ItIsShiftClicked_Then_ItGoesToThePackWithItsWear()
    {
        var rig = new MoveRig().With(2, Axe, 1).Worn(2, Worn);

        Assert.True(rig.Ops.QuickMove(ContainerIds.Bag, 2).IsDone);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Bag.Slots.Single(static s => s.Item == TestItems.Axe && !s.IsEmpty));
        Assert.True(rig.Bag.Slots.Select((s, i) => (s, i)).Single(p => !p.s.IsEmpty && p.s.Item == Axe).i >= Inventory.HotbarSize);
    }

    [Fact]
    public void Given_AWornHat_When_ItIsWornAndTakenOff_Then_TheWearTravelsBothWays()
    {
        var rig = new MoveRig().With(12, GearItems.Cap, 1).Worn(12, 3);

        Assert.True(rig.Ops.QuickMove(ContainerIds.Bag, 12).IsDone);
        var head = GearItems.Slot("clothing", "head");
        Assert.Equal(3, rig.Sim.Equipment.Get(head).Wear);

        Assert.True(rig.Ops.QuickMove(ContainerIds.Equipment, rig.Flat(head)).IsDone);
        Assert.Equal(3, rig.Bag.Slots.Single(static s => !s.IsEmpty && s.Item == GearItems.Cap).Wear);
    }

    [Fact]
    public void Given_AWornHatHeldOverTheWornSlot_When_ItIsPutThere_Then_TheEquipmentKeepsItsWear()
    {
        var rig = new MoveRig().With(12, GearItems.Cap, 1).Worn(12, 2);
        var head = GearItems.Slot("clothing", "head");

        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);
        Assert.True(rig.Ops.LeftClick(ContainerIds.Equipment, rig.Flat(head)).IsDone);

        Assert.Equal(new ItemStack(GearItems.Cap, 1, 2), rig.Sim.Equipment.Get(head));
    }

    [Fact]
    public void Given_AWornAxe_When_ThePackIsSorted_Then_ItKeepsItsWearAndStaysApartFromANewOne()
    {
        var rig = new MoveRig().With(15, Axe, 1).Worn(15, Worn).With(17, Axe, 1).With(20, MoveRig.Apple, 3);

        Assert.True(rig.Ops.Sort().IsDone);

        var axes = rig.Bag.Slots.Where(static s => !s.IsEmpty && s.Item == TestItems.Axe).Select(static s => s.Wear).Order().ToArray();
        Assert.Equal([0, Worn], axes);
    }

    [Fact]
    public void Given_AWornAxeInTheTrash_When_ItIsTakenBack_Then_ItKeepsItsWear()
    {
        var rig = RigWithAWornAxe();
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);
        _ = rig.Ops.LeftClick(ContainerIds.Trash, 0);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Sim.Trash.Stack);
        _ = rig.Ops.LeftClick(ContainerIds.Trash, 0);
        Assert.Equal(Worn, rig.Sim.Held.Stack.Wear);
    }

    [Fact]
    public void Given_AWornAxeHeld_When_TheHeldStackIsReturned_Then_ItIsBackWithItsWear()
    {
        var rig = RigWithAWornAxe();
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);

        Assert.True(rig.Ops.ReturnHeld().IsDone);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Bag.Slots[12]);
    }

    [Fact]
    public void Given_AWornStackInTheBag_When_ANewOneOfTheItemIsAdded_Then_ItTakesAnotherSlot()
    {
        var rig = RigWithAWornAxe(0);

        _ = rig.Bag.Add(Axe, 1);

        Assert.Equal(new ItemStack(Axe, 1, Worn), rig.Bag.Slots[0]);
        Assert.Equal(new ItemStack(Axe, 1, 0), rig.Bag.Slots[1]);
    }

    [Fact]
    public void Given_AWornAxe_When_OneOfThemIsRemovedFromItsSlot_Then_TheSlotIsEmpty()
    {
        var rig = RigWithAWornAxe(0);

        Assert.True(rig.Bag.RemoveFromSlot(0, 1));

        Assert.True(rig.Bag.Slots[0].IsEmpty);
    }
}
