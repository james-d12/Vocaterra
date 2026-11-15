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

    // All-air chunks never allocate their blocks: the array appears with the first block that is not air, and a read of a chunk without one is air.
    private static readonly BlockId[] EmptyBlocks = new BlockId[Volume];
    private static readonly Lazy<byte[]> AirDigest = new(() => SHA256.HashData(new byte[Volume * sizeof(ushort)]));

    /// <summary>The digest of a chunk of air (what a chunk that is not stored hashes as).</summary>
    internal static byte[] AirChunkDigest => AirDigest.Value;

    private BlockId[]? _blocks;
    private byte[]? _digest;
    private byte[]? _pristine;
    private bool _pristineRecorded;
    private int _nonAir;
    private byte[]? _water;
    private int _flowing;
    private ulong _kinds;
    private bool _kindsValid;

    internal VoxelChunk(VoxelChunkCoord coord) => Coord = coord;

    /// <summary>Where this chunk sits in the chunk grid.</summary>
    public VoxelChunkCoord Coord { get; }

    /// <summary>The blocks, layer by layer, row by row (index = (localY * 16 + localZ) * 16 + localX).</summary>
    public ReadOnlySpan<BlockId> Blocks => _blocks ?? EmptyBlocks;

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
                foreach (var block in Blocks)
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

    /// <summary>
    /// The encoded water level of every cell in storage order (<see cref="WaterLevel.Encoded"/>; 0 for source and for cells that are not water), or an empty span while the
    /// chunk holds no level other than source (<see cref="HasWaterLevels"/>). A save compares and writes it.
    /// </summary>
    public ReadOnlySpan<byte> WaterLevels => _flowing > 0 && _water is not null ? _water : default;

    /// <summary>The block at the chunk-local position.</summary>
    public BlockId this[int localX, int localY, int localZ] => _blocks is null ? BlockId.Air : _blocks[Index(localX, localY, localZ)];

    /// <summary>
    /// Whether the chunk's content differs from what it held just before its first write: a chunk nobody wrote, or whose edits were all undone, is not changed.
    /// An unchanged chunk can be evicted and regenerated; a changed one is state (B05 saves and digests the changed chunks). A chunk the scene builder filled by
    /// writing counts as changed until <see cref="VoxelGrid.SealPristine"/>.
    /// </summary>
    public bool IsChanged => _pristine is not null && !_pristine.AsSpan().SequenceEqual(ComputeDigest());

    /// <summary>
    /// The digest the chunk had just before its first write (recorded lazily, so reading and installing cost nothing), or the current digest while it has never
    /// been written. <see cref="IsChanged"/> is whether it differs from <see cref="ComputeDigest"/>.
    /// </summary>
    public byte[] PristineDigest => _pristine ?? ComputeDigest();

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

        if (_nonAir == 0 && _flowing == 0)
        {
            return _digest = AirDigest.Value;
        }

        var blockBytes = Volume * sizeof(ushort);
        var buffer = new byte[HasWaterLevels ? blockBytes + Volume : blockBytes];
        if (_flowing > 0 && _water is { } levels)
        {
            levels.CopyTo(buffer.AsSpan(blockBytes));
        }

        for (var i = 0; i < Volume; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(i * sizeof(ushort)), (ushort)_blocks![i]);
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
        copy._blocks = (BlockId[]?)_blocks?.Clone();
        copy._digest = _digest;
        copy._pristine = _pristine;
        copy._pristineRecorded = _pristineRecorded;
        copy._nonAir = _nonAir;
        copy._water = (byte[]?)_water?.Clone();
        copy._flowing = _flowing;
        copy._kinds = _kinds;
        copy._kindsValid = _kindsValid;
        copy.Version = Version;
        return copy;
    }

    /// <summary>
    /// Installs generated content into a chunk that holds nothing yet. Not an edit: the version stays 0 and no pristine digest is recorded, so the content is the
    /// chunk's pristine state.
    /// </summary>
    internal void Install(ChunkContent content)
    {
        _blocks = content.Blocks;
        _water = content.WaterLevels;
        _nonAir = 0;
        foreach (var block in _blocks)
        {
            if (block != BlockId.Air)
            {
                _nonAir++;
            }
        }

        _flowing = 0;
        if (_water is not null)
        {
            foreach (var level in _water)
            {
                if (level != 0)
                {
                    _flowing++;
                }
            }

            if (_flowing == 0)
            {
                _water = null;
            }
        }

        if (_nonAir == 0)
        {
            _blocks = null; // a chunk of air keeps no array
        }

        _digest = null;
        _kindsValid = false;
    }

    /// <summary>Forgets the pristine digest: what the chunk holds now becomes its pristine state.</summary>
    internal void SealPristine()
    {
        _pristine = null;
        _pristineRecorded = false;
    }

    internal bool Set(int localX, int localY, int localZ, BlockId block)
    {
        var index = Index(localX, localY, localZ);
        var old = _blocks is null ? BlockId.Air : _blocks[index];
        if (old == block)
        {
            return false;
        }

        RecordPristine();
        _blocks ??= new BlockId[Volume];
        if (old == BlockId.Air)
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

        RecordPristine();
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
        if (_blocks is null && block == BlockId.Air)
        {
            return 0;
        }

        for (var y = localY0; y <= localY1; y++)
        {
            var index = Index(localX, y, localZ);
            var old = _blocks is null ? BlockId.Air : _blocks[index];
            if (old == block)
            {
                // Filling water over water writes source: a non-source level there is reset.
                if (block == BlockId.Water && _water is not null && _water[index] != 0)
                {
                    RecordPristine();
                    ClearLevel(index);
                    changed++;
                }

                continue;
            }

            RecordPristine();
            _blocks ??= new BlockId[Volume];
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

    // Just before the first write (lazily, so a chunk nobody writes never pays for it): the digest the chunk has now is its pristine one. A write that changes nothing
    // never gets here.
    private void RecordPristine()
    {
        if (!_pristineRecorded)
        {
            _pristine = ComputeDigest();
            _pristineRecorded = true;
        }
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
