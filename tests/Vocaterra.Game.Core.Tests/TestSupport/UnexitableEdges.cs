using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>One water edge a body cannot leave: a column of standable water beside dry ground out of reach from it (water-plan H12).</summary>
/// <param name="Water">The water column (x, z) at the edge.</param>
/// <param name="Bank">The dry column beside it.</param>
/// <param name="Rise">How far the bank's floor stands above the water's bed, in blocks.</param>
/// <param name="Depth">How deep the water is over the bed, in blocks.</param>
/// <param name="Swims">Whether a body at the bed is swimming (water at the waist).</param>
internal readonly record struct UnexitableEdge((int X, int Z) Water, (int X, int Z) Bank, double Rise, double Depth, bool Swims);

/// <summary>
/// Finds the water edges of a grid that a body cannot leave (water-plan H12). For every column whose top cell is water, it takes the bed and the water over it, and for
/// each of the four neighbouring columns that is dry the lowest standable floor from the bed up. The body leaves by walking when the floor is at most a step
/// (<see cref="VoxelPhysics.StepHeight"/>) above the bed, and by the hop out (<see cref="SwimMotion.HopReach"/> above the feet, the feet as high as the swim state lets
/// them be, read with <see cref="Immersion"/>) when the water is deep enough to swim. Anything else is an edge nobody can climb: a body gets in only by dropping off it.
/// </summary>
internal static class UnexitableEdges
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Search = 12; // blocks above the bed in which to look for the bank's floor; a wall taller than that has no floor beside the water to leave onto

    private static readonly (int Dx, int Dz)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>
    /// Every unexitable edge of the grid, in a fixed order. A floor of bridge blocks (the deck, or its rail on it) is a built structure over the water and not a bank: nobody leaves the water onto a
    /// bridge from under it.
    /// </summary>
    public static List<UnexitableEdge> Scan(VoxelGrid grid)
    {
        var edges = new List<UnexitableEdge>();
        foreach (var top in grid.EachOfKind(BlockId.Water))
        {
            if (grid.GetBlock(top.X, top.Y + 1, top.Z) == BlockId.Water)
            {
                continue; // not the top of its column
            }

            var bedCell = top.Y;
            while (bedCell > 0 && grid.GetBlock(top.X, bedCell - 1, top.Z) == BlockId.Water)
            {
                bedCell--;
            }

            var bed = bedCell * U;
            var surface = (top.Y * U) + WaterSurfaceUnits(grid, top);
            var swims = TopSwimFeet(grid, top.X, top.Z, bed, out var highestFeet);
            foreach (var (dx, dz) in Sides)
            {
                var bx = top.X + dx;
                var bz = top.Z + dz;
                var floor = DryFloor(grid, bx, bz, bedCell);
                if (floor is not { } floorCell)
                {
                    continue;
                }

                if (grid.GetBlock(bx, floorCell - 1, bz) == BlockId.Bridge || grid.GetBlock(bx, floorCell - 2, bz) == BlockId.Bridge)
                {
                    continue;
                }

                if (HoldsBackHigherWater(grid, top, bx, bz))
                {
                    continue;
                }

                var floorTop = floorCell * U;
                var reachable = floorTop <= bed + VoxelPhysics.StepHeight || (swims && floorTop <= highestFeet + SwimMotion.HopReach);
                if (!reachable)
                {
                    edges.Add(new UnexitableEdge((top.X, top.Z), (bx, bz), (double)(floorTop - bed) / U, (double)(surface - bed) / U, swims));
                }
            }
        }

        edges.Sort(static (a, b) => a.Water.Z != b.Water.Z ? a.Water.Z.CompareTo(b.Water.Z) : a.Water.X != b.Water.X ? a.Water.X.CompareTo(b.Water.X) : a.Bank.X != b.Bank.X ? a.Bank.X.CompareTo(b.Bank.X) : a.Bank.Z.CompareTo(b.Bank.Z));
        return edges;
    }

    /// <summary>The edges grouped into stretches (edges whose water columns touch, eight ways), each as one line: the box, the count, the rise and the depth.</summary>
    public static List<string> Stretches(IReadOnlyList<UnexitableEdge> edges)
    {
        var group = new int[edges.Count];
        Array.Fill(group, -1);
        var byWater = new Dictionary<(int, int), List<int>>();
        for (var i = 0; i < edges.Count; i++)
        {
            if (!byWater.TryGetValue(edges[i].Water, out var list))
            {
                byWater[edges[i].Water] = list = [];
            }

            list.Add(i);
        }

        var lines = new List<string>();
        var next = 0;
        for (var i = 0; i < edges.Count; i++)
        {
            if (group[i] >= 0)
            {
                continue;
            }

            var stack = new Stack<int>();
            stack.Push(i);
            group[i] = next;
            var members = new List<UnexitableEdge>();
            while (stack.Count > 0)
            {
                var at = stack.Pop();
                members.Add(edges[at]);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (byWater.TryGetValue((edges[at].Water.X + dx, edges[at].Water.Z + dz), out var near))
                        {
                            foreach (var n in near.Where(n => group[n] < 0))
                            {
                                group[n] = next;
                                stack.Push(n);
                            }
                        }
                    }
                }
            }

            next++;
            lines.Add(
                $"x {members.Min(e => e.Water.X)}..{members.Max(e => e.Water.X)}, z {members.Min(e => e.Water.Z)}..{members.Max(e => e.Water.Z)}: {members.Count} edges, " +
                $"bank {members.Min(e => e.Rise):0.#}..{members.Max(e => e.Rise):0.#} over the bed, water {members.Min(e => e.Depth):0.##}..{members.Max(e => e.Depth):0.##} deep" +
                (members.Any(e => e.Swims) ? " (swimmable)" : " (too shallow to swim)"));
        }

        return lines;
    }

    // Whether a body standing on the bed swims, and the highest its feet can be while it still does (the hop out starts from there).
    private static bool TopSwimFeet(VoxelGrid grid, int x, int z, int bed, out int highestFeet)
    {
        highestFeet = bed;
        var cx = (x * U) + (U / 2);
        var cz = (z * U) + (U / 2);
        if (!Immersion.Of(grid, new PlayerBody(grid, cx, bed, cz, 0)).Body)
        {
            return false;
        }

        for (var feet = bed; feet < bed + (8 * U); feet += U / 16)
        {
            if (!Immersion.Of(grid, new PlayerBody(grid, cx, feet, cz, 0)).Body)
            {
                break;
            }

            highestFeet = feet;
        }

        return true;
    }

    // The bank column is the wall of a higher stretch of water beside it (a step of the river's staircase, whose layout H07a fixed): cut down, it would let that water run.
    private static bool HoldsBackHigherWater(VoxelGrid grid, BlockPos top, int bx, int bz)
    {
        foreach (var (dx, dz) in Sides)
        {
            if (grid.GetBlock(bx + dx, top.Y + 1, bz + dz) == BlockId.Water)
            {
                return true;
            }
        }

        return false;
    }

    private static int WaterSurfaceUnits(VoxelGrid grid, BlockPos top) =>
        WaterSurface.Units(grid.GetWater(top.X, top.Y, top.Z), waterAbove: false);

    // The cell y of the lowest standable floor at or above the bed cell in a dry column (the floor block solid, two free dry cells over it), or null when the
    // column holds water at the bed level (it is water, not a bank) or has no floor in reach. A column open at the bed level counts as a floor at the bed.
    private static int? DryFloor(VoxelGrid grid, int x, int z, int bedCell)
    {
        if (!grid.InBounds(x, bedCell, z))
        {
            return null;
        }

        if (grid.GetBlock(x, bedCell, z) == BlockId.Water || grid.GetBlock(x, bedCell + 1, z) == BlockId.Water)
        {
            return null;
        }

        for (var y = bedCell; y <= bedCell + Search; y++)
        {
            if (grid.IsSolid(x, y, z) && Free(grid, x, y + 1, z) && Free(grid, x, y + 2, z))
            {
                return y + 1;
            }
        }

        // no floor found going up: if the bed level itself is open the water has no wall here
        return grid.IsSolid(x, bedCell, z) ? null : bedCell;
    }

    private static bool Free(VoxelGrid grid, int x, int y, int z) => !grid.IsSolid(x, y, z) && grid.GetBlock(x, y, z) != BlockId.Water;
}
