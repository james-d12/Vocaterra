using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>What an item at its durability does (I07): a tool works at bare hands' speed, gear gives no stats, a bag still holds its slots, nothing is lost, and the tool says so once.</summary>
public sealed class WornOutTests
{
    private static readonly EquipSlot Head = GearItems.Slot("clothing", "head");
    private static readonly EquipSlot Body = GearItems.Slot("armour", "body"); // the mail is armour: the armour group's body slot
    private static readonly EquipSlot Hand = GearItems.Slot("offhand", "offhand");
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");

    private static VoxelSimulation HatchetOf(int wear)
    {
        var sim = WearRig.Sim();
        sim.Inventory.SetSlot(0, new ItemStack(GearItems.Hatchet, 1, wear));
        sim.Step([VoxelCommand.SelectSlot(0)]);
        return sim;
    }

    [Fact]
    public void Given_ATrunkAndAHatchetOneGatherFromWornOut_When_TheTicksAreAsked_Then_TheToolIsStillFast()
    {
        Assert.Equal(15, HatchetOf(2).GatherTicksOf(BlockId.Trunk));
    }

    [Fact]
    public void Given_AWornOutHatchet_When_TheTicksAreAsked_Then_ItWorksAtBareHandsSpeed()
    {
        var sim = HatchetOf(3);

        Assert.Equal(BlockDefinition.TrunkGatherTicks, sim.GatherTicksOf(BlockId.Trunk));
        WearRig.Chop(sim);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood)); // it still gathers, as hands do
    }

    [Fact]
    public void Given_AHatchetThatWearsOutInAGather_When_ItIsThrough_Then_ItIsNotLostAndToolWornIsRaisedOnce()
    {
        var sim = HatchetOf(2);
        var raised = 0;

        for (var chop = 0; chop < 3; chop++)
        {
            sim.Grid.SetBlock(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z, BlockId.Trunk);
            for (var i = 0; i < 80; i++)
            {
                sim.Step([VoxelCommand.Interact(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z)]);
                raised += sim.Events.Count(static e => e.Kind == SimulationEventKind.ToolWorn);
            }
        }

        Assert.Equal(1, raised);
        Assert.Equal(new ItemStack(GearItems.Hatchet, 1, 3), sim.Inventory.Slots[0]);
    }

    [Fact]
    public void Given_ToolWornRaised_When_TheEventIsRead_Then_ItNamesTheTool()
    {
        var sim = HatchetOf(2);
        SimulationEvent? worn = null;
        sim.Grid.SetBlock(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z, BlockId.Trunk);
        for (var i = 0; i < 15; i++)
        {
            sim.Step([VoxelCommand.Interact(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z)]);
            worn ??= sim.Events.Where(static e => e.Kind == SimulationEventKind.ToolWorn).Cast<SimulationEvent?>().FirstOrDefault();
        }

        Assert.Equal((int)GearItems.Hatchet, worn!.Value.Detail);
    }

    [Fact]
    public void Given_ACapAtItsLastHour_When_ItWearsOut_Then_ItGivesNoWarmthButStaysWorn()
    {
        var sim = WearRig.Sim(hourInOneTick: true);
        WearRig.Wear(sim, GearItems.Cap, Head, wear: 3);
        sim.RefreshStats();
        Assert.Equal(20, sim.Stats.Sum(StatKeys.Warmth));

        sim.Step([]);

        Assert.Equal(0, sim.Stats.Sum(StatKeys.Warmth));
        Assert.Equal(new ItemStack(GearItems.Cap, 1, 4), sim.Equipment.Get(Head));
        Assert.Contains(sim.Events, static e => e.Kind == SimulationEventKind.GearWornOut);
    }

    [Fact]
    public void Given_WornOutArmourAndShield_When_TheStatsAreBuilt_Then_NeitherGivesArmour()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Mail, Body);
        WearRig.Wear(sim, GearItems.Buckler, Hand);
        sim.RefreshStats();
        Assert.Equal(8, sim.Stats.Sum(StatKeys.Armour));

        _ = sim.Wear.Apply("damage", 6);

        Assert.Equal(5, sim.Stats.Sum(StatKeys.Armour)); // the buckler (6) is worn out at once, the mail (10) is not yet
        _ = sim.Wear.Apply("damage", 4);
        Assert.Equal(0, sim.Stats.Sum(StatKeys.Armour));
    }

    [Fact]
    public void Given_AWornOutLamp_When_TheStatsAreBuilt_Then_ItGivesNoLight()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Lamp, Hand, wear: 2);
        sim.RefreshStats();

        Assert.Equal(0, sim.Stats.Sum(StatKeys.Light));
    }

    [Fact]
    public void Given_ASatchetWornOutWithItemsInItsSlots_When_ItWearsOut_Then_ItStillHoldsItsSlotsAndNothingIsDropped()
    {
        var sim = WearRig.Sim(hourInOneTick: true);
        WearRig.Wear(sim, GearItems.Satchel, Back, wear: 1);
        Assert.Equal(40, sim.Inventory.SlotCount);
        sim.Inventory.SetSlot(37, new ItemStack(TestItems.Apple, 5));

        sim.Step([]);

        Assert.Equal(2, sim.Equipment.Get(Back).Wear);
        Assert.Equal(40, sim.Inventory.SlotCount);
        Assert.Equal(new ItemStack(TestItems.Apple, 5), sim.Inventory.Slots[37]);
        Assert.Equal(10, sim.Equipment.Capacity);
    }

    [Fact]
    public void Given_AWornOutPiece_When_ItIsTakenOffAndWornAgain_Then_ItIsStillWornOut()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Cap, Head, wear: 4);

        Assert.Equal(EquipResult.Done, sim.Unequip(Head));
        var slot = Enumerable.Range(0, sim.Inventory.SlotCount).First(i => sim.Inventory.Slots[i] is { IsEmpty: false } s && s.Item == GearItems.Cap);
        Assert.Equal(EquipResult.Done, sim.Equip(slot, Head));

        sim.RefreshStats();
        Assert.Equal(0, sim.Stats.Sum(StatKeys.Warmth));
        Assert.Equal(4, sim.Equipment.Get(Head).Wear);
    }
}
