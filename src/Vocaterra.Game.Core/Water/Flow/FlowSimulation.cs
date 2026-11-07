using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Flow;

/// <summary>
/// Runs the flow of water (water-plan 4.5): every <see cref="StepInterval"/> ticks (four times a second) it serves the cells that are due, up to
/// <see cref="Budget"/> cell updates, applies <see cref="FlowRules"/> to each and queues the neighbours of every cell that changed for the next step. Cells
/// it cannot reach in one step wait in the <see cref="FlowQueue"/> for the next, so a broken dam on a big map spreads over several steps and never stalls a frame.
/// It is a <see cref="IBlockChangeListener"/> of the grid: any block or level written by anyone else wakes the water round it, so it works for every lake,
/// river and spring there is or will be. It reads no clock and no random numbers; the order of the queue fixes the order of the updates.
/// </summary>
/// <remarks>
/// What wakes: a change at a cell queues, within <see cref="WakeRadius"/> cells sideways and 2 up or down, every cell that is flowing water or is air or
/// undergrowth with water beside it (a source is never queued: it cannot change). The reach is wide because a block put in the way of a drop changes where
/// water 4 cells off prefers to go. The flow's own writes queue only the six neighbours of the changed cell, which is enough because they never change where
/// water prefers to go (see <see cref="FlowRules"/>). A scene's build writes through column fills and wakes nothing, so a built map is at rest.
/// </remarks>
public sealed class FlowSimulation : IBlockChangeListener
{
    /// <summary>The ticks between two flow steps (60 ticks a second, so four steps a second).</summary>
    public const int StepInterval = 15;

    /// <summary>The most cell updates one step does; the rest wait for the next step.</summary>
    public const int Budget = 2048;

    /// <summary>How far, sideways, a change from outside wakes cells (the drop search reaches 4 cells and a cell it feeds is one further).</summary>
    public const int WakeRadius = FlowRules.DropSearchDistance + 1;

    private const int WakeBelow = 2;
    private const int WakeAbove = 2;

    private readonly VoxelGrid _grid;
    private readonly Func<long> _now;
    private bool _stepping;

    /// <summary>Creates the flow over a grid. The caller registers it with the grid's listeners.</summary>
    /// <param name="grid">The world.</param>
    /// <param name="now">The simulation's current tick; a cell woken now falls due at the next flow step after it.</param>
    public FlowSimulation(VoxelGrid grid, Func<long> now)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(now);
        _grid = grid;
        _now = now;
    }

    /// <summary>The cells waiting for an update.</summary>
    public FlowQueue Queue { get; } = new();

    /// <summary>The first flow step after a tick: the next tick that is a multiple of <see cref="StepInterval"/>.</summary>
    public static long DueAfter(long tick) => ((tick / StepInterval) + 1) * StepInterval;

    /// <summary>
    /// Runs the flow step of a tick: nothing unless <paramref name="tick"/> is a multiple of <see cref="StepInterval"/>. Returns how many cells were updated
    /// (served from the queue, whether or not they changed).
    /// </summary>
    public int Step(long tick)
    {
        if (tick % StepInterval != 0 || Queue.Count == 0)
        {
            return 0;
        }

        var updates = 0;
        _stepping = true;
        try
        {
            while (updates < Budget && Queue.TryDequeue(tick, out var entry))
            {
                updates++;
                Update(entry.Position, tick);
            }
        }
        finally
        {
            _stepping = false;
        }

        return updates;
    }

    /// <inheritdoc />
    public void OnBlockChanged(BlockChange change)
    {
        if (_stepping)
        {
            return; // the step queues the neighbours of its own writes itself
        }

        var due = DueAfter(_now());
        var p = change.Position;
        for (var y = p.Y - WakeBelow; y <= p.Y + WakeAbove; y++)
        {
            for (var z = p.Z - WakeRadius; z <= p.Z + WakeRadius; z++)
            {
                for (var x = p.X - WakeRadius; x <= p.X + WakeRadius; x++)
                {
                    WakeCell(x, y, z, due);
                }
            }
        }
    }

    private void Update(BlockPos position, long tick)
    {
        var (x, y, z) = (position.X, position.Y, position.Z);
        var current = FlowCell.Read(_grid, x, y, z);
        var next = FlowRules.Next(_grid, x, y, z);
        if (next == current)
        {
            return;
        }

        if (next.Block != current.Block)
        {
            _ = _grid.SetBlock(x, y, z, next.Block);
        }

        if (next.Block == BlockId.Water && next.Level != WaterLevel.Source)
        {
            _ = _grid.SetWater(x, y, z, next.Level);
        }

        var due = DueAfter(tick);
        WakeCell(x + 1, y, z, due);
        WakeCell(x - 1, y, z, due);
        WakeCell(x, y + 1, z, due);
        WakeCell(x, y - 1, z, due);
        WakeCell(x, y, z + 1, due);
        WakeCell(x, y, z - 1, due);
    }

    // Queues the cell when an update could do something to it: flowing water (it may drain or change), or air or undergrowth with water beside it (it may be fed).
    private void WakeCell(int x, int y, int z, long due)
    {
        if (FlowRules.IsFlowing(_grid, x, y, z))
        {
            _ = Queue.Schedule(new BlockPos(x, y, z), due);
            return;
        }

        if (FlowRules.CanTakeWater(_grid, x, y, z)
            && (FlowRules.IsWater(_grid, x + 1, y, z) || FlowRules.IsWater(_grid, x - 1, y, z) || FlowRules.IsWater(_grid, x, y + 1, z)
                || FlowRules.IsWater(_grid, x, y - 1, z) || FlowRules.IsWater(_grid, x, y, z + 1) || FlowRules.IsWater(_grid, x, y, z - 1)))
        {
            _ = Queue.Schedule(new BlockPos(x, y, z), due);
        }
    }
}
