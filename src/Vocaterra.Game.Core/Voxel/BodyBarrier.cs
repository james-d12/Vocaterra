namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// An invisible wall round a box of columns for the body (B07, D-game-34 items 6 and 15): while it is <see cref="Locked"/> every cell outside the footprint blocks a body, as the edge of the
/// bounded valley always did, whatever the streamed world holds there; when it is unlocked nothing is blocked. It touches only <see cref="VoxelGrid.BlocksBody"/> (the collider, the walk planner),
/// never <see cref="VoxelGrid.IsSolid"/>, so a ray, a placement and the renderer see the world as it is. The world's edge keeps this wall while the pass through the ring is closed and lifts all
/// round when it opens.
/// </summary>
public sealed class BodyBarrier
{
    /// <summary>Creates a wall round a footprint, locked.</summary>
    /// <param name="minX">The first column inside.</param>
    /// <param name="minZ">The first row inside.</param>
    /// <param name="maxX">The last column inside (inclusive).</param>
    /// <param name="maxZ">The last row inside (inclusive).</param>
    public BodyBarrier(int minX, int minZ, int maxX, int maxZ)
    {
        if (maxX < minX || maxZ < minZ)
        {
            throw new ArgumentException("A barrier's footprint must hold at least one column.");
        }

        MinX = minX;
        MinZ = minZ;
        MaxX = maxX;
        MaxZ = maxZ;
        Locked = true;
    }

    /// <summary>The first column inside.</summary>
    public int MinX { get; }

    /// <summary>The first row inside.</summary>
    public int MinZ { get; }

    /// <summary>The last column inside.</summary>
    public int MaxX { get; }

    /// <summary>The last row inside.</summary>
    public int MaxZ { get; }

    /// <summary>Whether the wall stands: a locked barrier blocks every column outside the footprint.</summary>
    public bool Locked { get; set; }

    /// <summary>Whether the wall blocks a body in the column (x, z) now.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool Blocks(int x, int z) => Locked && (x < MinX || x > MaxX || z < MinZ || z > MaxZ);
}
