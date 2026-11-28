using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Physics;

/// <summary>
/// The player box (<see cref="VoxelPhysics.PlayerWidth"/> x <see cref="VoxelPhysics.PlayerHeight"/> x <see cref="VoxelPhysics.PlayerWidth"/>,
/// given by its minimum corner in units) against the collision of the blocks of a grid (K03, building plan 3.3): a plain solid block is its whole cell, a block with a shape of its own
/// (<see cref="BlockShapes"/>: slab, stair, fence, gate, door, trapdoor) is its <see cref="VoxelShape.Collides"/> boxes, and a tall block (<see cref="BlockDefinition.Tall"/>, a fence of the scene) also blocks the
/// whole cell above it. A move is resolved one axis at a time and a blocked move snaps the box to the face of the box it met, so it works at any speed (no tunnelling) and leaves no gap.
/// </summary>
/// <remarks>
/// A cell is visited with the block below it, so that the part of a fence or closed gate that reaches half a block into the cell above (24 sixteenths) is met from that cell. Every visited box lies inside
/// its cell, so a sweep that scans cells nearest first meets the nearest box first. A plain cell costs one block read and one solid test.
/// </remarks>
internal static class VoxelCollider
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int W = VoxelPhysics.PlayerWidth;
    private const int H = VoxelPhysics.PlayerHeight;
    private const int S = U / 16; // one sixteenth in units

    private interface IBoxVisitor
    {
        /// <summary>A box of a cell, in units; returns true to stop the scan.</summary>
        bool Visit(int minX, int minY, int minZ, int maxX, int maxY, int maxZ);
    }

    /// <summary>Whether the box with minimum corner (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) overlaps no block's collision.</summary>
    public static bool IsFree(IVoxelReader grid, int x, int y, int z)
    {
        var x0 = FloorDiv(x, U);
        var x1 = FloorDiv(x + W - 1, U);
        var y0 = FloorDiv(y, U);
        var y1 = FloorDiv(y + H - 1, U);
        var z0 = FloorDiv(z, U);
        var z1 = FloorDiv(z + W - 1, U);
        var overlap = new OverlapVisitor(x, y, z, x + W, y + H, z + W);
        for (var bz = z0; bz <= z1; bz++)
        {
            for (var bx = x0; bx <= x1; bx++)
            {
                if (grid.IsWalled(bx, bz))
                {
                    return false;
                }

                var below = grid.GetBlock(bx, y0 - 1, bz);
                for (var by = y0; by <= y1; by++)
                {
                    var current = grid.GetBlock(bx, by, bz);
                    if (VisitCell(grid, bx, by, bz, current, below, ref overlap))
                    {
                        return false;
                    }

                    below = current;
                }
            }
        }

        return true;
    }

    /// <summary>Moves the box by <paramref name="dx"/> along X; returns true (and snaps to the face it met) if a block stopped it.</summary>
    public static bool MoveX(IVoxelReader grid, ref int x, int y, int z, int dx) => MoveAxis(grid, 0, ref x, y, z, dx);

    /// <summary>Moves the box by <paramref name="dy"/> along Y (positive is up); returns true (and snaps) if a block stopped it.</summary>
    public static bool MoveY(IVoxelReader grid, int x, ref int y, int z, int dy) => MoveAxis(grid, 1, ref y, x, z, dy);

    /// <summary>Moves the box by <paramref name="dz"/> along Z; returns true (and snaps to the face it met) if a block stopped it.</summary>
    public static bool MoveZ(IVoxelReader grid, int x, int y, ref int z, int dz) => MoveAxis(grid, 2, ref z, x, y, dz);

    /// <summary>
    /// How far a body with its feet at <paramref name="y"/> must be lifted to stand on what it overlaps at (<paramref name="x"/>, <paramref name="z"/>): the highest top among the boxes it overlaps that
    /// reach down to its feet (a box that starts above the feet is a ceiling or an overhang, never a step), or 0 when there is none. The caller checks the lift against <see cref="VoxelPhysics.StepHeight"/> and that the body fits there.
    /// </summary>
    public static int StepLift(IVoxelReader grid, int x, int y, int z)
    {
        var x0 = FloorDiv(x, U);
        var x1 = FloorDiv(x + W - 1, U);
        var y0 = FloorDiv(y, U);
        var y1 = FloorDiv(y + H - 1, U);
        var z0 = FloorDiv(z, U);
        var z1 = FloorDiv(z + W - 1, U);
        var lift = new LiftVisitor(x, y, z, x + W, y + H, z + W);
        for (var bz = z0; bz <= z1; bz++)
        {
            for (var bx = x0; bx <= x1; bx++)
            {
                if (grid.IsWalled(bx, bz))
                {
                    return 0;
                }

                var below = grid.GetBlock(bx, y0 - 1, bz);
                for (var by = y0; by <= y1; by++)
                {
                    var current = grid.GetBlock(bx, by, bz);
                    VisitCell(grid, bx, by, bz, current, below, ref lift);
                    below = current;
                }
            }
        }

        return lift.Lift;
    }

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
        var sweep = new SweepVisitor(axis, pos, size, other1, other1 + size1, other2, other2 + size2, delta);
        if (delta > 0)
        {
            var from = FloorDiv(pos + size, U);
            var to = FloorDiv(pos + size + delta - 1, U);
            for (var cell = from; cell <= to; cell++)
            {
                ScanCell(grid, axis, cell, lo1, hi1, lo2, hi2, ref sweep);
                if (sweep.Hit)
                {
                    pos = sweep.Snap;
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
                ScanCell(grid, axis, cell, lo1, hi1, lo2, hi2, ref sweep);
                if (sweep.Hit)
                {
                    pos = sweep.Snap;
                    return true;
                }
            }
        }

        pos += delta;
        return false;
    }

    private static void ScanCell(IVoxelReader grid, int axis, int cell, int lo1, int hi1, int lo2, int hi2, ref SweepVisitor sweep)
    {
        for (var i = lo1; i <= hi1; i++)
        {
            for (var j = lo2; j <= hi2; j++)
            {
                var (x, y, z) = axis switch
                {
                    0 => (cell, i, j),
                    1 => (i, cell, j),
                    _ => (i, j, cell),
                };
                if (grid.IsWalled(x, z))
                {
                    sweep.Visit(x * U, y * U, z * U, (x + 1) * U, (y + 1) * U, (z + 1) * U);
                    continue;
                }

                VisitCell(grid, x, y, z, grid.GetBlock(x, y, z), grid.GetBlock(x, y - 1, z), ref sweep);
            }
        }
    }

    // The boxes of one cell for a body (not the valley's wall, which the callers read per column). Lava collides with nothing: a moving body sinks into it (B10b).
    private static bool VisitCell<TVisitor>(IVoxelReader grid, int x, int y, int z, BlockId current, BlockId below, ref TVisitor visitor)
        where TVisitor : struct, IBoxVisitor
    {
        if (BlockDefinition.StopsBodyOf(current))
        {
            return false;
        }

        var ox = x * U;
        var oy = y * U;
        var oz = z * U;
        var tall = BlockDefinition.IsTall(below); // the cell over a tall block (a fence of the scene) is blocked: nothing steps or jumps onto it (issue #33)
        if (tall && visitor.Visit(ox, oy, oz, ox + U, oy + U, oz + U))
        {
            return true;
        }

        if (BlockShapes.HasShape(current))
        {
            var shape = BlockShapes.Of(current, grid.GetState(x, y, z), grid, x, y, z);
            foreach (var box in shape.Collides)
            {
                // The part of a box above 16 belongs to the cell above and is met from there.
                if (box.MinY < 16
                    && visitor.Visit(ox + (box.MinX * S), oy + (box.MinY * S), oz + (box.MinZ * S), ox + (box.MaxX * S), oy + (Math.Min(box.MaxY, 16) * S), oz + (box.MaxZ * S)))
                {
                    return true;
                }
            }
        }
        else if (!tall && grid.IsSolid(x, y, z) && visitor.Visit(ox, oy, oz, ox + U, oy + U, oz + U))
        {
            return true;
        }

        if (BlockShapes.HasShape(below))
        {
            var shape = BlockShapes.Of(below, grid.GetState(x, y - 1, z), grid, x, y - 1, z);
            if (shape.ReachesAboveCell)
            {
                foreach (var box in shape.Collides)
                {
                    if (box.MaxY > 16
                        && visitor.Visit(ox + (box.MinX * S), oy + (Math.Max(box.MinY - 16, 0) * S), oz + (box.MinZ * S), ox + (box.MaxX * S), oy + ((box.MaxY - 16) * S), oz + (box.MaxZ * S)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private struct OverlapVisitor(int minX, int minY, int minZ, int maxX, int maxY, int maxZ) : IBoxVisitor
    {
        public readonly bool Visit(int bMinX, int bMinY, int bMinZ, int bMaxX, int bMaxY, int bMaxZ) =>
            minX < bMaxX && maxX > bMinX && minY < bMaxY && maxY > bMinY && minZ < bMaxZ && maxZ > bMinZ;
    }

    private struct LiftVisitor(int minX, int minY, int minZ, int maxX, int maxY, int maxZ) : IBoxVisitor
    {
        public int Lift { get; private set; }

        public bool Visit(int bMinX, int bMinY, int bMinZ, int bMaxX, int bMaxY, int bMaxZ)
        {
            if (minX < bMaxX && maxX > bMinX && minY < bMaxY && maxY > bMinY && minZ < bMaxZ && maxZ > bMinZ && bMinY <= minY)
            {
                Lift = Math.Max(Lift, bMaxY - minY);
            }

            return false;
        }
    }

    // The nearest face a moving box meets: the body's extent on the move's axis is [pos, pos + size), on the two others [o1Min, o1Max) and [o2Min, o2Max).
    private struct SweepVisitor(int axis, int pos, int size, int o1Min, int o1Max, int o2Min, int o2Max, int delta) : IBoxVisitor
    {
        public bool Hit { get; private set; }

        public int Snap { get; private set; }

        public bool Visit(int bMinX, int bMinY, int bMinZ, int bMaxX, int bMaxY, int bMaxZ)
        {
            var (lo, hi, c1Lo, c1Hi, c2Lo, c2Hi) = axis switch
            {
                0 => (bMinX, bMaxX, bMinY, bMaxY, bMinZ, bMaxZ),
                1 => (bMinY, bMaxY, bMinX, bMaxX, bMinZ, bMaxZ),
                _ => (bMinZ, bMaxZ, bMinX, bMaxX, bMinY, bMaxY),
            };
            if (o1Min >= c1Hi || o1Max <= c1Lo || o2Min >= c2Hi || o2Max <= c2Lo)
            {
                return false;
            }

            if (delta > 0)
            {
                if (lo >= pos + size && lo < pos + size + delta && (!Hit || lo - size < Snap))
                {
                    Hit = true;
                    Snap = lo - size;
                }
            }
            else if (hi <= pos && hi > pos + delta && (!Hit || hi > Snap))
            {
                Hit = true;
                Snap = hi;
            }

            return false;
        }
    }
}
