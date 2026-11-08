using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.Water.Flow;

/// <summary>Small hand-built worlds for the flow tests: a stone floor up to <see cref="Floor"/> and air above, with a flow over it that the test steps by hand.</summary>
internal sealed class FlowWorld
{
    /// <summary>The y of the top of the floor; water sits at <c>Floor + 1</c>.</summary>
    public const int Floor = VoxelTestSupport.GroundTop;

    /// <summary>The y of the layer that rests on the floor.</summary>
    public const int Ground = Floor + 1;

    private long _tick;

    public FlowWorld()
    {
        Grid = VoxelTestSupport.FlatScene().Grid;
        Flow = new FlowSimulation(Grid, () => _tick);
        Grid.AddListener(Flow);
    }

    public VoxelGrid Grid { get; }

    public FlowSimulation Flow { get; }

    public long Tick => _tick;

    public BlockId At(int x, int y, int z) => Grid.GetBlock(x, y, z);

    public bool IsWater(int x, int y, int z) => Grid.GetBlock(x, y, z) == BlockId.Water;

    public WaterLevel Level(int x, int y, int z) => Grid.GetWater(x, y, z);

    /// <summary>Puts a block and wakes the water round it, as any edit does.</summary>
    public void Put(int x, int y, int z, BlockId block) => Grid.SetBlock(x, y, z, block);

    /// <summary>Puts a water source (waking its surroundings).</summary>
    public void Source(int x, int y, int z) => Grid.SetBlock(x, y, z, BlockId.Water);

    /// <summary>Steps the flow tick by tick, like the simulation does.</summary>
    public void Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _tick++;
            Flow.Step(_tick);
        }
    }

    /// <summary>Steps until nothing waits (or the cap is reached); returns the flow steps it took.</summary>
    public int Settle(int maxSteps = 200)
    {
        var steps = 0;
        while (Flow.Queue.Count > 0 && steps < maxSteps)
        {
            Run(FlowSimulation.StepInterval);
            steps++;
        }

        return steps;
    }

    /// <summary>Every water cell of the grid with its level, in grid order.</summary>
    public List<(BlockPos Position, WaterLevel Level)> WaterCells() =>
        Grid.BlocksOfKind(BlockId.Water).Select(p => (p, Grid.GetWater(p.X, p.Y, p.Z))).ToList();
}
