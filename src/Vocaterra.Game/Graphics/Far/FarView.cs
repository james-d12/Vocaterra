using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>A rectangle of ground in blocks, the edges included on the minimum side only (B42): where a stand-in is drawn.</summary>
/// <param name="MinX">The first block column.</param>
/// <param name="MinZ">The first block row.</param>
/// <param name="MaxX">The block column after the last.</param>
/// <param name="MaxZ">The block row after the last.</param>
public readonly record struct FarWindow(int MinX, int MinZ, int MaxX, int MaxZ);

/// <summary>One far-view tile in the cache: its mesh as the worker made it until the renderer has uploaded it, and the buffer it became.</summary>
public sealed class FarTile(FarTileKey key)
{
    /// <summary>The tile.</summary>
    public FarTileKey Key { get; } = key;

    /// <summary>The mesh as made, until the renderer takes it into <see cref="Buffer"/> (then <see langword="null"/>).</summary>
    public FarTileMesh? Pending { get; set; }

    /// <summary>The vertices on the graphics card, once uploaded.</summary>
    public VertexBuffer? Buffer { get; set; }

    /// <summary>The indices on the graphics card, once uploaded.</summary>
    public IndexBuffer? Indices { get; set; }

    /// <summary>The tile's buffers in the game's own OpenGL program's form (B17c: the far view drawn through the chunks' program, <see cref="FarTerrainRenderer.DrawGl"/>), instead of <see cref="Buffer"/> and <see cref="Indices"/>; <see langword="null"/> when the tile is drawn with MonoGame's effects.</summary>
    public Gl.GlFarBuffers? GlBuffers { get; set; }

    /// <summary>B42: while the tile stands in for leaves that wait, the footprints it is to be drawn inside (the renderer draws only the runs of its indices that lie in them, B47: <see cref="Granules"/>); <see langword="null"/> for a tile drawn whole.</summary>
    public IReadOnlyList<FarWindow>? Windows { get; set; }

    /// <summary>B47: the tile's faces sorted by granule of ground, so the faces inside a set of windows are a few runs of indices; <see langword="null"/> for a tile that has none (it is drawn whole).</summary>
    public FarGranules? Granules { get; private set; }

    /// <summary>Whether the tile has vertices on the card, in either form.</summary>
    public bool HasBuffers => Buffer is not null || GlBuffers is not null;

    /// <summary>Whether the tile's buffers are on the graphics card, so it can be drawn (a tile that is only <see cref="Pending"/> cannot: its leaf is drawn from a coarser tile until it is).</summary>
    public bool OnCard { get; set; }

    /// <summary>The triangles the tile has.</summary>
    public int Triangles { get; set; }

    /// <summary>Whether its opaque faces carry the block atlas's textures (the nearest levels) and not flat colours.</summary>
    public bool Textured { get; set; }

    /// <summary>How many of the first indices are the opaque faces', and how many after them the self-lit faces' (the water's are the rest).</summary>
    public (int Opaque, int Lit, int Water) Runs { get; set; }

    /// <summary>The memory the tile holds, in bytes.</summary>
    public long Bytes { get; set; }

    /// <summary>The box round the tile, once it is built.</summary>
    public BoundingBox Box { get; set; }

    /// <summary>The vertices the tile has.</summary>
    public int Vertices { get; set; }

    /// <summary>Takes the counts and the box of a mesh the tile now stands for.</summary>
    /// <param name="mesh">The mesh.</param>
    public void Adopt(FarTileMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Box = mesh.Box;
        Vertices = mesh.VertexCount;
        Triangles = mesh.Triangles;
        Textured = mesh.Textured;
        Bytes = mesh.Bytes;
        Runs = (mesh.OpaqueIndices, mesh.LitIndices, mesh.WaterIndices);
        Granules = mesh.Granules;
    }

    /// <summary>The runs of the tile's indices to draw for a bucket (B47): the whole of it, or the faces inside <see cref="Windows"/> when the tile stands in for leaves that wait and has granules.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="into">Receives the runs (first index, index count), emptied first.</param>
    public void RunsOf(FarBucket bucket, List<(int First, int Count)> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        if (Windows is { } windows && Granules is { } granules)
        {
            granules.Runs(bucket, windows, into);
            return;
        }

        into.Clear();
        var (first, count) = bucket switch
        {
            FarBucket.Opaque => (0, Runs.Opaque),
            FarBucket.Lit => (Runs.Opaque, Runs.Lit),
            _ => (Runs.Opaque + Runs.Lit, Runs.Water),
        };
        if (count > 0)
        {
            into.Add((first, count));
        }
    }

    /// <summary>Whether a worker has made the tile.</summary>
    public bool IsBuilt => Pending is not null || HasBuffers || OnCard;
}

/// <summary>What the far view holds and what it cost, for the run's report.</summary>
/// <param name="Cached">The tiles built and kept.</param>
/// <param name="Drawn">The tiles in the last draw list.</param>
/// <param name="Building">The tiles asked for and not built yet.</param>
/// <param name="Built">The tiles built so far (a tile that was dropped and came back counts again).</param>
/// <param name="Vertices">The vertices of the cached tiles.</param>
/// <param name="Triangles">The triangles of the last draw list.</param>
/// <param name="BuildMilliseconds">The sampling and meshing time of every tile built, summed over every thread.</param>
/// <param name="LongestBuildMilliseconds">The slowest tile.</param>
/// <param name="Bytes">The memory the cached tiles hold (vertices and indices, on the card or waiting to go there).</param>
public readonly record struct FarViewStats(int Cached, int Drawn, int Building, long Built, long Vertices, long Triangles, double BuildMilliseconds, double LongestBuildMilliseconds, long Bytes);

/// <summary>
/// The far view's tiles, chosen and cached (B07, D-game-34 item 21): each frame it asks <see cref="FarTileSelector"/> which tiles the eye needs, starts the missing ones on the shared worker threads
/// (nearest first, after the voxel residency's own jobs), collects the finished ones, and answers which tiles to draw. A tile whose own mesh is not ready yet is stood in for by its nearest cached
/// ancestor (its fallback, drawn a little behind), so walking does not open holes. Tiles are kept by key and only dropped past the cap, the farthest unused first, so crossing a tile edge builds only the
/// tiles that newly become leaves. A tile is a pure function of the sections of its footprint; a player's edit shows at a distance once the edited chunk has been ingested into the store (the store tells the change, and a tile over it, built or being built, is made again). No graphics device is touched here: the renderer uploads.
/// </summary>
public sealed class FarView
{
    private const int FallbackSteps = 4;

    // B47: how far ahead of a moving eye, in blocks at most, the leaves are asked for (half the voxel zone at the most).
    private const double AheadBlocks = 160;

    // From this level up (cells of 16 blocks and more) tiles are made before the finer ones that are as near.
    private const int CoarseFirstFrom = 4;
    private const double InnerGraceMilliseconds = 700;

    // B38: a leaf of levels 0 to 2 over footprints the ring will still make real waits for them (its stand-in is drawn meanwhile) and is built once from the real data; a footprint that never comes (a failed job) is given up on after this long.
    private const int RingWaitMaxLevel = 2;
    private const double RingWaitLimitMilliseconds = 240_000;

    private readonly FarTerrainSampler _sampler;
    private readonly LodStore _store;
    private readonly JobPool _pool;
    private readonly HashSet<FarTileKey> _dirty = [];
    private readonly HashSet<FarTileKey> _rebuilding = [];
    private readonly Dictionary<FarTileKey, FarTile> _tiles = [];
    private readonly HashSet<FarTileKey> _building = [];
    private readonly HashSet<FarTileKey> _bad = [];
    private readonly HashSet<FarTileKey> _standIns = [];
    private readonly HashSet<FarTileKey> _waiting = [];
    private readonly Dictionary<FarTileKey, double> _waitingSince = [];
    private Func<int, int, bool>? _awaitsRing;
    private readonly HashSet<FarTileKey> _used = [];
    private readonly Dictionary<FarTileKey, double> _unready = [];
    private readonly List<FarTileKey> _idle = [];
    private int _trims;
    // Each result carries the version of the settings it was made under (B07f): a job that was running when the view was reshaped or cleared finishes later and is refused, not taken for the answer to a newer request of the same key.
    private readonly ConcurrentQueue<(int Version, FarTileKey Key)> _failed = new();
    private readonly ConcurrentQueue<(int Version, FarTileMesh Mesh)> _done = new();
    private int _version;
    private readonly List<FarTile> _draw = [];
    private readonly List<FarTile> _fallbacks = [];
    private readonly List<FarTile> _below = [];
    private readonly Dictionary<FarTileKey, List<FarWindow>> _windows = [];
    private readonly HashSet<FarTileKey> _whole = [];
    private readonly HashSet<FarTileKey> _unfit = [];
    private HashSet<FarTileKey> _shownLast = [];
    private HashSet<FarTileKey> _shownNow = [];
    private readonly Dictionary<FarTileKey, double> _relapsed = [];
    private bool _clipStandIns = false;
    private readonly List<FarTile> _pending = [];
    private Func<int, int, bool>? _nearReady;
    private double _reach = 1.0;
    private int _unuploaded;
    private volatile HashSet<FarTileKey> _wanted = [];
    private IReadOnlyList<FarTileKey> _leaves = [];
    private IReadOnlyList<FarTileKey> _askCoarse = [];
    private IReadOnlyList<FarTileKey> _askFine = [];
    private (double X, double Z, int Voxel) _selectedFor = (double.NaN, 0, 0);
    private double _selectedReach = 1.0;
    private (double X, double Z) _eye;
    private (double X, double Z) _warmedFor = (double.NaN, 0);
    private UInt128 _grey;
    private long _built;
    private double _buildTicks;
    private double _longest;
    private int _drawnTriangles;

    /// <summary>Creates the far view of a world.</summary>
    /// <param name="sampler">The land.</param>
    /// <param name="settings">The shape of the view.</param>
    /// <param name="pool">The workers, shared with the residency.</param>
    /// <param name="store">The store of sections that the tiles are made of (and that real chunks flow into), or <see langword="null"/> for a fresh one in memory.</param>
    public FarView(FarTerrainSampler sampler, FarViewSettings settings, JobPool pool, LodStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pool);
        _sampler = sampler;
        Settings = settings;
        _store = store ?? new LodStore();
        World = sampler.FarWorld(_store, settings.TreeLevels);
        _pool = pool;
        FarPalette.Warm();
    }

    /// <summary>The shape of the view.</summary>
    public FarViewSettings Settings { get; private set; }

    /// <summary>The voxel zone's radius in blocks at the last update.</summary>
    public int VoxelViewDistance => _selectedFor.Voxel;

    /// <summary>The far world: the store and the model that fills it; the real chunks are ingested into it.</summary>
    public LodWorld World { get; private set; }

    /// <summary>The listed tiles and their triangles by level, for a run's report: "L0 12 tiles 340k, L1 ...".</summary>
    public string LevelSummary => string.Join(", ", _draw.GroupBy(static t => t.Key.Level).OrderBy(static g => g.Key).Select(static g => $"L{g.Key} {g.Count()} tiles {g.Sum(static t => (long)t.Triangles) / 1000}k"));

    /// <summary>The tiles whose sections changed and are being made again (the old tile is drawn until the new one is there).</summary>
    public int Stale
    {
        get
        {
            // A changed tile that is not needed (its columns are drawn by the chunks) is made again when it is, not now: it never holds a settle up (B07f).
            var stale = _rebuilding.Count;
            foreach (var key in _dirty)
            {
                if (IsNeeded(key))
                {
                    stale++;
                }
            }

            return stale;
        }
    }

    /// <summary>The tiles of the last <see cref="Update"/> to draw, the fallbacks first (they are drawn a little behind), then the leaves nearest first.</summary>
    public IReadOnlyList<FarTile> Draw => _draw;

    /// <summary>How many of the first tiles of <see cref="Draw"/> are fallbacks (ancestors standing in for leaves that are not built).</summary>
    public int FallbackCount => _fallbacks.Count;

    /// <summary>The keys asked for and not built yet (leaves and B35's stand-ins), for the tests.</summary>
    public IReadOnlyCollection<FarTileKey> Requested => _building;

    /// <summary>The cached tiles by key, for the tests.</summary>
    public IReadOnlyDictionary<FarTileKey, FarTile> Cache => _tiles;

    /// <summary>
    /// Tells the far view which level-0 footprints (x, z in sections) the ring will still make real (B38): a fine leaf (levels 0 to 2) over one waits for it, drawn from its stand-in meanwhile, and is built once from the real data instead of from the
    /// model first. <see langword="null"/> (the default) builds every leaf at once, as before.
    /// </summary>
    /// <param name="awaits">Whether the ring has yet to make the footprint real.</param>
    public void SetRingWait(Func<int, int, bool>? awaits) => _awaitsRing = awaits;

    /// <summary>
    /// Asked each frame (B45, the near-first gate): true holds back every tile of levels 0 to 3 (the fine tiles, which the ring's footprints are for) and the drainage warm-up, so the workers make the near view first; their leaves are asked for when it answers false,
    /// and B35's level-4 stand-ins cover the ground meanwhile. <see langword="null"/> never holds.
    /// </summary>
    public Func<bool>? HoldFine { get; set; }

    /// <summary>Asked each frame (B45): true holds back the coarse tiles (level 4 and up) and B35's stand-ins as well, a gate that holds all far work; <see langword="null"/> never holds.</summary>
    public Func<bool>? HoldCoarse { get; set; }

    // Whether a tile of the level is held back this frame.
    private bool IsHeld(int level) => level < CoarseFirstFrom ? HoldFine?.Invoke() == true : HoldCoarse?.Invoke() == true;

    /// <summary>The priority bias of the fine tiles (and so of the ring's footprints they wait for): every coarse tile and stand-in goes before it (B35), and the ring's jobs sit on the same scale (B38).</summary>
    public long FineBias => Settings.Range / 4;

    /// <summary>The leaves that wait for the ring's data and are not asked for yet (B38).</summary>
    public int Waiting => _waiting.Count;

    /// <summary>The leaf tiles of the last <see cref="Update"/> that are not built yet, not counting those that wait for the ring (<see cref="Waiting"/>).</summary>
    public int Missing
    {
        get
        {
            var missing = 0;
            foreach (var key in _leaves)
            {
                if (!(_tiles.TryGetValue(key, out var tile) && tile.IsBuilt) && !_bad.Contains(key) && !IsInner(key) && !_waiting.Contains(key))
                {
                    missing++;
                }
            }

            return missing;
        }
    }

    /// <summary>The leaves whose mesh the renderer has not yet put on the card (a leaf of it is drawn from a coarser tile, or not at all, until it is): a settled picture waits for this to be none.</summary>
    public int Unuploaded => _unuploaded;

    /// <summary>The built tiles of the last <see cref="Update"/> that wait for the card (the leaves' and the coarser tiles that stand in for them), for the renderer to upload.</summary>
    public IReadOnlyList<FarTile> PendingTiles => _pending;

    /// <summary>
    /// Tells the far view which chunk columns the near zone has drawn: with it, the far view also covers the part of the voxel zone whose columns are not all drawn yet (a slow machine, a jump), so nothing there is sky or a floating slab, and leaves
    /// out the tiles over columns that are. Without it the tiles wholly inside the zone are never drawn.
    /// </summary>
    /// <param name="ready">Whether a chunk column (x, z in chunks) is drawn; <see langword="null"/> for the far view to keep out of the zone.</param>
    public void SetNearReady(Func<int, int, bool>? ready)
    {
        if ((ready is null) != (_nearReady is null))
        {
            _selectedFor = (double.NaN, 0, 0);
        }

        _nearReady = ready;
    }

    /// <summary>What the range is multiplied by for the choice of tiles (the renderer's: the fog is planar, the choice radial, so at the screen's side a tile needs a longer reach to end in full fog).</summary>
    public double ReachScale
    {
        get => _reach;
        set => _reach = Math.Clamp(value, 1.0, 1.6);
    }

    // Whether a leaf lies wholly inside the zone the voxel chunks cover.
    private bool IsInner(FarTileKey key) => _nearReady is not null && key.FarthestDistance(Settings, _eye.X, _eye.Z) < Settings.InnerFraction * _selectedFor.Voxel;

    // Whether a leaf has to be drawn: not when it lies wholly inside the zone whose chunk columns are all drawn.
    private bool IsNeeded(FarTileKey key)
    {
        if (!IsInner(key) || _nearReady is not { } ready)
        {
            return true;
        }

        var size = Settings.SizeOf(key.Level);
        var (c0x, c0z) = (key.MinX(Settings) >> 4, key.MinZ(Settings) >> 4);
        var (c1x, c1z) = ((key.MinX(Settings) + size - 1) >> 4, (key.MinZ(Settings) + size - 1) >> 4);
        for (var cz = c0z; cz <= c1z; cz++)
        {
            for (var cx = c0x; cx <= c1x; cx++)
            {
                if (!ready(cx, cz))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The tiles whose build failed (a torn section, a bug): they are left out until their sections change, so a bad one never holds a settle up.</summary>
    public int Failed => _bad.Count;

    /// <summary>What the view holds and cost.</summary>
    public FarViewStats Stats
    {
        get
        {
            long vertices = 0;
            long bytes = 0;
            foreach (var tile in _tiles.Values)
            {
                vertices += tile.Vertices;
                bytes += tile.Bytes;
            }

            return new FarViewStats(_tiles.Count, _draw.Count, _building.Count, _built, vertices, _drawnTriangles, TimeSpan.FromTicks((long)_buildTicks).TotalMilliseconds, _longest, bytes);
        }
    }

    /// <summary>The block kinds the far land is drawn grey for (the greyscale view).</summary>
    public UInt128 GreyKinds => _grey;

    /// <summary>Takes the set of block kinds drawn grey (<see cref="Vocaterra.Game.Graphics.World.ChunkMesher.KindBit"/>): the tiles are made again when it changes, so the far land follows the chunks' colours.</summary>
    /// <param name="greyKinds">The set.</param>
    /// <param name="dispose">Frees a tile's buffer.</param>
    public void SetGreyKinds(UInt128 greyKinds, Action<FarTile> dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        if (greyKinds == _grey)
        {
            return;
        }

        _grey = greyKinds;
        Clear(dispose);
        _selectedFor = (double.NaN, 0, 0);
    }

    /// <summary>Takes other settings (the quality changed): every tile is dropped when the shape changed and the new leaves are built.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="dispose">Frees a tile's buffer.</param>
    public void Reconfigure(FarViewSettings settings, Action<FarTile> dispose)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dispose);
        var treesChanged = settings.TreeLevels != Settings.TreeLevels;
        var reshaped = settings.Levels != Settings.Levels || treesChanged || settings.TexturedLevels != Settings.TexturedLevels; // a tile is built for its levels' textures, so a change of them makes every tile again (B07d)
        var recoloured = !ReferenceEquals(settings.Ground, Settings.Ground); // the climate colours are baked into the tiles' vertex colours (B22, B17c's setting): every tile is made again
        Settings = settings;
        if (reshaped)
        {
            World = _sampler.FarWorld(_store, settings.TreeLevels);
            if (treesChanged)
            {
                _ = _store.DropModel(); // the model's sections were made for the old tree levels (real sections are the generator's and stay)
            }
        }

        _selectedFor = (double.NaN, 0, 0);
        if (reshaped || recoloured)
        {
            DropAll(dispose);
        }
    }

    private bool? _glPath;

    /// <summary>B31: takes the path the tiles are drawn through (the game's own GL program, or MonoGame's effects). A tile's buffers are made for one path and cannot be read back from the card (MonoGame's are write-only), so when the path changes every tile is dropped and built again from its data, as for a change of levels.</summary>
    /// <param name="gl">Whether the GL program draws.</param>
    /// <param name="dispose">Frees a tile's buffer.</param>
    /// <returns>Whether the tiles were dropped (the path changed after one was set).</returns>
    public bool UsePath(bool gl, Action<FarTile> dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        var changed = _glPath is { } before && before != gl;
        _glPath = gl;
        _clipStandIns = true; // a stand-in is drawn inside its windows in either path (B42 in the game's own program, B47 in MonoGame's effects too)
        if (!changed)
        {
            return false;
        }

        _selectedFor = (double.NaN, 0, 0);
        DropAll(dispose);
        _draw.Clear();
        return true;
    }

    private void DropAll(Action<FarTile> dispose)
    {
        foreach (var tile in _tiles.Values)
        {
            dispose(tile);
        }

        _tiles.Clear();
        _building.Clear();
        _wanted = [];
        _dirty.Clear();
        _rebuilding.Clear();
        _bad.Clear();
        _version++;
        _pool.Clear(JobLane.Tiles);
        while (_done.TryDequeue(out _))
        {
        }

        while (_failed.TryDequeue(out _))
        {
        }
    }

    /// <summary>One frame's work: choose the leaves, collect the tiles the workers finished, ask for the missing ones and drop the surplus.</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="voxelViewDistance">The voxel zone's radius in blocks.</param>
    /// <param name="dispose">Frees a tile's buffer (a tile dropped from the cache).</param>
    /// <param name="inlineBudgetMilliseconds">With no worker thread, the most time to spend building tiles on the calling thread; <see langword="null"/> builds every missing tile now.</param>
    public void Update(double eyeX, double eyeZ, int voxelViewDistance, Action<FarTile> dispose, double? inlineBudgetMilliseconds = 3)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        if (!Settings.Enabled)
        {
            _draw.Clear();
            return;
        }

        // The leaves change only when the eye moves far enough to cross a tile edge or a split distance, a few blocks at the finest level: re-select when it moved at least a cell.
        var step = 8;
        _eye = (eyeX, eyeZ);
        if (double.IsNaN(_selectedFor.X) || Math.Abs(eyeX - _selectedFor.X) >= step || Math.Abs(eyeZ - _selectedFor.Z) >= step || _selectedFor.Voxel != voxelViewDistance || Math.Abs(_reach - _selectedReach) > 0.05)
        {
            var (fromX, fromZ) = (_selectedFor.X, _selectedFor.Z);
            _leaves = FarTileSelector.Select(Settings, eyeX, eyeZ, voxelViewDistance, _nearReady is not null, _reach);
            _selectedFor = (eyeX, eyeZ, voxelViewDistance);
            _selectedReach = _reach;
            SelectAhead(fromX, fromZ, eyeX, eyeZ, voxelViewDistance);
            _wanted = [.. _leaves, .. _askCoarse];
        }

        WarmDrainage(eyeX, eyeZ);
        Invalidate(dispose);
        Collect();
        Request(eyeX, eyeZ);
        if (_pool.Workers == 0)
        {
            _ = _pool.RunInline(inlineBudgetMilliseconds);
            Collect();
        }

        Assemble();
        Trim(eyeX, eyeZ, dispose);
    }

    // B47: the leaves a little ahead of the eye along the way it is going (the eye flying at 190 blocks a second reaches the ground a tile build takes, 50 to 300 ms, in 10 to 60 blocks): their coarse tiles are asked for now and their stand-ins (RequestStandIns) too,
    // so what comes into view is covered the frame it does and the sky is not the first thing there. Only when the eye has moved since the last selection; a stationary view asks for nothing more.
    private void SelectAhead(double fromX, double fromZ, double eyeX, double eyeZ, int voxelViewDistance)
    {
        var (dx, dz) = (eyeX - fromX, eyeZ - fromZ);
        var length = Math.Sqrt((dx * dx) + (dz * dz));
        if (double.IsNaN(fromX) || length < 1 || length > AheadBlocks)
        {
            _askCoarse = [];
            _askFine = [];
            return; // no way is known (the first selection) or the eye jumped (a teleport: the ground ahead of a jump is not where the eye goes)
        }

        var ahead = Math.Min(AheadBlocks, voxelViewDistance / 2.0);
        var own = new HashSet<FarTileKey>(_leaves);
        List<FarTileKey> coarse = [];
        List<FarTileKey> fine = [];
        foreach (var key in FarTileSelector.Select(Settings, eyeX + (dx / length * ahead), eyeZ + (dz / length * ahead), voxelViewDistance, _nearReady is not null, _reach))
        {
            if (own.Contains(key))
            {
                continue;
            }

            (key.Level >= CoarseFirstFrom ? coarse : fine).Add(key);
        }

        _askCoarse = coarse;
        _askFine = fine;
    }

    /// <summary>Chooses what to draw again from the tiles now on the card (B47): the renderer calls it after a frame's uploads, so a tile the card just took covers its ground in this frame, not the next (a leaf with nothing over it for a frame is a flash of sky).</summary>
    public void Reassemble()
    {
        if (Settings.Enabled && !double.IsNaN(_selectedFor.X))
        {
            Assemble();
        }
    }

    // B40: the drainage network's landmasses within the far range are read on the workers ahead of the tiles (a first read is 0.12 to 0.53 s, and a tile that needs an unread landmass would read it itself): at the first update and when the eye
    // has moved a quarter of the range from where it was last done (a teleport; a walk is covered by the overlap). One job a row, nearest the eye first, behind every tile and chunk job; never with no worker (a run on one thread keeps its order).
    private void WarmDrainage(double eyeX, double eyeZ)
    {
        var range = Settings.Range;
        if (_pool.Workers == 0 || range <= 0 || HoldFine?.Invoke() == true || (!double.IsNaN(_warmedFor.X) && Math.Abs(eyeX - _warmedFor.X) < range / 4 && Math.Abs(eyeZ - _warmedFor.Z) < range / 4))
        {
            return;
        }

        var step = _sampler.DrainageRowStep;
        _warmedFor = (eyeX, eyeZ);
        if (step <= 0)
        {
            return;
        }

        var (x0, x1) = ((int)Math.Floor(eyeX) - range, (int)Math.Floor(eyeX) + range);
        var sampler = _sampler;
        var stamp = (eyeX, eyeZ);
        var first = (int)Math.Floor((eyeZ - range) / step) * step;
        for (var z = first; z <= eyeZ + range; z += step)
        {
            var row = z;
            _pool.Submit(JobLane.Ring, (long)Math.Abs(row - eyeZ), () => _warmedFor != stamp, () => sampler.WarmDrainageRow(row, x0, x1));
        }
    }

    /// <summary>Runs until every leaf is built (a scripted run's settle step, and the tests).</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="voxelViewDistance">The voxel zone's radius.</param>
    /// <param name="dispose">Frees a tile's buffer.</param>
    /// <param name="timeoutMilliseconds">The longest to wait.</param>
    /// <returns>Whether every leaf was built in time.</returns>
    public bool Settle(double eyeX, double eyeZ, int voxelViewDistance, Action<FarTile> dispose, double timeoutMilliseconds = 180_000)
    {
        var started = MonotonicClock.Milliseconds();
        while (true)
        {
            Update(eyeX, eyeZ, voxelViewDistance, dispose, null);
            if (Missing == 0)
            {
                return true;
            }

            if (MonotonicClock.Milliseconds() - started > timeoutMilliseconds)
            {
                return false;
            }

            if (_pool.Workers > 0)
            {
                Thread.Sleep(1);
            }
        }
    }

    // The tiles whose sections the store changed (real chunks came in): a tile that is wanted is made again, one that is not is dropped.
    private void Invalidate(Action<FarTile> dispose)
    {
        while (_store.Changed.TryDequeue(out var section))
        {
            var key = new FarTileKey(section.Level, section.X, section.Z);
            _ = _bad.Remove(key);
            if (_building.Contains(key))
            {
                _ = _dirty.Add(key); // being made from the sections as they were: made again when it is done
                continue;
            }

            if (!_tiles.TryGetValue(key, out var tile) || !tile.IsBuilt)
            {
                continue;
            }

            if (_wanted.Contains(key) && IsNeeded(key))
            {
                _ = _dirty.Add(key);
            }
            else
            {
                dispose(tile); // not drawn now: made again from the new sections when it is wanted and needed
                _ = _tiles.Remove(key);
            }
        }
    }

    // Takes the tiles the workers finished into the cache.
    private void Collect()
    {
        while (_failed.TryDequeue(out var late))
        {
            if (late.Version != _version)
            {
                continue; // a job of settings that are gone
            }

            var failed = late.Key;
            _ = _building.Remove(failed);
            _ = _rebuilding.Remove(failed);
            _ = _bad.Add(failed);
        }

        while (_done.TryDequeue(out var finished))
        {
            if (finished.Version != _version)
            {
                finished.Mesh.Release();
                continue; // made under settings that are gone: the key may be asked for again under the new ones and this is not its answer
            }

            var mesh = finished.Mesh;
            _ = _building.Remove(mesh.Key);
            _ = _rebuilding.Remove(mesh.Key);
            if (!_wanted.Contains(mesh.Key) && !_tiles.ContainsKey(mesh.Key))
            {
                mesh.Release();
                continue; // made for a view the eye has left: not kept
            }

            if (!_tiles.TryGetValue(mesh.Key, out var tile))
            {
                _tiles[mesh.Key] = tile = new FarTile(mesh.Key);
            }

            tile.Pending?.Release(); // a newer mesh of the tile replaces one the card never took
            tile.Pending = mesh;
            if (!tile.HasBuffers)
            {
                tile.Adopt(mesh); // a tile made again keeps its old buffers and their counts until the renderer swaps them
            }

            if (_wanted.Contains(mesh.Key) && !_leaves.Contains(mesh.Key))
            {
                // A stand-in (B35) that is built is an ordinary cached tile: it leaves the wanted set, so it is drawn while a leaf under it waits and dropped like any tile once none does.
                var rest = new HashSet<FarTileKey>(_wanted);
                _ = rest.Remove(mesh.Key);
                _wanted = rest;
            }

            NoteFineBuilt(mesh.Key);
            _built++;
            _buildTicks += TimeSpan.FromMilliseconds(mesh.BuildMilliseconds).Ticks;
            _longest = Math.Max(_longest, mesh.BuildMilliseconds);
        }
    }

    // Asks for the leaves that are neither built nor being built.
    private void Request(double eyeX, double eyeZ)
    {
        var settings = Settings;
        var grey = _grey;
        var source = World;
        var version = _version;
        _waiting.Clear();
        foreach (var key in _askCoarse.Count == 0 ? _leaves : _leaves.Concat(_askCoarse))
        {
            if (!IsNeeded(key))
            {
                _ = _unready.Remove(key);
                continue;
            }

            if (IsInner(key) && !(_tiles.TryGetValue(key, out var have) && have.IsBuilt))
            {
                // Inside the zone, the far land is only a filler until the chunks are drawn: it is made only for columns that have stayed undrawn for a moment (a jump, a slow machine), so a normal arrival makes none.
                var now = MonotonicClock.Milliseconds();
                if (!_unready.TryGetValue(key, out var since))
                {
                    _unready[key] = now;
                    continue;
                }

                if (now - since < InnerGraceMilliseconds)
                {
                    continue;
                }
            }

            if (_bad.Contains(key) || (_tiles.TryGetValue(key, out var tile) && tile.IsBuilt && !_dirty.Contains(key)) || _building.Contains(key))
            {
                continue;
            }

            if (!(tile?.IsBuilt ?? false) && !IsInner(key) && WaitsForRing(key))
            {
                _ = _waiting.Add(key); // the stand-in covers it (RequestStandIns); asked for when the ring's data is in
                continue;
            }

            if (IsHeld(key.Level))
            {
                continue; // B45: the near view is not made yet; asked for in the frame the gate opens
            }

            _ = _building.Add(key);

            if (_dirty.Remove(key))
            {
                _ = _rebuilding.Add(key);
            }

            // The coarse levels are cheap and cover the horizon: they are made early (their distance counts a sixteenth), so a view that has just begun shows the far land at once and the detail comes in from the near out.
            var distance = (long)key.NearestDistance(settings, eyeX, eyeZ) / (key.Level >= CoarseFirstFrom ? 16 : 1);
            if (key.Level < CoarseFirstFrom)
            {
                distance += FineBias; // B35: every coarse tile and stand-in (a sixteenth of a distance of at most 1.6 ranges) goes before every fine one: the whole view is covered first, the detail comes in from the near out
            }
            var captured = key;
            _pool.Submit(
                JobLane.Tiles,
                distance,
                () => !_wanted.Contains(captured),
                () =>
                {
                    try
                    {
                        _done.Enqueue((version, FarTileMesher.Build(source, settings, captured, grey, cut: true)));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _failed.Enqueue((version, captured)); // a torn section or a bug in one tile: the key is released, the others go on
                    }
                });
        }

        if (_waitingSince.Count > 0)
        {
            _idle.Clear();
            _idle.AddRange(_waitingSince.Keys.Where(k => !_waiting.Contains(k)));
            foreach (var done in _idle)
            {
                _ = _waitingSince.Remove(done);
            }

            _idle.Clear();
        }

        RequestStandIns(eyeX, eyeZ);

        if (_unready.Count > 0 && _unready.Count > _leaves.Count)
        {
            _unready.Clear();
        }

        // A job skipped as stale leaves its key in _building: forget the keys that are no longer wanted so they can be asked for again.
        if (_building.Count > 0)
        {
            _ = _building.RemoveWhere(key => !_wanted.Contains(key) && !_tiles.ContainsKey(key));
        }
    }

    // B38: whether a leaf over footprints the ring will still make real is held back (a level 0 to 2 leaf, for at most RingWaitLimitMilliseconds).
    private bool WaitsForRing(FarTileKey key)
    {
        if (_awaitsRing is not { } awaits || key.Level > RingWaitMaxLevel)
        {
            return false;
        }

        var (x0, z0) = (key.MinX(Settings), key.MinZ(Settings));
        var size = Settings.SizeOf(key.Level);
        var (fx0, fz0) = (x0 >> LodSection.Shift, z0 >> LodSection.Shift);
        var (fx1, fz1) = ((x0 + size - 1) >> LodSection.Shift, (z0 + size - 1) >> LodSection.Shift);
        var waits = false;
        for (var fz = fz0; fz <= fz1 && !waits; fz++)
        {
            for (var fx = fx0; fx <= fx1 && !waits; fx++)
            {
                waits = awaits(fx, fz);
            }
        }

        if (!waits)
        {
            return false;
        }

        var now = MonotonicClock.Milliseconds();
        if (!_waitingSince.TryGetValue(key, out var since))
        {
            _waitingSince[key] = now;
            return true;
        }

        return now - since < RingWaitLimitMilliseconds;
    }

    // B35: a fine leaf (below CoarseFirstFrom) whose ancestors within FallbackSteps are neither built nor asked for gets its level-4 ancestor (the coarsest level of the fine range; the top level when the view has fewer) asked
    // for at the coarse priority, so on a fresh world the ring between the near zone and the horizon is covered within seconds instead of when the fine tiles land. Rendering only: the tile is an ordinary tile of the
    // store's sections. It joins the wanted set while a leaf needs it and is dropped like any other tile when none does.
    private void RequestStandIns(double eyeX, double eyeZ)
    {
        if (HoldCoarse?.Invoke() == true)
        {
            return; // B45: a gate that holds everything holds the stand-ins too (the default lets them through)
        }

        var settings = Settings;
        var top = settings.TopLevel;
        var target = Math.Min(CoarseFirstFrom, top);
        List<FarTileKey>? asked = null;
        List<FarTileKey>? keep = null;
        foreach (var key in _askFine.Count == 0 ? _leaves : _leaves.Concat(_askFine))
        {
            if (key.Level >= target || !IsNeeded(key) || (_tiles.TryGetValue(key, out var own) && own.IsBuilt) || CoverFromBelow(key, null))
            {
                continue;
            }

            var ancestor = key;
            var covered = false;
            FarTileKey? stand = null;
            for (var step = 0; step < FallbackSteps && ancestor.Level < top; step++)
            {
                ancestor = ancestor.GetParent();
                if (_building.Contains(ancestor) && !_wanted.Contains(ancestor))
                {
                    (keep ??= []).Add(ancestor); // asked for earlier and left out of the wanted set by a re-selection: its job must not be dropped as stale
                }

                if (_building.Contains(ancestor) || _bad.Contains(ancestor) || (_tiles.TryGetValue(ancestor, out var have) && have.IsBuilt))
                {
                    covered = true;
                    break;
                }

                if (ancestor.Level == target)
                {
                    stand = ancestor;
                }
            }

            if (!covered && stand is { } wanted && _building.Add(wanted))
            {
                (asked ??= []).Add(wanted);
            }
        }

        if (asked is null && keep is null)
        {
            return;
        }

        var grey = _grey;
        var source = World;
        var version = _version;
        var set = new HashSet<FarTileKey>(_wanted);
        set.UnionWith(asked ?? []);
        set.UnionWith(keep ?? []);

        _wanted = set; // the workers read the set: replaced whole, never changed in place
        foreach (var key in asked ?? [])
        {
            var captured = key;
            _pool.Submit(
                JobLane.Tiles,
                (long)key.NearestDistance(settings, eyeX, eyeZ) / 16,
                () => !_wanted.Contains(captured),
                () =>
                {
                    try
                    {
                        _done.Enqueue((version, FarTileMesher.Build(source, settings, captured, grey, cut: true)));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _failed.Enqueue((version, captured));
                    }
                });
        }
    }

    // The draw list (B42). A leaf is drawn from its own tile once that is on the card. Until then, in this order: the tiles under it that are on the card and cover it whole (a leaf that merged from finer ones keeps showing them: no pop to
    // a coarser level and back); else the nearest coarser tile that is on the card, a stand-in. A stand-in used to hide every leaf under it that was already on the card until all of them were (so no face of a coarser tile was ever over a
    // finer one's), and so it popped over the finished fine land again whenever a new leaf appeared under it: the flicker B42 found. Now a stand-in is drawn only inside the footprints of
    // the leaves that wait under it (FarTile.Windows; B47: the renderer draws the runs of its indices that lie in them, in either path) and the leaves that are on the card are drawn as they are; where it cannot (a window finer than a granule of the stand-in's ground), the stand-in is whole and the leaves under it wait for it, as before.
    // Either way no ground is drawn twice. A leaf that is not needed (it lies inside the voxel zone and its columns are drawn) is never given a stand-in, and no stand-in draws its ground there: the far pass is drawn before the chunks and its
    // depth is cleared for them, so a coarse cell that stands higher than the blocks under it would show against the sky above them (the owner's video of 2026-11-19: flat grey boxes over a near mountain's ridge for two frames). A leaf whose
    // columns were drawn and are not for a moment (a chunk the view newly wants in a column, the others still drawn) is held the same way for InnerGraceMilliseconds (ShownByChunks).
    // A built tile waiting for the card is in PendingTiles, coarse levels first.
    private void Assemble()
    {
        foreach (var old in _fallbacks)
        {
            old.Windows = null;
        }

        _draw.Clear();
        _fallbacks.Clear();
        _standIns.Clear();
        _pending.Clear();
        _below.Clear();
        _unuploaded = 0;
        long triangles = 0;
        var top = Settings.TopLevel;
        NoteRelapses(MonotonicClock.Milliseconds());
        foreach (var key in _leaves)
        {
            if (ShownByChunks(key))
            {
                continue;
            }

            _tiles.TryGetValue(key, out var tile);
            if (tile?.Pending is not null)
            {
                _unuploaded++;
                _pending.Add(tile);
            }

            if (tile is { OnCard: true })
            {
                continue;
            }

            if (CoverFromBelow(key, null))
            {
                continue;
            }

            var ancestor = key;
            for (var step = 0; step < FallbackSteps && ancestor.Level < top; step++)
            {
                ancestor = ancestor.GetParent();
                if (_tiles.TryGetValue(ancestor, out var stand))
                {
                    if (stand.OnCard)
                    {
                        _ = _standIns.Add(ancestor);
                        break;
                    }

                    if (stand.Pending is not null && !_pending.Contains(stand))
                    {
                        _pending.Add(stand);
                    }
                }
            }
        }

        if (_standIns.Count > 1 && !_clipStandIns)
        {
            // A coarser stand-in (B35's level-4 tile, still cached from an earlier need) covers a finer one that another leaf picked: only the coarsest is drawn, so no two stand-ins overlap. (Clipped stand-ins may nest: each waiting leaf is in the window of the finest one over it.)
            _idle.Clear();
            foreach (var key in _standIns)
            {
                if (HasStandInAbove(key, top))
                {
                    _idle.Add(key);
                }
            }

            foreach (var key in _idle)
            {
                _ = _standIns.Remove(key);
            }

            _idle.Clear();
        }

        // The windows of each stand-in: the footprints of the leaves under it that wait (not the ones that are on the card, not those the tiles below cover). None under the chunks: a stand-in drawn there used to keep its ground as a backdrop
        // (the chunks draw over it with a depth buffer of their own), but its coarse cells stand above the blocks in places and showed against the sky, and the backdrop leaf could make the stand-in unfit, so whole (the LOD review's finding 9).
        _windows.Clear();
        _unfit.Clear();
        foreach (var key in _standIns)
        {
            _windows[key] = [];
        }

        var uncovered = 0;
        var needed = 0;
        foreach (var key in _leaves)
        {
            if (ShownByChunks(key))
            {
                continue;
            }

            needed++;
            _tiles.TryGetValue(key, out var tile);
            if (tile is { OnCard: true })
            {
                continue;
            }

            if (CoverFromBelow(key, _below))
            {
                continue;
            }

            if (StandInOf(key, top) is { } stand)
            {
                var size = Settings.SizeOf(key.Level);
                _windows[stand].Add(new FarWindow(key.MinX(Settings), key.MinZ(Settings), key.MinX(Settings) + size, key.MinZ(Settings) + size));
                NoteFit(stand, key);
                continue;
            }

            uncovered++;
        }

        // A stand-in whose windows are whole granules of its own ground is drawn inside them (B47); any other (a leaf finer than four levels under it: a coarse tile picked by another leaf, while the nearer stand-ins are not on the card yet) is whole and hides the leaves on the card under it,
        // and the stand-ins nested in it are not drawn (its ground is all its own).
        _whole.Clear();
        foreach (var key in _standIns)
        {
            var windows = _clipStandIns ? Merge(_windows[key]) : [];
            _windows[key] = windows;
            if (!_clipStandIns || _unfit.Contains(key) || _tiles[key].Granules is null)
            {
                _ = _whole.Add(key);
            }
        }

        if (_clipStandIns && _whole.Count > 0)
        {
            _idle.Clear();
            foreach (var key in _standIns)
            {
                if (HasWholeAbove(key, top))
                {
                    _idle.Add(key);
                }
            }

            foreach (var key in _idle)
            {
                _ = _standIns.Remove(key);
                _ = _whole.Remove(key);
            }

            _idle.Clear();
        }

        foreach (var key in _standIns)
        {
            var stand = _tiles[key];
            if (!_whole.Contains(key) && _windows[key].Count == 0)
            {
                continue; // nothing waits under it (every leaf under it is on the card or covered from below): it draws nothing
            }

            stand.Windows = _whole.Contains(key) ? OutsideTheChunks(stand, top) : _windows[key];
            _fallbacks.Add(stand);
            triangles += stand.Triangles;
        }

        foreach (var key in _leaves)
        {
            if (ShownByChunks(key) || !_tiles.TryGetValue(key, out var tile) || !tile.OnCard || CoveredByWholeStandIn(key, top))
            {
                continue;
            }

            _draw.Add(tile);
            triangles += tile.Triangles;
        }

        foreach (var tile in _below)
        {
            if (CoveredByWholeStandIn(tile.Key, top))
            {
                continue; // a whole stand-in over it (too many windows) is all the ground's drawing
            }

            _draw.Add(tile);
            triangles += tile.Triangles;
        }

        _draw.InsertRange(0, _fallbacks);
        Uncovered = uncovered;
        NoteCoverage(needed, uncovered);
        _drawnTriangles = (int)Math.Min(triangles, int.MaxValue);
    }

    // Notes a stand-in whose window is finer than a granule of its ground (B47): it cannot be drawn inside its windows.
    private void NoteFit(FarTileKey stand, FarTileKey leaf)
    {
        if (!FarGranules.Fits(Settings, stand.Level, leaf.Level))
        {
            _ = _unfit.Add(stand);
        }
    }

    // Notes which leaves the chunks drew in this assembly (not needed) and since when a leaf that they drew in the last one has been needed again. Inside the zone a column stops being drawn whole for a frame or two when the view newly wants a
    // chunk in it (a layer the surface band adds as the eye climbs or goes down) while its other chunks are still drawn: no ground is missing there, and a stand-in would only put coarse cells over the blocks. Request holds such a leaf's own
    // tile back for InnerGraceMilliseconds; the draw list holds back its stand-in as long.
    private void NoteRelapses(double now)
    {
        _shownNow.Clear();
        foreach (var key in _leaves)
        {
            if (!IsNeeded(key))
            {
                _ = _shownNow.Add(key);
                _ = _relapsed.Remove(key);
            }
            else if (_shownLast.Contains(key) && IsInner(key))
            {
                _ = _relapsed.TryAdd(key, now);
            }
        }

        (_shownLast, _shownNow) = (_shownNow, _shownLast);
        if (_relapsed.Count > 0)
        {
            _idle.Clear();
            foreach (var (key, since) in _relapsed)
            {
                if (now - since >= InnerGraceMilliseconds || !IsInner(key))
                {
                    _idle.Add(key);
                }
            }

            foreach (var key in _idle)
            {
                _ = _relapsed.Remove(key);
            }

            _idle.Clear();
        }
    }

    // Whether the chunks show a leaf's ground: its columns are drawn (IsNeeded is false), or they were in the last moment and a column is only waiting for a chunk the view newly wants (NoteRelapses). The far view draws nothing there.
    private bool ShownByChunks(FarTileKey key) => !IsNeeded(key) || _relapsed.ContainsKey(key);

    // The windows a stand-in that is drawn whole is drawn inside: every granule of its ground but those that the chunks show entirely (a coarse cell standing above the blocks would show against the sky over them, see Assemble); null when
    // there are none of those (or the tile has no granules), so it is drawn whole. Its nested stand-ins are gone (HasWholeAbove), so every leaf under it that the chunks show is one of its own.
    private List<FarWindow>? OutsideTheChunks(FarTile stand, int top)
    {
        if (stand.Granules is not { } granules)
        {
            return null;
        }

        var (side, blocks) = (granules.Side, granules.Blocks);
        long[]? shown = null;
        foreach (var key in _leaves)
        {
            if (!ShownByChunks(key) || StandInOf(key, top) != stand.Key)
            {
                continue;
            }

            // The leaf's footprint, in blocks from the stand-in's corner: it lies wholly inside the stand-in's, and leaves and granules are squares of powers of two, so the leaf covers whole granules or lies inside one.
            var size = Settings.SizeOf(key.Level);
            var (x0, z0) = (key.MinX(Settings) - granules.OriginX, key.MinZ(Settings) - granules.OriginZ);
            shown ??= new long[side * side];
            for (var gz = Math.Max(0, z0 / blocks); gz < Math.Min(side, (z0 + size + blocks - 1) / blocks); gz++)
            {
                for (var gx = Math.Max(0, x0 / blocks); gx < Math.Min(side, (x0 + size + blocks - 1) / blocks); gx++)
                {
                    var w = Math.Min(x0 + size, (gx + 1) * blocks) - Math.Max(x0, gx * blocks);
                    var h = Math.Min(z0 + size, (gz + 1) * blocks) - Math.Max(z0, gz * blocks);
                    shown[(gz * side) + gx] += (long)w * h;
                }
            }
        }

        var full = (long)blocks * blocks;
        if (shown is null || !shown.Any(a => a >= full))
        {
            return null;
        }

        // A run of granules along each row that the chunks do not show entirely; Merge joins the rows.
        var windows = new List<FarWindow>();
        for (var gz = 0; gz < side; gz++)
        {
            var gx = 0;
            while (gx < side)
            {
                if (shown[(gz * side) + gx] >= full)
                {
                    gx++;
                    continue;
                }

                var start = gx;
                while (gx < side && shown[(gz * side) + gx] < full)
                {
                    gx++;
                }

                windows.Add(new FarWindow(granules.OriginX + (start * blocks), granules.OriginZ + (gz * blocks), granules.OriginX + (gx * blocks), granules.OriginZ + ((gz + 1) * blocks)));
            }
        }

        return Merge(windows);
    }

    // Joins windows that meet along a whole side (runs of leaves in a row, then rows of runs), so a long edge of waiting leaves is a few rectangles.
    private static List<FarWindow> Merge(List<FarWindow> windows)
    {
        for (var round = 0; round < 2 && windows.Count > 1; round++)
        {
            windows.Sort(round == 0 ? static (a, b) => a.MinZ != b.MinZ ? a.MinZ.CompareTo(b.MinZ) : a.MinX.CompareTo(b.MinX) : static (a, b) => a.MinX != b.MinX ? a.MinX.CompareTo(b.MinX) : a.MinZ.CompareTo(b.MinZ));
            var merged = new List<FarWindow>(windows.Count);
            foreach (var w in windows)
            {
                if (merged.Count > 0 && merged[^1] is var last && (round == 0 ? last.MinZ == w.MinZ && last.MaxZ == w.MaxZ && last.MaxX == w.MinX : last.MinX == w.MinX && last.MaxX == w.MaxX && last.MaxZ == w.MinZ))
                {
                    merged[^1] = round == 0 ? last with { MaxX = w.MaxX } : last with { MaxZ = w.MaxZ };
                }
                else
                {
                    merged.Add(w);
                }
            }

            windows = merged;
        }

        return windows;
    }

    // The stand-in (in this frame's set) that covers a leaf, or null.
    private FarTileKey? StandInOf(FarTileKey key, int top)
    {
        if (_standIns.Count == 0)
        {
            return null;
        }

        var ancestor = key;
        while (ancestor.Level < top)
        {
            ancestor = ancestor.GetParent();
            if (_standIns.Contains(ancestor))
            {
                return ancestor; // any level up: a coarser stand-in may have taken the place of the one this leaf picked
            }
        }

        return null;
    }

    private bool CoveredByWholeStandIn(FarTileKey key, int top) => _whole.Count > 0 && StandInOf(key, top) is { } stand && _whole.Contains(stand);

    // B42: whether the tiles under a leaf that are on the card cover it whole (down to FallbackSteps levels), and they are put in the list when one is given. A leaf that merged from finer ones (the eye moved away) keeps showing them until its own tile lands.
    private bool CoverFromBelow(FarTileKey key, List<FarTile>? into)
    {
        if (key.Level == 0)
        {
            return false;
        }

        var found = into is null ? null : new List<FarTile>();
        if (!Below(key, FallbackSteps, found))
        {
            return false;
        }

        if (into is not null)
        {
            into.AddRange(found!);
        }

        return true;
    }

    private bool Below(FarTileKey key, int depth, List<FarTile>? into)
    {
        if (depth <= 0 || key.Level == 0)
        {
            return false;
        }

        var start = into?.Count ?? 0;
        foreach (var child in key.Children())
        {
            if (_tiles.TryGetValue(child, out var tile) && tile.OnCard && !_dirty.Contains(child))
            {
                into?.Add(tile);
            }
            else if (!Below(child, depth - 1, into))
            {
                into?.RemoveRange(start, into.Count - start);
                return false;
            }
        }

        return true;
    }

    /// <summary>How many needed leaves of the last <see cref="Update"/> have neither their own tile on the card nor a stand-in drawn: the sky-coloured gaps (B35).</summary>
    public int Uncovered { get; private set; }

    /// <summary>The wall-clock milliseconds from the first update with leaves until no needed leaf was uncovered, or <see langword="null"/> while some are (B35, for the run's report).</summary>
    public double? CoveredAfterMilliseconds { get; private set; }

    /// <summary>The wall-clock milliseconds from the first update with leaves until every needed leaf was on the card, or <see langword="null"/> while some are not (B35, for the run's report).</summary>
    public double? FilledAfterMilliseconds { get; private set; }

    private double? _firstLeaves;
    private readonly List<(double? Covered, double? Filled)> _coverageHistory = [];

    /// <summary>The coverage and fill times of the episodes before the one in progress (B45: the start, then each jump begins a new one with <see cref="BeginCoverage"/>), in milliseconds, <see langword="null"/> for one that never came.</summary>
    public IReadOnlyList<(double? Covered, double? Filled)> CoverageHistory => _coverageHistory;

    /// <summary>The eye started an episode (the start is the first, a jump the next): the times above count from the next update with leaves, and those of the episode that ends are kept in <see cref="CoverageHistory"/> (B45, for the run's report).</summary>
    public void BeginCoverage()
    {
        if (_firstLeaves is not null)
        {
            _coverageHistory.Add((CoveredAfterMilliseconds, FilledAfterMilliseconds));
        }

        _firstLeaves = null;
        CoveredAfterMilliseconds = null;
        FilledAfterMilliseconds = null;
    }
    private readonly Dictionary<FarTileKey, (double First, double Last, int Distance)> _fineBuilds = [];

    // B38: when each fine leaf (level 0 to 2) was first built and last built, and how far from the eye it was when first built, for the run's report of how long a stand-in stays on screen.
    private void NoteFineBuilt(FarTileKey key)
    {
        if (key.Level > 2 || _firstLeaves is not { } first)
        {
            return;
        }

        var now = MonotonicClock.Milliseconds() - first;
        _fineBuilds[key] = _fineBuilds.TryGetValue(key, out var seen) ? (seen.First, now, seen.Distance) : (now, now, (int)key.NearestDistance(Settings, _eye.X, _eye.Z));
    }

    /// <summary>Per distance band (blocks from the eye when the leaf was first built) the fine leaves (levels 0 to 2) built: how many, and the median and 90th percentile of the milliseconds from the first leaves to the first build and to the last build (B38, for the run's report).</summary>
    public IReadOnlyList<(int From, int To, int Count, double FirstMedian, double FirstP90, double LastMedian, double LastP90)> FineBuildBands()
    {
        var edges = new[] { 0, 400, 500, 600, 700, 800, 1000, 1500, int.MaxValue };
        var rows = new List<(int, int, int, double, double, double, double)>();
        for (var i = 0; i + 1 < edges.Length; i++)
        {
            var inBand = _fineBuilds.Values.Where(v => v.Distance >= edges[i] && v.Distance < edges[i + 1]).ToList();
            if (inBand.Count == 0)
            {
                continue;
            }

            static double At(List<double> sorted, int percent) => sorted[Math.Min(sorted.Count - 1, sorted.Count * percent / 100)];
            var first = inBand.Select(static v => v.First).Order().ToList();
            var last = inBand.Select(static v => v.Last).Order().ToList();
            rows.Add((edges[i], edges[i + 1], inBand.Count, At(first, 50), At(first, 90), At(last, 50), At(last, 90)));
        }

        return rows;
    }

    private void NoteCoverage(int needed, int uncovered)
    {
        if (needed == 0)
        {
            return;
        }

        var now = MonotonicClock.Milliseconds();
        _firstLeaves ??= now;
        if (CoveredAfterMilliseconds is null && uncovered == 0)
        {
            CoveredAfterMilliseconds = now - _firstLeaves.Value;
        }

        if (FilledAfterMilliseconds is null && Missing == 0 && _waiting.Count == 0 && _unuploaded == 0 && _building.Count == 0)
        {
            FilledAfterMilliseconds = now - _firstLeaves.Value;
        }
    }

    private bool HasWholeAbove(FarTileKey key, int top)
    {
        var ancestor = key;
        while (ancestor.Level < top)
        {
            ancestor = ancestor.GetParent();
            if (_whole.Contains(ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasStandInAbove(FarTileKey key, int top)
    {
        var ancestor = key;
        while (ancestor.Level < top)
        {
            ancestor = ancestor.GetParent();
            if (_standIns.Contains(ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // Drops the farthest tiles that are not wanted or standing in, past the cap; and, now and then, the meshes still waiting in memory of tiles that are no longer drawn (the eye moved on before the card took them).
    private void Trim(double eyeX, double eyeZ, Action<FarTile> dispose)
    {
        var settings = Settings;
        _trims++;
        if (_trims % 16 == 0)
        {
            DropUnwantedPending(dispose);
        }

        if (_tiles.Count <= settings.MaxTiles)
        {
            return;
        }

        _used.Clear();
        foreach (var tile in _draw)
        {
            _ = _used.Add(tile.Key);
        }

        _used.UnionWith(_wanted);
        KeepAhead(_used);
        foreach (var tile in _pending)
        {
            _ = _used.Add(tile.Key);
        }

        var victims = _tiles.Keys.Where(key => !_used.Contains(key)).OrderByDescending(key => key.NearestDistance(settings, eyeX, eyeZ)).Take(_tiles.Count - settings.MaxTiles).ToList();
        foreach (var key in victims)
        {
            dispose(_tiles[key]);
            _ = _tiles.Remove(key);
            _ = _dirty.Remove(key);
        }
    }

    // B47: the tiles asked for ahead of the eye (coarse leaves, and the ancestors a fine leaf ahead may be stood in for by) are kept like the wanted ones: built, they are not drawn yet, and must not be taken for tiles nobody uses.
    private void KeepAhead(HashSet<FarTileKey> used)
    {
        foreach (var key in _askCoarse)
        {
            _ = used.Add(key);
        }

        foreach (var leaf in _askFine)
        {
            var ancestor = leaf;
            for (var step = 0; step < FallbackSteps && ancestor.Level < Settings.TopLevel; step++)
            {
                ancestor = ancestor.GetParent();
                _ = used.Add(ancestor);
            }
        }
    }

    private void DropUnwantedPending(Action<FarTile> dispose)
    {
        _used.Clear();
        foreach (var tile in _draw)
        {
            _ = _used.Add(tile.Key);
        }

        _used.UnionWith(_wanted);
        KeepAhead(_used);
        foreach (var tile in _pending)
        {
            _ = _used.Add(tile.Key);
        }

        _idle.Clear();
        foreach (var (key, tile) in _tiles)
        {
            if (tile.Pending is not null && !_used.Contains(key))
            {
                _idle.Add(key);
            }
        }

        foreach (var key in _idle)
        {
            // The newer mesh is dropped, and with it the tile: a tile that kept its old buffer would show the old sections as if they were the current ones when it is wanted again, with nothing to say it is stale (B07f). It is made again when it is wanted.
            dispose(_tiles[key]);
            _ = _tiles.Remove(key);
            _ = _dirty.Remove(key);
        }
    }

    /// <summary>Frees every tile.</summary>
    /// <param name="dispose">Frees a tile's buffer.</param>
    public void Clear(Action<FarTile> dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        foreach (var tile in _tiles.Values)
        {
            dispose(tile);
        }

        _tiles.Clear();
        _draw.Clear();
        _building.Clear();
        _dirty.Clear();
        _rebuilding.Clear();
        _bad.Clear();
        _wanted = [];
        _version++;
        _pool.Clear(JobLane.Tiles);
    }
}
