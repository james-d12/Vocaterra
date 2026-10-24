using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// Where a scripted run (the demos, the headless walk) stops when it walks up to an NPC to talk: about two blocks from the figure, on ground a
/// player can stand on, with a clear line from the eye to the NPC's chest (S6; the S3 run put the camera inside the guide's figure). It is a pure
/// function of the grid, the NPC's feet and the cell the walk starts from, in integer arithmetic, so the same world gives the same stand on every
/// machine.
/// </summary>
public static class NpcStandOff
{
    /// <summary>How far from the NPC the player stands, in blocks (the Chebyshev distance of the ring searched first).</summary>
    public const int Blocks = 2;

    /// <summary>The squared horizontal distance up to which a cell is "straight in front" rather than a diagonal corner (5 is 2.24 blocks).</summary>
    private const long NearSquare = 5;

    /// <summary>The eye height of a standing player above the feet cell's floor, in sixteenths of a block (1.62 blocks).</summary>
    private const int EyeSixteenths = 26;

    /// <summary>The height of the NPC's chest above its feet, in sixteenths of a block (1.2 blocks, the point the aim uses).</summary>
    private const int ChestSixteenths = 19;

    /// <summary>
    /// The cell to walk to: a standing cell on the ring of <see cref="Blocks"/> around the NPC (the ring one block further out when that has none)
    /// that sees the NPC's chest, the nearest to <paramref name="from"/> (the player's feet cell), ties to the smaller z and then the smaller x.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="npcFeet">The cell the NPC's feet are in.</param>
    /// <param name="from">The cell the player walks from.</param>
    /// <returns>The feet cell to stand in, or <see langword="null"/> when no cell around the NPC qualifies.</returns>
    public static BlockPos? Choose(VoxelGrid grid, BlockPos npcFeet, BlockPos from)
    {
        ArgumentNullException.ThrowIfNull(grid);
        for (var radius = Blocks; radius <= Blocks + 1; radius++)
        {
            BlockPos? best = null;
            var bestNear = false;
            long bestDistance = long.MaxValue;
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                    {
                        continue;
                    }

                    var column = new BlockPos(npcFeet.X + dx, npcFeet.Y, npcFeet.Z + dz);
                    if (StandingCell(grid, column) is not { } stand || !SeesChest(grid, stand, npcFeet))
                    {
                        continue;
                    }

                    // A cell straight in front of the NPC or a knight's move from it (up to 2.24 blocks) beats a diagonal corner (2.83): the figure
                    // fills the view better. Among those, the one nearest to where the player comes from; ties to the smaller z and then x.
                    var square = ((long)dx * dx) + ((long)dz * dz);
                    var near = square <= NearSquare;
                    var ex = (long)stand.X - from.X;
                    var ez = (long)stand.Z - from.Z;
                    var distance = (ex * ex) + (ez * ez);
                    var better = best is null
                        || (near && !bestNear)
                        || (near == bestNear && (distance < bestDistance || (distance == bestDistance && (stand.Z, stand.X).CompareTo((best.Value.Z, best.Value.X)) < 0)));
                    if (better)
                    {
                        bestDistance = distance;
                        bestNear = near;
                        best = stand;
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }

    // The feet cell in the column within one level of the NPC's own: air with air above, solid dry ground below.
    private static BlockPos? StandingCell(VoxelGrid grid, BlockPos column)
    {
        for (var dy = 0; dy >= -1; dy--)
        {
            var feet = new BlockPos(column.X, column.Y + dy, column.Z);
            if (grid.InBounds(feet.X, feet.Y, feet.Z)
                && !grid.IsSolid(feet.X, feet.Y, feet.Z)
                && !grid.IsSolid(feet.X, feet.Y + 1, feet.Z)
                && grid.IsSolid(feet.X, feet.Y - 1, feet.Z)
                && grid.GetBlock(feet.X, feet.Y, feet.Z) != BlockId.Water)
            {
                return feet;
            }
        }

        return null;
    }

    // A straight line from the eye to the NPC's chest, sampled every sixteenth of a block, passes through no solid block.
    private static bool SeesChest(VoxelGrid grid, BlockPos stand, BlockPos npcFeet)
    {
        var ax = (stand.X * 16) + 8;
        var ay = (stand.Y * 16) + EyeSixteenths;
        var az = (stand.Z * 16) + 8;
        var bx = (npcFeet.X * 16) + 8;
        var by = (npcFeet.Y * 16) + ChestSixteenths;
        var bz = (npcFeet.Z * 16) + 8;
        var steps = Math.Max(Math.Abs(bx - ax), Math.Max(Math.Abs(by - ay), Math.Abs(bz - az)));
        for (var i = 1; i < steps; i++)
        {
            var x = Floor16(ax + (((bx - ax) * i) / steps));
            var y = Floor16(ay + (((by - ay) * i) / steps));
            var z = Floor16(az + (((bz - az) * i) / steps));
            if (grid.IsSolid(x, y, z))
            {
                return false;
            }
        }

        return true;
    }

    private static int Floor16(int sixteenths) => sixteenths >= 0 ? sixteenths / 16 : -(((-sixteenths) + 15) / 16);
}
