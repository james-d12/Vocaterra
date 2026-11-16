using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The worn bag in the simulation (I03b): the commands change the active count, a refused change says why, and the state and the digest carry the count.</summary>
public sealed class BagSimulationTests
{
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");

    [Fact]
    public void Given_NoBag_When_TheSimulationStarts_Then_TheBagHasThirtySlotsFromTheData()
    {
        var sim = GearItems.Sim();

        Assert.Equal(30, sim.Inventory.SlotCount);
        Assert.Equal(sim.Equipment.Data.BagSlots, sim.Inventory.BaseSlots);
        Assert.Equal(30, sim.CaptureState().Slots.Count);
    }

    [Fact]
    public void Given_TheLargeBagInTheBag_When_TheEquipCommandIsStepped_Then_ThereAreFiftySlotsAndTheStateCarriesThem()
    {
        var sim = GearItems.Sim();
        var slot = GearItems.Bag(sim, GearItems.Rucksack);

        sim.Step([VoxelCommand.Equip(slot, Back)]);

        Assert.Equal(50, sim.Inventory.SlotCount);
        var state = sim.CaptureState();
        Assert.Equal(50, state.Slots.Count);
        Assert.NotNull(state.Worn);
    }

    [Fact]
    public void Given_AFullBagOfThirty_When_AnItemIsGranted_Then_ItDoesNotFitUntilABagIsWorn()
    {
        var sim = GearItems.Sim();
        Assert.Equal(0, sim.GrantItems(ItemId.Berry, 99 * 30));
        Assert.Equal(1, sim.GrantItems(ItemId.Berry, 1));

        sim.Inventory.SetSlot(0, new ItemStack(GearItems.Sack, 1));
        Assert.Equal(EquipResult.Done, sim.Equip(0, Back));

        Assert.Equal(0, sim.GrantItems(ItemId.Berry, 99 * 11)); // the slot the bag left and the ten it adds
        Assert.Equal(1, sim.GrantItems(ItemId.Berry, 1)); // full again at forty
        Assert.Equal(40, sim.Inventory.SlotCount);
    }

    [Fact]
    public void Given_ABagWithItemsInItsSlots_When_TheUnequipCommandIsStepped_Then_TheEventCarriesTheNewReasonAndNothingChanges()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Sack, 3), Back);
        sim.Inventory.SetSlot(35, new ItemStack(TestItems.Apple, 1));
        var before = sim.ComputeStateDigest();

        sim.Step([VoxelCommand.Unequip(Back)]);

        var refused = Assert.Single(sim.Events, static e => e.Kind == SimulationEventKind.EquipRefused);
        Assert.Equal((int)EquipResult.BagNotEmpty, refused.Detail);
        Assert.Equal(40, sim.Inventory.SlotCount);
        Assert.Equal(GearItems.Sack, sim.Equipment.Get(Back).Item);
        Assert.NotEqual(before, sim.ComputeStateDigest()); // the tick moved the clock; the bag and the worn bag did not change
        Assert.Equal(new ItemStack(TestItems.Apple, 1), sim.Inventory.Slots[35]);
    }

    [Fact]
    public void Given_AWornBagAndItemsInItsSlots_When_TheStateIsRestoredIntoAFreshSimulation_Then_TheCountTheItemsAndTheDigestAreBack()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Rucksack, 3), Back);
        sim.Inventory.SetSlot(47, new ItemStack(TestItems.Apple, 1));
        sim.Step();
        var digest = sim.ComputeStateDigest();

        var restored = GearItems.Sim();
        restored.Restore(sim.CaptureState());

        Assert.Equal(50, restored.Inventory.SlotCount);
        Assert.Equal(new ItemStack(TestItems.Apple, 1), restored.Inventory.Slots[47]);
        Assert.Equal(GearItems.Rucksack, restored.Equipment.Get(Back).Item);
        Assert.Equal(digest, restored.ComputeStateDigest());
    }

    [Fact]
    public void Given_ASimulationWithABiggerBagThanTheStateHas_When_TheStateIsRestored_Then_TheBagShrinksAndNothingOfTheOldBagStays()
    {
        var plain = GearItems.Sim();
        plain.Step();
        var big = GearItems.Sim();
        _ = big.Equip(GearItems.Bag(big, GearItems.Rucksack, 3), Back);
        big.Inventory.SetSlot(48, new ItemStack(TestItems.Apple, 1));

        big.Restore(plain.CaptureState());

        Assert.Equal(30, big.Inventory.SlotCount);
        Assert.True(big.Equipment.IsEmpty);
        Assert.Equal(plain.ComputeStateDigest(), big.ComputeStateDigest());
    }

    [Fact]
    public void Given_AStateWhoseSlotsDoNotMatchTheWornBag_When_ItIsRestored_Then_ItIsRefused()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Sack, 3), Back);
        var state = sim.CaptureState();
        var fresh = GearItems.Sim();

        _ = Assert.Throws<ArgumentException>(() => fresh.Restore(state with { Slots = state.Slots.Take(30).ToArray() }));
        _ = Assert.Throws<ArgumentException>(() => fresh.Restore(state with { Worn = null }));
    }

    [Fact]
    public void Given_TheSameSeedAndTheSameCommandsWithABagWornAndSwapped_When_TheyRunTwice_Then_TheDigestsAreEqual()
    {
        string Run()
        {
            var sim = GearItems.Sim();
            var small = GearItems.Bag(sim, GearItems.Sack, 3);
            var large = GearItems.Bag(sim, GearItems.Rucksack, 4);
            sim.Step([VoxelCommand.Equip(small, Back)]);
            sim.Inventory.SetSlot(33, new ItemStack(ItemId.Wood, 7));
            sim.Step();
            sim.Step([VoxelCommand.Equip(large, Back)]); // the small bag takes slot 4
            sim.Inventory.SetSlot(45, new ItemStack(ItemId.Berry, 3));
            sim.Step([VoxelCommand.Equip(4, Back)]); // refused: slot 45 would be dropped, and slot 3 holds nothing to swap
            sim.Inventory.SetSlot(45, ItemStack.Empty);
            sim.Step([VoxelCommand.Equip(4, Back)]); // allowed now
            sim.Step();
            return sim.ComputeStateDigest();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Given_TheSameBagContentsWithAndWithoutAWornBag_When_TheDigestIsTaken_Then_TheyDiffer()
    {
        var without = GearItems.Sim();
        var with = GearItems.Sim();
        _ = with.Equip(GearItems.Bag(with, GearItems.Sack, 3), Back);

        Assert.NotEqual(without.ComputeStateDigest(), with.ComputeStateDigest()); // the worn bag and the active count are in it
    }

    [Fact]
    public void Given_NothingWornAndNoBag_When_TheDigestIsTaken_Then_ItCoversThirtySlotsAndNothingElse()
    {
        var a = GearItems.Sim();
        var b = GearItems.Sim();
        b.Inventory.SetSlot(29, new ItemStack(ItemId.Wood, 1));

        Assert.NotEqual(a.ComputeStateDigest(), b.ComputeStateDigest()); // the last slot counts
        b.Inventory.SetSlot(29, ItemStack.Empty);
        Assert.Equal(a.ComputeStateDigest(), b.ComputeStateDigest());
    }
}
