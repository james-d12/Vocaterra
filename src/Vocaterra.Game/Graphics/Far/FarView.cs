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
        Vertices = mesh.Vertices.Length;
        Triangles = mesh.Triangles;
        Textured = mesh.Textured;
        Bytes = mesh.Bytes;
        Runs = (mesh.OpaqueIndices, mesh.LitIndices, mesh.WaterIndices);
    }

    /// <summary>Whether a worker has made the tile.</summary>
    public bool IsBuilt => Pending is not null || Buffer is not null;
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
/// tiles that newly become leaves. A tile is a pure function of the seed and its key, so a player's edits never show at a distance. No graphics device is touched here: the renderer uploads.
/// </summary>
public sealed class FarView
{
    private const int FallbackSteps = 4;

    // From this level up (cells of 16 blocks and more) tiles are made before the finer ones that are as near.
    private const int CoarseFirstFrom = 4;

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
    private readonly List<FarTileKey> _idle = [];
    private int _trims;
    private readonly ConcurrentQueue<FarTileKey> _failed = new();
    private readonly ConcurrentQueue<FarTileMesh> _done = new();
    private readonly List<FarTile> _draw = [];
    private readonly List<FarTile> _fallbacks = [];
    private volatile HashSet<FarTileKey> _wanted = [];
    private IReadOnlyList<FarTileKey> _leaves = [];
    private (double X, double Z, int Voxel) _selectedFor = (double.NaN, 0, 0);
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

    /// <summary>The far world: the store and the model that fills it; the real chunks are ingested into it.</summary>
    public LodWorld World { get; private set; }

    /// <summary>The listed tiles and their triangles by level, for a run's report: "L0 12 tiles 340k, L1 ...".</summary>
    public string LevelSummary => string.Join(", ", _draw.GroupBy(static t => t.Key.Level).OrderBy(static g => g.Key).Select(static g => $"L{g.Key} {g.Count()} tiles {g.Sum(static t => (long)t.Triangles) / 1000}k"));

    /// <summary>The tiles whose sections changed and are being made again (the old tile is drawn until the new one is there).</summary>
    public int Stale => _dirty.Count + _rebuilding.Count;

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
                if (!(_tiles.TryGetValue(key, out var tile) && tile.IsBuilt) && !_bad.Contains(key))
                {
                    missing++;
                }
            }

            return missing;
        }
    }

    /// <summary>The tiles of the last draw list whose mesh the renderer has not yet uploaded (a tile of it is drawn from its fallback, or not at all, until it has): a settled picture waits for this to be none.</summary>
    public int Unuploaded
    {
        get
        {
            var count = 0;
            foreach (var tile in _draw)
            {
                count += tile.Pending is not null ? 1 : 0;
            }

            return count;
        }
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
        var reshaped = settings.Levels != Settings.Levels || settings.TreeLevels != Settings.TreeLevels;
        Settings = settings;
        if (reshaped)
        {
            World = _sampler.FarWorld(_store, settings.TreeLevels);
        }

        _selectedFor = (double.NaN, 0, 0);
        if (reshaped)
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
            _pool.Clear(JobLane.Tiles);
            while (_done.TryDequeue(out _))
            {
            }
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
        if (double.IsNaN(_selectedFor.X) || Math.Abs(eyeX - _selectedFor.X) >= step || Math.Abs(eyeZ - _selectedFor.Z) >= step || _selectedFor.Voxel != voxelViewDistance)
        {
            _leaves = FarTileSelector.Select(Settings, eyeX, eyeZ, voxelViewDistance);
            _selectedFor = (eyeX, eyeZ, voxelViewDistance);
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

            if (_wanted.Contains(key))
            {
                _ = _dirty.Add(key);
            }
            else
            {
                dispose(tile);
                _ = _tiles.Remove(key);
            }
        }
    }

    // Takes the tiles the workers finished into the cache.
    private void Collect()
    {
        while (_failed.TryDequeue(out var failed))
        {
            _ = _building.Remove(failed);
            _ = _rebuilding.Remove(failed);
            _ = _bad.Add(failed);
        }

        while (_done.TryDequeue(out var mesh))
        {
            _ = _building.Remove(mesh.Key);
            _ = _rebuilding.Remove(mesh.Key);
            if (!_wanted.Contains(mesh.Key) && !_tiles.ContainsKey(mesh.Key))
            {
                continue; // made for a view the eye has left: not kept
            }

            if (!_tiles.TryGetValue(mesh.Key, out var tile))
            {
                _tiles[mesh.Key] = tile = new FarTile(mesh.Key);
            }

            tile.Pending = mesh;
            if (tile.Buffer is null)
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
        foreach (var key in _leaves)
        {
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
                        _done.Enqueue(FarTileMesher.Build(source, settings, captured, grey));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _failed.Enqueue(captured); // a torn section or a bug in one tile: the key is released, the others go on
                    }
                });
        }

        // A job skipped as stale leaves its key in _building: forget the keys that are no longer wanted so they can be asked for again.
        if (_building.Count > 0)
        {
            _ = _building.RemoveWhere(key => !_wanted.Contains(key) && !_tiles.ContainsKey(key));
        }
    }

    // The draw list: each leaf's own tile when built, else the nearest built ancestor (once), drawn behind.
    private void Assemble()
    {
        _draw.Clear();
        _fallbacks.Clear();
        _standIns.Clear();
        long triangles = 0;
        foreach (var key in _leaves)
        {
            if (_tiles.TryGetValue(key, out var tile) && tile.IsBuilt)
            {
                _draw.Add(tile);
                triangles += tile.Triangles;
                continue;
            }

            var ancestor = key;
            for (var step = 0; step < FallbackSteps && ancestor.Level < Settings.TopLevel; step++)
            {
                ancestor = ancestor.GetParent();
                if (_tiles.TryGetValue(ancestor, out var stand) && stand.IsBuilt)
                {
                    if (_standIns.Add(ancestor))
                    {
                        _fallbacks.Add(stand);
                        triangles += stand.Triangles;
                    }

                    break;
                }
            }
        }

        _draw.InsertRange(0, _fallbacks);
        _drawnTriangles = (int)Math.Min(triangles, int.MaxValue);
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
        var victims = _tiles.Keys.Where(key => !_used.Contains(key)).OrderByDescending(key => key.NearestDistance(settings, eyeX, eyeZ)).Take(_tiles.Count - settings.MaxTiles).ToList();
        foreach (var key in victims)
        {
            dispose(_tiles[key]);
            _ = _tiles.Remove(key);
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
            var tile = _tiles[key];
            if (tile.Buffer is null)
            {
                dispose(tile);
                _ = _tiles.Remove(key);
            }
            else
            {
                tile.Pending = null; // the card's buffers stay; the memory of the newer mesh goes
            }
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
        _bad.Clear();
        _wanted = [];
        _pool.Clear(JobLane.Tiles);
    }
}
