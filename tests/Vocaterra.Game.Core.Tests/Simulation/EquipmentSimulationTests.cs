using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The equipment in the simulation (I03a): the equip and unequip commands applied in the tick, their events, the state and the digest (nothing worn leaves the digest as it was), and the start aid.</summary>
public sealed class EquipmentSimulationTests
{
    private static readonly EquipSlot Body = GearItems.Slot("clothing", "body");

    [Fact]
    public void Given_AShirtInTheBag_When_TheEquipCommandIsStepped_Then_ItIsWornAndTheEventSaysSo()
    {
        var sim = GearItems.Sim();
        var bag = GearItems.Bag(sim, GearItems.Shirt);

        sim.Step([VoxelCommand.Equip(bag, Body)]);

        Assert.Equal(GearItems.Shirt, sim.Equipment.Get(Body).Item);
        Assert.True(sim.Inventory.Slots[bag].IsEmpty);
        var raised = Assert.Single(sim.Events, static e => e.Kind == SimulationEventKind.ItemEquipped);
        Assert.Equal((int)GearItems.Shirt, raised.Detail);
        Assert.Equal(sim.Equipment.Data.FlatIndex(Body), raised.Amount);
    }

    [Fact]
    public void Given_ARefusedEquip_When_ItIsStepped_Then_NothingChangesAndTheEventCarriesTheReason()
    {
        var sim = GearItems.Sim();
        var bag = GearItems.Bag(sim, GearItems.Hat);

        sim.Step([VoxelCommand.Equip(bag, Body)]);

        var refused = Assert.Single(sim.Events, static e => e.Kind == SimulationEventKind.EquipRefused);
        Assert.Equal((int)EquipResult.WrongSlot, refused.Detail);
        Assert.True(sim.Equipment.IsEmpty);
        Assert.Equal(GearItems.Hat, sim.Inventory.Slots[bag].Item);
        var control = GearItems.Sim();
        _ = GearItems.Bag(control, GearItems.Hat);
        control.Step();
        Assert.Equal(control.ComputeStateDigest(), sim.ComputeStateDigest());
    }

    [Fact]
    public void Given_AWornShirt_When_TheUnequipCommandsAreStepped_Then_ItGoesToTheChosenSlotAndTheFirstFreeOne()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt, 5), Body);

        sim.Step([VoxelCommand.Unequip(Body, 20)]);
        Assert.Equal(GearItems.Shirt, sim.Inventory.Slots[20].Item);
        Assert.Single(sim.Events, static e => e.Kind == SimulationEventKind.ItemUnequipped);

        _ = sim.Equip(20, Body);
        sim.Step([VoxelCommand.Unequip(Body)]);
        Assert.Equal(GearItems.Shirt, sim.Inventory.Slots[0].Item); // the first free slot
        Assert.True(sim.Equipment.IsEmpty);
    }

    [Fact]
    public void Given_AnUnequipWithAFullBag_When_ItIsStepped_Then_ItIsRefusedAndTheItemStaysWorn()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt, 5), Body);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            sim.Inventory.SetSlot(i, new ItemStack(TestItems.Apple, 1));
        }

        sim.Step([VoxelCommand.Unequip(Body)]);

        Assert.Equal((int)EquipResult.BagFull, Assert.Single(sim.Events, static e => e.Kind == SimulationEventKind.EquipRefused).Detail);
        Assert.Equal(GearItems.Shirt, sim.Equipment.Get(Body).Item);
    }

    [Fact]
    public void Given_NothingWorn_When_TheDigestIsComputed_Then_ItIsTheDigestOfASimulationThatHasNoEquipmentAtAll()
    {
        var withGear = GearItems.Sim();
        var without = VoxelSimulation.Create(VoxelTestSupport.FlatScene(), GearItems.Catalogue);
        VoxelCommand[] walk = [VoxelCommand.Move(1, 0), VoxelCommand.Look(40, 3)];

        VoxelTestSupport.Run(withGear, 90, walk);
        VoxelTestSupport.Run(without, 90, walk);

        Assert.Equal(without.ComputeStateDigest(), withGear.ComputeStateDigest());
    }

    [Fact]
    public void Given_AShirtPutOnAndTakenOff_When_TheDigestIsComputed_Then_ItReturnsToTheUnwornDigestOnlyWhileNothingIsWorn()
    {
        var sim = GearItems.Sim();
        var bag = GearItems.Bag(sim, GearItems.Shirt);
        var unworn = sim.ComputeStateDigest();

        _ = sim.Equip(bag, Body);
        var worn = sim.ComputeStateDigest();
        _ = sim.Unequip(Body, bag);

        Assert.NotEqual(unworn, worn);
        Assert.Equal(unworn, sim.ComputeStateDigest());
    }

    [Fact]
    public void Given_TheSameCommandListWithEquips_When_TwoRunsOfOneSeedStep_Then_TheDigestsAreTheSameAtEveryTick()
    {
        string[] Run()
        {
            var sim = GearItems.Sim();
            var shirt = GearItems.Bag(sim, GearItems.Shirt, 5);
            var ring = GearItems.Bag(sim, GearItems.Ring, 6);
            var scarf = GearItems.Bag(sim, GearItems.Scarf, 7);
            var digests = new List<string>();
            for (var tick = 0; tick < 120; tick++)
            {
                var commands = tick switch
                {
                    10 => [VoxelCommand.Equip(shirt, Body)],
                    20 => [VoxelCommand.Equip(ring, GearItems.Nth("accessories", 1)), VoxelCommand.Equip(scarf, GearItems.Slot("cosmetic", "body"))],
                    50 => [VoxelCommand.Equip(scarf, GearItems.Slot("clothing", "body"))],
                    80 => [VoxelCommand.Unequip(GearItems.Nth("accessories", 1), 30)],
                    _ => new[] { VoxelCommand.Move(0, 1) },
                };
                sim.Step(commands);
                digests.Add(sim.ComputeStateDigest());
            }

            return [.. digests];
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Given_DifferentWornItems_When_TheDigestIsComputed_Then_TheyDiffer()
    {
        var a = GearItems.Sim();
        var b = GearItems.Sim();
        _ = a.Equip(GearItems.Bag(a, GearItems.Shirt), Body);
        _ = b.Equip(GearItems.Bag(b, GearItems.Scarf), Body);
        var c = GearItems.Sim();
        _ = c.Equip(GearItems.Bag(c, GearItems.Shirt), GearItems.Slot("cosmetic", "body"));

        Assert.Equal(3, new[] { a.ComputeStateDigest(), b.ComputeStateDigest(), c.ComputeStateDigest() }.Distinct().Count());
    }

    [Fact]
    public void Given_WornItems_When_TheStateIsCapturedAndRestored_Then_AnotherSimulationWearsThemWithTheSameDigest()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt, 5), Body);
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Hat, 6), GearItems.Slot("clothing", "head"));
        var restored = GearItems.Sim();

        restored.Restore(sim.CaptureState());

        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
        Assert.Equal(sim.Equipment.Slots, restored.Equipment.Slots);
        Assert.Equal(50, restored.Stats.Get("warmth"));
    }

    [Fact]
    public void Given_NothingWorn_When_TheStateIsCaptured_Then_ItCarriesNoWornList()
    {
        Assert.Null(GearItems.Sim().CaptureState().Worn);
    }

    [Fact]
    public void Given_ASimulationWithNoGroups_When_AnEquipIsAsked_Then_ItIsRefusedAsNoSuchSlot()
    {
        var sim = VoxelSimulation.Create(VoxelTestSupport.FlatScene(), GearItems.Catalogue);
        var bag = GearItems.Bag(sim, GearItems.Shirt);

        Assert.Equal(EquipResult.NoSuchSlot, sim.Equip(bag, new EquipSlot(0, 0)));
        Assert.Equal(0, sim.Equipment.Data.SlotCount);
    }

    [Fact]
    public void Given_AnItemThatCanBeWorn_When_TheStartAidWearsIt_Then_ItIsGivenAndWornInTheFirstSlotThatTakesIt()
    {
        var sim = GearItems.Sim();

        var result = sim.WearFromStart(GearItems.Scarf, out var slot);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(Body, slot);
        Assert.Equal(GearItems.Scarf, sim.Equipment.Get(Body).Item);
        Assert.Equal(0, sim.Inventory.Count(GearItems.Scarf));
        Assert.Equal(EquipResult.Done, sim.WearFromStart(GearItems.Scarf, out var second)); // the next free slot of a group that takes it
        Assert.Equal(GearItems.Slot("armour", "body"), second);
    }

    [Fact]
    public void Given_ItemsThatCannotBeWornOrWornNoMore_When_TheStartAidIsAsked_Then_ItRefusesAndGivesNothing()
    {
        var sim = GearItems.Sim();

        Assert.Equal(EquipResult.NotWearable, sim.WearFromStart(TestItems.Apple, out _));
        Assert.Equal(0, sim.Inventory.Count(TestItems.Apple));
    }

    [Fact]
    public void Given_AnItemWhoseWearKindNoSlotHas_When_TheStartAidWearsIt_Then_ItIsRefusedWithNoSlotAndNothingIsGiven()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)230, "test-wings", 1, "test-wings", ItemHeld.Sprite, ["clothing"], Wear: "wings"));
        var sim = VoxelSimulation.Create(VoxelTestSupport.FlatScene(), items, equipment: GearItems.Data);

        Assert.Equal(EquipResult.WrongSlot, sim.WearFromStart((ItemId)230, out var slot));
        Assert.Null(slot);
        Assert.Equal(0, sim.Inventory.Count((ItemId)230));
    }

    [Fact]
    public void Given_ARingWornAlready_When_TheStartAidWearsAnotherRing_Then_ItTakesTheSameSlotAndTheOldOneGoesToTheBag()
    {
        var sim = GearItems.Sim();
        _ = sim.WearFromStart(GearItems.Ring, out var first);

        Assert.Equal(EquipResult.Done, sim.WearFromStart(GearItems.Ring, out var second));

        Assert.Equal(first, second); // a duplicate may not go in another slot, so the first slot is swapped
        Assert.Equal(1, sim.Inventory.Count(GearItems.Ring));
    }
}
