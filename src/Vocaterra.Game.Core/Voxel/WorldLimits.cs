namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The box a <see cref="VoxelGrid"/> holds: its minimum corner and its extent in blocks, both aligned to whole chunks. A bounded grid's box starts at
/// (0, 0, 0); a streamed grid's box is the world's limits (x and z plus and minus 30 000, y -64 to 320 by default), given by the caller (B06 reads them from
/// <c>world.json</c>). Everything outside the box reads as solid for collision (<see cref="VoxelGrid.IsSolid"/>), as today's edge wall.
/// </summary>
/// <param name="MinX">The smallest x inside, a multiple of 16.</param>
/// <param name="MinY">The smallest y inside, a multiple of 16 (may be negative).</param>
/// <param name="MinZ">The smallest z inside, a multiple of 16.</param>
/// <param name="SizeX">The extent along x, a positive multiple of 16.</param>
/// <param name="SizeY">The extent along y, a positive multiple of 16.</param>
/// <param name="SizeZ">The extent along z, a positive multiple of 16.</param>
public readonly record struct WorldLimits(int MinX, int MinY, int MinZ, int SizeX, int SizeY, int SizeZ)
{
    /// <summary>The default half extent of a streamed world along x and z, in blocks.</summary>
    public const int DefaultHalfExtent = 30_000;

    /// <summary>The default lowest y of a streamed world.</summary>
    public const int DefaultMinY = -64;

    /// <summary>The default end (exclusive) of a streamed world's y range.</summary>
    public const int DefaultEndY = 320;

    /// <summary>The limits of a streamed world with the defaults: x and z from -30 000 to 30 000, y from -64 to 320.</summary>
    public static WorldLimits Default { get; } = Symmetric(DefaultHalfExtent, DefaultMinY, DefaultEndY);

    /// <summary>The limits of a bounded grid: from the origin, <paramref name="sizeX"/> by <paramref name="sizeY"/> by <paramref name="sizeZ"/> blocks.</summary>
    public static WorldLimits Bounded(int sizeX, int sizeY, int sizeZ) => new(0, 0, 0, sizeX, sizeY, sizeZ);

    /// <summary>The limits of a world centred on the origin in x and z: from -<paramref name="halfExtent"/> to <paramref name="halfExtent"/>, and in y from <paramref name="minY"/> to <paramref name="endY"/> (exclusive).</summary>
    public static WorldLimits Symmetric(int halfExtent, int minY, int endY) => new(-halfExtent, minY, -halfExtent, halfExtent * 2, endY - minY, halfExtent * 2);

    /// <summary>One past the largest x inside.</summary>
    public int EndX => MinX + SizeX;

    /// <summary>One past the largest y inside.</summary>
    public int EndY => MinY + SizeY;

    /// <summary>One past the largest z inside.</summary>
    public int EndZ => MinZ + SizeZ;

    /// <summary>The smallest chunk column inside.</summary>
    public int MinChunkX => MinX >> VoxelChunk.Shift;

    /// <summary>The smallest chunk layer inside.</summary>
    public int MinChunkY => MinY >> VoxelChunk.Shift;

    /// <summary>The smallest chunk row inside.</summary>
    public int MinChunkZ => MinZ >> VoxelChunk.Shift;

    /// <summary>One past the largest chunk column inside.</summary>
    public int EndChunkX => EndX >> VoxelChunk.Shift;

    /// <summary>One past the largest chunk layer inside.</summary>
    public int EndChunkY => EndY >> VoxelChunk.Shift;

    /// <summary>One past the largest chunk row inside.</summary>
    public int EndChunkZ => EndZ >> VoxelChunk.Shift;

    /// <summary>Whether the block position lies inside.</summary>
    public bool Contains(int x, int y, int z) => (uint)(x - MinX) < (uint)SizeX && (uint)(y - MinY) < (uint)SizeY && (uint)(z - MinZ) < (uint)SizeZ;

    /// <summary>Whether the chunk lies inside.</summary>
    public bool Contains(VoxelChunkCoord chunk) =>
        (uint)(chunk.X - MinChunkX) < (uint)(EndChunkX - MinChunkX) && (uint)(chunk.Y - MinChunkY) < (uint)(EndChunkY - MinChunkY) && (uint)(chunk.Z - MinChunkZ) < (uint)(EndChunkZ - MinChunkZ);

    /// <summary>Refuses limits that are not a positive whole number of chunks in every direction, or that overflow an integer.</summary>
    /// <exception cref="ArgumentException">The limits are not chunk-aligned or not positive.</exception>
    internal void Validate()
    {
        const int Mask = VoxelChunk.Size - 1;
        if (SizeX <= 0 || SizeY <= 0 || SizeZ <= 0
            || SizeX % VoxelChunk.Size != 0 || SizeY % VoxelChunk.Size != 0 || SizeZ % VoxelChunk.Size != 0
            || (MinX & Mask) != 0 || (MinY & Mask) != 0 || (MinZ & Mask) != 0
            || (long)MinX + SizeX > int.MaxValue / 2 || (long)MinY + SizeY > int.MaxValue / 2 || (long)MinZ + SizeZ > int.MaxValue / 2
            || (long)MinX < int.MinValue / 2 || (long)MinY < int.MinValue / 2 || (long)MinZ < int.MinValue / 2)
        {
            throw new ArgumentException($"The grid size must be a positive multiple of {VoxelChunk.Size} in every direction, from a corner on a chunk boundary.");
        }
    }
}
