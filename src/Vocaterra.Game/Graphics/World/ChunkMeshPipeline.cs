using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Graphics.World;

/// <summary>The vertices of one mesh of a chunk, ready for the card (B21): the quads' corners as <see cref="MeshVertex"/> (the layout of the engine's vertex), cut to what a 16-bit index reaches.</summary>
/// <param name="Vertices">Four vertices to a quad, or <see langword="null"/> for none.</param>
/// <param name="Quads">How many quads.</param>
public readonly record struct MeshPart(MeshVertex[]? Vertices, int Quads)
{
    /// <summary>The most quads a chunk mesh holds: it is drawn with 16-bit indices, so 65 532 vertices. A full chunk of cubes tops out at 12 288 quads.</summary>
    public const int MaxQuads = 16383;

    /// <summary>The part of a built mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    public static MeshPart Of(MeshBuffer mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var quads = Math.Min(mesh.FaceCount, MaxQuads);
        return quads == 0 ? default : new MeshPart(mesh.Vertices[..(quads * 4)].ToArray(), quads);
    }
}

/// <summary>What a worker made of one job (B21): a chunk's meshes, or its grass layer, with what the game loop needs to decide whether to take it.</summary>
public sealed class ChunkMeshResult
{
    /// <summary>The chunk.</summary>
    public VoxelChunkCoord Coord { get; init; }

    /// <summary>The chunk's stamp when the snapshot was taken: an edit since makes the result stale.</summary>
    public int Stamp { get; init; }

    /// <summary>The pipeline's epoch when the snapshot was taken (the grey set, the quality's options).</summary>
    public int Epoch { get; init; }

    /// <summary>The job's number, so a result that a newer job for the chunk replaced is told from the current one.</summary>
    public long JobId { get; init; }

    /// <summary>Whether this is the grass layer's job (the meshes are empty) and not the chunk's.</summary>
    public bool IsGrass { get; init; }

    /// <summary>The light band the mesh (or the grass) was baked in.</summary>
    public LightBand Band { get; init; }

    /// <summary>The block kinds the chunk holds (<see cref="ChunkMeshes.Kinds"/>).</summary>
    public UInt128 Kinds { get; set; }

    /// <summary>The chunk is fully buried: it has no mesh at all.</summary>
    public bool Buried { get; set; }

    /// <summary>The worker found the job stale before building and made nothing.</summary>
    public bool Skipped { get; set; }

    /// <summary>The job threw (the exception is <see cref="ChunkMeshPipeline.Failure"/>).</summary>
    public bool Failed { get; set; }

    /// <summary>The opaque mesh.</summary>
    public MeshPart Opaque { get; set; }

    /// <summary>The translucent (water) mesh.</summary>
    public MeshPart Alpha { get; set; }

    /// <summary>The undergrowth.</summary>
    public MeshPart Plants { get; set; }

    /// <summary>The self-lit pieces.</summary>
    public MeshPart Lit { get; set; }

    /// <summary>The lanterns' glow.</summary>
    public MeshPart Glow { get; set; }

    /// <summary>The grass layer's tufts (a grass job).</summary>
    public MeshPart GrassMesh { get; set; }

    /// <summary>The grass layer's first quad of each quadrant (a grass job with tufts).</summary>
    public int[]? GrassStart { get; set; }

    /// <summary>The grass layer's quads by tier (a grass job with tufts).</summary>
    public int[]? GrassCumulative { get; set; }

    /// <summary>How long the worker took to build it, in milliseconds.</summary>
    public double Milliseconds { get; set; }
}

/// <summary>Where the pipeline hands what it decided to take: the renderer's upload, and the other things it does with a mesh that came or went.</summary>
public interface IChunkMeshSink
{
    /// <summary>A chunk's meshes are current: put them on the card in place of the old ones, and record the chunk as meshed.</summary>
    /// <param name="result">The result.</param>
    void Install(ChunkMeshResult result);

    /// <summary>A chunk's grass layer is current: put it on the card in place of the old one.</summary>
    /// <param name="result">The result.</param>
    void InstallGrass(ChunkMeshResult result);

    /// <summary>A chunk's grass job came back stale: the layer is due again.</summary>
    /// <param name="coord">The chunk.</param>
    void GrassRefused(VoxelChunkCoord coord);

    /// <summary>A chunk that has a mesh is no longer wanted and its mesh is out of date: let it go.</summary>
    /// <param name="coord">The chunk.</param>
    void Release(VoxelChunkCoord coord);
}

/// <summary>
/// Meshing off the game thread (B21, review P1 and P2): the chunks whose mesh is missing or out of date are found without a scan of the view, a snapshot of the blocks each reads is taken on the game thread
/// (<see cref="ChunkSnapshot"/>), the job pool's workers build the meshes from it (<see cref="JobLane.Mesh"/>), and the game thread puts what comes back on the card within the frame's budget.
/// <para>
/// A chunk has a <i>stamp</i>, bumped by every change that could alter its mesh: an edit of one of its blocks, an edit of a block of a neighbour that it reads (a border cell, with the corner and the edge neighbours too, and
/// the cells of the chunk below that the water's depth reads), a light or the sky's reach changing near it. A job carries the stamp its snapshot was taken at; a result whose chunk has another stamp is refused and the chunk is
/// built again, so a mesh is never taken from blocks older than the grid's (a worker even skips a job already stale when it starts). A change of the grey set or of the quality's options moves a whole-pipeline epoch the same way.
/// </para>
/// <para>
/// What to build comes from lists, not from a pass over the view: a block change lists the chunks it touched (<see cref="OnBlockChanged"/>), a change of the wanted chunks lists those still without a mesh
/// (<see cref="SetWanted"/>, once), a change of the light band those still in the old one; each frame walks a bounded window of each list, nearest first and in the frustum first. A chunk edited near the eye is meshed at once on
/// the game thread (at most <see cref="MaxInlineEdits"/> a frame within <see cref="InlineEditReach"/> blocks), so a block broken or placed shows in the same frame; the others go to the workers, and the old mesh stays on screen until the new one replaces it.
/// With no worker the same jobs run on the game thread within the frame's budget, as before.
/// </para>
/// </summary>
public sealed class ChunkMeshPipeline : IBlockChangeListener, IChunkResidencyListener, IDisposable
{
    /// <summary>The most mesh jobs handed to the workers and not yet taken back.</summary>
    public const int MaxInFlight = 24;

    /// <summary>The most jobs a frame hands to the workers.</summary>
    public const int MaxSubmitsPerFrame = 16;

    /// <summary>How many entries of a list of chunks to build a frame looks at (it takes the nearest, in the frustum first).</summary>
    public const int Window = 512;

    /// <summary>The most chunks a frame meshes on the game thread because an edit touched them and they are near the eye.</summary>
    public const int MaxInlineEdits = 4;

    /// <summary>How near the eye, in blocks, an edited chunk's box must be to be meshed on the game thread in the frame of the edit.</summary>
    public const int InlineEditReach = 40;

    private const int CompactAfter = 2048;
    private const int MaxSpareSnapshots = 64;

    private readonly VoxelGrid _grid;
    private readonly ChunkVersionTracker _tracker;
    private readonly Func<VoxelChunkCoord, MeshOptions> _optionsFor;
    private readonly RiverCurrents? _currents;
    private readonly Dictionary<VoxelChunkCoord, Slot> _slots = [];
    private readonly HashSet<VoxelChunkCoord> _wantedSet = [];
    private readonly List<VoxelChunkCoord> _todo = [];
    private readonly List<VoxelChunkCoord> _stale = [];
    private readonly List<VoxelChunkCoord> _rebuild = [];
    private readonly ConcurrentQueue<ChunkMeshResult> _results = new();
    private readonly ConcurrentQueue<ChunkSnapshot> _spare = new();
    private readonly Comparer _comparer;
    private IReadOnlyList<VoxelChunkCoord> _wanted = [];
    private int _todoHead;
    private int _staleHead;
    private int _stamps;
    private long _jobIds;
    private int _epoch;
    private int _flying;
    private int _doneThisFrame;
    private int _submittedThisFrame;
    private int _inlineThisFrame;
    private long _events;
    private long _editBase;
    private Vector3 _eye;
    private Exception? _failure;
    private bool _disposed;
    private long _submitted;
    private long _installed;
    private long _refused;
    private long _inline;
    private long _scanned;
    private double _workerMilliseconds;
    private double _longestWorkerMilliseconds;

    /// <summary>Creates the pipeline of a grid and listens to its changes and to chunks coming and going.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="tracker">What the renderer knows of each chunk's mesh (which kinds, which band).</param>
    /// <param name="optionsFor">The mesh options of a chunk as they are now (the quality's, the band's, the lights near it, the sky over it): asked on the game thread when a snapshot is taken.</param>
    /// <param name="currents">The river's currents, or <see langword="null"/>.</param>
    /// <param name="pool">The workers, or <see langword="null"/> to build on the game thread within the frame's budget.</param>
    public ChunkMeshPipeline(VoxelGrid grid, ChunkVersionTracker tracker, Func<VoxelChunkCoord, MeshOptions> optionsFor, RiverCurrents? currents = null, JobPool? pool = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(optionsFor);
        _grid = grid;
        _tracker = tracker;
        _optionsFor = optionsFor;
        _currents = currents;
        Pool = pool;
        _comparer = new Comparer(this);
        _editBase = grid.EditCount;
        grid.AddListener(this);
        grid.AddResidencyListener(this);
    }

    /// <summary>The workers, or <see langword="null"/> (or a pool of none) to build on the game thread.</summary>
    public JobPool? Pool { get; set; }

    /// <summary>The light band new meshes are baked in: a mesh in another band is stale, drawn with its band's tint until it is built again.</summary>
    public LightBand Band { get; set; }

    /// <summary>Whether a job runs on a worker (a pool with at least one).</summary>
    public bool IsAsync => Pool is { Workers: > 0 };

    /// <summary>The first exception a job threw on a worker, or <see langword="null"/> (the mesher is pure, so none should).</summary>
    public Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Jobs handed to the workers and not yet taken back.</summary>
    public int InFlight => _flying;

    /// <summary>
    /// Chunks that want a mesh and have none, or an old one: the lists' remaining entries and the jobs in flight. Zero when the view is made. Entries are counted until the walk reaches them, so it can be a little over.
    /// </summary>
    public int Pending => _rebuild.Count + (_todo.Count - _todoHead) + (_stale.Count - _staleHead) + _flying;

    /// <summary>Whether there is anything left to do (a job in flight, or a chunk on a list that the walk has not dropped or built).</summary>
    public bool Busy
    {
        get
        {
            TrimHeads();
            return _flying > 0 || _rebuild.Count > 0 || _todoHead < _todo.Count || _staleHead < _stale.Count || !_results.IsEmpty;
        }
    }

    /// <summary>Jobs handed to the workers (or run inline) so far.</summary>
    public long Submitted => _submitted;

    /// <summary>Meshes and grass layers put on the card.</summary>
    public long Installed => _installed;

    /// <summary>Results refused because their chunk changed after the snapshot (or the options did), or because a newer job for the chunk took their place.</summary>
    public long Refused => _refused;

    /// <summary>Chunks meshed on the game thread in the frame of an edit.</summary>
    public long InlineEdits => _inline;

    /// <summary>List entries and results looked at in the frames so far: what a frame costs when nothing changed is this staying where it was.</summary>
    public long Scanned => _scanned;

    /// <summary>The time the workers (or the inline builds) spent making meshes, in milliseconds, and the longest one.</summary>
    public (double Total, double Longest) BuildMilliseconds => (_workerMilliseconds, _longestWorkerMilliseconds);

    /// <summary>The chunks the pipeline has a record of (meshed, in flight or skipped).</summary>
    public int Tracked => _slots.Count;

    /// <summary>Stops listening to the grid.</summary>
    public void Dispose()
    {
        _disposed = true;
        _ = _grid.RemoveListener(this);
        _ = _grid.RemoveResidencyListener(this);
    }

    /// <summary>The wanted chunks changed (the eye moved to another chunk, chunks came or went): the chunks without a mesh among them are listed, nearest first as the view lists them; those with a mesh are not looked at again.</summary>
    /// <param name="wanted">The list the view set gave.</param>
    public void SetWanted(IReadOnlyList<VoxelChunkCoord> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        _wanted = wanted;
        _wantedSet.Clear();
        _todo.Clear();
        _todoHead = 0;
        _stale.Clear();
        _staleHead = 0;
        for (var i = 0; i < wanted.Count; i++)
        {
            var coord = wanted[i];
            _ = _wantedSet.Add(coord);
            if (!_slots.TryGetValue(coord, out var slot))
            {
                _todo.Add(coord);
            }
            else if (WantsFresh(slot))
            {
                _todo.Add(coord);
            }
            else if (WantsBand(slot))
            {
                _stale.Add(coord);
            }
        }
    }

    /// <summary>The light band changed: the meshes of another band among the wanted chunks are listed, to be built again after everything that has no mesh.</summary>
    public void BandChanged()
    {
        _stale.Clear();
        _staleHead = 0;
        for (var i = 0; i < _wanted.Count; i++)
        {
            if (NeededStale(_wanted[i]))
            {
                _stale.Add(_wanted[i]);
            }
        }
    }

    /// <summary>What a chunk's mesh is made from changed without a block of it or its neighbours changing (a light near it, the sky's reach over it): it is built again.</summary>
    /// <param name="coord">The chunk.</param>
    public void Invalidate(VoxelChunkCoord coord) => Touch(coord, edited: false);

    /// <summary>The meshes in flight were made from options that changed (the grey set, the quality's options): they are refused when they come back and the chunks they were for are built again.</summary>
    public void BumpEpoch() => _epoch++;

    /// <summary>Every mesh was let go (the quality's options changed): what is known of each chunk goes, jobs in flight are refused, and the next <see cref="SetWanted"/> lists the view again.</summary>
    public void Reset()
    {
        _epoch++;
        _slots.Clear();
        _rebuild.Clear();
        _todo.Clear();
        _stale.Clear();
        (_todoHead, _staleHead) = (0, 0);
        _wantedSet.Clear();
        _wanted = [];
    }

    /// <summary>The chunk's mesh was let go (it went out of range, or its chunk left): what is known of it goes too.</summary>
    /// <param name="coord">The chunk.</param>
    public void Forget(VoxelChunkCoord coord) => _ = _slots.Remove(coord);

    /// <summary>Whether the pipeline has a mesh recorded for the chunk (it was taken and not let go since).</summary>
    /// <param name="coord">The chunk.</param>
    public bool IsMeshed(VoxelChunkCoord coord) => _slots.TryGetValue(coord, out var slot) && slot.Meshed;

    /// <summary>The stamp of a chunk now (0 for a chunk the pipeline knows nothing of): it changes whenever the chunk's mesh would.</summary>
    /// <param name="coord">The chunk.</param>
    public int StampOf(VoxelChunkCoord coord) => _slots.TryGetValue(coord, out var slot) ? slot.Stamp : 0;

    /// <inheritdoc />
    public void OnChunkInstalled(VoxelChunkCoord coord) => _ = _slots.Remove(coord);

    /// <inheritdoc />
    public void OnChunkEvicted(VoxelChunkCoord coord) => _ = _slots.Remove(coord);

    /// <inheritdoc />
    public void OnBlockChanged(BlockChange change)
    {
        _events++;
        var p = change.Position;
        var chunk = VoxelChunkCoord.FromBlock(p.X, p.Y, p.Z);
        var (lx, ly, lz) = (p.X & (VoxelChunk.Size - 1), p.Y & (VoxelChunk.Size - 1), p.Z & (VoxelChunk.Size - 1));
        // The chunk, and each neighbour that reads the cell: across a face, over an edge or a corner (the corner shading looks at the diagonal cells).
        var (x0, x1) = (lx == 0 ? -1 : 0, lx == VoxelChunk.Size - 1 ? 1 : 0);
        var (y0, y1) = (ly == 0 ? -1 : 0, ly == VoxelChunk.Size - 1 ? 1 : 0);
        var (z0, z1) = (lz == 0 ? -1 : 0, lz == VoxelChunk.Size - 1 ? 1 : 0);
        for (var dz = z0; dz <= z1; dz++)
        {
            for (var dy = y0; dy <= y1; dy++)
            {
                for (var dx = x0; dx <= x1; dx++)
                {
                    Touch(new VoxelChunkCoord(chunk.X + dx, chunk.Y + dy, chunk.Z + dz), edited: true);
                }
            }
        }

        // The depth of a water cell reads three cells down, in the chunk above too: a cell of water that came or went in the top layers changes how that chunk draws its water.
        if (ly >= VoxelChunk.Size - 3 && (change.OldBlock == BlockId.Water || change.NewBlock == BlockId.Water))
        {
            Touch(new VoxelChunkCoord(chunk.X, chunk.Y + 1, chunk.Z), edited: true);
        }
    }

    /// <summary>
    /// One frame's work: takes the results that came back (within the budget), makes the edited chunks near the eye on the game thread, and hands the nearest chunks that want a mesh to the workers (or builds them, with no workers)
    /// until the budget is spent. Returns how many meshes it put on the card or built.
    /// </summary>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="frustum">The camera's frustum, to take what is on screen first, or <see langword="null"/>.</param>
    /// <param name="budgetMilliseconds">The most time to spend on the game thread, or <see langword="null"/> for no limit (a scripted run before its picture).</param>
    /// <param name="startedMilliseconds">The clock when the frame's work began.</param>
    /// <param name="sink">Where the chunks to put on the card go.</param>
    public int Frame(Vector3 eye, BoundingFrustum? frustum, double? budgetMilliseconds, double startedMilliseconds, IChunkMeshSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _eye = eye;
        _doneThisFrame = 0;
        _submittedThisFrame = 0;
        _inlineThisFrame = 0;
        CheckUnheardEdits();
        Drain(budgetMilliseconds, startedMilliseconds, sink);
        ProcessRebuild(budgetMilliseconds, startedMilliseconds, sink);
        ProcessWindow(_todo, ref _todoHead, stale: false, frustum, budgetMilliseconds, startedMilliseconds, sink);
        ProcessWindow(_stale, ref _staleHead, stale: true, frustum, budgetMilliseconds, startedMilliseconds, sink);
        return _doneThisFrame;
    }

    /// <summary>
    /// Asks for a chunk's grass layer to be built (the renderer decides which chunks, by their distance), with the options as they are now. Returns false when it cannot take it this frame (too many jobs in flight); with no workers the layer is built now and given to the sink.
    /// </summary>
    /// <param name="coord">The chunk.</param>
    /// <param name="options">The grass layer's options (the sky's tile and the biomes are read on the game thread: the worker gets values).</param>
    /// <param name="sink">Where the layer goes with no workers.</param>
    /// <returns>Whether the job was taken.</returns>
    public bool SubmitGrass(VoxelChunkCoord coord, GrassLayerOptions options, IChunkMeshSink sink)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sink);
        if (IsAsync && (_flying >= MaxInFlight || _submittedThisFrame >= MaxSubmitsPerFrame))
        {
            return false;
        }

        var slot = SlotOf(coord);
        if (slot.GrassJobId != 0)
        {
            return true; // one is on its way
        }

        var snapshot = Rent();
        if (!snapshot.Fill(_grid, coord, _currents, null))
        {
            Return(snapshot);
            return true; // not resident: nothing to make (taken, so the caller does not wait for it)
        }

        var job = new Job(coord, slot, Volatile.Read(ref slot.Stamp), _epoch, ++_jobIds, snapshot, default, 0, options);
        slot.GrassJobId = job.JobId;
        Dispatch(job, sink);
        return true;
    }

    /// <summary>Whether a grass job for the chunk is on its way.</summary>
    /// <param name="coord">The chunk.</param>
    public bool IsGrassFlying(VoxelChunkCoord coord) => _slots.TryGetValue(coord, out var slot) && slot.GrassJobId != 0;

    /// <summary>The slot of a chunk that has none yet is made when a job is first handed out; asking only tells whether the pipeline holds a record.</summary>
    /// <param name="coord">The chunk.</param>
    public bool Knows(VoxelChunkCoord coord) => _slots.ContainsKey(coord);

    private Slot SlotOf(VoxelChunkCoord coord)
    {
        if (!_slots.TryGetValue(coord, out var slot))
        {
            _slots[coord] = slot = new Slot { Stamp = ++_stamps };
        }

        return slot;
    }

    // A chunk's mesh may be out of date: its stamp moves and, if the pipeline has a record of it, it is listed to be built again.
    private void Touch(VoxelChunkCoord coord, bool edited)
    {
        if (!_slots.TryGetValue(coord, out var slot))
        {
            return; // never built, not in flight: it is built from the blocks as they are when its turn comes
        }

        Volatile.Write(ref slot.Stamp, ++_stamps);
        slot.Skip = false;
        slot.Edited |= edited;
        if (!slot.Queued)
        {
            slot.Queued = true;
            _rebuild.Add(coord);
        }
    }

    // Edits the grid did not tell the listener of (a fill of a column after the pipeline was made) show as edits that outnumber the events heard: every chunk whose key moved is built again, as every edit once caused.
    private void CheckUnheardEdits()
    {
        if (_grid.EditCount - _editBase == _events)
        {
            return;
        }

        _editBase = _grid.EditCount - _events;
        foreach (var (coord, slot) in _slots)
        {
            if (slot.Meshed && _tracker.IsDue(coord))
            {
                Touch(coord, edited: true);
            }
        }
    }

    // Takes the results the workers left, a frame's worth within the budget (always one, so nothing waits for ever).
    private void Drain(double? budget, double started, IChunkMeshSink sink)
    {
        while (_results.TryDequeue(out var result))
        {
            _flying--;
            _scanned++;
            Complete(result, sink);
            if (Over(budget, started))
            {
                return;
            }
        }
    }

    // The decision about one result, with the grid as it is now.
    private void Complete(ChunkMeshResult result, IChunkMeshSink sink)
    {
        if (!_slots.TryGetValue(result.Coord, out var slot))
        {
            return; // the chunk went (evicted, released) while the job ran
        }

        var latest = result.IsGrass ? slot.GrassJobId == result.JobId : slot.JobId == result.JobId;
        if (!latest)
        {
            _refused++; // a newer job for the chunk took its place (the chunk changed after this one's snapshot): this result is of older blocks
            return;
        }

        if (result.IsGrass)
        {
            slot.GrassJobId = 0;
        }
        else
        {
            slot.JobId = 0;
        }

        if (result.Failed)
        {
            slot.Skip = true; // nothing will be built from what throws; the exception is Failure
            return;
        }

        var stale = result.Skipped || result.Stamp != Volatile.Read(ref slot.Stamp) || result.Epoch != _epoch || !_grid.IsResident(result.Coord);
        if (stale)
        {
            _refused++;
            if (result.IsGrass)
            {
                sink.GrassRefused(result.Coord);
            }
            else if (!slot.Queued)
            {
                slot.Queued = true;
                _rebuild.Add(result.Coord);
            }

            return;
        }

        if (result.IsGrass)
        {
            sink.InstallGrass(result);
            _installed++;
            _doneThisFrame++;
            return;
        }

        _workerMilliseconds += result.Milliseconds;
        _longestWorkerMilliseconds = Math.Max(_longestWorkerMilliseconds, result.Milliseconds);
        if (!_wantedSet.Contains(result.Coord))
        {
            // The eye moved on while the job ran: the mesh is not wanted, and a mesh it replaces would be out of date, so the chunk is left as if never built (it is built when it is wanted again).
            if (slot.Meshed)
            {
                slot.Meshed = false;
                sink.Release(result.Coord);
            }

            return;
        }

        slot.Meshed = true;
        slot.Band = result.Band;
        sink.Install(result);
        _installed++;
        _doneThisFrame++;
        if (result.Band != Band)
        {
            _stale.Add(result.Coord); // baked in another band than the look's by the time it came back: built again after what has no mesh
        }
    }

    // The chunks that were told of a change (an edit, a light, the sky): edited ones near the eye are built at once on this thread, the rest handed on, nearest first.
    private void ProcessRebuild(double? budget, double started, IChunkMeshSink sink)
    {
        if (_rebuild.Count == 0)
        {
            return;
        }

        _rebuild.Sort(_comparer);
        var keep = 0;
        for (var i = 0; i < _rebuild.Count; i++)
        {
            var coord = _rebuild[i];
            _scanned++;
            if (!_slots.TryGetValue(coord, out var slot))
            {
                continue;
            }

            if (!_wantedSet.Contains(coord) || !_grid.TryGetChunk(coord, out var chunk) || chunk.IsEmpty)
            {
                // Not wanted any more (or emptied): a mesh it holds is out of date and goes; it is built if it is wanted again.
                slot.Queued = false;
                slot.Edited = false;
                if (slot.Meshed)
                {
                    slot.Meshed = false;
                    sink.Release(coord);
                }

                continue;
            }

            if (slot.Edited && slot.Meshed && _inlineThisFrame < MaxInlineEdits && ChunkViewSet.DistanceSquared(_eye.X, _eye.Y, _eye.Z, coord.X, coord.Y, coord.Z) <= (long)InlineEditReach * InlineEditReach
                && BuildNow(coord, slot, sink))
            {
                slot.Queued = false;
                slot.Edited = false;
                _inlineThisFrame++;
                _inline++;
                continue;
            }

            if (CanSubmit(budget, started) && Submit(coord, slot, sink) is var outcome && outcome != Outcome.Later)
            {
                slot.Queued = false;
                slot.Edited = false;
                continue;
            }

            _rebuild[keep++] = coord;
        }

        _rebuild.RemoveRange(keep, _rebuild.Count - keep);
    }

    // The nearest chunks of a list that still want a mesh: the window's in-frustum entries first, then the rest.
    private void ProcessWindow(List<VoxelChunkCoord> list, ref int head, bool stale, BoundingFrustum? frustum, double? budget, double started, IChunkMeshSink sink)
    {
        TrimHead(list, ref head, stale);
        if (head >= list.Count)
        {
            return;
        }

        var end = Math.Min(list.Count, head + Window);
        var passes = frustum is null ? 1 : 2;
        for (var pass = 0; pass < passes; pass++)
        {
            for (var i = head; i < end; i++)
            {
                var coord = list[i];
                _scanned++;
                if (!Needed(coord, stale))
                {
                    continue;
                }

                if (frustum is not null && frustum.Intersects(BoxOf(coord)) != (pass == 0))
                {
                    continue;
                }

                if (!CanSubmit(budget, started))
                {
                    TrimHead(list, ref head, stale);
                    return;
                }

                _ = Submit(coord, SlotOf(coord), sink); // a chunk whose neighbours are not all resident yet stays listed and is tried again next frame
            }
        }

        TrimHead(list, ref head, stale);
    }

    private void TrimHeads()
    {
        TrimHead(_todo, ref _todoHead, stale: false);
        TrimHead(_stale, ref _staleHead, stale: true);
    }

    // Moves the head of a list past the entries that want nothing now, and empties the list when it is all done.
    private void TrimHead(List<VoxelChunkCoord> list, ref int head, bool stale)
    {
        while (head < list.Count && !Needed(list[head], stale))
        {
            head++;
        }

        if (head >= list.Count)
        {
            list.Clear();
            head = 0;
        }
        else if (head > CompactAfter)
        {
            list.RemoveRange(0, head);
            head = 0;
        }
    }

    private bool Needed(VoxelChunkCoord coord, bool stale) => stale ? NeededStale(coord) : NeededFresh(coord);

    // A chunk that has no mesh and nothing on the way, and is not known to be nothing to build.
    private bool NeededFresh(VoxelChunkCoord coord) => !_slots.TryGetValue(coord, out var slot) || WantsFresh(slot);

    private static bool WantsFresh(Slot slot) => !slot.Meshed && slot.JobId == 0 && !slot.Queued && !slot.Skip;

    // A chunk with a mesh baked in another band than the current one and nothing on the way.
    private bool NeededStale(VoxelChunkCoord coord) => _slots.TryGetValue(coord, out var slot) && WantsBand(slot);

    private bool WantsBand(Slot slot) => slot.Meshed && slot.JobId == 0 && !slot.Queued && slot.Band != Band;

    private bool CanSubmit(double? budget, double started) =>
        !Over(budget, started) && (!IsAsync || (_flying < MaxInFlight && _submittedThisFrame < MaxSubmitsPerFrame));

    // The budget is spent, with one piece of work always done (a view fills even on a slow frame).
    private bool Over(double? budget, double started) => budget is { } b && _doneThisFrame + _submittedThisFrame > 0 && MonotonicClock.Milliseconds() - started >= b;

    private enum Outcome
    {
        Done,
        Later,
        Nothing,
    }

    // Hands a chunk to the workers, or builds it now with no workers.
    private Outcome Submit(VoxelChunkCoord coord, Slot slot, IChunkMeshSink sink)
    {
        var options = _optionsFor(coord);
        var snapshot = Rent();
        if (!snapshot.Fill(_grid, coord, _currents, options.Lights))
        {
            Return(snapshot);
            slot.Skip = true; // not resident: nothing to build (it is made when it comes)
            return Outcome.Nothing;
        }

        if (snapshot.NonAirCount == 0)
        {
            Return(snapshot);
            slot.Skip = true; // emptied: the view does not list it
            return Outcome.Nothing;
        }

        if (!snapshot.Complete)
        {
            Return(snapshot);
            return Outcome.Later;
        }

        var job = new Job(coord, slot, Volatile.Read(ref slot.Stamp), _epoch, ++_jobIds, snapshot, options, _tracker.GreyKinds, null);
        slot.JobId = job.JobId;
        Dispatch(job, sink);
        return Outcome.Done;
    }

    // Builds an edited chunk now, on this thread, and gives it to the sink: the same job as a worker's.
    private bool BuildNow(VoxelChunkCoord coord, Slot slot, IChunkMeshSink sink)
    {
        var options = _optionsFor(coord);
        var snapshot = Rent();
        if (!snapshot.Fill(_grid, coord, _currents, options.Lights) || snapshot.NonAirCount == 0 || !snapshot.Complete)
        {
            Return(snapshot);
            return false;
        }

        var job = new Job(coord, slot, Volatile.Read(ref slot.Stamp), _epoch, ++_jobIds, snapshot, options, _tracker.GreyKinds, null);
        slot.JobId = job.JobId;
        Complete(Run(job), sink);
        return true;
    }

    private void Dispatch(Job job, IChunkMeshSink sink)
    {
        _submitted++;
        _submittedThisFrame++;
        if (Pool is { Workers: > 0 } pool)
        {
            _flying++;
            var priority = ChunkViewSet.DistanceSquared(_eye.X, _eye.Y, _eye.Z, job.Coord.X, job.Coord.Y, job.Coord.Z);
            pool.Submit(JobLane.Mesh, priority, static () => false, () => _results.Enqueue(Run(job)));
            return;
        }

        // No workers: the job runs here, within the frame's budget (counted by the caller), and its result is taken at once.
        Complete(Run(job), sink);
    }

    // The job: what a worker (or the game thread with no workers) does. Reads the snapshot and the immutable inputs, nothing of the grid.
    private ChunkMeshResult Run(Job job)
    {
        var snapshot = job.Snapshot;
        var started = MonotonicClock.Milliseconds();
        var result = new ChunkMeshResult { Coord = job.Coord, Stamp = job.Stamp, Epoch = job.Epoch, JobId = job.JobId, IsGrass = job.Grass is not null, Band = job.Grass?.Band ?? job.Options.Band };
        try
        {
            if (Volatile.Read(ref job.Slot.Stamp) != job.Stamp || job.Epoch != Volatile.Read(ref _epoch))
            {
                result.Skipped = true; // stale before it began: an edit came after the snapshot
            }
            else if (job.Grass is { } grassOptions)
            {
                if (GrassMesher.Build(snapshot, job.Coord.X, job.Coord.Y, job.Coord.Z, grassOptions) is { } layer)
                {
                    result.GrassMesh = MeshPart.Of(layer.Mesh);
                    result.GrassStart = layer.QuadrantStart;
                    result.GrassCumulative = layer.Cumulative;
                }
            }
            else if (ChunkMesher.IsBuried(snapshot, job.Coord, snapshot.NonAirCount, snapshot.KindMask))
            {
                result.Buried = true;
                result.Kinds = snapshot.KindMask;
            }
            else
            {
                var meshes = ChunkMesher.Build(snapshot, job.Coord.X, job.Coord.Y, job.Coord.Z, job.Grey, job.Options);
                result.Kinds = meshes.Kinds;
                result.Opaque = MeshPart.Of(meshes.Opaque);
                result.Alpha = MeshPart.Of(meshes.Alpha);
                result.Plants = MeshPart.Of(meshes.PlantMesh);
                result.Lit = MeshPart.Of(meshes.LitMesh);
                result.Glow = MeshPart.Of(meshes.GlowMesh);
            }
        }
        catch (Exception ex)
        {
            result.Failed = true;
            _ = Interlocked.CompareExchange(ref _failure, ex, null);
        }
        finally
        {
            result.Milliseconds = MonotonicClock.Milliseconds() - started;
            Return(snapshot);
        }

        return result;
    }

    private ChunkSnapshot Rent() => _spare.TryDequeue(out var snapshot) ? snapshot : new ChunkSnapshot();

    private void Return(ChunkSnapshot snapshot)
    {
        if (_spare.Count < MaxSpareSnapshots && !_disposed)
        {
            _spare.Enqueue(snapshot);
        }
    }

    private static BoundingBox BoxOf(VoxelChunkCoord coord)
    {
        var origin = new Vector3(coord.X * VoxelChunk.Size, coord.Y * VoxelChunk.Size, coord.Z * VoxelChunk.Size);
        return new BoundingBox(origin, origin + new Vector3(VoxelChunk.Size));
    }

    private sealed class Slot
    {
        public int Stamp; // read by the workers, written by the game thread
        public long JobId; // the newest mesh job in flight, 0 for none
        public long GrassJobId;
        public bool Meshed;
        public bool Queued;
        public bool Edited;
        public bool Skip;
        public LightBand Band; // the band the mesh on the card was baked in
    }

    private sealed class Job(VoxelChunkCoord coord, Slot slot, int stamp, int epoch, long jobId, ChunkSnapshot snapshot, MeshOptions options, UInt128 grey, GrassLayerOptions? grass)
    {
        public VoxelChunkCoord Coord { get; } = coord;

        public Slot Slot { get; } = slot;

        public int Stamp { get; } = stamp;

        public int Epoch { get; } = epoch;

        public long JobId { get; } = jobId;

        public ChunkSnapshot Snapshot { get; } = snapshot;

        public MeshOptions Options { get; } = options;

        public UInt128 Grey { get; } = grey;

        public GrassLayerOptions? Grass { get; } = grass;
    }

    // Nearest to the eye first, ties by layer, row, column: the same order on every run.
    private sealed class Comparer(ChunkMeshPipeline owner) : IComparer<VoxelChunkCoord>
    {
        public int Compare(VoxelChunkCoord a, VoxelChunkCoord b)
        {
            var eye = owner._eye;
            var da = ChunkViewSet.DistanceSquared(eye.X, eye.Y, eye.Z, a.X, a.Y, a.Z);
            var db = ChunkViewSet.DistanceSquared(eye.X, eye.Y, eye.Z, b.X, b.Y, b.Z);
            return da != db ? da.CompareTo(db) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
        }
    }
}
