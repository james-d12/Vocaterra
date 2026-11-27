using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// Picking: a 3D digital differential analyser (an Amanatides and Woo grid walk) from a point along a direction, in exact integer
/// arithmetic. It returns the first block that is not air within the reach: solid blocks, and also the walk-through ones a player can
/// interact with (a berry bush, a torch, a door, the surface of water). The block the origin is inside is skipped. Undergrowth
/// (<see cref="BlockDefinition.Decor"/>: tall grass, ferns, reeds, flowers, S14) is skipped like air, so it never hides a berry bush or a trunk
/// behind it and never becomes the target of a gather or a placement. With <c>throughWater</c> (H05) water is skipped too, from above the
/// surface and from below it: the outline, the debug text and a placement then act on the first block behind the water, and the placement
/// cell (<see cref="BlockRayHit.Adjacent"/>) may itself be a water cell (a dam). Filling a bottle still uses the ray that stops at water.
/// </summary>
public static class BlockRay
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>The default reach: 4 blocks from the eye to the face that is hit.</summary>
    public const int DefaultReachInBlocks = VoxelPhysics.ReachInBlocks;

    /// <summary>Casts a ray from the player's eye along the player's facing, with the default reach.</summary>
    public static BlockRayHit? Cast(IVoxelReader grid, PlayerBody body, int reachInBlocks = DefaultReachInBlocks, bool throughWater = false)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Cast(grid, body.EyeX, body.EyeY, body.EyeZ, body.Yaw, body.Pitch, reachInBlocks, throughWater);
    }

    /// <summary>
    /// Casts a ray from (<paramref name="originX"/>, <paramref name="originY"/>, <paramref name="originZ"/>) in units along the facing
    /// (<paramref name="yaw"/>, <paramref name="pitch"/>, see <see cref="Angles"/>) for at most <paramref name="reachInBlocks"/> blocks.
    /// </summary>
    /// <returns>The first block that is not air, or <see langword="null"/> when there is none within the reach (or the ray leaves the grid first).</returns>
    public static BlockRayHit? Cast(IVoxelReader grid, int originX, int originY, int originZ, int yaw, int pitch, int reachInBlocks = DefaultReachInBlocks, bool throughWater = false)
    {
        var (dx, dy, dz) = Angles.Direction(yaw, pitch);
        return Cast(grid, originX, originY, originZ, dx, dy, dz, (long)reachInBlocks * U, throughWater);
    }

    /// <summary>
    /// Casts a ray along the fixed-point unit direction (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>), where
    /// <see cref="Angles.One"/> is 1.0, for at most <paramref name="reachUnits"/> units.
    /// </summary>
    public static BlockRayHit? Cast(IVoxelReader grid, int originX, int originY, int originZ, int dx, int dy, int dz, long reachUnits, bool throughWater = false)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (dx == 0 && dy == 0 && dz == 0)
        {
            return null;
        }

        var cx = VoxelCollider.FloorDiv(originX, U);
        var cy = VoxelCollider.FloorDiv(originY, U);
        var cz = VoxelCollider.FloorDiv(originZ, U);
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var stepZ = Math.Sign(dz);
        long adx = Math.Abs((long)dx);
        long ady = Math.Abs((long)dy);
        long adz = Math.Abs((long)dz);

        // n* is the distance in units, along its axis, from the origin to the next block boundary the ray will cross on that axis.
        // The ray parameter of that crossing is n / |d|, so two crossings compare by cross-multiplying.
        long nx = stepX > 0 ? ((long)(cx + 1) * U) - originX : originX - ((long)cx * U);
        long ny = stepY > 0 ? ((long)(cy + 1) * U) - originY : originY - ((long)cy * U);
        long nz = stepZ > 0 ? ((long)(cz + 1) * U) - originZ : originZ - ((long)cz * U);

        while (true)
        {
            // Pick the axis whose boundary comes first (ties: x, then y, then z). An axis with no motion never comes first.
            var axis = -1;
            if (stepX != 0)
            {
                axis = 0;
            }

            if (stepY != 0 && (axis < 0 || ny * adx < nx * ady))
            {
                axis = 1;
            }

            if (stepZ != 0 && (axis < 0 || (axis == 0 && nz * adx < nx * adz) || (axis == 1 && nz * ady < ny * adz)))
            {
                axis = 2;
            }

            long n;
            long a;
            switch (axis)
            {
                case 0:
                    n = nx;
                    a = adx;
                    nx += U;
                    cx += stepX;
                    break;
                case 1:
                    n = ny;
                    a = ady;
                    ny += U;
                    cy += stepY;
                    break;
                default:
                    n = nz;
                    a = adz;
                    nz += U;
                    cz += stepZ;
                    break;
            }

            // The distance along the ray (the direction is a unit vector, One = 1.0) is n * One / a; keep it within the reach.
            if (n * Angles.One > reachUnits * a || !grid.InBounds(cx, cy, cz))
            {
                return null;
            }

            var id = grid.GetBlock(cx, cy, cz);
            if (id == BlockId.Air || BlockDefinition.IsDecor(id) || (throughWater && id == BlockId.Water))
            {
                continue; // air, and undergrowth (S14): the crosshair looks through the grass at what stands behind it
            }

            var face = axis switch
            {
                0 => stepX > 0 ? BlockFace.West : BlockFace.East,
                1 => stepY > 0 ? BlockFace.Down : BlockFace.Up,
                _ => stepZ > 0 ? BlockFace.North : BlockFace.South,
            };
            var block = new BlockPos(cx, cy, cz);

            // The origin relative to the cell's minimum corner, in units: where the ray meets the cell is worked out in these.
            long rx = originX - ((long)cx * U);
            long ry = originY - ((long)cy * U);
            long rz = originZ - ((long)cz * U);
            if (BlockShapes.HasShape(id))
            {
                var shape = BlockShapes.Of(id, grid.GetState(cx, cy, cz), grid, cx, cy, cz);
                if (!shape.IsDrawnCube)
                {
                    var boxHit = HitBoxes(shape, rx, ry, rz, dx, dy, dz, reachUnits);
                    if (boxHit.Distance == PastReach)
                    {
                        return null; // the nearest box is beyond the reach, and every later cell is farther
                    }

                    if (boxHit.Distance == Missed)
                    {
                        continue; // the ray went through the empty part of the cell: an open gate's gap, the open half of a slab
                    }

                    var adjacent = boxHit.OnCellBoundary ? block.Offset(boxHit.Face) : block;
                    return new BlockRayHit(block, id, boxHit.Face, adjacent, boxHit.Distance, boxHit.X, boxHit.Y, boxHit.Z);
                }
            }

            var plane = axis switch
            {
                0 => stepX > 0 ? 0 : U,
                1 => stepY > 0 ? 0 : U,
                _ => stepZ > 0 ? 0 : U,
            };
            var (hitX, hitY, hitZ) = PointAt(rx, ry, rz, dx, dy, dz, n, a, axis, plane);
            return new BlockRayHit(block, id, face, block.Offset(face), (int)(n * Angles.One / a), hitX, hitY, hitZ);
        }
    }

    private const int PastReach = -1;
    private const int Missed = -2;

    private readonly record struct BoxHit(int Distance, BlockFace Face, bool OnCellBoundary, int X, int Y, int Z);

    // Where the ray is when it has gone num / den of the way along the axis it entered through: the entry axis is exactly on its plane, the others follow the direction (truncated), all kept inside the cell.
    private static (int X, int Y, int Z) PointAt(long rx, long ry, long rz, int dx, int dy, int dz, long num, long den, int axis, long plane)
    {
        var x = axis == 0 ? plane : rx + (dx * num / den);
        var y = axis == 1 ? plane : ry + (dy * num / den);
        var z = axis == 2 ? plane : rz + (dz * num / den);
        return ((int)Math.Clamp(x, 0, U), (int)Math.Clamp(y, 0, U), (int)Math.Clamp(z, 0, U));
    }

    // The slab method in exact arithmetic: for each box the ray's distance to the entry plane and to the exit plane on each axis is a fraction (units along the axis over |direction|), compared by cross-multiplying.
    // The nearest box the ray enters wins; the face is the one of its entry axis (ties: x, then y, then z).
    private static BoxHit HitBoxes(VoxelShape shape, long rx, long ry, long rz, int dx, int dy, int dz, long reachUnits)
    {
        const int Sixteenth = U / 16;
        var found = false;
        long bestNum = 0;
        long bestDen = 1;
        var bestAxis = 0;
        long bestPlane = 0;
        foreach (var box in shape.Drawn)
        {
            if (box.Volume == 0)
            {
                continue;
            }

            long enterNum = 0;
            long enterDen = 1;
            var enterAxis = -1;
            long enterPlane = 0;
            long exitNum = 0;
            long exitDen = 1;
            var exitSet = false;
            var inside = true;
            for (var axis = 0; axis < 3 && inside; axis++)
            {
                var (r, d, lo, hi) = axis switch
                {
                    0 => (rx, dx, (long)box.MinX * Sixteenth, (long)box.MaxX * Sixteenth),
                    1 => (ry, dy, (long)box.MinY * Sixteenth, (long)box.MaxY * Sixteenth),
                    _ => (rz, dz, (long)box.MinZ * Sixteenth, (long)box.MaxZ * Sixteenth),
                };
                if (d == 0)
                {
                    inside = r >= lo && r < hi; // moving parallel to the slab: it must already be between its planes
                    continue;
                }

                long ad = Math.Abs((long)d);
                var near = d > 0 ? lo - r : r - hi;
                var far = d > 0 ? hi - r : r - lo;
                if (enterAxis < 0 || near * enterDen > enterNum * ad)
                {
                    enterNum = near;
                    enterDen = ad;
                    enterAxis = axis;
                    enterPlane = d > 0 ? lo : hi;
                }

                if (!exitSet || far * exitDen < exitNum * ad)
                {
                    exitNum = far;
                    exitDen = ad;
                    exitSet = true;
                }
            }

            if (!inside || enterAxis < 0 || exitNum <= 0 || enterNum * exitDen >= exitNum * enterDen)
            {
                continue; // misses, or only grazes an edge, or lies behind
            }

            if (!found || enterNum * bestDen < bestNum * enterDen)
            {
                found = true;
                bestNum = enterNum;
                bestDen = enterDen;
                bestAxis = enterAxis;
                bestPlane = enterPlane;
            }
        }

        if (!found)
        {
            return new BoxHit(Missed, BlockFace.Up, false, 0, 0, 0);
        }

        if (bestNum * Angles.One > reachUnits * bestDen)
        {
            return new BoxHit(PastReach, BlockFace.Up, false, 0, 0, 0);
        }

        var positive = bestAxis switch
        {
            0 => dx > 0,
            1 => dy > 0,
            _ => dz > 0,
        };
        var face = bestAxis switch
        {
            0 => positive ? BlockFace.West : BlockFace.East,
            1 => positive ? BlockFace.Down : BlockFace.Up,
            _ => positive ? BlockFace.North : BlockFace.South,
        };
        var (x, y, z) = PointAt(rx, ry, rz, dx, dy, dz, bestNum, bestDen, bestAxis, bestPlane);
        var onBoundary = bestPlane == (positive ? 0 : U);
        return new BoxHit((int)(bestNum * Angles.One / bestDen), face, onBoundary, x, y, z);
    }
}
