using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.World.Far;
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

    /// <summary>The box round the tile, once it is built.</summary>
    public BoundingBox Box { get; set; }

    /// <summary>The vertices the tile has.</summary>
    public int Vertices { get; set; }

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
/// <param name="Bytes">The memory the cached tiles hold (16 bytes a vertex, on the card or waiting to go there).</param>
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

    private readonly FarTerrainSampler _sampler;
    private readonly JobPool _pool;
    private readonly Dictionary<FarTileKey, FarTile> _tiles = [];
    private readonly HashSet<FarTileKey> _building = [];
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
    public FarView(FarTerrainSampler sampler, FarViewSettings settings, JobPool pool)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pool);
        _sampler = sampler;
        Settings = settings;
        _pool = pool;
        FarPalette.Warm();
    }

    /// <summary>The shape of the view.</summary>
    public FarViewSettings Settings { get; private set; }

    /// <summary>The tiles of the last <see cref="Update"/> to draw, the fallbacks first (they are drawn a little behind), then the leaves nearest first.</summary>
    public IReadOnlyList<FarTile> Draw => _draw;

    /// <summary>How many of the first tiles of <see cref="Draw"/> are fallbacks (ancestors standing in for leaves that are not built).</summary>
    public int FallbackCount => _fallbacks.Count;

    /// <summary>The leaf tiles of the last <see cref="Update"/> that are not built yet.</summary>
    public int Missing => _leaves.Count(key => !(_tiles.TryGetValue(key, out var tile) && tile.IsBuilt));

    /// <summary>What the view holds and cost.</summary>
    public FarViewStats Stats
    {
        get
        {
            long vertices = 0;
            foreach (var tile in _tiles.Values)
            {
                vertices += tile.Vertices;
            }

            return new FarViewStats(_tiles.Count, _draw.Count, _building.Count, _built, vertices, _drawnTriangles, TimeSpan.FromTicks((long)_buildTicks).TotalMilliseconds, _longest, vertices * 16);
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
        var reshaped = settings.BaseCell != Settings.BaseCell || settings.TileCells != Settings.TileCells || settings.Levels != Settings.Levels;
        Settings = settings;
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
        var step = Settings.BaseCell;
        if (double.IsNaN(_selectedFor.X) || Math.Abs(eyeX - _selectedFor.X) >= step || Math.Abs(eyeZ - _selectedFor.Z) >= step || _selectedFor.Voxel != voxelViewDistance)
        {
            _leaves = FarTileSelector.Select(Settings, eyeX, eyeZ, voxelViewDistance);
            _selectedFor = (eyeX, eyeZ, voxelViewDistance);
            _wanted = [.. _leaves];
        }

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

    // Takes the tiles the workers finished into the cache.
    private void Collect()
    {
        while (_done.TryDequeue(out var mesh))
        {
            _ = _building.Remove(mesh.Key);
            if (!_tiles.TryGetValue(mesh.Key, out var tile))
            {
                _tiles[mesh.Key] = tile = new FarTile(mesh.Key);
            }

            tile.Pending = mesh;
            tile.Box = mesh.Box;
            tile.Vertices = mesh.Vertices.Length;
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
        foreach (var key in _leaves)
        {
            if ((_tiles.TryGetValue(key, out var tile) && tile.IsBuilt) || !_building.Add(key))
            {
                continue;
            }

            var distance = (long)key.NearestDistance(settings, eyeX, eyeZ);
            var captured = key;
            _pool.Submit(
                JobLane.Tiles,
                distance,
                () => !_wanted.Contains(captured),
                () => _done.Enqueue(FarTileMesher.Build(_sampler, settings, captured, grey)));
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
        var standIns = new HashSet<FarTileKey>();
        long triangles = 0;
        var perTile = FarTileTopology.TriangleCount(Settings.TileCells);
        foreach (var key in _leaves)
        {
            if (_tiles.TryGetValue(key, out var tile) && tile.IsBuilt)
            {
                _draw.Add(tile);
                triangles += perTile;
                continue;
            }

            var ancestor = key;
            for (var step = 0; step < FallbackSteps && ancestor.Level < Settings.TopLevel; step++)
            {
                ancestor = ancestor.GetParent();
                if (_tiles.TryGetValue(ancestor, out var stand) && stand.IsBuilt)
                {
                    if (standIns.Add(ancestor))
                    {
                        _fallbacks.Add(stand);
                        triangles += perTile;
                    }

                    break;
                }
            }
        }

        _draw.InsertRange(0, _fallbacks);
        _drawnTriangles = (int)Math.Min(triangles, int.MaxValue);
    }

    // Drops the farthest tiles that are not wanted or standing in, past the cap.
    private void Trim(double eyeX, double eyeZ, Action<FarTile> dispose)
    {
        var settings = Settings;
        if (_tiles.Count <= settings.MaxTiles)
        {
            return;
        }

        var used = new HashSet<FarTileKey>(_draw.Select(static t => t.Key));
        used.UnionWith(_wanted);
        var victims = _tiles.Keys.Where(key => !used.Contains(key)).OrderByDescending(key => key.NearestDistance(settings, eyeX, eyeZ)).Take(_tiles.Count - settings.MaxTiles).ToList();
        foreach (var key in victims)
        {
            dispose(_tiles[key]);
            _ = _tiles.Remove(key);
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
        _wanted = [];
        _pool.Clear(JobLane.Tiles);
    }
}
