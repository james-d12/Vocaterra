using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Colour;
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
    private readonly Func<int, int, int, (int Temperature, int Humidity)>? _ground;
    private readonly JobPool _pool;
    private readonly FileLodPersistence? _persistence;
    private readonly LodEdits _edits;
    private readonly LodRing _ring;
    private readonly ChunkResidency _residency;
    private double _heldSince = double.NaN;
    private double _catchUpUntil = double.NegativeInfinity;
    private int _viewDistance;
    private int _maxResident;
    private int _radius;

    private StreamedWorldHost(Scene scene, QualityPreset preset, int workers, string? lodWorld, string? lodFolder)
    {
        _world = scene.Streaming!;
        _grid = scene.Grid;
        _places = scene.Places;
        _ground = scene.Places is { } places ? GroundClimate.Of(places) : null;
        _pool = new JobPool(workers);
        _viewDistance = preset.ViewDistance;
        _maxResident = preset.MaxResidentChunks;
        Bands = new ColumnBandCache(_world.Bands);
        _residency = MakeResidency(preset);
        _persistence = lodWorld is null && lodFolder is null ? null : new FileLodPersistence(lodFolder ?? FileLodPersistence.FolderFor(lodWorld!), "lod-format-" + LodFormat + "; " + lodWorld);
        Far = new FarView(_world.Far, FarSettings(preset), _pool, new LodStore(_persistence));
        _radius = RingRadius(preset);
        _ring = new LodRing(Far.World, _world, _pool);

        // The far store's edits are the loaded save's (B07f): what the last run left in the sections and this world does not hold is dropped (the ring makes those footprints again from the generator), and every chunk this world has changed is queued to be taken in, so a save's buildings stand in the far view at the start.
        var current = _grid.ChangedChunkCoords();
        if (_persistence is not null)
        {
            EditsDropped = DropStaleEdits(current);
        }

        _edits = new LodEdits(_grid, Far.World, _pool, _persistence);
        _edits.Seed(current);
    }

    /// <summary>How many sections of the store were dropped at the start because they held edits that the world loaded does not have (a save without them, a new game, a quit without saving).</summary>
    public int EditsDropped { get; }

    // The footprints that hold a recorded edit this world lacks are forgotten; the journal then lists the world's own changed chunks.
    private int DropStaleEdits(IReadOnlyList<VoxelChunkCoord> current)
    {
        var journal = _persistence!;
        var held = new HashSet<VoxelChunkCoord>(current);
        var footprints = new HashSet<(int Sx, int Sz)>();
        foreach (var coord in journal.Edited())
        {
            if (!held.Contains(coord))
            {
                _ = footprints.Add((coord.X >> 1, coord.Z >> 1));
            }
        }

        var dropped = 0;
        foreach (var (sx, sz) in footprints)
        {
            dropped += Far.World.ResetFootprint(sx, sz);
        }

        journal.Replace(current);
        if (dropped > 0)
        {
            _ = journal.Flush(TimeSpan.FromSeconds(10)); // a section asked about before its drop reached the disk would read the old bytes
        }

        return dropped;
    }

    // The far view's shape for a preset, with the ground climate its grass and leaves are coloured by (B22): the same map and the same function as the chunks'.
    private FarViewSettings FarSettings(QualityPreset preset) => preset.Far with { Ground = _ground };

    // The ring's radius: the preset's, or VOCATERRA_LOD_RING (blocks, 0 for none) for a dev run that wants the model alone.
    private static int RingRadius(QualityPreset preset) => int.TryParse(Environment.GetEnvironmentVariable("VOCATERRA_LOD_RING"), out var forced) ? Math.Max(0, forced) : preset.Far.RealRadius;

    /// <summary>The version of the sections' format and of how they are made: a change makes every world's saved sections start again.</summary>
    public const int LodFormat = 3;

    /// <summary>The host of a scene's streaming, or <see langword="null"/> when the scene's grid is bounded (the valley).</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="preset">The quality in force (the view distance, the far range).</param>
    /// <param name="workers">The worker threads, 0 to 2: 0 keeps everything on the game loop's thread.</param>
    /// <param name="lodWorld">The name of the world the far view's sections are kept under on disk (its seed, generator version and world-generation hash), or <see langword="null"/> to keep them in memory only.</param>
    /// <param name="lodFolder">The folder to keep them in instead of the one named for the world (tests).</param>
    public static StreamedWorldHost? For(Scene scene, QualityPreset preset, int workers, string? lodWorld = null, string? lodFolder = null) =>
        scene.Grid.IsStreamed && scene.Streaming is not null ? new StreamedWorldHost(scene, preset, workers, lodWorld, lodFolder) : null;

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

    /// <summary>How many sections the far view's store holds in memory.</summary>
    public int LodSections => Far.World.Store.Count;

    /// <summary>The bytes those sections take, packed.</summary>
    public long LodBytes => Far.World.Store.Bytes;

    /// <summary>How many chunks of the player's edits were taken into the store.</summary>
    public long EditedChunksTaken => _edits.Ingested;

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

    /// <summary>The longest a frame waits to make the near zone of a place the body moved to (a start, a jump, a respawn, a load), in milliseconds.</summary>
    public const double NearZoneLimitMilliseconds = 1_500;

    /// <summary>How long after a move to a far place the frames spend a larger budget installing and meshing, so the view round the body fills in within a second or two and not over ten (B07b), in milliseconds.</summary>
    public const double CatchUpMilliseconds = 2_000;

    /// <summary>The install budget of a frame while catching up.</summary>
    public const double CatchUpInstallBudgetMilliseconds = 6;

    /// <summary>The mesh budget of a frame while catching up.</summary>
    public const double CatchUpMeshBudgetMilliseconds = 10;

    /// <summary>Whether the frames are still catching up after a move to a far place (<see cref="CatchUpMilliseconds"/>).</summary>
    public bool IsCatchingUp => MonotonicClock.Milliseconds() < _catchUpUntil;

    /// <summary>Whether the near zone of a place is made (<see cref="ChunkResidency.IsNearReady"/>), without the hold's clock.</summary>
    /// <param name="x">The place's x.</param>
    /// <param name="y">The place's y.</param>
    /// <param name="z">The place's z.</param>
    public bool IsNearReady(float x, float y, float z) => _residency.IsNearReady(x, y, z);

    /// <summary>Makes the near zone of a place before the frame goes on, within <see cref="NearZoneLimitMilliseconds"/> (B07b); nothing to do when it is made.</summary>
    /// <param name="x">The place's x.</param>
    /// <param name="y">The place's y.</param>
    /// <param name="z">The place's z.</param>
    /// <returns>Whether the near zone was not ready and the call had to make it.</returns>
    public bool MakeNearZone(float x, float y, float z)
    {
        if (_residency.IsNearReady(x, y, z))
        {
            return false;
        }

        _ = _residency.SettleNear(x, y, z, NearZoneLimitMilliseconds);
        _catchUpUntil = MonotonicClock.Milliseconds() + CatchUpMilliseconds;
        return true;
    }

    /// <summary>Takes another quality: the residency's radii and the far range follow.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    public void ApplyPreset(QualityPreset preset, Action<FarTile> dispose)
    {
        if (preset.ViewDistance != _viewDistance || preset.MaxResidentChunks != _maxResident)
        {
            // The same driver takes the new radii (B21, review C5): what its workers are making and have made stays valid, where a new driver forgot it and left its jobs to finish for nobody.
            _viewDistance = preset.ViewDistance;
            _maxResident = preset.MaxResidentChunks;
            _residency.Reconfigure(PolicyFor(preset.ViewDistance), preset.MaxResidentChunks);
        }

        Far.Reconfigure(FarSettings(preset), dispose);
        _radius = RingRadius(preset);
    }

    /// <summary>One frame: the residency installs and plans within a small budget, and the far view chooses and collects its tiles.</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeY">The eye's y.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    public void Update(float eyeX, float eyeY, float eyeZ, Action<FarTile> dispose)
    {
        _residency.Update(eyeX, eyeY, eyeZ, _pool.Workers == 0 ? InlineBudgetMilliseconds : IsCatchingUp ? CatchUpInstallBudgetMilliseconds : InstallBudgetMilliseconds);
        _ring.Update(eyeX, eyeZ, _radius);
        _edits.Update();
        Far.Update(eyeX, eyeZ, _viewDistance, dispose, InlineBudgetMilliseconds);
    }

    /// <summary>Brings the voxel chunks and the far tiles round the eye wholly up to date, waiting for the workers (a scripted run's picture, a jump to another place).</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeY">The eye's y.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="dispose">Frees a far tile's buffer.</param>
    /// <param name="limitMilliseconds">The longest each wait takes (the chunks, the tiles, the ring), or <see langword="null"/> for the usual bounds: a test on a loaded machine gives a generous one, so it waits on the condition and not on the clock.</param>
    /// <returns>Whether everything settled.</returns>
    public bool Settle(float eyeX, float eyeY, float eyeZ, Action<FarTile> dispose, double? limitMilliseconds = null)
    {
        var chunks = limitMilliseconds is { } given ? _residency.Settle(eyeX, eyeY, eyeZ, given) : _residency.Settle(eyeX, eyeY, eyeZ);
        var started = MonotonicClock.Milliseconds();
        var tiles = false;
        // The ring's real chunks come into the store and the tiles over them are made again: a picture waits for both (a scripted run only; a live game's frames go on).
        while (true)
        {
            _ring.Update(eyeX, eyeZ, _radius);
            _edits.Update();
            tiles = limitMilliseconds is { } far ? Far.Settle(eyeX, eyeZ, _viewDistance, dispose, far) : Far.Settle(eyeX, eyeZ, _viewDistance, dispose);
            if ((_ring.IsIdle && _edits.IsIdle && Far.Stale == 0 && tiles) || MonotonicClock.Milliseconds() - started > (limitMilliseconds ?? SettleLimitMilliseconds))
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
        // Bounded all through (C10): the pool waits a couple of seconds for a job that is still running, then the writer is asked to drain, sync and close its own files, and is waited for up to a limit; it never holds the exit.
        _edits.Dispose();
        _pool.Dispose();
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
    private const int PerFrame = 16;

    private readonly VoxelGrid _grid;
    private readonly LodWorld _world;
    private readonly JobPool _pool;
    private readonly ILodEditJournal? _journal;
    private readonly HashSet<VoxelChunkCoord> _dirty = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<VoxelChunkCoord, BlockId[]> _latest = new();
    private long _ingested;
    private int _open;

    public LodEdits(VoxelGrid grid, LodWorld world, JobPool pool, ILodEditJournal? journal = null)
    {
        _grid = grid;
        _world = world;
        _pool = pool;
        _journal = journal;
        grid.AddResidencyListener(this);
        grid.AddListener(this);
    }

    public long Ingested => Interlocked.Read(ref _ingested);

    /// <summary>Whether nothing waits to be copied and no job is queued or running.</summary>
    public bool IsIdle => _dirty.Count == 0 && Volatile.Read(ref _open) == 0;

    /// <summary>Queues chunks to be taken in (a loaded save's changed chunks, at the start).</summary>
    public void Seed(IEnumerable<VoxelChunkCoord> coords) => _dirty.UnionWith(coords);

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

    /// <summary>One frame: some of the changed chunks are copied and sent to the workers.</summary>
    public void Update()
    {
        for (var n = 0; n < PerFrame && _dirty.Count > 0; n++)
        {
            var coord = default(VoxelChunkCoord);
            foreach (var next in _dirty)
            {
                coord = next; // the struct enumerator allocates nothing, where First() allocates every frame while edits are pending
                break;
            }

            _ = _dirty.Remove(coord);
            if (!_grid.TryGetChunk(coord, out var chunk))
            {
                continue;
            }

            // A chunk that is changed again while its job waits is not ingested twice: the job takes the newest blocks (the whole chunk is copied once per burst of edits).
            var blocks = chunk.Blocks.ToArray();
            var fresh = true;
            _ = _latest.AddOrUpdate(coord, _ => blocks, (_, _) =>
            {
                fresh = false;
                return blocks;
            });
            if (!fresh)
            {
                continue;
            }

            _journal?.Record(coord); // before the edit reaches the store, so the journal always covers what the store holds
            _ = Interlocked.Increment(ref _open);
            _pool.Submit(JobLane.Ring, 0, static () => false, () =>
            {
                try
                {
                    if (_latest.TryRemove(coord, out var newest))
                    {
                        _world.IngestChunk(coord, newest);
                        _ = Interlocked.Increment(ref _ingested);
                    }
                }
                finally
                {
                    _ = Interlocked.Decrement(ref _open);
                }
            });
        }
    }

    public void Dispose()
    {
        _ = _grid.RemoveResidencyListener(this);
        _ = _grid.RemoveListener(this);
    }
}
