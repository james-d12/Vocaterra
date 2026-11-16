using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Far;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// Everything the game does to stream the tall world (B07), in one place: the worker threads (at most <see cref="JobPool.MaxWorkers"/> in all), the residency of the voxel chunks and the far view's
/// tiles. The game loop calls <see cref="Update"/> once a frame with the eye, and <see cref="Settle"/> before a scripted picture. Only the game loop's thread touches the grid; the workers touch the pure
/// source. A bounded valley has none of this: <see cref="For"/> answers <see langword="null"/> for it.
/// </summary>
public sealed class StreamedWorldHost : IDisposable
{
    /// <summary>The most time an interactive frame spends installing chunks and generating on the game loop's own thread when no worker is running.</summary>
    public const double InlineBudgetMilliseconds = 3;

    /// <summary>The most time an interactive frame spends installing what the workers made.</summary>
    public const double InstallBudgetMilliseconds = 1.5;

    /// <summary>The longest the simulation waits for the world round the player (a start or a jump), in milliseconds.</summary>
    public const double HoldLimitMilliseconds = 10_000;

    private readonly StreamedWorld _world;
    private readonly VoxelGrid _grid;
    private readonly IPlaceInfo? _places;
    private readonly JobPool _pool;
    private readonly FileLodPersistence? _persistence;
    private readonly LodEdits _edits;
    private readonly LodRing _ring;
    private ChunkResidency _residency;
    private double _heldSince = double.NaN;
    private int _viewDistance;
    private int _radius;

    private StreamedWorldHost(Scene scene, QualityPreset preset, int workers, string? lodWorld)
    {
        _world = scene.Streaming!;
        _grid = scene.Grid;
        _places = scene.Places;
        _pool = new JobPool(workers);
        _viewDistance = preset.ViewDistance;
        Bands = new ColumnBandCache(_world.Bands);
        _residency = MakeResidency(preset);
        _persistence = lodWorld is null ? null : new FileLodPersistence(FileLodPersistence.FolderFor(lodWorld), "lod-format-" + LodFormat + "; " + lodWorld);
        Far = new FarView(_world.Far, preset.Far, _pool, new LodStore(_persistence));
        _radius = preset.Far.RealRadius;
        _ring = new LodRing(Far.World, _world, _pool);
        _edits = new LodEdits(_grid, Far.World, _pool);
    }

    /// <summary>The version of the sections' format and of how they are made: a change makes every world's saved sections start again.</summary>
    public const int LodFormat = 1;

    /// <summary>The host of a scene's streaming, or <see langword="null"/> when the scene's grid is bounded (the valley).</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="preset">The quality in force (the view distance, the far range).</param>
    /// <param name="workers">The worker threads, 0 to 2: 0 keeps everything on the game loop's thread.</param>
    /// <param name="lodWorld">The name of the world the far view's sections are kept under on disk (its seed, generator version and world-generation hash), or <see langword="null"/> to keep them in memory only.</param>
    public static StreamedWorldHost? For(Scene scene, QualityPreset preset, int workers, string? lodWorld = null) =>
        scene.Grid.IsStreamed && scene.Streaming is not null ? new StreamedWorldHost(scene, preset, workers, lodWorld) : null;

    /// <summary>The residency radii for a view distance: the voxel view reaches its distance plus a chunk, the meshes need a chunk each side more, the keep adds slack.</summary>
    /// <param name="viewDistance">The voxel view distance in blocks.</param>
    public static BandStreamPolicy PolicyFor(int viewDistance)
    {
        var load = ((viewDistance + VoxelChunk.Size - 1) / VoxelChunk.Size) + 3;
        return new BandStreamPolicy(load, load + 2, 4, 6);
    }

    /// <summary>The shared worker threads.</summary>
    public JobPool Pool => _pool;

    /// <summary>The band cache.</summary>
    public ColumnBandCache Bands { get; }

    /// <summary>The voxel chunks' residency.</summary>
    public ChunkResidency Residency => _residency;

    /// <summary>The far view's tiles.</summary>
    public FarView Far { get; }

    /// <summary>The ring of real chunks the far view's store is made from.</summary>
    public LodRing Ring => _ring;

    /// <summary>What the far view's store holds and what the ring has done, for a run's report.</summary>
    public string LodSummary => $"{Far.World.Store.Count} sections ({Far.World.Store.Bytes / (1024.0 * 1024.0):0.0} MB packed), ring {_ring.Done} footprints real ({_ring.Pending} pending), {_edits.Ingested} edited chunks taken in";

    /// <summary>Whether the world round a place is made, so the simulation may run there (<see cref="ChunkResidency.IsNearReady"/>); true after <see cref="HoldLimitMilliseconds"/> of waiting, so a world that cannot be made never freezes the game.</summary>
    /// <param name="x">The place's x.</param>
    /// <param name="y">The place's y.</param>
    /// <param name="z">The place's z.</param>
    public bool IsReadyAround(float x, float y, float z)
    {
        if (_residency.IsNearReady(x, y, z))
        {
            _heldSince = double.NaN;
            return true;
        }

        var now = MonotonicClock.Milliseconds();
        _heldSince = double.IsNaN(_heldSince) ? now : _heldSince;
        return now - _heldSince > HoldLimitMilliseconds;
    }

    /// <summary>Takes another quality: the residency's radii and the far range follow.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    public void ApplyPreset(QualityPreset preset, Action<FarTile> dispose)
    {
        if (preset.ViewDistance != _viewDistance)
        {
            _viewDistance = preset.ViewDistance;
            _residency = MakeResidency(preset);
        }

        Far.Reconfigure(preset.Far, dispose);
        _radius = preset.Far.RealRadius;
    }

    /// <summary>One frame: the residency installs and plans within a small budget, and the far view chooses and collects its tiles.</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeY">The eye's y.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    public void Update(float eyeX, float eyeY, float eyeZ, Action<FarTile> dispose)
    {
        _residency.Update(eyeX, eyeY, eyeZ, _pool.Workers == 0 ? InlineBudgetMilliseconds : InstallBudgetMilliseconds);
        _ring.Update(eyeX, eyeZ, _radius);
        _edits.Update();
        Far.Update(eyeX, eyeZ, _viewDistance, dispose, InlineBudgetMilliseconds);
    }

    /// <summary>Brings the voxel chunks and the far tiles round the eye wholly up to date, waiting for the workers (a scripted run's picture, a jump to another place).</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeY">The eye's y.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    /// <returns>Whether everything settled.</returns>
    public bool Settle(float eyeX, float eyeY, float eyeZ, Action<FarTile> dispose)
    {
        var chunks = _residency.Settle(eyeX, eyeY, eyeZ);
        var started = MonotonicClock.Milliseconds();
        var tiles = false;
        // The ring's real chunks come into the store and the tiles over them are made again: a picture waits for both (a scripted run only; a live game's frames go on).
        while (true)
        {
            _ring.Update(eyeX, eyeZ, _radius);
            _edits.Update();
            tiles = Far.Settle(eyeX, eyeZ, _viewDistance, dispose);
            if ((_ring.IsIdle && Far.Stale == 0 && tiles) || MonotonicClock.Milliseconds() - started > SettleLimitMilliseconds)
            {
                break;
            }

            if (_pool.Workers > 0)
            {
                Thread.Sleep(2);
            }
        }

        return chunks && tiles && _ring.IsIdle;
    }

    /// <summary>The longest a settle waits for the ring and the tiles made again over it, in milliseconds.</summary>
    public const double SettleLimitMilliseconds = 300_000;

    /// <inheritdoc />
    public void Dispose()
    {
        _edits.Dispose();
        _pool.Dispose();
        _persistence?.Flush();
        _persistence?.Dispose();
    }

    private ChunkResidency MakeResidency(QualityPreset preset) =>
        new(_grid, _world, PolicyFor(preset.ViewDistance), _pool, _places, preset.MaxResidentChunks, Bands);
}

/// <summary>
/// The player's edits into the far world's store (B07c): a chunk that a save loaded changed, or that a block was changed in, is copied (on the game loop's thread, 8 KB) and handed to a worker that voxelises it into the real level-0 section it belongs to
/// and mips the levels above, so a removed tree or a house built stays that way when it is far off, and after a restart.
/// </summary>
internal sealed class LodEdits : IChunkResidencyListener, IBlockChangeListener, IDisposable
{
    private const int PerFrame = 2;

    private readonly VoxelGrid _grid;
    private readonly LodWorld _world;
    private readonly JobPool _pool;
    private readonly HashSet<VoxelChunkCoord> _dirty = [];
    private long _ingested;

    public LodEdits(VoxelGrid grid, LodWorld world, JobPool pool)
    {
        _grid = grid;
        _world = world;
        _pool = pool;
        grid.AddResidencyListener(this);
        grid.AddListener(this);
    }

    public long Ingested => Interlocked.Read(ref _ingested);

    public void OnChunkInstalled(VoxelChunkCoord coord)
    {
        if (_grid.TryGetChunk(coord, out var chunk) && chunk.IsChanged)
        {
            _ = _dirty.Add(coord);
        }
    }

    public void OnChunkEvicted(VoxelChunkCoord coord)
    {
    }

    public void OnBlockChanged(BlockChange change) => _ = _dirty.Add(new VoxelChunkCoord(change.Position.X >> VoxelChunk.Shift, change.Position.Y >> VoxelChunk.Shift, change.Position.Z >> VoxelChunk.Shift));

    /// <summary>One frame: a couple of the changed chunks are copied and sent to the workers.</summary>
    public void Update()
    {
        for (var n = 0; n < PerFrame && _dirty.Count > 0; n++)
        {
            var coord = _dirty.First();
            _ = _dirty.Remove(coord);
            if (!_grid.TryGetChunk(coord, out var chunk))
            {
                continue;
            }

            var blocks = chunk.Blocks.ToArray();
            _pool.Submit(JobLane.Ring, 0, static () => false, () =>
            {
                _world.IngestChunk(coord, blocks);
                _ = Interlocked.Increment(ref _ingested);
            });
        }
    }

    public void Dispose()
    {
        _ = _grid.RemoveResidencyListener(this);
        _ = _grid.RemoveListener(this);
    }
}
