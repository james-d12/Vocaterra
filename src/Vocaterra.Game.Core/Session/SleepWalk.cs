using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// A bounded walk over the voxel grid for the sleep places (Y05a): which cells a body reaches on foot from a place within a few steps. A wall stops it, so the guest room's
/// bed is not "at" from the grass behind its south wall. The steps are the walker's: one block up, level, or down by up to three, with the shaft of the move free. No clock, no random numbers.
/// </summary>
internal static class SleepWalk
{
    /// <summary>
    /// The world as the walk reads it: the grid, with some cells read as other blocks (<see cref="Land.CampPlan.HasWake"/> looks at the camp before it is set) and the grid itself never written.
    /// A grid is a view with no cells changed.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="changed">The cells read as other blocks, or <see langword="null"/> for none.</param>
    internal readonly struct WalkView(VoxelGrid grid, IReadOnlyDictionary<BlockPos, BlockId>? changed = null) : IVoxelReader
    {
        public static implicit operator WalkView(VoxelGrid grid) => new(grid);

        public WorldLimits Limits => grid.Limits;

        public bool InBounds(int x, int y, int z) => grid.InBounds(x, y, z);

        public BlockId GetBlock(int x, int y, int z) =>
            changed is not null && changed.TryGetValue(new BlockPos(x, y, z), out var id) && grid.InBounds(x, y, z) ? id : grid.GetBlock(x, y, z);

        public byte GetState(int x, int y, int z) => grid.GetState(x, y, z);

        public WaterLevel GetWater(int x, int y, int z) => grid.GetWater(x, y, z);

        public int HighestBlock(int x, int z) => grid.HighestBlock(x, z);

        // The rules are the grid's (BodyCollision: outside the grid is solid; a tall block, a fence or a closed gate collides in the cell above it; nothing stands on a tall block or a partial shape), on the blocks as this view reads them.
        public bool BlocksBody(int x, int y, int z) => BodyCollision.BlocksBody(this, x, y, z);

        public bool CanStandOn(int x, int y, int z) => BodyCollision.CanStandOn(this, x, y, z);

        public bool IsSolid(int x, int y, int z) => !InBounds(x, y, z) || BlockDefinition.IsSolid(GetBlock(x, y, z));
    }

    private static readonly (int Dx, int Dz)[] Directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>The cell a body stands in at the place's column (the cell, one above, one below or two above, the first that can be stood in), or <see langword="null"/> when the column has none.</summary>
    public static BlockPos? StartFor(WalkView grid, BlockPos cell)
    {
        foreach (var dy in new[] { 0, 1, -1, 2 })
        {
            var feet = new BlockPos(cell.X, cell.Y + dy, cell.Z);
            if (Standable(grid, feet))
            {
                return feet;
            }
        }

        return null;
    }

    /// <summary>Whether the body whose feet are at <paramref name="feet"/> can walk from <paramref name="start"/> in at most <paramref name="maxSteps"/> steps (a body in the air, up to two cells above the ground of a reached column, counts as in it).</summary>
    public static bool Reaches(WalkView grid, BlockPos start, BlockPos feet, int maxSteps)
    {
        var seen = new HashSet<BlockPos> { start };
        var frontier = new List<BlockPos> { start };
        var next = new List<BlockPos>();
        for (var step = 0; ; step++)
        {
            foreach (var cell in frontier)
            {
                if (cell.X == feet.X && cell.Z == feet.Z && feet.Y - cell.Y is >= -1 and <= 2)
                {
                    return true;
                }
            }

            if (step >= maxSteps)
            {
                return false;
            }

            next.Clear();
            foreach (var cell in frontier)
            {
                Expand(grid, cell, seen, next);
            }

            if (next.Count == 0)
            {
                return false;
            }

            (frontier, next) = (next, frontier);
        }
    }

    /// <summary>Every cell reached from the start in at most <paramref name="maxSteps"/> steps, with the steps.</summary>
    public static Dictionary<BlockPos, int> ReachFrom(WalkView grid, BlockPos start, int maxSteps)
    {
        var steps = new Dictionary<BlockPos, int> { [start] = 0 };
        var seen = new HashSet<BlockPos> { start };
        var frontier = new List<BlockPos> { start };
        var next = new List<BlockPos>();
        for (var step = 1; step <= maxSteps && frontier.Count > 0; step++)
        {
            next.Clear();
            foreach (var cell in frontier)
            {
                Expand(grid, cell, seen, next);
            }

            foreach (var cell in next)
            {
                steps[cell] = step;
            }

            (frontier, next) = (next, frontier);
        }

        return steps;
    }

    private static void Expand(WalkView grid, BlockPos current, HashSet<BlockPos> seen, List<BlockPos> into)
    {
        foreach (var (dx, dz) in Directions)
        {
            for (var dy = 1; dy >= -3; dy--)
            {
                var cell = new BlockPos(current.X + dx, current.Y + dy, current.Z + dz);
                if (seen.Contains(cell) || !Standable(grid, cell))
                {
                    continue;
                }

                var clear = true;
                for (var y = cell.Y; y <= Math.Max(cell.Y + 1, current.Y + 1) && clear; y++)
                {
                    clear = !grid.BlocksBody(cell.X, y, cell.Z);
                }

                if (clear && (dy <= 0 || !grid.BlocksBody(current.X, current.Y + 2, current.Z)))
                {
                    seen.Add(cell);
                    into.Add(cell);
                    break;
                }
            }
        }
    }

    private static bool Standable(WalkView grid, BlockPos feet) =>
        grid.InBounds(feet.X, feet.Y, feet.Z)
        && !grid.BlocksBody(feet.X, feet.Y, feet.Z)
        && !grid.BlocksBody(feet.X, feet.Y + 1, feet.Z)
        && grid.CanStandOn(feet.X, feet.Y - 1, feet.Z)
        && grid.GetBlock(feet.X, feet.Y, feet.Z) != BlockId.Water
        && grid.GetBlock(feet.X, feet.Y - 1, feet.Z) != BlockId.Water;
}
