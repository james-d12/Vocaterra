using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The blocks of a 3D world, stored sparsely in 16 x 16 x 16 chunks keyed by <see cref="VoxelChunkCoord"/>: a chunk that holds nothing but air is never
/// allocated, and a cache of the last chunk used serves the reads of a walk through one chunk. X grows east, Y grows up and Z grows south. The grid has two modes.
/// A <b>bounded</b> grid (the constructor of a size, the scene builder's and the tests') is the box from (0, 0, 0) to its size; every dense member
/// (<see cref="Chunks"/>, <see cref="ChunksX"/>, <see cref="BlocksOfKind"/>, <see cref="ComputeDigest"/>) is valid there. A <b>streamed</b> grid (the constructor of
/// <see cref="WorldLimits"/>) holds the chunks installed into it or generated on read by its <see cref="IChunkGenerator"/>, within limits that may reach below y 0; the
/// dense members throw there, because the residency, not the grid, decides what is loaded. Everything outside the limits reads as solid for collision
/// (<see cref="IsSolid"/>) and as air for lookups (<see cref="GetBlock"/>). Reading is public; writing stays inside the simulation (the scene builder and
/// <see cref="VoxelSimulation"/>).
/// </summary>
public sealed class VoxelGrid : IVoxelWriter
{
    private readonly Dictionary<VoxelChunkCoord, VoxelChunk> _chunks = new();

    // A bounded grid's chunks never come or go, so beside the dictionary (which every enumeration reads) it keeps one reference slot for each position of its chunk box:
    // a lookup that misses the cache is an index, not a hash. Empty slots are the chunks that hold nothing and are not allocated. Null on a streamed grid.
    private readonly VoxelChunk?[]? _slots;
    private readonly WorldLimits _limits;
    private readonly bool _streamed;
    private readonly IChunkGenerator? _generator;

    // The read cache: the last chunk used, its coordinates and its block array (the shared array of air for a chunk that holds nothing), so that a read inside it is a
    // compare and an index. _cacheBlocks is null while the cache is empty.
    private VoxelChunk? _cacheChunk;
    private BlockId[]? _cacheBlocks;
    private int _cacheX;
    private int _cacheY;
    private int _cacheZ;
    private VoxelChunk[]? _dense;
    private byte[]? _digest;
    private long _digestEdits = -1;
    private readonly List<IBlockChangeListener> _listeners = [];
    private readonly List<IChunkResidencyListener> _residencyListeners = [];

    /// <summary>Creates a bounded grid: the box from (0, 0, 0), a positive multiple of 16 blocks in every direction.</summary>
    internal VoxelGrid(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0
            || sizeX % VoxelChunk.Size != 0 || sizeY % VoxelChunk.Size != 0 || sizeZ % VoxelChunk.Size != 0)
        {
            throw new ArgumentException($"The grid size must be a positive multiple of {VoxelChunk.Size} in every direction.");
        }

        _limits = WorldLimits.Bounded(sizeX, sizeY, sizeZ);
        _slots = new VoxelChunk?[_limits.EndChunkX * _limits.EndChunkY * _limits.EndChunkZ];
    }

    /// <summary>
    /// Creates a streamed grid over <paramref name="limits"/> (from <c>world.json</c> later; never from content here). It starts with no chunk resident. A read of a chunk
    /// inside the limits that is not resident asks <paramref name="generator"/> for it and installs the result; without a generator such a chunk reads as air.
    /// </summary>
    /// <exception cref="ArgumentException">The limits are not chunk-aligned and positive.</exception>
    internal VoxelGrid(WorldLimits limits, IChunkGenerator? generator = null)
    {
        limits.Validate();
        _limits = limits;
        _streamed = true;
        _generator = generator;
    }

    private VoxelGrid(VoxelGrid source)
    {
        _limits = source._limits;
        _streamed = source._streamed;
        _generator = source._generator;
        Barrier = source.Barrier is { } wall ? new BodyBarrier(wall.MinX, wall.MinZ, wall.MaxX, wall.MaxZ) { Locked = wall.Locked } : null;
        EditCount = source.EditCount;
        ResidencyVersion = source.ResidencyVersion;
        _digest = source._digest;
        _digestEdits = source._digestEdits;
        _slots = source._slots is null ? null : new VoxelChunk?[source._slots.Length];
        foreach (var (coord, chunk) in source._chunks)
        {
            var copy = chunk.Clone();
            _chunks[coord] = copy;
            if (_slots is not null)
            {
                _slots[SlotOf(coord.X, coord.Y, coord.Z)] = copy;
            }
        }
    }

    /// <summary>The box the grid holds (a bounded grid's starts at the origin).</summary>
    public WorldLimits Limits => _limits;

    /// <summary>Whether this is a streamed grid (chunks installed or generated on read, the dense members unavailable) rather than a bounded one.</summary>
    public bool IsStreamed => _streamed;

    /// <summary>The extent in blocks along x (east).</summary>
    public int SizeX => _limits.SizeX;

    /// <summary>The extent in blocks along y (up).</summary>
    public int SizeY => _limits.SizeY;

    /// <summary>The extent in blocks along z (south).</summary>
    public int SizeZ => _limits.SizeZ;

    /// <summary>The number of chunk columns (east). Dense: bounded grids only.</summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    public int ChunksX => Bounded(nameof(ChunksX), _limits.EndChunkX);

    /// <summary>The number of chunk layers (up). Dense: bounded grids only.</summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    public int ChunksY => Bounded(nameof(ChunksY), _limits.EndChunkY);

    /// <summary>The number of chunk rows (south). Dense: bounded grids only.</summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    public int ChunksZ => Bounded(nameof(ChunksZ), _limits.EndChunkZ);

    /// <summary>
    /// The number of block edits that changed a block or a water level since the grid was created (a write of the block already there does not count). The
    /// world digest is cached against it. Installing and evicting chunks are not edits and never move it.
    /// </summary>
    public long EditCount { get; private set; }

    /// <summary>
    /// How many times the set of resident chunks changed (an install or an eviction) on a streamed grid; 0 on a bounded one, whose chunks never come or go. A
    /// reader that polls compares it with the value it last saw; <see cref="IChunkResidencyListener"/> says which chunk.
    /// </summary>
    public long ResidencyVersion { get; private set; }

    /// <summary>The coordinates of the resident chunks (a live view, in no fixed order): a reader on the game loop's thread walks it without copying, and never while a chunk is installed or evicted.</summary>
    public IReadOnlyCollection<VoxelChunkCoord> ResidentChunks => _chunks.Keys;

    /// <summary>How many chunks are resident (allocated, even if only to say that they hold air).</summary>
    public int ResidentChunkCount => _chunks.Count;

    /// <summary>
    /// Every chunk in digest order: layer by layer (Y), then row by row (Z), then column (X): index = (cy * ChunksZ + cz) * ChunksX + cx. Dense: bounded grids only.
    /// A chunk that holds nothing but air gets its object (without block storage) the first time this is read.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    public IReadOnlyList<VoxelChunk> Chunks
    {
        get
        {
            Bounded(nameof(Chunks), 0);
            return _dense ??= BuildDense();
        }
    }

    /// <summary>
    /// The water level at the position. A water cell with no stored level is <see cref="WaterLevel.Source"/>. Outside the grid, or where the
    /// block is not water, there is no level to read and the result is <see cref="WaterLevel.Source"/> too: check <see cref="GetBlock"/> first.
    /// </summary>
    public WaterLevel GetWater(int x, int y, int z) =>
        _limits.Contains(x, y, z) && ReadChunk(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift) is { } chunk
            ? chunk.GetWater(x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1))
            : WaterLevel.Source;

    /// <summary>Whether the block position lies inside the grid's limits.</summary>
    public bool InBounds(int x, int y, int z) => _limits.Contains(x, y, z);

    /// <summary>The block at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>); <see cref="BlockId.Air"/> outside the limits. A streamed grid generates a chunk it does not hold yet.</summary>
    public BlockId GetBlock(int x, int y, int z)
    {
        // A cached chunk lies inside the limits, which are whole chunks, so a hit needs no limits check.
        var blocks = _cacheBlocks;
        if (blocks is not null && (x >> VoxelChunk.Shift) == _cacheX && (y >> VoxelChunk.Shift) == _cacheY && (z >> VoxelChunk.Shift) == _cacheZ)
        {
            return blocks[LocalIndex(x, y, z)];
        }

        return _limits.Contains(x, y, z) && ReadChunk(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift) is { } chunk
            ? chunk.BlockArray[LocalIndex(x, y, z)]
            : BlockId.Air;
    }

    /// <summary>
    /// The block at the position if its chunk is resident, <see cref="BlockId.Air"/> otherwise (and outside the limits): a peek that never generates and never
    /// installs, for the renderer. It can differ from <see cref="GetBlock"/> only where the chunk is not resident.
    /// </summary>
    public BlockId PeekBlock(int x, int y, int z) =>
        _limits.Contains(x, y, z) && Find(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift) is { } chunk
            ? chunk[x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1)]
            : BlockId.Air;

    /// <summary>Whether bodies collide at the position. Everything outside the limits counts as solid (walls, floor and ceiling), so a body cannot leave the world.</summary>
    public bool IsSolid(int x, int y, int z)
    {
        var blocks = _cacheBlocks;
        if (blocks is not null && (x >> VoxelChunk.Shift) == _cacheX && (y >> VoxelChunk.Shift) == _cacheY && (z >> VoxelChunk.Shift) == _cacheZ)
        {
            return BlockDefinition.IsSolid(blocks[LocalIndex(x, y, z)]);
        }

        return !InBounds(x, y, z) || BlockDefinition.IsSolid(GetBlock(x, y, z));
    }

    /// <summary>
    /// Whether a body's box collides with the cell: a solid block, or the space just above a tall block (<see cref="BlockDefinition.Tall"/>, a fence),
    /// which collides as two blocks high so that no step-up or jump takes a body onto it (issue #33), or a block that stops the body without being solid (<see cref="BlockDefinition.StopsBody"/>: lava). The block ray and placement read <see cref="IsSolid"/>.
    /// </summary>
    public bool BlocksBody(int x, int y, int z) => (Barrier is { } barrier && barrier.Blocks(x, z)) || IsSolid(x, y, z) || BlockDefinition.IsTall(GetBlock(x, y - 1, z)) || BlockDefinition.StopsBodyOf(GetBlock(x, y, z));

    /// <summary>The invisible wall a body cannot cross while it is locked (<see cref="BodyBarrier"/>), or <see langword="null"/> for none (a bounded grid has its limits). Only <see cref="BlocksBody"/> reads it.</summary>
    public BodyBarrier? Barrier { get; internal set; }

    /// <summary>Whether a body can stand on the block at the position: it is solid and not tall (nothing stands on a fence).</summary>
    public bool CanStandOn(int x, int y, int z) => IsSolid(x, y, z) && !BlockDefinition.IsTall(GetBlock(x, y, z));

    /// <summary>
    /// The chunk at the given chunk coordinates, resident afterwards: a streamed grid generates it (or makes an empty one without a generator) and installs it when it
    /// is not resident; a bounded grid makes an empty chunk object (no block storage) for one that holds nothing.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates lie outside the limits.</exception>
    public VoxelChunk GetChunk(VoxelChunkCoord coord)
    {
        if (!_limits.Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The chunk lies outside the grid.");
        }

        return Lookup(coord) ?? Create(coord, null);
    }

    /// <summary>The chunk if it is resident; never generates and never installs.</summary>
    public bool TryGetChunk(VoxelChunkCoord coord, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out VoxelChunk? chunk)
    {
        chunk = _limits.Contains(coord) ? Find(coord.X, coord.Y, coord.Z) : null;
        return chunk is not null;
    }

    /// <summary>Whether the chunk is resident.</summary>
    public bool IsResident(VoxelChunkCoord coord) => _limits.Contains(coord) && Find(coord.X, coord.Y, coord.Z) is not null;

    /// <summary>Registers a listener for installs and evictions. Adding one already registered does nothing.</summary>
    public void AddResidencyListener(IChunkResidencyListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (!_residencyListeners.Contains(listener))
        {
            _residencyListeners.Add(listener);
        }
    }

    /// <summary>Unregisters a residency listener; returns whether it was registered.</summary>
    public bool RemoveResidencyListener(IChunkResidencyListener listener) => _residencyListeners.Remove(listener);

    /// <summary>
    /// The y of the highest block in the column that is not air, or <c>Limits.MinY - 1</c> (-1 on a bounded grid) when the column is empty or outside the limits.
    /// Trees and roofs count. A streamed grid generates the chunks of the column it does not hold, so the answer never depends on what is resident.
    /// </summary>
    public int HighestBlock(int x, int z)
    {
        if ((uint)(x - _limits.MinX) >= (uint)_limits.SizeX || (uint)(z - _limits.MinZ) >= (uint)_limits.SizeZ)
        {
            return _limits.MinY - 1;
        }

        for (var cy = _limits.EndChunkY - 1; cy >= _limits.MinChunkY; cy--)
        {
            var chunk = ReadChunk(x >> VoxelChunk.Shift, cy, z >> VoxelChunk.Shift);
            if (chunk is null || chunk.IsEmpty)
            {
                continue;
            }

            for (var ly = VoxelChunk.Size - 1; ly >= 0; ly--)
            {
                if (chunk[x & (VoxelChunk.Size - 1), ly, z & (VoxelChunk.Size - 1)] != BlockId.Air)
                {
                    return (cy << VoxelChunk.Shift) + ly;
                }
            }
        }

        return _limits.MinY - 1;
    }

    /// <summary>
    /// The world digest: the SHA-256 of the grid size (three 32-bit little-endian integers: X, Y, Z) followed by the 32-byte digest of every
    /// chunk in <see cref="Chunks"/> order (a chunk that is not stored is a chunk of air). Every edit that changes a block changes it. Goldens use this; the game's state hash is <see cref="Vocaterra.Game.Core.World.WorldStateDigest"/>. Cached until the next edit; chunk digests are cached per chunk, so recomputing after one edit hashes one chunk and then the 32-byte chunk digests.
    /// Dense: bounded grids only; the state digest of the game and of saves (B05) is <see cref="Vocaterra.Game.Core.World.WorldStateDigest"/>, which hashes the changed chunks only.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    public byte[] ComputeDigest()
    {
        Bounded(nameof(ComputeDigest), 0);
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
        foreach (var coord in DenseOrder())
        {
            hash.AppendData(_chunks.TryGetValue(coord, out var chunk) ? chunk.ComputeDigest() : VoxelChunk.AirChunkDigest);
        }

        _digest = hash.GetHashAndReset();
        _digestEdits = EditCount;
        return _digest;
    }

    /// <summary>The world digest as 64 lowercase hex characters.</summary>
    public string ComputeDigestHex() => Convert.ToHexString(ComputeDigest()).ToLowerInvariant();

    /// <summary>
    /// The positions of every block of kind <paramref name="kind"/>, chunk by chunk (chunks in <see cref="Chunks"/> order, blocks in storage order
    /// inside each); a chunk without the kind is passed by without reading its blocks. For searches that must not read the whole grid. Dense: bounded grids only.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed.</exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is air, which fills every chunk that is not stored.</exception>
    public List<BlockPos> BlocksOfKind(BlockId kind)
    {
        Bounded(nameof(BlocksOfKind), 0);
        if (kind == BlockId.Air)
        {
            throw new ArgumentException("Air is everywhere: BlocksOfKind answers for the kinds a chunk stores.", nameof(kind));
        }

        var found = new List<BlockPos>();
        var bit = BlockKinds.Bit(kind);
        foreach (var coord in DenseOrder())
        {
            if (!_chunks.TryGetValue(coord, out var chunk) || (chunk.KindMask & bit) == UInt128.Zero)
            {
                continue;
            }

            var origin = chunk.Coord.Origin;
            var blocks = chunk.Blocks;
            for (var i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == kind)
                {
                    found.Add(new BlockPos(
                        origin.X + (i & (VoxelChunk.Size - 1)),
                        origin.Y + (i >> (2 * VoxelChunk.Shift)),
                        origin.Z + ((i >> VoxelChunk.Shift) & (VoxelChunk.Size - 1))));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// A copy that edits apart from this grid: every resident chunk copied (<see cref="VoxelChunk.Clone"/>), with the same edit count and the same cached
    /// world digest, so the copy and the original are alike in everything a reader sees. Listeners are not copied.
    /// </summary>
    internal VoxelGrid Clone() => new(this);

    /// <summary>
    /// Installs a chunk that is not resident (a streamed grid only), as generated: <paramref name="content"/> (<see langword="null"/> for air) becomes the chunk's
    /// pristine state. Not an edit: <see cref="EditCount"/> does not move, no <see cref="IBlockChangeListener"/> is told and the flow simulation stays asleep;
    /// <see cref="ResidencyVersion"/> moves and the residency listeners are told. Returns false, and changes nothing, when the chunk is already resident (a duplicate is dropped).
    /// </summary>
    /// <exception cref="InvalidOperationException">The grid is bounded.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The chunk lies outside the limits.</exception>
    internal bool InstallChunk(VoxelChunkCoord coord, ChunkContent? content)
    {
        RequireStreamed(nameof(InstallChunk));
        if (!_limits.Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The chunk lies outside the grid.");
        }

        if (Find(coord.X, coord.Y, coord.Z) is not null)
        {
            return false;
        }

        Create(coord, content);
        return true;
    }

    /// <summary>
    /// Drops a resident chunk from memory (a streamed grid only), if it is unchanged: <see cref="VoxelChunk.IsChanged"/> chunks are state and stay, and so does a chunk
    /// that is not resident (false). Not an edit; <see cref="ResidencyVersion"/> moves and the residency listeners are told.
    /// </summary>
    /// <exception cref="InvalidOperationException">The grid is bounded.</exception>
    internal bool TryEvictChunk(VoxelChunkCoord coord)
    {
        RequireStreamed(nameof(TryEvictChunk));
        if (!_chunks.TryGetValue(coord, out var chunk) || chunk.IsChanged)
        {
            return false;
        }

        _chunks.Remove(coord);
        if (ReferenceEquals(_cacheChunk, chunk))
        {
            _cacheChunk = null;
            _cacheBlocks = null;
        }

        ResidencyVersion++;
        foreach (var listener in _residencyListeners.ToArray())
        {
            listener.OnChunkEvicted(coord);
        }

        return true;
    }

    /// <summary>The resident chunks' coordinates in no fixed order (a snapshot), for a host's residency driver (<see cref="World.Streaming.GridResidency"/>); the simulation never enumerates them.</summary>
    internal List<VoxelChunkCoord> ResidentCoordinateList() => new(_chunks.Keys);

    /// <summary>The resident chunks' coordinates in a fixed order (layer, then row, then column), for the streamer's policy; the simulation never enumerates them.</summary>
    internal List<VoxelChunkCoord> ResidentCoordinates()
    {
        var coords = new List<VoxelChunkCoord>(_chunks.Keys);
        coords.Sort(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
        return coords;
    }

    /// <summary>
    /// The resident chunks that are <see cref="VoxelChunk.IsChanged"/>, sorted by coordinate (layer, then row, then column): the world's state (B05). The state digest and a save
    /// write exactly these. The cost is one cheap test per resident chunk plus a digest per chunk that was written.
    /// </summary>
    internal List<VoxelChunk> ChangedChunks()
    {
        var changed = new List<VoxelChunk>();
        foreach (var chunk in _chunks.Values)
        {
            if (chunk.IsChanged)
            {
                changed.Add(chunk);
            }
        }

        changed.Sort(static (a, b) => a.Coord.Y != b.Coord.Y ? a.Coord.Y.CompareTo(b.Coord.Y) : a.Coord.Z != b.Coord.Z ? a.Coord.Z.CompareTo(b.Coord.Z) : a.Coord.X.CompareTo(b.Coord.X));
        return changed;
    }

    /// <summary>
    /// Replaces the blocks of a resident chunk as one write of the build (B13: the valley's underground, laid into the buried stone chunk by chunk): it is not an edit (<see cref="EditCount"/> and the
    /// listeners are not touched) and the caller seals the grid afterwards. The water levels stay as they were.
    /// </summary>
    /// <param name="chunk">The resident chunk.</param>
    /// <param name="blocks">The 4 096 new blocks, which the chunk takes over.</param>
    internal void ReplaceBlocks(VoxelChunk chunk, BlockId[] blocks)
    {
        chunk.Install(new ChunkContent(blocks, chunk.HasWaterLevels ? chunk.WaterLevels.ToArray() : null));
        if (ReferenceEquals(_cacheChunk, chunk))
        {
            Cache(chunk);
        }
    }

    /// <summary>Makes what every chunk holds now its pristine state (<see cref="VoxelChunk.PristineDigest"/>, <see cref="VoxelChunk.IsChanged"/>): the end of a build that wrote the world.</summary>
    internal void SealPristine()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.SealPristine();
        }
    }

    /// <summary>Fills the column (<paramref name="x"/>, <paramref name="z"/>) from <paramref name="y0"/> to <paramref name="y1"/> inclusive (clipped to the limits) with a block, chunk by chunk.</summary>
    internal void FillColumn(int x, int z, int y0, int y1, BlockId block)
    {
        if ((uint)(x - _limits.MinX) >= (uint)_limits.SizeX || (uint)(z - _limits.MinZ) >= (uint)_limits.SizeZ)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Column ({x}, {z}) lies outside the grid.");
        }

        y0 = Math.Max(y0, _limits.MinY);
        y1 = Math.Min(y1, _limits.EndY - 1);
        var (cx, cz) = (x >> VoxelChunk.Shift, z >> VoxelChunk.Shift);
        while (y0 <= y1)
        {
            var cy = y0 >> VoxelChunk.Shift;
            var top = Math.Min(y1, ((cy + 1) << VoxelChunk.Shift) - 1);
            if (ChunkForWrite(cx, cy, cz, block) is { } chunk)
            {
                EditCount += chunk.FillColumn(x & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1), y0 & (VoxelChunk.Size - 1), top & (VoxelChunk.Size - 1), block);
                Written(chunk);
            }

            y0 = top + 1;
        }
    }

    /// <summary>Writes a block; returns whether the grid changed.</summary>
    internal bool SetBlock(int x, int y, int z, BlockId block)
    {
        if (!InBounds(x, y, z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Block ({x}, {y}, {z}) lies outside the grid.");
        }

        if (ChunkForWrite(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift, block) is not { } chunk)
        {
            return false; // air written over a chunk that holds nothing
        }

        var (lx, ly, lz) = (x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1));
        var oldBlock = chunk[lx, ly, lz];
        var oldLevel = chunk.GetWater(lx, ly, lz);
        var changed = chunk.Set(lx, ly, lz, block);
        if (changed)
        {
            Written(chunk);
            EditCount++;
            Raise(new BlockChange(new BlockPos(x, y, z), oldBlock, block, oldLevel, chunk.GetWater(lx, ly, lz)));
        }

        return changed;
    }

    /// <summary>
    /// Writes the level of a water cell; returns whether it changed. Counts as an edit and re-meshes the chunk, like a block edit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The position lies outside the grid.</exception>
    /// <exception cref="InvalidOperationException">The block at the position is not water.</exception>
    internal bool SetWater(int x, int y, int z, WaterLevel level)
    {
        if (!InBounds(x, y, z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Block ({x}, {y}, {z}) lies outside the grid.");
        }

        var (lx, ly, lz) = (x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1));
        if (ReadChunk(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift) is not { } chunk || chunk[lx, ly, lz] != BlockId.Water)
        {
            throw new InvalidOperationException($"Block ({x}, {y}, {z}) is not water, so it has no level.");
        }

        var oldLevel = chunk.GetWater(lx, ly, lz);
        if (!chunk.SetWater(lx, ly, lz, level))
        {
            return false;
        }

        EditCount++;
        Raise(new BlockChange(new BlockPos(x, y, z), BlockId.Water, BlockId.Water, oldLevel, level));
        return true;
    }

    bool IVoxelWriter.SetBlock(int x, int y, int z, BlockId block) => SetBlock(x, y, z, block);

    bool IVoxelWriter.SetWater(int x, int y, int z, WaterLevel level) => SetWater(x, y, z, level);

    void IVoxelWriter.FillColumn(int x, int z, int y0, int y1, BlockId block) => FillColumn(x, z, y0, y1, block);

    /// <summary>Registers a listener for block and level changes (<see cref="SetBlock"/>, <see cref="SetWater"/>; never <see cref="FillColumn"/>). Adding one already registered does nothing.</summary>
    public void AddListener(IBlockChangeListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }
    }

    /// <summary>Unregisters a listener; returns whether it was registered.</summary>
    public bool RemoveListener(IBlockChangeListener listener) => _listeners.Remove(listener);

    private void Raise(BlockChange change)
    {
        // Iterate a snapshot: a listener may add or remove listeners, or edit the grid, while it is told.
        foreach (var listener in _listeners.ToArray())
        {
            listener.OnBlockChanged(change);
        }
    }

    // The chunk if it is resident: the last one used first (a walk of reads stays in a chunk), then the dictionary.
    private VoxelChunk? Find(int cx, int cy, int cz)
    {
        if (_cacheBlocks is not null && cx == _cacheX && cy == _cacheY && cz == _cacheZ)
        {
            return _cacheChunk;
        }

        VoxelChunk? chunk;
        if (_slots is not null)
        {
            chunk = _slots[SlotOf(cx, cy, cz)];
            if (chunk is not null)
            {
                Cache(chunk);
            }

            return chunk;
        }

        if (_chunks.TryGetValue(new VoxelChunkCoord(cx, cy, cz), out chunk))
        {
            Cache(chunk);
            return chunk;
        }

        return null;
    }

    // The slot of a chunk of the bounded box (the dense order of Chunks): layer, then row, then column. The caller has checked the limits.
    private int SlotOf(int cx, int cy, int cz) => (((cy * _limits.EndChunkZ) + cz) * _limits.EndChunkX) + cx;

    private void Cache(VoxelChunk chunk)
    {
        _cacheChunk = chunk;
        _cacheBlocks = chunk.BlockArray;
        (_cacheX, _cacheY, _cacheZ) = (chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z);
    }

    // A write may give the cached chunk its block array (a chunk of air gets one with its first block): the cache then holds the shared array of air, so it is taken again.
    private void Written(VoxelChunk chunk)
    {
        if (ReferenceEquals(_cacheChunk, chunk) && ReferenceEquals(_cacheBlocks, VoxelChunk.EmptyBlocks))
        {
            Cache(chunk);
        }
    }

    private static int LocalIndex(int x, int y, int z) =>
        (((y & (VoxelChunk.Size - 1)) << VoxelChunk.Shift) + (z & (VoxelChunk.Size - 1)) << VoxelChunk.Shift) + (x & (VoxelChunk.Size - 1));

    // The chunk of a read inside the limits: resident, or generated and installed when the grid has a generator, else null (air).
    private VoxelChunk? ReadChunk(int cx, int cy, int cz) => Find(cx, cy, cz) ?? (_generator is null ? null : Generate(new VoxelChunkCoord(cx, cy, cz)));

    private VoxelChunk? Lookup(VoxelChunkCoord coord) => ReadChunk(coord.X, coord.Y, coord.Z);

    // The chunk a write goes to: resident, or generated; a chunk that holds nothing is made only for a block that is not air (air over nothing changes nothing).
    private VoxelChunk? ChunkForWrite(int cx, int cy, int cz, BlockId block) =>
        ReadChunk(cx, cy, cz) ?? (block == BlockId.Air ? null : Create(new VoxelChunkCoord(cx, cy, cz), null));

    private VoxelChunk Generate(VoxelChunkCoord coord) => Create(coord, _generator!.Generate(coord));

    private VoxelChunk Create(VoxelChunkCoord coord, ChunkContent? content)
    {
        var chunk = new VoxelChunk(coord);
        if (content is not null)
        {
            chunk.Install(content);
        }

        _chunks[coord] = chunk;
        if (_slots is not null)
        {
            _slots[SlotOf(coord.X, coord.Y, coord.Z)] = chunk;
        }

        Cache(chunk);
        if (_streamed)
        {
            ResidencyVersion++;
            foreach (var listener in _residencyListeners.ToArray())
            {
                listener.OnChunkInstalled(coord);
            }
        }

        return chunk;
    }

    private VoxelChunk[] BuildDense()
    {
        var dense = new VoxelChunk[_limits.EndChunkX * _limits.EndChunkY * _limits.EndChunkZ];
        var i = 0;
        foreach (var coord in DenseOrder())
        {
            dense[i++] = Find(coord.X, coord.Y, coord.Z) ?? Create(coord, null);
        }

        return dense;
    }

    // Chunk coordinates in the dense order of a bounded grid: layer (Y), then row (Z), then column (X).
    private IEnumerable<VoxelChunkCoord> DenseOrder()
    {
        for (var cy = _limits.MinChunkY; cy < _limits.EndChunkY; cy++)
        {
            for (var cz = _limits.MinChunkZ; cz < _limits.EndChunkZ; cz++)
            {
                for (var cx = _limits.MinChunkX; cx < _limits.EndChunkX; cx++)
                {
                    yield return new VoxelChunkCoord(cx, cy, cz);
                }
            }
        }
    }

    private int Bounded(string member, int value) =>
        _streamed
            ? throw new NotSupportedException($"{member} is dense and valid on bounded grids only: a streamed grid holds the chunks installed into it, and the residency policy decides which.")
            : value;

    private void RequireStreamed(string member)
    {
        if (!_streamed)
        {
            throw new InvalidOperationException($"{member} needs a streamed grid: the chunks of a bounded grid never come or go.");
        }
    }
}
