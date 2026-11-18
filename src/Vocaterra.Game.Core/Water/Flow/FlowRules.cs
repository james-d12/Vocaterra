using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Flow;

/// <summary>
/// The rules of flowing water (water-plan 4.5), pure: <see cref="Next"/> reads a cell and the cells round it and returns what the cell becomes. Nothing
/// is written and nothing is remembered, so the rules are tested cell by cell on a small grid. A cell is worked out from its neighbours ("pulled"), which
/// is the same as water pushing out of them, and has a fixed point where every cell agrees with its neighbours, so the flow always settles.
/// <list type="bullet">
/// <item>Water flows <b>down first</b>: a cell under water (a source or flowing) is falling water, drawn full.</item>
/// <item>It spreads <b>sideways only when it cannot go down</b>, that is when it rests on a solid block, on source water or on the edge of the world. It loses
/// one level per cell and reaches 7 cells. Falling water that lands starts afresh, as a source would.</item>
/// <item>Sideways it prefers the directions that reach a <b>drop</b> soonest, looking up to <see cref="DropSearchDistance"/> cells out (the cell with the drop under it is at most that far from the water); with no drop in sight it
/// spreads every way.</item>
/// <item>Flowing water with no neighbour feeding it <b>drains</b> away. Sources never change, and nothing here makes one: two sources make no third.</item>
/// </list>
/// A cell can take water when it is air or one of the sown undergrowth plants (<see cref="BlockDefinition.IsUndergrowth"/>), or is flowing water already; every other block, and the edge of
/// the world, stops it. That is the whole of it: no coordinate and no named place appears here, so a dam is any block put in the way. Whether a cell can take
/// water, can go down, or counts as a drop depends on blocks and sources only, never on flowing water, so a change made by the flow itself never alters where
/// the water prefers to go (the simulation relies on it when it decides whom to wake).
/// </summary>
public static class FlowRules
{
    /// <summary>How far out, in cells, the search for a drop looks when water picks its way sideways: the cell with the drop under it is at most this many cells away.</summary>
    public const int DropSearchDistance = 4;

    private const int NoDrop = 1000;

    // The four horizontal directions in a fixed order: north (-z), east (+x), south (+z), west (-x). The opposite of i is (i + 2) % 4.
    private static readonly int[] StepX = [0, 1, 0, -1];
    private static readonly int[] StepZ = [-1, 0, 1, 0];

    /// <summary>What a cell becomes on its next update: its own state when nothing feeds it or it cannot change.</summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The cell's column.</param>
    /// <param name="y">The cell's layer.</param>
    /// <param name="z">The cell's row.</param>
    public static FlowCell Next(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var current = FlowCell.Read(grid, x, y, z);
        var kind = KindAt(grid, x, y, z);
        if (kind is Kind.Blocked or Kind.Source)
        {
            return current;
        }

        if (KindAt(grid, x, y + 1, z) is Kind.Source or Kind.Flowing)
        {
            return new FlowCell(BlockId.Water, WaterLevel.Flowing(1, falling: true)); // down first
        }

        var best = WaterLevel.MaxDistance + 1;
        for (var i = 0; i < 4; i++)
        {
            var nx = x + StepX[i];
            var nz = z + StepZ[i];
            var neighbourKind = KindAt(grid, nx, y, nz);
            if (neighbourKind is not (Kind.Source or Kind.Flowing) || CanGoDown(grid, nx, y, nz))
            {
                continue;
            }

            var reach = neighbourKind == Kind.Source ? 0 : SpreadDistance(grid.GetWater(nx, y, nz));
            if (reach < WaterLevel.MaxDistance && reach + 1 < best && (PreferredDirections(grid, nx, y, nz) & (1 << Opposite(i))) != 0)
            {
                best = reach + 1;
            }
        }

        if (best <= WaterLevel.MaxDistance)
        {
            return new FlowCell(BlockId.Water, WaterLevel.Flowing(best));
        }

        return kind == Kind.Flowing ? new FlowCell(BlockId.Air, WaterLevel.Source) : current; // unfed water drains; air and undergrowth stay
    }

    /// <summary>Whether water at the cell would flow down: the cell under it can take water (air, undergrowth or flowing water).</summary>
    public static bool CanGoDown(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return KindAt(grid, x, y - 1, z) is Kind.Open or Kind.Flowing;
    }

    /// <summary>
    /// The sideways directions the water at a cell spreads in, as a bit mask (bit 0 north, 1 east, 2 south, 3 west). Only directions into a cell that can take
    /// water count. The ones that reach a drop soonest win, looking <see cref="DropSearchDistance"/> cells out; with no drop in sight every open direction wins.
    /// </summary>
    public static int PreferredDirections(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        Span<int> scores = stackalloc int[4];
        var best = NoDrop;
        var open = 0;
        for (var i = 0; i < 4; i++)
        {
            var nx = x + StepX[i];
            var nz = z + StepZ[i];
            if (!CanPass(grid, nx, y, nz))
            {
                scores[i] = -1;
                continue;
            }

            open |= 1 << i;
            scores[i] = CanGoDown(grid, nx, y, nz) ? 0 : SlopeDistance(grid, nx, y, nz, 1, Opposite(i));
            best = Math.Min(best, scores[i]);
        }

        if (best == NoDrop)
        {
            return open;
        }

        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            if (scores[i] == best)
            {
                mask |= 1 << i;
            }
        }

        return mask;
    }

    // The distance from a level for spreading: falling water that has landed starts afresh (0).
    private static int SpreadDistance(WaterLevel level) => level.Falling ? 0 : level.Distance;

    // The fewest steps from here to a cell that has a drop under it, or NoDrop; never turns back the way it came. Called with depth 1 for the cell next to the water's own.
    private static int SlopeDistance(IVoxelReader grid, int x, int y, int z, int depth, int from)
    {
        var best = NoDrop;
        for (var i = 0; i < 4; i++)
        {
            if (i == from)
            {
                continue;
            }

            var nx = x + StepX[i];
            var nz = z + StepZ[i];
            if (!CanPass(grid, nx, y, nz))
            {
                continue;
            }

            if (CanGoDown(grid, nx, y, nz))
            {
                return depth;
            }

            if (depth + 1 < DropSearchDistance)
            {
                best = Math.Min(best, SlopeDistance(grid, nx, y, nz, depth + 1, Opposite(i)));
            }
        }

        return best;
    }

    // Whether water spreading sideways could enter the cell: air, undergrowth, or flowing water (a source is already full).
    private static bool CanPass(IVoxelReader grid, int x, int y, int z) => KindAt(grid, x, y, z) is Kind.Open or Kind.Flowing;

    private static int Opposite(int direction) => (direction + 2) & 3;

    private static Kind KindAt(IVoxelReader grid, int x, int y, int z)
    {
        if (!grid.InBounds(x, y, z))
        {
            return Kind.Blocked;
        }

        var block = grid.GetBlock(x, y, z);
        if (block == BlockId.Water)
        {
            return grid.GetWater(x, y, z).IsSource ? Kind.Source : Kind.Flowing;
        }

        return block == BlockId.Air || BlockDefinition.IsUndergrowth(block) ? Kind.Open : Kind.Blocked;
    }

    /// <summary>Whether the cell is air or undergrowth, or flowing water: somewhere water can run to.</summary>
    internal static bool CanTakeWater(IVoxelReader grid, int x, int y, int z) => KindAt(grid, x, y, z) is Kind.Open or Kind.Flowing;

    /// <summary>Whether the cell is a source or flowing water.</summary>
    internal static bool IsWater(IVoxelReader grid, int x, int y, int z) => KindAt(grid, x, y, z) is Kind.Source or Kind.Flowing;

    /// <summary>Whether the cell is flowing (not source) water.</summary>
    internal static bool IsFlowing(IVoxelReader grid, int x, int y, int z) => KindAt(grid, x, y, z) == Kind.Flowing;

    private enum Kind
    {
        Blocked,
        Open,
        Source,
        Flowing,
    }
}
