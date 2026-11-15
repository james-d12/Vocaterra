using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>Filling the bottle at the water (S6): an empty bottle and half a second of holding make a bottle of water, and without a bottle nothing is blocked.</summary>
public sealed class BottleTests
{
    // The flat scene's player stands at (32.5, 10, 32.5), eye at y 11.62; the water two blocks ahead (north) at the feet's level is within reach.
    private static readonly BlockPos Ahead = new(32, GroundTop + 2, 30);

    private static VoxelSimulation WithWater() => FlatSim(g => g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, BlockId.Water));

    private static VoxelCommand InteractAhead => VoxelCommand.Interact(Ahead.X, Ahead.Y, Ahead.Z);

    [Fact]
    public void TheNewItemsHaveStableIdsAndDefinitions()
    {
        Assert.Equal(26, (int)ItemId.Bottle);
        Assert.Equal(27, (int)ItemId.WaterBottle);
        Assert.Equal(28, (int)TestItems.Bread);
        Assert.Equal("bottle", TestItems.Catalogue.Get(ItemId.Bottle).Name);
        Assert.Equal("water-bottle", TestItems.Catalogue.Get(ItemId.WaterBottle).Name);
        Assert.Equal("bread", TestItems.Catalogue.Get(TestItems.Bread).Name);
        Assert.Null(BlockDefinition.PlacedBy(ItemId.Bottle));
    }

    [Fact]
    public void HoldingOnWaterWithABottleFillsItAfterTheFillTicks()
    {
        var sim = WithWater();
        sim.GrantItems(ItemId.Bottle, 1);

        Run(sim, VoxelSimulation.BottleFillTicks - 1, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Bottle));
        Assert.Equal(0, sim.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal(VoxelSimulation.BottleFillTicks - 1, sim.GatherProgress);

        sim.Step([InteractAhead]);

        Assert.Equal(0, sim.Inventory.Count(ItemId.Bottle));
        Assert.Equal(1, sim.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal(BlockId.Water, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z)); // the river is not used up
        Assert.Null(sim.GatherTarget);
        var filled = sim.Events.Single(static e => e.Kind == SimulationEventKind.BottleFilled);
        Assert.Equal((Ahead.X, Ahead.Y, Ahead.Z, (int)ItemId.WaterBottle), (filled.X, filled.Y, filled.Z, filled.Detail));
        var picked = sim.Events.Single(static e => e.Kind == SimulationEventKind.ItemPickedUp);
        Assert.Equal(((int)ItemId.WaterBottle, 1), (picked.Detail, picked.Amount));
    }

    [Fact]
    public void WaterWithoutABottleSaysSoOncePerHoldAndBlocksNothing()
    {
        var sim = WithWater();

        sim.Step([InteractAhead]);
        Assert.Contains(sim.Events, static e => e.Kind == SimulationEventKind.WaterWithoutBottle);
        for (var i = 0; i < 100; i++)
        {
            sim.Step([InteractAhead]);
            Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.WaterWithoutBottle); // once per hold
        }

        Assert.Equal(0, sim.Inventory.Count(ItemId.WaterBottle));
        Assert.Null(sim.GatherTarget);
        sim.Step([]); // letting go and holding again says it again
        sim.Step([InteractAhead]);
        Assert.Contains(sim.Events, static e => e.Kind == SimulationEventKind.WaterWithoutBottle);

        // Nothing is blocked: the player can still walk.
        var before = sim.Player.MinX;
        Run(sim, 10, VoxelCommand.Move(1, 0));
        Assert.NotEqual(before, sim.Player.MinX);
    }

    [Fact]
    public void LettingGoOrLookingAwayResetsTheFill()
    {
        var sim = WithWater();
        sim.GrantItems(ItemId.Bottle, 1);
        Run(sim, 20, InteractAhead);
        Assert.Equal(20, sim.GatherProgress);

        sim.Step([]);

        Assert.Equal(0, sim.GatherProgress);
        Run(sim, VoxelSimulation.BottleFillTicks - 1, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Bottle)); // not yet: the progress started again from zero
    }

    [Fact]
    public void AFullBottleIsNotFilledAgainAndTwoBottlesTakeTwoHolds()
    {
        var sim = WithWater();
        sim.GrantItems(ItemId.WaterBottle, 1);
        Run(sim, 100, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.WaterBottle)); // a full bottle is no empty one: nothing to fill

        sim.GrantItems(ItemId.Bottle, 2);
        Run(sim, VoxelSimulation.BottleFillTicks, InteractAhead);
        Assert.Equal(2, sim.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void TheFillIsDeterministicAndShowsInTheStateDigest()
    {
        string Digest()
        {
            var sim = WithWater();
            sim.GrantItems(ItemId.Bottle, 1);
            Run(sim, 40, InteractAhead);
            return sim.ComputeStateDigest();
        }

        Assert.Equal(Digest(), Digest());
        var empty = WithWater();
        empty.GrantItems(ItemId.Bottle, 1);
        Run(empty, 40);
        Assert.NotEqual(Digest(), empty.ComputeStateDigest());
    }

    [Fact]
    public void OutOfReachWaterIsNotFilled()
    {
        var far = new BlockPos(32, GroundTop + 2, 20);
        var sim = FlatSim(g => g.SetBlock(far.X, far.Y, far.Z, BlockId.Water));
        sim.GrantItems(ItemId.Bottle, 1);

        Run(sim, 60, VoxelCommand.Interact(far.X, far.Y, far.Z));

        Assert.Equal(1, sim.Inventory.Count(ItemId.Bottle));
        Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.WaterWithoutBottle);
    }
}
