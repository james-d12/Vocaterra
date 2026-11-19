using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>A flat-world simulation with the test gear for the tests of wear (I07): a trunk two blocks ahead to gather, and the option of a day that runs an in-game hour in one tick.</summary>
internal static class WearRig
{
    /// <summary>Where the trunk grows in the flat scene (two blocks ahead of the player).</summary>
    public static readonly BlockPos Ahead = new(32, VoxelTestSupport.GroundTop + 2, 30);

    /// <summary>A simulation with the test gear and the shipped slot groups; <paramref name="hourInOneTick"/> makes the day cycle 24 ticks long, so every tick is an in-game hour.</summary>
    public static VoxelSimulation Sim(bool hourInOneTick = false)
    {
        var day = hourInOneTick ? DayData.Default with { CycleSeconds = 0.4 } : null;
        return VoxelSimulation.Create(VoxelTestSupport.FlatScene(), GearItems.Catalogue, day, equipment: GearItems.Data);
    }

    /// <summary>Puts the item in the bag slot, with the wear, and wears it in its slot.</summary>
    public static void Wear(VoxelSimulation sim, ItemId item, EquipSlot slot, int wear = 0)
    {
        sim.Inventory.SetSlot(20, new ItemStack(item, 1, wear));
        Assert.Equal(EquipResult.Done, sim.Equip(20, slot));
    }

    /// <summary>Gathers the trunk ahead to the end (the tick the block goes) and grows another in its place.</summary>
    public static void Chop(VoxelSimulation sim)
    {
        sim.Grid.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, BlockId.Trunk);
        var ticks = sim.GatherTicksOf(BlockId.Trunk);
        for (var i = 0; i < ticks; i++)
        {
            sim.Step([VoxelCommand.Interact(Ahead.X, Ahead.Y, Ahead.Z)]);
        }
    }
}
