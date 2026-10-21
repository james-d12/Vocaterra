using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// A 16 x 16 x 16 cube of blocks, the unit of re-meshing and of the per-chunk digest. Blocks are stored layer by layer, row by row:
/// index = (localY * 16 + localZ) * 16 + localX. Every edit increments <see cref="Version"/>, so a renderer can keep the version it meshed
/// and rebuild a chunk when it differs; the digest is cached until the next edit.
/// </summary>
public sealed class VoxelChunk
{
    /// <summary>The chunk edge in blocks.</summary>
    public const int Size = 16;

    /// <summary>log2 of <see cref="Size"/>, for shifts.</summary>
    public const int Shift = 4;

    /// <summary>The number of blocks in a chunk.</summary>
    public const int Volume = Size * Size * Size;

    private readonly BlockId[] _blocks = new BlockId[Volume];
    private byte[]? _digest;

    internal VoxelChunk(VoxelChunkCoord coord) => Coord = coord;

    /// <summary>Where this chunk sits in the chunk grid.</summary>
    public VoxelChunkCoord Coord { get; }

    /// <summary>The blocks, layer by layer, row by row (index = (localY * 16 + localZ) * 16 + localX).</summary>
    public ReadOnlySpan<BlockId> Blocks => _blocks;

    /// <summary>
    /// How many block edits this chunk has had since the grid was created. It changes whenever the content changes, so a renderer compares
    /// it with the value it last meshed.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>The block at the chunk-local position.</summary>
    public BlockId this[int localX, int localY, int localZ] => _blocks[Index(localX, localY, localZ)];

    /// <summary>
    /// The SHA-256 of the chunk: its 4 096 block ids, each a 16-bit little-endian integer, in storage order. Cached until the next edit.
    /// </summary>
    public byte[] ComputeDigest()
    {
        if (_digest is { } cached)
        {
            return cached;
        }

        var buffer = new byte[Volume * sizeof(ushort)];
        for (var i = 0; i < Volume; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(i * sizeof(ushort)), (ushort)_blocks[i]);
        }

        _digest = SHA256.HashData(buffer);
        return _digest;
    }

    /// <summary>The chunk digest as 64 lowercase hex characters.</summary>
    public string ComputeDigestHex() => Convert.ToHexString(ComputeDigest()).ToLowerInvariant();

    internal bool Set(int localX, int localY, int localZ, BlockId block)
    {
        var index = Index(localX, localY, localZ);
        if (_blocks[index] == block)
        {
            return false;
        }

        _blocks[index] = block;
        _digest = null;
        Version++;
        return true;
    }

    private static int Index(int localX, int localY, int localZ) => (((localY << Shift) + localZ) << Shift) + localX;
}
