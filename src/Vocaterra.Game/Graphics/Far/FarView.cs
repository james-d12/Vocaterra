using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Graphics.Far;

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

    // From this level up (cells of 16 blocks and more) tiles are made before the finer ones that are as near.
    private const int CoarseFirstFrom = 4;
    private const double InnerGraceMilliseconds = 700;

    private readonly FarTerrainSampler _sampler;
    private readonly LodStore _store;
    private readonly JobPool _pool;
    private readonly HashSet<FarTileKey> _dirty = [];
    private readonly HashSet<FarTileKey> _rebuilding = [];
    private readonly Dictionary<FarTileKey, FarTile> _tiles = [];
    private readonly HashSet<FarTileKey> _building = [];
    private readonly HashSet<FarTileKey> _bad = [];
    private readonly HashSet<FarTileKey> _standIns = [];
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
    private readonly List<FarTile> _pending = [];
    private Func<int, int, bool>? _nearReady;
    private double _reach = 1.0;
    private int _unuploaded;
    private volatile HashSet<FarTileKey> _wanted = [];
    private IReadOnlyList<FarTileKey> _leaves = [];
    private (double X, double Z, int Voxel) _selectedFor = (double.NaN, 0, 0);
    private double _selectedReach = 1.0;
    private (double X, double Z) _eye;
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

    /// <summary>The leaf tiles of the last <see cref="Update"/> that are not built yet.</summary>
    public int Missing
    {
        get
        {
            var missing = 0;
            foreach (var key in _leaves)
            {
                if (!(_tiles.TryGetValue(key, out var tile) && tile.IsBuilt) && !_bad.Contains(key) && !IsInner(key))
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
            _leaves = FarTileSelector.Select(Settings, eyeX, eyeZ, voxelViewDistance, _nearReady is not null, _reach);
            _selectedFor = (eyeX, eyeZ, voxelViewDistance);
            _selectedReach = _reach;
            _wanted = [.. _leaves];
        }

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
        foreach (var key in _leaves)
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

            if (_bad.Contains(key) || (_tiles.TryGetValue(key, out var tile) && tile.IsBuilt && !_dirty.Contains(key)) || !_building.Add(key))
            {
                continue;
            }

            if (_dirty.Remove(key))
            {
                _ = _rebuilding.Add(key);
            }

            // The coarse levels are cheap and cover the horizon: they are made early (their distance counts a sixteenth), so a view that has just begun shows the far land at once and the detail comes in from the near out.
            var distance = (long)key.NearestDistance(settings, eyeX, eyeZ) / (key.Level >= CoarseFirstFrom ? 16 : 1);
            var captured = key;
            _pool.Submit(
                JobLane.Tiles,
                distance,
                () => !_wanted.Contains(captured),
                () =>
                {
                    try
                    {
                        _done.Enqueue((version, FarTileMesher.Build(source, settings, captured, grey)));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _failed.Enqueue((version, captured)); // a torn section or a bug in one tile: the key is released, the others go on
                    }
                });
        }

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

    // The draw list: a leaf's own tile once it is on the card; until then the nearest coarser tile that is stands in for it, and while one does every leaf under it waits and the stand-in alone covers the ground (levels never overlap: a swap
    // is whole, when all the leaves under a stand-in are on the card, so no face of a coarser tile is ever behind or in front of a finer one's). A built tile waiting for the card is in PendingTiles, coarse levels first.
    private void Assemble()
    {
        _draw.Clear();
        _fallbacks.Clear();
        _standIns.Clear();
        _pending.Clear();
        _unuploaded = 0;
        long triangles = 0;
        var top = Settings.TopLevel;
        foreach (var key in _leaves)
        {
            if (!IsNeeded(key))
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

        foreach (var key in _standIns)
        {
            var stand = _tiles[key];
            _fallbacks.Add(stand);
            triangles += stand.Triangles;
        }

        foreach (var key in _leaves)
        {
            if (!_tiles.TryGetValue(key, out var tile) || !tile.OnCard || !IsNeeded(key) || CoveredByStandIn(key, top))
            {
                continue;
            }

            _draw.Add(tile);
            triangles += tile.Triangles;
        }

        _draw.InsertRange(0, _fallbacks);
        _drawnTriangles = (int)Math.Min(triangles, int.MaxValue);
    }

    private bool CoveredByStandIn(FarTileKey key, int top)
    {
        if (_standIns.Count == 0)
        {
            return false;
        }

        var ancestor = key;
        for (var step = 0; step < FallbackSteps && ancestor.Level < top; step++)
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

    private void DropUnwantedPending(Action<FarTile> dispose)
    {
        _used.Clear();
        foreach (var tile in _draw)
        {
            _ = _used.Add(tile.Key);
        }

        _used.UnionWith(_wanted);
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
