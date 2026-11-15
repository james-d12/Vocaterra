using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The water-at-rest invariant (water-plan 4.7): waking every water cell of the shipped scene changes nothing, and does so inside the flow budget.</summary>
public sealed class WaterAtRestTests
{
    private const int StepCap = 200;

    [Fact]
    public void Given_TheBuiltScene_When_EveryWaterCellIsWokenAndTheFlowRunsToStandstill_Then_NoBlockOrLevelChanged()
    {
        var sim = VoxelSimulation.Create(VoxelTestSupport.BuildRoute(42), TestItems.Catalogue);
        var log = new ChangeLog();
        sim.Grid.AddListener(log);

        var result = WakeAllAndRun(sim);

        Assert.True(result.Settled, $"the flow had not settled after {StepCap} steps");
        var changed = log.Net().Count;
        Assert.True(changed == 0, $"{changed} cells changed after waking all water ({result.Steps} steps, {result.TotalUpdates} updates)");
        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    [Fact]
    public void Given_TheBuiltScene_When_EveryWaterCellIsWoken_Then_NoStepGoesPastTheBudgetAndTheWorstStepIsReported()
    {
        var sim = VoxelSimulation.Create(VoxelTestSupport.BuildRoute(42), TestItems.Catalogue);

        var result = WakeAllAndRun(sim);

        // Timing is reported, never asserted: the update count per step is the guarantee (it is capped by FlowSimulation.Budget).
        Console.WriteLine($"worst step {result.WorstStepMilliseconds:F1} ms for {result.WorstStepUpdates} updates; {result.Steps} steps, {result.TotalUpdates} updates");
        Assert.True(result.WorstStepUpdates <= FlowSimulation.Budget, $"a step did {result.WorstStepUpdates} updates, past the budget of {FlowSimulation.Budget}");
        Assert.True(result.Settled);
    }

    /// <summary>Queues every water cell and every neighbour of water, then steps the flow (as the simulation does, every 15 ticks) until the queue is empty or the cap is reached.</summary>
    internal static RunResult WakeAllAndRun(VoxelSimulation sim)
    {
        var grid = sim.Grid;
        var due = FlowSimulation.DueAfter(0);
        foreach (var water in grid.BlocksOfKind(BlockId.Water))
        {
            _ = sim.Flow.Queue.Schedule(water, due);
            foreach (var (dx, dy, dz) in Neighbours)
            {
                var (x, y, z) = (water.X + dx, water.Y + dy, water.Z + dz);
                if (grid.InBounds(x, y, z))
                {
                    _ = sim.Flow.Queue.Schedule(new BlockPos(x, y, z), due);
                }
            }
        }

        var (steps, total, worstUpdates, worstMs) = (0, 0, 0, 0.0);
        long tick = due;
        while (sim.Flow.Queue.Count > 0 && steps < StepCap)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var updates = sim.Flow.Step(tick);
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            (steps, total) = (steps + 1, total + updates);
            worstUpdates = Math.Max(worstUpdates, updates);
            worstMs = Math.Max(worstMs, ms);
            tick += FlowSimulation.StepInterval;
        }

        return new RunResult(sim.Flow.Queue.Count == 0, steps, total, worstUpdates, worstMs);
    }

    /// <summary>Records the first and the latest state of every cell written, so the net changes are known without copying the grid.</summary>
    internal sealed class ChangeLog : IBlockChangeListener
    {
        private readonly Dictionary<BlockPos, (BlockId Block, WaterLevel Level)> _first = [];
        private readonly Dictionary<BlockPos, (BlockId Block, WaterLevel Level)> _last = [];

        public void OnBlockChanged(BlockChange change)
        {
            _ = _first.TryAdd(change.Position, (change.OldBlock, change.OldLevel));
            _last[change.Position] = (change.NewBlock, change.NewLevel);
        }

        /// <summary>The cells whose state now differs from what they were first.</summary>
        public List<(BlockPos Pos, BlockId Before, WaterLevel BeforeLevel, BlockId After, WaterLevel AfterLevel)> Net()
        {
            var list = new List<(BlockPos, BlockId, WaterLevel, BlockId, WaterLevel)>();
            foreach (var (pos, first) in _first)
            {
                var last = _last[pos];
                if (first != last)
                {
                    list.Add((pos, first.Block, first.Level, last.Block, last.Level));
                }
            }

            return list;
        }
    }

    private static readonly (int, int, int)[] Neighbours = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    internal readonly record struct RunResult(bool Settled, int Steps, int TotalUpdates, int WorstStepUpdates, double WorstStepMilliseconds);
}
