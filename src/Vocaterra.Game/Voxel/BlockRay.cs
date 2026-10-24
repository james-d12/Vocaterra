namespace Vocaterra.Game.Voxel;

/// <summary>What a block ray hit: the block, the face it entered through and the cell in front of that face (where a block would be placed).</summary>
/// <param name="Block">The block that was hit.</param>
/// <param name="Id">The kind of that block.</param>
/// <param name="Face">The face of the block the ray entered through (its outward normal points back towards the viewer).</param>
/// <param name="Adjacent">The cell on the viewer's side of <paramref name="Face"/>: <paramref name="Block"/> offset by the face's normal.</param>
/// <param name="DistanceUnits">The distance from the ray's origin to the face, in units of 1/4096 block (rounded down).</param>
public readonly record struct BlockRayHit(BlockPos Block, BlockId Id, BlockFace Face, BlockPos Adjacent, int DistanceUnits);

/// <summary>
/// Picking: a 3D digital differential analyser (an Amanatides and Woo grid walk) from a point along a direction, in exact integer
/// arithmetic. It returns the first block that is not air within the reach: solid blocks, and also the walk-through ones a player can
/// interact with (a berry bush, a torch, a door, the surface of water). The block the origin is inside is skipped. Undergrowth
/// (<see cref="BlockDefinition.Decor"/>: tall grass, ferns, reeds, flowers, S14) is skipped like air, so it never hides a berry bush or a trunk
/// behind it and never becomes the target of a gather or a placement.
/// </summary>
public static class BlockRay
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>The default reach: 4 blocks from the eye to the face that is hit.</summary>
    public const int DefaultReachInBlocks = VoxelPhysics.ReachInBlocks;

    /// <summary>Casts a ray from the player's eye along the player's facing, with the default reach.</summary>
    public static BlockRayHit? Cast(VoxelGrid grid, PlayerBody3D body, int reachInBlocks = DefaultReachInBlocks)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Cast(grid, body.EyeX, body.EyeY, body.EyeZ, body.Yaw, body.Pitch, reachInBlocks);
    }

    /// <summary>
    /// Casts a ray from (<paramref name="originX"/>, <paramref name="originY"/>, <paramref name="originZ"/>) in units along the facing
    /// (<paramref name="yaw"/>, <paramref name="pitch"/>, see <see cref="Angles"/>) for at most <paramref name="reachInBlocks"/> blocks.
    /// </summary>
    /// <returns>The first block that is not air, or <see langword="null"/> when there is none within the reach (or the ray leaves the grid first).</returns>
    public static BlockRayHit? Cast(VoxelGrid grid, int originX, int originY, int originZ, int yaw, int pitch, int reachInBlocks = DefaultReachInBlocks)
    {
        var (dx, dy, dz) = Angles.Direction(yaw, pitch);
        return Cast(grid, originX, originY, originZ, dx, dy, dz, (long)reachInBlocks * U);
    }

    /// <summary>
    /// Casts a ray along the fixed-point unit direction (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>), where
    /// <see cref="Angles.One"/> is 1.0, for at most <paramref name="reachUnits"/> units.
    /// </summary>
    public static BlockRayHit? Cast(VoxelGrid grid, int originX, int originY, int originZ, int dx, int dy, int dz, long reachUnits)
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
            if (id == BlockId.Air || BlockDefinition.IsDecor(id))
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
            return new BlockRayHit(block, id, face, block.Offset(face), (int)(n * Angles.One / a));
        }
    }
}
