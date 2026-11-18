using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// The box of chunks an authored region fills (B06): its first chunk and its size in chunks. The numbers come from the region's own layout (a valley of 384 x 128 x 384 blocks is 24 x 8 x 24
/// chunks), never from a constant, so a region of another size or place is only different data.
/// </summary>
/// <param name="MinChunkX">The first chunk column.</param>
/// <param name="MinChunkY">The first chunk layer.</param>
/// <param name="MinChunkZ">The first chunk row.</param>
/// <param name="ChunksX">How many chunk columns.</param>
/// <param name="ChunksY">How many chunk layers.</param>
/// <param name="ChunksZ">How many chunk rows.</param>
public readonly record struct RegionBounds(int MinChunkX, int MinChunkY, int MinChunkZ, int ChunksX, int ChunksY, int ChunksZ)
{
    /// <summary>The first block column.</summary>
    public int MinX => MinChunkX << VoxelChunk.Shift;

    /// <summary>The last block column.</summary>
    public int MaxX => ((MinChunkX + ChunksX) << VoxelChunk.Shift) - 1;

    /// <summary>The first block layer.</summary>
    public int MinY => MinChunkY << VoxelChunk.Shift;

    /// <summary>The last block layer.</summary>
    public int MaxY => ((MinChunkY + ChunksY) << VoxelChunk.Shift) - 1;

    /// <summary>The first block row.</summary>
    public int MinZ => MinChunkZ << VoxelChunk.Shift;

    /// <summary>The last block row.</summary>
    public int MaxZ => ((MinChunkZ + ChunksZ) << VoxelChunk.Shift) - 1;

    /// <summary>One past the last chunk layer.</summary>
    public int EndChunkY => MinChunkY + ChunksY;

    /// <summary>Whether the chunk is inside the box.</summary>
    /// <param name="chunk">The chunk.</param>
    public bool Covers(VoxelChunkCoord chunk) =>
        (uint)(chunk.X - MinChunkX) < (uint)ChunksX && (uint)(chunk.Y - MinChunkY) < (uint)ChunksY && (uint)(chunk.Z - MinChunkZ) < (uint)ChunksZ;

    /// <summary>Whether the chunk column (any layer) is inside the box's footprint.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public bool CoversColumn(int chunkX, int chunkZ) => (uint)(chunkX - MinChunkX) < (uint)ChunksX && (uint)(chunkZ - MinChunkZ) < (uint)ChunksZ;

    /// <summary>Whether the block column is inside the box's footprint.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool ContainsColumn(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

    /// <summary>The footprint's column nearest to the given one (the column itself when it is inside): the point of the edge a column outside is eased towards.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public (int X, int Z) Clamp(int x, int z) => (Math.Clamp(x, MinX, MaxX), Math.Clamp(z, MinZ, MaxZ));

    /// <summary>The straight-line distance in whole blocks (rounded down) from the column to the footprint: 0 inside, 1 for the column beside the edge.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int DistanceTo(int x, int z)
    {
        var dx = x < MinX ? MinX - x : x > MaxX ? x - MaxX : 0;
        var dz = z < MinZ ? MinZ - z : z > MaxZ ? z - MaxZ : 0;
        if (dx == 0 || dz == 0)
        {
            return dx + dz;
        }

        return (int)IntMath.ISqrt(((long)dx * dx) + ((long)dz * dz));
    }
}
