using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>Flowing water inside the running simulation (water-plan H06): waking on a gathered or a placed block, damming, pouring, saving mid-flood and determinism.</summary>
public sealed class WaterFlowTests
{
    private const int Ground = GroundTop + 1;

    // In the flat scene the player stands at (32.5, 10, 32.5): a cell two blocks north is in reach.
    private static VoxelCommand InteractAt(int x, int y, int z) => VoxelCommand.Interact(x, y, z);

    // A channel one cell wide along x at z = 30, between walls, closed at its west end: x = 28 is the plug, the stream starts at x = 30.
    private static void BuildChannel(VoxelGrid grid)
    {
        for (var x = 28; x <= 44; x++)
        {
            grid.SetBlock(x, Ground, 29, BlockId.Stone);
            grid.SetBlock(x, Ground, 31, BlockId.Stone);
        }

        grid.SetBlock(28, Ground, 30, BlockId.Stone);
    }

    private static VoxelSimulation SimWithChannel()
    {
        var sim = FlatSim(BuildChannel);
        Assert.True(sim.PourWater(new BlockPos(30, Ground, 30)));
        return sim;
    }

    [Fact]
    public void Given_ABuiltWorldWithWater_When_ManyTicksPass_Then_NothingMovesAndNothingIsScheduled()
    {
        var sim = FlatSim(g => g.SetBlock(20, Ground, 20, BlockId.Water));
        var digest = sim.Grid.ComputeDigestHex();
        var state = sim.ComputeStateDigest();

        Run(sim, 60 * 20);

        Assert.Equal(digest, sim.Grid.ComputeDigestHex());
        Assert.Equal(0, sim.Flow.Queue.Count);
        Assert.NotEqual(state, sim.ComputeStateDigest()); // only the tick moved
    }

    [Fact]
    public void Given_APouredSource_When_TheSimulationRuns_Then_ItSpreadsOverAFewSecondsAndStops()
    {
        var sim = FlatSim();

        Assert.True(sim.PourWater(new BlockPos(20, Ground, 20)));
        Assert.False(sim.PourWater(new BlockPos(20, Ground, 20))); // already a source
        Assert.True(sim.Flow.Queue.Count > 0);
        Run(sim, 30);
        Assert.Equal(WaterLevel.Flowing(2), sim.Grid.GetWater(22, Ground, 20));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(23, Ground, 20));

        Run(sim, 60 * 5);
        Assert.Equal(WaterLevel.Flowing(7), sim.Grid.GetWater(27, Ground, 20));
        Assert.Equal(0, sim.Flow.Queue.Count);
        var settled = sim.Grid.ComputeDigestHex();
        Run(sim, 60 * 5);
        Assert.Equal(settled, sim.Grid.ComputeDigestHex());
    }

    [Fact]
    public void Given_APourIntoAStoneCell_When_Tried_Then_ItIsRefused()
    {
        var sim = FlatSim();

        Assert.False(sim.PourWater(new BlockPos(20, GroundTop, 20)));
        Assert.False(sim.PourWater(new BlockPos(-1, Ground, 20)));
        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    [Fact]
    public void Given_WaterBehindAGatherableBlock_When_ThePlayerGathersIt_Then_TheWaterFlowsIntoTheGap()
    {
        // A source shut in by stone and a trunk: nothing moves until the trunk is gathered.
        var sim = FlatSim(g =>
        {
            g.SetBlock(32, Ground, 29, BlockId.Water);
            g.SetBlock(31, Ground, 29, BlockId.Stone);
            g.SetBlock(33, Ground, 29, BlockId.Stone);
            g.SetBlock(32, Ground, 28, BlockId.Stone);
            g.SetBlock(31, Ground, 30, BlockId.Stone);
            g.SetBlock(33, Ground, 30, BlockId.Stone);
            g.SetBlock(32, Ground, 30, BlockId.Trunk);
        });
        Run(sim, 200);
        var sealedDigest = sim.Grid.ComputeDigestHex();
        Assert.Equal(0, sim.Flow.Queue.Count);

        Run(sim, BlockDefinition.Get(BlockId.Trunk).GatherTicks, InteractAt(32, Ground, 30));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, Ground, 30));
        Assert.True(sim.Flow.Queue.Count > 0, "the gather woke the water");
        Run(sim, 30);

        Assert.Equal(BlockId.Water, sim.Grid.GetBlock(32, Ground, 30));
        Assert.Equal(WaterLevel.Flowing(1), sim.Grid.GetWater(32, Ground, 30));
        Assert.NotEqual(sealedDigest, sim.Grid.ComputeDigestHex());
    }

    [Fact]
    public void Given_AStream_When_ThePlayerPlacesPlanksInTheWater_Then_TheyDisplaceItAndDamTheStream()
    {
        var sim = SimWithChannel();
        Run(sim, 60 * 6);
        Assert.Equal(WaterLevel.Flowing(7), sim.Grid.GetWater(37, Ground, 30));
        sim.GrantItems(ItemId.Wood, 2);

        sim.Step([VoxelCommand.Place(ItemId.Wood, 33, Ground, 30)]); // into a water cell

        var placed = Assert.Single(sim.Events, e => e.Kind == SimulationEventKind.BlockPlaced);
        Assert.Equal((33, Ground, 30, (int)BlockId.Planks), (placed.X, placed.Y, placed.Z, placed.Detail));
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(33, Ground, 30));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
        Assert.True(sim.Flow.Queue.Count > 0, "the placed block woke the stream");

        Run(sim, 60 * 6);
        Assert.Equal(WaterLevel.Flowing(2), sim.Grid.GetWater(32, Ground, 30));
        for (var x = 34; x <= 37; x++)
        {
            Assert.Equal(BlockId.Air, sim.Grid.GetBlock(x, Ground, 30)); // the stream beyond the dam ran dry
        }

        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    [Fact]
    public void Given_ADam_When_ItIsRemoved_Then_TheStreamRunsOnAgain()
    {
        var sim = SimWithChannel();
        sim.GrantItems(ItemId.Wood, 1);
        sim.Step([VoxelCommand.Place(ItemId.Wood, 33, Ground, 30)]);
        Run(sim, 60 * 6);
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(34, Ground, 30));

        sim.Grid.SetBlock(33, Ground, 30, BlockId.Air); // the dam goes
        Run(sim, 60 * 6);

        Assert.Equal(WaterLevel.Flowing(3), sim.Grid.GetWater(33, Ground, 30));
        Assert.Equal(WaterLevel.Flowing(7), sim.Grid.GetWater(37, Ground, 30));
    }

    [Fact]
    public void Given_WaterOnlyBelowACell_When_APlankIsPlacedThere_Then_WaterStillCountsAsSomethingToBuildOn()
    {
        var sim = FlatSim(g => g.SetBlock(34, Ground, 33, BlockId.Water));
        sim.GrantItems(ItemId.Wood, 1);

        sim.Step([VoxelCommand.Place(ItemId.Wood, 34, Ground + 1, 33)]);

        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(34, Ground + 1, 33));
    }

    [Fact]
    public void Given_ASolidBlockPlacedOnThePlayersOwnWaterCell_When_Tried_Then_ItIsRefused()
    {
        var sim = FlatSim(g => g.SetBlock(32, Ground, 32, BlockId.Water));
        sim.GrantItems(ItemId.Wood, 1);

        sim.Step([VoxelCommand.Place(ItemId.Wood, 32, Ground, 32)]);

        Assert.Equal(BlockId.Water, sim.Grid.GetBlock(32, Ground, 32));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void Given_TwoRunsOfTheSameCommands_When_Compared_Then_EveryDigestAgrees()
    {
        var a = SimWithChannel();
        var b = SimWithChannel();
        a.GrantItems(ItemId.Wood, 1);
        b.GrantItems(ItemId.Wood, 1);

        for (var tick = 0; tick < 600; tick++)
        {
            VoxelCommand[] commands = tick == 200 ? [VoxelCommand.Place(ItemId.Wood, 33, Ground, 30)] : [];
            a.Step(commands);
            b.Step(commands);
            Assert.Equal(a.ComputeStateDigest(), b.ComputeStateDigest());
        }
    }

    [Fact]
    public void Given_WaterWaitingToFlow_When_TheStateDigestIsRead_Then_TheWaitingCellsCountInIt()
    {
        var waiting = FlatSim();
        var idle = FlatSim();
        _ = waiting.PourWater(new BlockPos(20, Ground, 20));
        idle.Grid.SetBlock(20, Ground, 20, BlockId.Water);
        idle.Flow.Queue.Clear();

        Assert.Equal(waiting.Grid.ComputeDigestHex(), idle.Grid.ComputeDigestHex());
        Assert.NotEqual(waiting.ComputeStateDigest(), idle.ComputeStateDigest());
    }

    [Fact]
    public void Given_AFloodInTheMiddle_When_SavedAndRestored_Then_TheDigestIsTheSameAndTheFloodFinishesTheSameWay()
    {
        var original = SimWithChannel();
        var open = FlatSim();
        _ = open.PourWater(new BlockPos(48, Ground, 48));
        Run(original, 50); // mid-flood: the stream has taken three steps
        Run(open, 50);

        var restored = Restored(original);
        var restoredOpen = Restored(open);

        Assert.True(original.Flow.Queue.Count > 0);
        Assert.Equal(original.ComputeStateDigest(), restored.ComputeStateDigest());
        Assert.Equal(open.ComputeStateDigest(), restoredOpen.ComputeStateDigest());
        for (var tick = 0; tick < 400; tick++)
        {
            original.Step();
            restored.Step();
            open.Step();
            restoredOpen.Step();
            Assert.Equal(original.ComputeStateDigest(), restored.ComputeStateDigest());
            Assert.Equal(open.ComputeStateDigest(), restoredOpen.ComputeStateDigest());
        }

        Assert.Equal(WaterLevel.Flowing(7), restored.Grid.GetWater(37, Ground, 30));
        Assert.Equal(0, restored.Flow.Queue.Count);
    }

    [Fact]
    public void Given_AStateWithoutAFlowQueue_When_Restored_Then_NothingWaits()
    {
        var sim = FlatSim();
        _ = sim.PourWater(new BlockPos(20, Ground, 20));

        sim.Restore(sim.CaptureState() with { Flow = null });

        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    // What a save does: the scene diff onto a freshly built scene, then the simulation's state.
    private static VoxelSimulation Restored(VoxelSimulation sim)
    {
        var state = sim.CaptureState();
        var scene = FlatScene(g =>
        {
            if (sim.Grid.GetBlock(28, Ground, 30) == BlockId.Stone)
            {
                BuildChannel(g);
            }
        });
        var pristine = scene.Grid.Clone();
        var diff = VoxelGridDiff.Capture(sim.Grid, pristine);
        VoxelGridDiff.Apply(scene.Grid, diff);
        var restored = VoxelSimulation.Create(scene, TestItems.Catalogue);
        restored.Restore(state);
        return restored;
    }
}
