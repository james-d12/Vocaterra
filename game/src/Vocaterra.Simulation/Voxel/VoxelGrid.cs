using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// The blocks of a 3D world, stored in 16 x 16 x 16 chunks. X grows east, Y grows up and Z grows south. The grid is bounded: everything
/// outside it reads as solid for collision (<see cref="IsSolid"/>) and as air for lookups (<see cref="GetBlock"/>). Reading is public;
/// writing stays inside the simulation (the scene builder and <see cref="VoxelSimulation"/>).
/// </summary>
public sealed class VoxelGrid
{
    private readonly VoxelChunk[] _chunks;
    private byte[]? _digest;
    private long _digestEdits = -1;

    internal VoxelGrid(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0
            || sizeX % VoxelChunk.Size != 0 || sizeY % VoxelChunk.Size != 0 || sizeZ % VoxelChunk.Size != 0)
        {
            throw new ArgumentException($"The grid size must be a positive multiple of {VoxelChunk.Size} in every direction.");
        }

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        ChunksX = sizeX / VoxelChunk.Size;
        ChunksY = sizeY / VoxelChunk.Size;
        ChunksZ = sizeZ / VoxelChunk.Size;
        _chunks = new VoxelChunk[ChunksX * ChunksY * ChunksZ];
        for (var cy = 0; cy < ChunksY; cy++)
        {
            for (var cz = 0; cz < ChunksZ; cz++)
            {
                for (var cx = 0; cx < ChunksX; cx++)
                {
                    _chunks[ChunkIndex(cx, cy, cz)] = new VoxelChunk(new VoxelChunkCoord(cx, cy, cz));
                }
            }
        }
    }

    /// <summary>The width in blocks (east).</summary>
    public int SizeX { get; }

    /// <summary>The height in blocks (up).</summary>
    public int SizeY { get; }

    /// <summary>The depth in blocks (south).</summary>
    public int SizeZ { get; }

    /// <summary>The number of chunk columns (east).</summary>
    public int ChunksX { get; }

    /// <summary>The number of chunk layers (up).</summary>
    public int ChunksY { get; }

    /// <summary>The number of chunk rows (south).</summary>
    public int ChunksZ { get; }

    /// <summary>
    /// The number of block edits that changed a block since the grid was created (a write of the block already there does not count). The
    /// world digest is cached against it.
    /// </summary>
    public long EditCount { get; private set; }

    /// <summary>Every chunk in digest order: layer by layer (Y), then row by row (Z), then column (X): index = (cy * ChunksZ + cz) * ChunksX + cx.</summary>
    public IReadOnlyList<VoxelChunk> Chunks => _chunks;

    /// <summary>Whether the block position lies inside the grid.</summary>
    public bool InBounds(int x, int y, int z) => (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;

    /// <summary>The block at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>); <see cref="BlockId.Air"/> outside the grid.</summary>
    public BlockId GetBlock(int x, int y, int z) =>
        InBounds(x, y, z) ? ChunkAt(x, y, z)[x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1)] : BlockId.Air;

    /// <summary>Whether bodies collide at the position. Everything outside the grid counts as solid (walls, floor and ceiling), so a body cannot leave it.</summary>
    public bool IsSolid(int x, int y, int z) => !InBounds(x, y, z) || BlockDefinition.IsSolid(GetBlock(x, y, z));

    /// <summary>The chunk at the given chunk coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates lie outside the chunk grid.</exception>
    public VoxelChunk GetChunk(VoxelChunkCoord coord)
    {
        if ((uint)coord.X >= (uint)ChunksX || (uint)coord.Y >= (uint)ChunksY || (uint)coord.Z >= (uint)ChunksZ)
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The chunk lies outside the grid.");
        }

        return _chunks[ChunkIndex(coord.X, coord.Y, coord.Z)];
    }

    /// <summary>
    /// The y of the highest block in the column that is not air, or -1 when the column is empty (or outside the grid). Trees and roofs count.
    /// </summary>
    public int HighestBlock(int x, int z)
    {
        if ((uint)x >= (uint)SizeX || (uint)z >= (uint)SizeZ)
        {
            return -1;
        }

        for (var y = SizeY - 1; y >= 0; y--)
        {
            if (GetBlock(x, y, z) != BlockId.Air)
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>
    /// The world digest: the SHA-256 of the grid size (three 32-bit little-endian integers: X, Y, Z) followed by the 32-byte digest of every
    /// chunk in <see cref="Chunks"/> order. Every edit that changes a block changes it. Goldens and the game's state hash use this. Cached
    /// until the next edit; chunk digests are cached per chunk, so recomputing after one edit hashes one chunk and then the 32-byte chunk digests.
    /// </summary>
    public byte[] ComputeDigest()
    {
        if (_digest is { } cached && _digestEdits == EditCount)
        {
            return cached;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(header, SizeX);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], SizeY);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], SizeZ);
        hash.AppendData(header);
        foreach (var chunk in _chunks)
        {
            hash.AppendData(chunk.ComputeDigest());
        }

        _digest = hash.GetHashAndReset();
        _digestEdits = EditCount;
        return _digest;
    }

    /// <summary>The world digest as 64 lowercase hex characters.</summary>
    public string ComputeDigestHex() => Convert.ToHexString(ComputeDigest()).ToLowerInvariant();

    /// <summary>Writes a block; returns whether the grid changed.</summary>
    internal bool SetBlock(int x, int y, int z, BlockId block)
    {
        if (!InBounds(x, y, z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Block ({x}, {y}, {z}) lies outside the grid.");
        }

        var changed = ChunkAt(x, y, z).Set(x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1), block);
        if (changed)
        {
            EditCount++;
        }

        return changed;
    }

    private VoxelChunk ChunkAt(int x, int y, int z) => _chunks[ChunkIndex(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift)];

    private int ChunkIndex(int cx, int cy, int cz) => (((cy * ChunksZ) + cz) * ChunksX) + cx;
}
