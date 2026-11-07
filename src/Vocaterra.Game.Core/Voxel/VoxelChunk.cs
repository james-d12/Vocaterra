using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vocaterra.Game.Core.Voxel;

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
    private int _nonAir;
    private byte[]? _water;
    private int _flowing;
    private ulong _kinds;
    private bool _kindsValid;

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

    /// <summary>How many of the 4 096 blocks are not air (kept up to date by every edit, so it costs nothing to read).</summary>
    public int NonAirCount => _nonAir;

    /// <summary>Whether the chunk holds nothing but air: a renderer skips it and a search passes it by.</summary>
    public bool IsEmpty => _nonAir == 0;

    /// <summary>
    /// The block kinds the chunk holds, one bit each (bit <c>(int)BlockId</c>; every kind is below 64). Computed on first use after an edit and cached,
    /// so a search for one kind skips every chunk without it.
    /// </summary>
    public ulong KindMask
    {
        get
        {
            if (!_kindsValid)
            {
                var mask = 0UL;
                foreach (var block in _blocks)
                {
                    mask |= 1UL << (int)block;
                }

                _kinds = mask;
                _kindsValid = true;
            }

            return _kinds;
        }
    }

    /// <summary>Whether any water cell of the chunk holds a level other than source. A chunk the scene builds never does, and then it carries no level layer at all.</summary>
    public bool HasWaterLevels => _flowing > 0;

    /// <summary>The block at the chunk-local position.</summary>
    public BlockId this[int localX, int localY, int localZ] => _blocks[Index(localX, localY, localZ)];

    /// <summary>
    /// The SHA-256 of the chunk: its 4 096 block ids, each a 16-bit little-endian integer, in storage order. When the chunk holds a water
    /// level other than source (<see cref="HasWaterLevels"/>), its 4 096 level bytes follow in the same order; otherwise the digest is exactly
    /// what it was before levels existed. Cached until the next edit.
    /// </summary>
    public byte[] ComputeDigest()
    {
        if (_digest is { } cached)
        {
            return cached;
        }

        var blockBytes = Volume * sizeof(ushort);
        var buffer = new byte[HasWaterLevels ? blockBytes + Volume : blockBytes];
        if (_flowing > 0 && _water is { } levels)
        {
            levels.CopyTo(buffer.AsSpan(blockBytes));
        }

        for (var i = 0; i < Volume; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(i * sizeof(ushort)), (ushort)_blocks[i]);
        }

        _digest = SHA256.HashData(buffer);
        return _digest;
    }

    /// <summary>The chunk digest as 64 lowercase hex characters.</summary>
    public string ComputeDigestHex() => Convert.ToHexString(ComputeDigest()).ToLowerInvariant();

    /// <summary>The water level at the chunk-local position: <see cref="WaterLevel.Source"/> when the chunk has no level layer or the cell holds none. Meaningful for water cells only.</summary>
    public WaterLevel GetWater(int localX, int localY, int localZ) =>
        _water is null ? WaterLevel.Source : WaterLevel.FromEncoded(_water[Index(localX, localY, localZ)]);

    /// <summary>
    /// A copy with its own blocks and the same version and counts. The cached digest is shared: an edit replaces it and never writes into it.
    /// </summary>
    internal VoxelChunk Clone()
    {
        var copy = new VoxelChunk(Coord);
        _blocks.CopyTo(copy._blocks, 0);
        copy._digest = _digest;
        copy._nonAir = _nonAir;
        copy._water = (byte[]?)_water?.Clone();
        copy._flowing = _flowing;
        copy._kinds = _kinds;
        copy._kindsValid = _kindsValid;
        copy.Version = Version;
        return copy;
    }

    internal bool Set(int localX, int localY, int localZ, BlockId block)
    {
        var index = Index(localX, localY, localZ);
        if (_blocks[index] == block)
        {
            return false;
        }

        if (_blocks[index] == BlockId.Air)
        {
            _nonAir++;
        }
        else if (block == BlockId.Air)
        {
            _nonAir--;
        }

        _blocks[index] = block;
        if (block != BlockId.Water)
        {
            ClearLevel(index);
        }

        _digest = null;
        _kindsValid = false;
        Version++;
        return true;
    }

    /// <summary>Sets the level of the water cell at the chunk-local position (the caller has checked it is water); allocates the layer on the first non-source level. Returns whether the level changed.</summary>
    internal bool SetWater(int localX, int localY, int localZ, WaterLevel level)
    {
        var index = Index(localX, localY, localZ);
        var old = _water?[index] ?? 0;
        if (old == level.Encoded)
        {
            return false;
        }

        _water ??= new byte[Volume];
        _water[index] = level.Encoded;
        _flowing += (level.Encoded != 0 ? 1 : 0) - (old != 0 ? 1 : 0);
        _digest = null;
        Version++;
        return true;
    }

    /// <summary>Fills the chunk-local column (localX, localZ) from <paramref name="localY0"/> to <paramref name="localY1"/> inclusive with a block; returns how many blocks changed.</summary>
    internal int FillColumn(int localX, int localZ, int localY0, int localY1, BlockId block)
    {
        var changed = 0;
        for (var y = localY0; y <= localY1; y++)
        {
            var index = Index(localX, y, localZ);
            var old = _blocks[index];
            if (old == block)
            {
                // Filling water over water writes source: a non-source level there is reset.
                if (block == BlockId.Water && _water is not null && _water[index] != 0)
                {
                    ClearLevel(index);
                    changed++;
                }

                continue;
            }

            if (old == BlockId.Air)
            {
                _nonAir++;
            }
            else if (block == BlockId.Air)
            {
                _nonAir--;
            }

            _blocks[index] = block;
            ClearLevel(index);
            changed++;
        }

        if (changed > 0)
        {
            _digest = null;
            _kindsValid = false;
            Version++;
        }

        return changed;
    }

    private void ClearLevel(int index)
    {
        if (_water is not null && _water[index] != 0)
        {
            _water[index] = 0;
            _flowing--;
        }
    }

    private static int Index(int localX, int localY, int localZ) => (((localY << Shift) + localZ) << Shift) + localX;
}
