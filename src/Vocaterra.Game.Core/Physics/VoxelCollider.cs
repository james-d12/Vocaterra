using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Physics;

/// <summary>
/// The player box (<see cref="VoxelPhysics.PlayerWidth"/> x <see cref="VoxelPhysics.PlayerHeight"/> x <see cref="VoxelPhysics.PlayerWidth"/>,
/// given by its minimum corner in units) against the solid blocks of a <see cref="VoxelGrid"/> (<see cref="VoxelGrid.BlocksBody"/>: a fence counts as two blocks high). A move is resolved one axis at a time and a
/// blocked move snaps the box to the block edge, so it works at any speed (no tunnelling) and leaves no gap.
/// </summary>
internal static class VoxelCollider
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int W = VoxelPhysics.PlayerWidth;
    private const int H = VoxelPhysics.PlayerHeight;

    /// <summary>Whether the box with minimum corner (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) overlaps no solid block.</summary>
    public static bool IsFree(IVoxelReader grid, int x, int y, int z)
    {
        var x0 = FloorDiv(x, U);
        var x1 = FloorDiv(x + W - 1, U);
        var y0 = FloorDiv(y, U);
        var y1 = FloorDiv(y + H - 1, U);
        var z0 = FloorDiv(z, U);
        var z1 = FloorDiv(z + W - 1, U);
        for (var by = y0; by <= y1; by++)
        {
            for (var bz = z0; bz <= z1; bz++)
            {
                for (var bx = x0; bx <= x1; bx++)
                {
                    if (Collides(grid, bx, by, bz))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Moves the box by <paramref name="dx"/> along X; returns true (and snaps to the block edge) if a solid block stopped it.</summary>
    public static bool MoveX(IVoxelReader grid, ref int x, int y, int z, int dx) => MoveAxis(grid, 0, ref x, y, z, dx);

    /// <summary>Moves the box by <paramref name="dy"/> along Y (positive is up); returns true (and snaps) if a solid block stopped it.</summary>
    public static bool MoveY(IVoxelReader grid, int x, ref int y, int z, int dy) => MoveAxis(grid, 1, ref y, x, z, dy);

    /// <summary>Moves the box by <paramref name="dz"/> along Z; returns true (and snaps to the block edge) if a solid block stopped it.</summary>
    public static bool MoveZ(IVoxelReader grid, int x, int y, ref int z, int dz) => MoveAxis(grid, 2, ref z, x, y, dz);

    /// <summary>The floor division of a by a positive b (rounds towards negative infinity).</summary>
    public static int FloorDiv(int a, int b)
    {
        var q = a / b;
        return (a % b != 0 && a < 0) ? q - 1 : q;
    }

    // For axis 0 the other two coordinates are (y, z); for axis 1 they are (x, z); for axis 2 they are (x, y).
    private static bool MoveAxis(IVoxelReader grid, int axis, ref int pos, int other1, int other2, int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        var size = axis == 1 ? H : W;
        var size1 = axis == 0 ? H : W;
        var size2 = axis == 2 ? H : W;
        var lo1 = FloorDiv(other1, U);
        var hi1 = FloorDiv(other1 + size1 - 1, U);
        var lo2 = FloorDiv(other2, U);
        var hi2 = FloorDiv(other2 + size2 - 1, U);
        if (delta > 0)
        {
            var from = FloorDiv(pos + size, U);
            var to = FloorDiv(pos + size + delta - 1, U);
            for (var cell = from; cell <= to; cell++)
            {
                if (Blocked(grid, axis, cell, lo1, hi1, lo2, hi2))
                {
                    pos = (cell * U) - size;
                    return true;
                }
            }
        }
        else
        {
            var from = FloorDiv(pos - 1, U);
            var to = FloorDiv(pos + delta, U);
            for (var cell = from; cell >= to; cell--)
            {
                if (Blocked(grid, axis, cell, lo1, hi1, lo2, hi2))
                {
                    pos = (cell + 1) * U;
                    return true;
                }
            }
        }

        pos += delta;
        return false;
    }

    // The body's collision: BlocksBody for planning (it includes lava, so a walk is never routed through it), but a moving body sinks into lava (B10b).
    // The wall at the valley's edge (B07) stops the body whatever the cell holds, lava included.
    private static bool Collides(IVoxelReader grid, int x, int y, int z) =>
        grid.IsWalled(x, z) || (grid.BlocksBody(x, y, z) && !BlockDefinition.StopsBodyOf(grid.GetBlock(x, y, z)));

    private static bool Blocked(IVoxelReader grid, int axis, int cell, int lo1, int hi1, int lo2, int hi2)
    {
        for (var i = lo1; i <= hi1; i++)
        {
            for (var j = lo2; j <= hi2; j++)
            {
                var solid = axis switch
                {
                    0 => Collides(grid, cell, i, j),
                    1 => Collides(grid, i, cell, j),
                    _ => Collides(grid, i, j, cell),
                };
                if (solid)
                {
                    return true;
                }
            }
        }

        return false;
    }
}


