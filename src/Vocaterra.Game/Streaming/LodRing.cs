using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The ring of real chunks round the eye (B07c, the Voxy way: the far world is built from the world's own chunks): workers generate the chunks of every 32 x 32 footprint within a radius of the eye, the surface and what lies near it, nearest
/// first, and the far world's store takes them in as real level-0 sections, mipped up the levels as soon as a whole level above is real, so that trees, rivers, houses and the valley are exact out to the radius and the model only stands in beyond it. A footprint
/// already real in the store (from an earlier run, or an edit) is not made again. Each job runs on a worker in the ring's own lane, after the far tiles', and holds a few at a time; the game loop only drains results. Since B07d the game loop does not plan either:
/// the plan, which asks the store (and so the disk) about thousands of footprints, is made by a job on a worker (the review's P3), and only a pool with no worker (a test, <c>--prefetch 0</c>) plans on the caller's thread, as it always did.
/// </summary>
/// <remarks>
/// A footprint's four chunk columns are made only from the layer that holds what the far view draws (B07d, the review's R1): the generator's own surface band says which layers of a column meet the ground, and the far view draws nothing more than 24 blocks under the
/// ground over it or next to a lower column, so from two layers under the lowest band bottom of the 4 x 4 chunk columns round the footprint up is made, and what lies under is stone, as a sealed layer of the real world would show. The picture is the same (a test builds the
/// tiles from both and compares them); the work is a fraction, because the underground's caves and ores are what cost most.
/// </remarks>
public sealed class LodRing
{
    private const int MinInFlight = 8;

    // The layers under the lowest layer that meets the ground in the window that are still made: the far view draws a cave only this far under the ground over it, and a cliff face next to a lower column from its foot (that column's own band).
    private const int Margin = 2;

    private readonly LodWorld _world;
    private readonly StreamedWorld _streamed;
    private readonly JobPool _pool;
    private readonly ColumnBandCache? _bands;
    private readonly ConcurrentDictionary<long, byte> _finished = new();
    private readonly ConcurrentDictionary<long, byte> _flying = new();
    private readonly ConcurrentQueue<(long Key, bool Ok)> _results = new();
    private readonly ConcurrentQueue<List<(int Sx, int Sz, long Distance)>> _planned = new();
    private readonly Queue<(int Sx, int Sz, long Distance)> _plan = [];
    private (double X, double Z, int Radius) _plannedFor = (double.NaN, 0, 0);
    private int _inFlight;
    private int _planning;
    private long _done;
    private long _skippedLayers;
    private long _madeLayers;

    /// <summary>Creates the ring of a far world.</summary>
    /// <param name="world">The far world the chunks are ingested into.</param>
    /// <param name="streamed">The world whose chunks they are.</param>
    /// <param name="pool">The workers.</param>
    /// <param name="bands">The surface bands of the chunk columns (the residency's cache), or <see langword="null"/> to make the whole column as the ring did before B07d.</param>
    public LodRing(LodWorld world, StreamedWorld streamed, JobPool pool, ColumnBandCache? bands = null)
    {
        _world = world;
        _streamed = streamed;
        _pool = pool;
        _bands = bands;
    }

    /// <summary>How many footprints the ring has made real this run.</summary>
    public long Done => Interlocked.Read(ref _done);

    /// <summary>How many chunk layers the ring generated, and how many it did not need to (stone under the layers the far view draws).</summary>
    public (long Made, long Skipped) Layers => (Interlocked.Read(ref _madeLayers), Interlocked.Read(ref _skippedLayers));

    /// <summary>How many footprints are planned or being made, and a plan that is being made counts as one more (so the ring is not idle before it knows its work).</summary>
    public int Pending => _plan.Count + Volatile.Read(ref _inFlight) + Volatile.Read(ref _planning) + _planned.Count;

    /// <summary>Whether a plan for the last place asked about has come in (a plan made on a worker comes a frame or more after the ask).</summary>
    public bool HasPlan => !double.IsNaN(_plannedFor.X) && Volatile.Read(ref _planning) == 0 && _planned.IsEmpty;

    /// <summary>The footprints still to be made from the last plan (those not yet started), for a progress line.</summary>
    public int Waiting => _plan.Count;

    // Enough in flight to keep every worker busy while the game loop only refills a frame at a time.
    private int MaxInFlight => Math.Max(MinInFlight, _pool.Workers * 2);

    private static long KeyOf(int sx, int sz) => ((long)sx << 32) ^ (uint)sz;

    /// <summary>Whether the ring has nothing left to do within its radius.</summary>
    public bool IsIdle => Pending == 0;

    /// <summary>Whether the plan is made on a worker (the default; a dev run may turn it off to measure what it costs the game thread).</summary>
    public bool PlanOnWorker { get; set; } = true;

    /// <summary>Whether a footprint's chunk columns are made only from where the far view draws (the default; false makes the whole column, the ring as it was).</summary>
    public bool SurfaceOnly { get; set; } = true;

    /// <summary>One frame: take what the workers made, plan when the eye has moved, and keep a few jobs going.</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="radius">The radius in blocks (0 for no ring).</param>
    public void Update(double eyeX, double eyeZ, int radius)
    {
        while (_results.TryDequeue(out var result))
        {
            _ = _flying.TryRemove(result.Key, out _);
            if (result.Ok)
            {
                _finished[result.Key] = 0;
            }
        }

        while (_planned.TryDequeue(out var plan))
        {
            _plan.Clear();
            foreach (var item in plan)
            {
                _plan.Enqueue(item);
            }
        }

        if (radius <= 0)
        {
            _plan.Clear();
            return;
        }

        if (Volatile.Read(ref _planning) == 0 && (double.IsNaN(_plannedFor.X) || Math.Abs(eyeX - _plannedFor.X) >= LodSection.Size || Math.Abs(eyeZ - _plannedFor.Z) >= LodSection.Size || radius != _plannedFor.Radius))
        {
            _plannedFor = (eyeX, eyeZ, radius);
            if (_pool.Workers == 0 || !PlanOnWorker)
            {
                var plan = Plan(eyeX, eyeZ, radius);
                _plan.Clear();
                foreach (var item in plan)
                {
                    _plan.Enqueue(item);
                }
            }
            else
            {
                _ = Interlocked.Exchange(ref _planning, 1);
                _pool.Submit(JobLane.Ring, 0, static () => false, () =>
                {
                    try
                    {
                        _planned.Enqueue(Plan(eyeX, eyeZ, radius));
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref _planning, 0);
                    }
                });
            }
        }

        while (Volatile.Read(ref _inFlight) < MaxInFlight && _plan.TryDequeue(out var next))
        {
            var (sx, sz) = (next.Sx, next.Sz);
            var key = KeyOf(sx, sz);
            if (_finished.ContainsKey(key) || !_flying.TryAdd(key, 0))
            {
                continue; // made, or being made, since the plan was
            }

            _ = Interlocked.Increment(ref _inFlight);
            _pool.Submit(JobLane.Ring, next.Distance, static () => false, () =>
            {
                var ok = false;
                try
                {
                    Ingest(sx, sz);
                    ok = true;
                }
                finally
                {
                    _results.Enqueue((key, ok)); // a footprint that failed is planned again by the next plan; the pool keeps the failure
                    _ = Interlocked.Decrement(ref _inFlight);
                }
            });
        }
    }

    // The footprints within the radius that are not made yet and not in flight, nearest first: a plan is made again from what is left, so a move never drops a footprint that is still wanted. On a worker (or the caller's thread with no worker):
    // it asks the store about each footprint, and a store with a disk reads a file for the ones it does not hold in memory.
    private List<(int Sx, int Sz, long Distance)> Plan(double eyeX, double eyeZ, int radius)
    {
        var found = new List<(int Sx, int Sz, long Distance)>();
        var span = (radius / LodSection.Size) + 1;
        var (cx, cz) = ((int)Math.Floor(eyeX / LodSection.Size), (int)Math.Floor(eyeZ / LodSection.Size));
        var lowest = _world.SyRange(0).Min;
        for (var dz = -span; dz <= span; dz++)
        {
            for (var dx = -span; dx <= span; dx++)
            {
                var (sx, sz) = (cx + dx, cz + dz);
                var (ex, ez) = (((sx + 0.5) * LodSection.Size) - eyeX, ((sz + 0.5) * LodSection.Size) - eyeZ);
                var distance = (long)Math.Sqrt((ex * ex) + (ez * ez));
                var key = KeyOf(sx, sz);
                if (distance > radius || _finished.ContainsKey(key) || _flying.ContainsKey(key))
                {
                    continue;
                }

                if (_world.Store.IsWhollyReal(new LodSectionKey(0, sx, lowest, sz)))
                {
                    _finished[key] = 0; // made in an earlier run
                    continue;
                }

                found.Add((sx, sz, distance));
            }
        }

        found.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        return found;
    }

    /// <summary>The lowest chunk layer a footprint's columns are made from: two layers under the lowest layer that meets the ground in the 4 x 4 chunk columns round it (so a cliff face next to a lower column is made from its foot); a column a region owns, or no bands, makes the whole column.</summary>
    /// <param name="sx">The footprint's x.</param>
    /// <param name="sz">The footprint's z.</param>
    public int LowestLayerOf(int sx, int sz) => LowestLayerOf(sx, sz, null);

    /// <summary>The same layer, with the bands that are not held made from the shaped window of a group (B39) when one is given.</summary>
    /// <param name="sx">The footprint's x.</param>
    /// <param name="sz">The footprint's z.</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    public int LowestLayerOf(int sx, int sz, ColumnGroup? group)
    {
        var floor = _streamed.Source.MinChunkY;
        if (_bands is null || !SurfaceOnly)
        {
            return floor;
        }

        var lowest = int.MaxValue;
        for (var dz = -1; dz <= 2; dz++)
        {
            for (var dx = -1; dx <= 2; dx++)
            {
                var (cx, cz) = ((2 * sx) + dx, (2 * sz) + dz);
                if (_streamed.Regions.ColumnOwner(cx, cz) is not null)
                {
                    return floor; // a region's chunks are its own; its columns and their neighbours are made whole
                }

                var band = _bands.Get(cx, cz, group);
                if (!band.IsEmpty)
                {
                    lowest = Math.Min(lowest, band.MinLayer);
                }
            }
        }

        return lowest == int.MaxValue ? floor : Math.Max(floor, lowest - Margin);
    }

    // The shaped window a footprint is made from (B39), once for everything it reads: the 4 x 4 chunk columns round the footprint's own 2 x 2 when a band of the ring round it is not held yet (the footprint's bands and its four columns are all cut from it),
    // or the own 2 x 2 alone when they all are (or the bands are not read). A window is about 0.5 MB for the 4 x 4 and 0.25 MB for the 2 x 2, and a job holds one while it runs.
    private ColumnGroup ShapeFor(int sx, int sz)
    {
        if (_bands is not null && SurfaceOnly)
        {
            for (var dz = -1; dz <= 2; dz++)
            {
                for (var dx = -1; dx <= 2; dx++)
                {
                    if ((dx is < 0 or > 1 || dz is < 0 or > 1) && !_bands.TryGet((2 * sx) + dx, (2 * sz) + dz, out _))
                    {
                        return _streamed.Bands.ShapeGroup((2 * sx) - 1, (2 * sz) - 1, 4);
                    }
                }
            }
        }

        return _streamed.Bands.ShapeGroup(2 * sx, 2 * sz, 2);
    }

    /// <summary>The four chunk columns of a footprint, the layers the far view draws, and the lowest layer made (what <see cref="Ingest"/> takes in): the footprint's bands and columns all come from one shaped window.</summary>
    /// <param name="sx">The footprint's x.</param>
    /// <param name="sz">The footprint's z.</param>
    public (IReadOnlyList<ChunkData>[] Columns, int First) MakeColumns(int sx, int sz)
    {
        var max = _streamed.Source.MaxChunkY - 1;
        var group = ShapeFor(sx, sz);
        var first = LowestLayerOf(sx, sz, group);
        var columns = new IReadOnlyList<ChunkData>[4];
        for (var k = 0; k < 4; k++)
        {
            columns[k] = _streamed.GenerateLayers((2 * sx) + (k & 1), (2 * sz) + (k >> 1), first, max, group);
        }

        return (columns, first);
    }

    // On a worker: the four chunk columns of a footprint, the layers the far view draws, into real level-0 sections.
    private void Ingest(int sx, int sz)
    {
        var source = _streamed.Source;
        var (min, max) = (source.MinChunkY, source.MaxChunkY - 1);
        var (columns, first) = MakeColumns(sx, sz);
        _ = Interlocked.Add(ref _madeLayers, 4L * (max - first + 1));
        _ = Interlocked.Add(ref _skippedLayers, 4L * (first - min));
        _world.IngestStack(sx, sz, (cx, cy, cz) =>
        {
            var column = columns[((cz - (2 * sz)) << 1) | (cx - (2 * sx))];
            var index = cy - first;
            return (uint)index < (uint)column.Count ? column[index] : null;
        }, solidBelowChunkY: first);
        _ = Interlocked.Increment(ref _done);
    }
}
