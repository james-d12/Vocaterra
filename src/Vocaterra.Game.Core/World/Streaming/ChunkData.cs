using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// One generated chunk, immutable (B03): 16 x 16 x 16 block ids and, when some water is not a source, the water levels. It is what an <see cref="IChunkSource"/> hands out and what a
/// later milestone installs into a grid, so it owns no <see cref="VoxelChunk"/>. The order is a chunk's own: index = (localY * 16 + localZ) * 16 + localX, and a water level is
/// <see cref="WaterLevel.Encoded"/> (0 for a source and for a cell that is not water), so a grid can adopt both without translating. A chunk of air holds no array at all.
/// </summary>
public sealed class ChunkData
{
    /// <summary>The number of blocks in a chunk.</summary>
    public const int Volume = VoxelChunk.Volume;

    private static readonly BlockId[] NoBlocks = new BlockId[Volume];

    private readonly BlockId[]? _blocks;
    private readonly byte[]? _water;

    /// <summary>Takes ownership of the arrays: the caller must not keep or write them.</summary>
    internal ChunkData(VoxelChunkCoord coord, BlockId[]? blocks, byte[]? water)
    {
        Coord = coord;
        _blocks = blocks;
        _water = water;
        if (blocks is not null)
        {
            var solid = 0;
            foreach (var block in blocks)
            {
                solid += block == BlockId.Air ? 0 : 1;
            }

            NonAirCount = solid;
            if (solid == 0)
            {
                _blocks = null;
            }
        }

        if (_water is not null && Array.TrueForAll(_water, static level => level == 0))
        {
            _water = null;
        }
    }

    /// <summary>Where the chunk sits in the chunk grid.</summary>
    public VoxelChunkCoord Coord { get; }

    /// <summary>How many of the 4 096 blocks are not air.</summary>
    public int NonAirCount { get; }

    /// <summary>Whether the chunk holds nothing but air.</summary>
    public bool IsEmpty => NonAirCount == 0;

    /// <summary>The blocks, layer by layer, row by row (index = (localY * 16 + localZ) * 16 + localX).</summary>
    public ReadOnlySpan<BlockId> Blocks => _blocks ?? NoBlocks;

    /// <summary>Whether any water cell holds a level other than source.</summary>
    public bool HasWaterLevels => _water is not null;

    /// <summary>The encoded water level of every cell in storage order, or an empty span while every water cell is a source.</summary>
    public ReadOnlySpan<byte> WaterLevels => _water ?? default;

    /// <summary>The block at the chunk-local position.</summary>
    public BlockId this[int localX, int localY, int localZ] => Blocks[Index(localX, localY, localZ)];

    /// <summary>A chunk of air.</summary>
    /// <param name="coord">Its position.</param>
    public static ChunkData Empty(VoxelChunkCoord coord) => new(coord, null, null);

    /// <summary>A chunk made from copies of the given blocks and water levels (for a source that has them already, such as an authored region).</summary>
    /// <param name="coord">The chunk's position.</param>
    /// <param name="blocks">The 4 096 blocks, in storage order.</param>
    /// <param name="waterLevels">The 4 096 encoded water levels, or an empty span when every water cell is a source.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static ChunkData From(VoxelChunkCoord coord, ReadOnlySpan<BlockId> blocks, ReadOnlySpan<byte> waterLevels)
    {
        if (blocks.Length != Volume || (waterLevels.Length != 0 && waterLevels.Length != Volume))
        {
            throw new ArgumentException($"A chunk holds {Volume} blocks and either no water levels or {Volume} of them.");
        }

        return new ChunkData(coord, blocks.ToArray(), waterLevels.Length == 0 ? null : waterLevels.ToArray());
    }

    /// <summary>The storage index of a chunk-local position.</summary>
    /// <param name="localX">The x, 0 to 15.</param>
    /// <param name="localY">The y, 0 to 15.</param>
    /// <param name="localZ">The z, 0 to 15.</param>
    public static int Index(int localX, int localY, int localZ) => (((localY * VoxelChunk.Size) + localZ) * VoxelChunk.Size) + localX;

    /// <summary>The SHA-256 of the chunk as <see cref="VoxelChunk.ComputeDigest"/> makes it (the block ids as 16-bit little-endian integers, then the water levels when there are any), in lowercase hex.</summary>
    public string ComputeDigestHex()
    {
        var blockBytes = Volume * sizeof(ushort);
        var buffer = new byte[HasWaterLevels ? blockBytes + Volume : blockBytes];
        var blocks = Blocks;
        for (var i = 0; i < Volume; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(i * sizeof(ushort)), (ushort)blocks[i]);
        }

        if (_water is not null)
        {
            _water.CopyTo(buffer, blockBytes);
        }

        return Convert.ToHexString(SHA256.HashData(buffer)).ToLowerInvariant();
    }
}
