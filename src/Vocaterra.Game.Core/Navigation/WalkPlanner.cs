using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Navigation;

/// <summary>
/// Plans a walk over the voxel world for the scripted run (S13): on a 384 x 384 map with a mountainside trail, a river and a forest, a straight line to
/// a column rarely works. It searches (A*) over the cells a body can stand in, taking steps of up to one block up and up to three down, with no
/// wading and no cutting a corner through a block, and returns the turning points of the cheapest way. Everything is integer arithmetic, and the
/// search breaks ties in a fixed order, so a script walks the same way on every machine. A planned walk follows the trail and the road because
/// they are the cheapest way over the mountainside and the forest, not because it knows about them.
/// </summary>
public static class WalkPlanner
{
    /// <summary>The most cells the search expands before it gives up (a column with no way to it must not hang a run).</summary>
    private const int MaxExpansions = 600_000;

    private const int Straight = 10;
    private const int Diagonal = 14;
    private const int VerticalPenalty = 3;
    private const int MaxDrop = 3;

    private static readonly (int Dx, int Dz)[] Moves = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// The turning points of the cheapest walk from the cell the feet are in to the column (<paramref name="targetX"/>, <paramref name="targetZ"/>),
    /// the last of them in that column; each hop runs along one of the eight directions. <see langword="null"/> when there is no way (or the search
    /// gave up).
    /// </summary>
    public static IReadOnlyList<(int X, int Z)>? Plan(IVoxelReader grid, BlockPos from, int targetX, int targetZ)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (from.X == targetX && from.Z == targetZ)
        {
            return [(targetX, targetZ)];
        }

        var limits = grid.Limits;
        var start = new BlockPos(from.X, from.Y, from.Z);
        var cost = new Dictionary<BlockPos, int> { [start] = 0 };
        var previous = new Dictionary<BlockPos, BlockPos> { [start] = start };
        var queue = new PriorityQueue<BlockPos, (int Priority, int Order)>();
        var order = 0;
        queue.Enqueue(start, (Heuristic(from.X, from.Z, targetX, targetZ), order++));
        var expanded = 0;
        BlockPos? goal = null;
        while (queue.TryDequeue(out var current, out _))
        {
            var (cx, cy, cz) = (current.X, current.Y, current.Z);
            if (cx == targetX && cz == targetZ)
            {
                goal = current;
                break;
            }

            if (++expanded > MaxExpansions)
            {
                break;
            }

            var known = cost[current];
            foreach (var (dx, dz) in Moves)
            {
                var nx = cx + dx;
                var nz = cz + dz;
                if ((uint)(nx - limits.MinX) >= (uint)limits.SizeX || (uint)(nz - limits.MinZ) >= (uint)limits.SizeZ)
                {
                    continue;
                }

                var ny = StepTo(grid, cx, cy, cz, nx, nz);
                if (ny < limits.MinY)
                {
                    continue;
                }

                if (dx != 0 && dz != 0
                    && (StepTo(grid, cx, cy, cz, cx + dx, cz) < limits.MinY || StepTo(grid, cx, cy, cz, cx, cz + dz) < limits.MinY))
                {
                    continue; // no cutting a corner through a block
                }

                if (Pinched(grid, nx, ny, nz) && !(nx == targetX && nz == targetZ))
                {
                    continue; // a one-block gap between trees: a body does not follow it
                }

                var next = new BlockPos(nx, ny, nz);
                var total = known + (dx != 0 && dz != 0 ? Diagonal : Straight) + (ny != cy ? VerticalPenalty : 0) + Tightness(grid, nx, ny, nz);
                if (cost.TryGetValue(next, out var old) && old <= total)
                {
                    continue;
                }

                cost[next] = total;
                previous[next] = current;
                queue.Enqueue(next, (total + Heuristic(nx, nz, targetX, targetZ), order++));
            }
        }

        if (goal is not { } end)
        {
            return null;
        }

        var cells = new List<(int X, int Z)>();
        for (var at = end; ; at = previous[at])
        {
            cells.Add((at.X, at.Z));
            if (previous[at] == at)
            {
                break;
            }
        }

        cells.Reverse();
        return TurningPoints(cells);
    }

    /// <summary>
    /// The feet cell a body can step to from (<c>fromX</c>, <c>fromY</c>, <c>fromZ</c>) at the neighbouring column (x, z): up one, level, or down by up to three, or <c>Limits.MinY - 1</c> (-1 on a bounded grid) when none can be
    /// stood in. A cell can be stood in when the feet and the head are free (<see cref="VoxelGrid.BlocksBody"/>), the block under it can be stood on (not a fence), and neither it nor the block under is water.
    /// </summary>
    public static int StepTo(IVoxelReader grid, int fromX, int fromY, int fromZ, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        for (var dy = 1; dy >= -MaxDrop; dy--)
        {
            var y = fromY + dy;
            if (dy == 1 && grid.BlocksBody(fromX, fromY + 2, fromZ))
            {
                continue; // no head room to step up
            }

            if (Standable(grid, x, y, z))
            {
                // A drop needs the cells above the landing free all the way down, up to the head's height now: the body moves into the column first and falls there.
                if (dy < 0)
                {
                    var clear = true;
                    for (var above = y + 1; above <= fromY + 1 && clear; above++)
                    {
                        clear = !grid.BlocksBody(x, above, z);
                    }

                    if (!clear)
                    {
                        continue;
                    }
                }

                return y;
            }
        }

        return grid.Limits.MinY - 1;
    }

    /// <summary>Whether a body can stand with its feet in the cell.</summary>
    public static bool Standable(IVoxelReader grid, int x, int y, int z) =>
        grid.InBounds(x, y, z)
        && !grid.BlocksBody(x, y, z)
        && !grid.BlocksBody(x, y + 1, z)
        && grid.CanStandOn(x, y - 1, z)
        && grid.GetBlock(x, y, z) != BlockId.Water
        && grid.GetBlock(x, y - 1, z) != BlockId.Water;

    private static bool IsTreeBlock(BlockId block) => BlockDefinition.IsTrunk(block) || BlockDefinition.IsLeaves(block);

    // A cell with a tree (trunk or leaves, of any species) on both sides of it, west and east or north and south, is a one-block gap in the forest: a walk avoids it.
    private static bool Pinched(IVoxelReader grid, int x, int y, int z)
    {
        bool Tree(int tx, int tz) => IsTreeBlock(grid.GetBlock(tx, y, tz)) || IsTreeBlock(grid.GetBlock(tx, y + 1, tz));
        return (Tree(x - 1, z) && Tree(x + 1, z)) || (Tree(x, z - 1) && Tree(x, z + 1));
    }

    // A cell between trunks or against a wall is dearer than one in the open, by 4 for each of the four sides that has a solid block at the feet or the head:
    // a walk keeps to the trail, the road and the clearings, and does not squeeze through a one-block gap in the forest that a body can hardly follow.
    private static int Tightness(IVoxelReader grid, int x, int y, int z)
    {
        var sides = 0;
        foreach (var (dx, dz) in Moves.AsSpan(0, 4))
        {
            if (grid.BlocksBody(x + dx, y, z + dz) || grid.BlocksBody(x + dx, y + 1, z + dz))
            {
                sides++;
            }
        }

        return sides * 4;
    }

    // The turning points of a run of cells: the cell where the direction changes, and the last one (cells one apart along one of the eight directions).
    private static List<(int X, int Z)> TurningPoints(List<(int X, int Z)> cells)
    {
        var points = new List<(int X, int Z)>();
        for (var i = 1; i < cells.Count; i++)
        {
            var last = i == cells.Count - 1;
            if (last)
            {
                points.Add(cells[i]);
                break;
            }

            var before = (X: cells[i].X - cells[i - 1].X, Z: cells[i].Z - cells[i - 1].Z);
            var after = (X: cells[i + 1].X - cells[i].X, Z: cells[i + 1].Z - cells[i].Z);
            if (before != after)
            {
                points.Add(cells[i]);
            }
        }

        if (points.Count == 0)
        {
            points.Add(cells[^1]);
        }

        return points;
    }

    private static int Heuristic(int x, int z, int targetX, int targetZ)
    {
        var dx = Math.Abs(x - targetX);
        var dz = Math.Abs(z - targetZ);
        return (Straight * Math.Max(dx, dz)) + ((Diagonal - Straight) * Math.Min(dx, dz));
    }

}


