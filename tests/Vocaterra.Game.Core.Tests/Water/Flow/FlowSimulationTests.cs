using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;
using static Vocaterra.Game.Core.Tests.Water.Flow.FlowWorld;

namespace Vocaterra.Game.Core.Tests.Water.Flow;

/// <summary>The flow step on a hand-built world: its pace, its budget and carry-over, waking, spreading, settling and draining.</summary>
public sealed class FlowSimulationTests
{
    [Fact]
    public void Given_NothingScheduled_When_ManyStepsRun_Then_TheGridIsUntouched()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Settle();
        var before = world.Grid.ComputeDigestHex();

        world.Run(15 * 100);

        Assert.Equal(before, world.Grid.ComputeDigestHex());
        Assert.Equal(0, world.Flow.Queue.Count);
    }

    [Fact]
    public void Given_ACellDue_When_TheTickIsNotAStep_Then_NothingIsUpdated()
    {
        var world = new FlowWorld();
        world.Flow.Queue.Schedule(new BlockPos(1, Ground, 1), 0);

        Assert.Equal(0, world.Flow.Step(14));
        Assert.Equal(1, world.Flow.Queue.Count);
        Assert.Equal(1, world.Flow.Step(15));
    }

    [Fact]
    public void Given_ASourceOnOpenGround_When_TheFlowRuns_Then_ItSpreadsOneCellEveryStepToSevenCellsAndStops()
    {
        var world = new FlowWorld();
        world.Source(30, Ground, 30);

        world.Run(15);
        Assert.Equal(WaterLevel.Flowing(1), world.Level(31, Ground, 30));
        Assert.False(world.IsWater(32, Ground, 30));

        world.Run(15);
        Assert.Equal(WaterLevel.Flowing(2), world.Level(32, Ground, 30));
        Assert.False(world.IsWater(33, Ground, 30));

        world.Settle();
        Assert.Equal(WaterLevel.Flowing(7), world.Level(37, Ground, 30));
        Assert.False(world.IsWater(38, Ground, 30));
        Assert.Equal(WaterLevel.Flowing(7), world.Level(30, Ground, 37));
        Assert.Equal(0, world.Flow.Queue.Count);
    }

    [Fact]
    public void Given_ASourceOnOpenGround_When_Settled_Then_ThePatchIsADiamondOfThinningWaterWithOneSource()
    {
        var world = new FlowWorld();
        world.Source(30, Ground, 30);
        world.Settle();

        var cells = world.WaterCells();

        Assert.Single(cells, c => c.Level.IsSource);
        foreach (var (position, level) in cells.Where(c => !c.Level.IsSource))
        {
            Assert.Equal(Ground, position.Y);
            Assert.Equal(Math.Abs(position.X - 30) + Math.Abs(position.Z - 30), level.Distance);
        }

        Assert.Equal(1 + (2 * 7 * 8), cells.Count); // a diamond of radius 7 holds 1 + 2 * 7 * (7 + 1) cells
    }

    [Fact]
    public void Given_ASourceInTheAir_When_TheFlowRuns_Then_ItFallsToTheGroundAndSpreadsThere()
    {
        var world = new FlowWorld();
        world.Source(30, Ground + 4, 30);
        world.Settle();

        for (var y = Ground; y < Ground + 4; y++)
        {
            Assert.Equal(WaterLevel.Flowing(1, falling: true), world.Level(30, y, 30));
        }

        Assert.Equal(WaterLevel.Flowing(1), world.Level(31, Ground, 30));
        Assert.Equal(WaterLevel.Flowing(7), world.Level(37, Ground, 30));
        Assert.False(world.IsWater(31, Ground + 1, 30)); // nothing spreads in the air
        Assert.Equal(1, world.WaterCells().Count(c => c.Level.IsSource));
    }

    [Fact]
    public void Given_ASourceAtTheEdgeOfAStep_When_TheFlowRuns_Then_TheWaterFallsOverAndSpreadsBelow()
    {
        var world = new FlowWorld();
        for (var x = 20; x <= 30; x++)
        {
            for (var z = 20; z <= 40; z++)
            {
                world.Put(x, Ground, z, BlockId.Stone); // a platform one block high
            }
        }

        world.Source(29, Ground + 1, 30);
        world.Settle();

        Assert.Equal(WaterLevel.Flowing(1), world.Level(30, Ground + 1, 30)); // along the platform to its rim
        Assert.Equal(WaterLevel.Flowing(1, falling: true), world.Level(31, Ground, 30)); // and over it
        Assert.Equal(WaterLevel.Flowing(1), world.Level(32, Ground, 30)); // then along the ground below, afresh
    }

    [Fact]
    public void Given_AWaterChannelWithAHoleAtTheEnd_When_TheFlowRuns_Then_TheWaterRunsDownTheChannelAndIntoTheHole()
    {
        var world = new FlowWorld();
        for (var x = 20; x <= 40; x++)
        {
            world.Put(x, Ground, 29, BlockId.Stone);
            world.Put(x, Ground, 31, BlockId.Stone);
        }

        world.Put(25, Floor, 30, BlockId.Air); // a hole in the channel's floor, 3 cells from the source
        world.Source(22, Ground, 30);
        world.Settle();

        Assert.Equal(WaterLevel.Flowing(1, falling: true), world.Level(25, Floor, 30)); // fell in
        Assert.False(world.IsWater(21, Ground, 30) && world.Level(21, Ground, 30).Distance > 1);
    }

    [Fact]
    public void Given_AFlood_When_TheSourceIsRemoved_Then_TheWaterDrainsAwayCompletely()
    {
        var world = new FlowWorld();
        world.Source(30, Ground, 30);
        world.Settle();
        Assert.True(world.WaterCells().Count > 1);

        world.Put(30, Ground, 30, BlockId.Air);
        world.Settle();

        Assert.Empty(world.WaterCells());
    }

    [Fact]
    public void Given_TwoSources_When_Settled_Then_NoNewSourceAppears()
    {
        var world = new FlowWorld();
        world.Source(30, Ground, 30);
        world.Source(32, Ground, 30);
        world.Settle();

        Assert.Equal(2, world.WaterCells().Count(c => c.Level.IsSource));
        Assert.Equal(WaterLevel.Flowing(1), world.Level(31, Ground, 30));
    }

    [Fact]
    public void Given_ADamBlock_When_PlacedInAStream_Then_TheWaterBeyondItDrains()
    {
        var world = ChannelWorld();
        world.Source(21, Ground, 30);
        world.Settle();
        Assert.Equal(WaterLevel.Flowing(6), world.Level(27, Ground, 30));

        world.Put(24, Ground, 30, BlockId.Planks);
        world.Settle();

        Assert.Equal(WaterLevel.Flowing(2), world.Level(23, Ground, 30));
        Assert.Equal(BlockId.Planks, world.At(24, Ground, 30));
        Assert.False(world.IsWater(25, Ground, 30));
        Assert.False(world.IsWater(27, Ground, 30));
    }

    [Fact]
    public void Given_ADamBlock_When_ItIsRemoved_Then_TheWaterFlowsOnAgain()
    {
        var world = ChannelWorld();
        world.Source(21, Ground, 30);
        world.Put(24, Ground, 30, BlockId.Planks);
        world.Settle();
        Assert.False(world.IsWater(25, Ground, 30));

        world.Put(24, Ground, 30, BlockId.Air);
        world.Settle();

        Assert.Equal(WaterLevel.Flowing(3), world.Level(24, Ground, 30));
        Assert.Equal(WaterLevel.Flowing(6), world.Level(27, Ground, 30));
    }

    [Fact]
    public void Given_ABlockPutBesideWater_When_NothingElseChanges_Then_TheWaterIsWokenAndSettlesWithoutChange()
    {
        var world = new FlowWorld();
        world.Source(30, Ground, 30);
        world.Settle();
        var before = world.Grid.ComputeDigestHex();

        world.Put(34, Ground + 1, 30, BlockId.Planks); // above the water: it wakes cells but changes nothing
        Assert.True(world.Flow.Queue.Count > 0);
        world.Settle();

        Assert.NotEqual(before, world.Grid.ComputeDigestHex()); // the plank itself
        Assert.Equal(WaterLevel.Flowing(4), world.Level(34, Ground, 30));
        Assert.Equal(0, world.Flow.Queue.Count);
    }

    [Fact]
    public void Given_ABlockFarFromAnyWater_When_Put_Then_NothingIsScheduled()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Settle();

        world.Put(50, Ground, 50, BlockId.Planks);

        Assert.Equal(0, world.Flow.Queue.Count);
    }

    [Fact]
    public void Given_MoreCellsDueThanTheBudget_When_AStepRuns_Then_ItUpdatesTheBudgetAndTheRestWaitsInOrder()
    {
        var world = new FlowWorld();
        var cells = new List<BlockPos>();
        for (var z = 0; z < 60 && cells.Count < 5000; z++)
        {
            for (var x = 0; x < 64 && cells.Count < 5000; x++)
            {
                for (var y = Ground; y < Ground + 2 && cells.Count < 5000; y++)
                {
                    cells.Add(new BlockPos(x, y, z));
                }
            }
        }

        foreach (var cell in cells)
        {
            world.Flow.Queue.Schedule(cell, 0);
        }

        Assert.Equal(FlowSimulation.Budget, world.Flow.Step(15));
        Assert.Equal(cells.Count - FlowSimulation.Budget, world.Flow.Queue.Count);

        var next = world.Flow.Queue.Snapshot()[0]; // the leftovers keep their place: the first is the 2049th in (y, z, x) order
        var expected = cells.OrderBy(c => c.Y).ThenBy(c => c.Z).ThenBy(c => c.X).ElementAt(FlowSimulation.Budget);
        Assert.Equal(expected, next.Position);

        Assert.Equal(FlowSimulation.Budget, world.Flow.Step(30));
        Assert.Equal(cells.Count - (2 * FlowSimulation.Budget), world.Flow.Queue.Count);
        Assert.Equal(cells.Count - (2 * FlowSimulation.Budget), world.Flow.Step(45));
        Assert.Equal(0, world.Flow.Queue.Count);
    }

    [Fact]
    public void Given_LeftoversAndNewWork_When_TheNextStepRuns_Then_TheLeftoversComeFirst()
    {
        var world = new FlowWorld();
        for (var i = 0; i < FlowSimulation.Budget + 10; i++)
        {
            world.Flow.Queue.Schedule(new BlockPos(i % 60, Ground, i / 60), 0);
        }

        world.Flow.Step(15);
        world.Flow.Queue.Schedule(new BlockPos(0, Ground + 5, 0), 15); // new work, due later than the leftovers (due 0)

        var snapshot = world.Flow.Queue.Snapshot();
        Assert.Equal(11, snapshot.Count);
        Assert.Equal(new BlockPos(0, Ground + 5, 0), snapshot[^1].Position);
        Assert.Equal(0, snapshot[0].Due);
    }

    [Fact]
    public void Given_TwoWorldsMadeTheSameWay_When_BothFlow_Then_TheirDigestsAgreeAtEveryStep()
    {
        var a = FloodWorld();
        var b = FloodWorld();

        for (var step = 0; step < 40; step++)
        {
            a.Run(15);
            b.Run(15);
            Assert.Equal(a.Grid.ComputeDigestHex(), b.Grid.ComputeDigestHex());
            Assert.Equal(a.Flow.Queue.Snapshot(), b.Flow.Queue.Snapshot());
        }

        Assert.True(a.WaterCells().Count > 100, $"{a.WaterCells().Count} water cells");
    }

    [Fact]
    public void Given_ARegionOfRoomsAndHoles_When_Flooded_Then_ItSettlesWithinTheCapWithNoPhantomWater()
    {
        var world = FloodWorld();
        var steps = world.Settle(400);

        Assert.True(steps < 400, $"still moving after {steps} steps");
        foreach (var (position, level) in world.WaterCells().Where(c => !c.Level.IsSource))
        {
            // Every flowing cell is fed: the rules agree with the world they left (a fixed point).
            Assert.Equal(new FlowCell(BlockId.Water, level), FlowRules.Next(world.Grid, position.X, position.Y, position.Z));
        }
    }

    [Fact]
    public void Given_RoughGroundWithPitsAndPillars_When_Flooded_Then_ItSettlesAndAgreesWithTheRules()
    {
        var world = new FlowWorld();
        for (var z = 16; z < 48; z++)
        {
            for (var x = 16; x < 48; x++)
            {
                var h = ((x * 7) + (z * 13) + (x * z)) % 11; // a fixed pattern, no random numbers
                if (h < 3)
                {
                    world.Put(x, Floor, z, BlockId.Air); // a pit
                    world.Put(x, Floor - 1, z, h == 0 ? BlockId.Air : BlockId.Stone);
                }
                else if (h > 8)
                {
                    world.Put(x, Ground, z, BlockId.Stone); // a pillar
                }
            }
        }

        world.Source(30, Ground + 3, 30);
        world.Source(20, Ground, 40);
        world.Source(41, Ground + 1, 22);
        var steps = world.Settle(600);

        Assert.True(steps < 600, $"still moving after {steps} steps");
        foreach (var (position, level) in world.WaterCells())
        {
            Assert.Equal(new FlowCell(BlockId.Water, level), FlowRules.Next(world.Grid, position.X, position.Y, position.Z));
        }
    }

    // A straight channel one cell wide along x at z = 30 between stone walls, from x = 20 to 40.
    private static FlowWorld ChannelWorld()
    {
        var world = new FlowWorld();
        for (var x = 19; x <= 41; x++)
        {
            world.Put(x, Ground, 29, BlockId.Stone);
            world.Put(x, Ground, 31, BlockId.Stone);
        }

        world.Put(19, Ground, 30, BlockId.Stone);
        return world;
    }

    // Two sources, walls, a staircase and some holes: enough to make the water choose, fall and drain.
    private static FlowWorld FloodWorld()
    {
        var world = new FlowWorld();
        for (var i = 0; i < 12; i++)
        {
            world.Put(24 + i, Ground, 28, BlockId.Stone);
            world.Put(24, Ground, 28 + i, BlockId.Stone);
            world.Put(30 + (i % 3), Ground + 1, 30 + (i / 3), BlockId.Stone);
        }

        world.Put(38, Floor, 30, BlockId.Air);
        world.Put(26, Floor, 37, BlockId.Air);
        world.Source(28, Ground, 30);
        world.Source(31, Ground + 2, 33);
        return world;
    }
}
