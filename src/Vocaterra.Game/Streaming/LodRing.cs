using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The ring of real chunks round the eye (B07c, the Voxy way: the far world is built from the world's own chunks): workers generate the chunks of every 32 x 32 footprint within a radius of the eye, the surface and everything over and under it, nearest
/// first, and the far world's store takes them in as real level-0 sections, mipped up the levels as soon as a whole level above is real, so that trees, rivers, houses and the valley are exact out to the radius and the model only stands in beyond it. A footprint
/// already real in the store (from an earlier run, or an edit) is not made again. Each job runs on a worker in the ring's own lane, after the far tiles', and holds a few at a time; the game loop only plans.
/// </summary>
public sealed class LodRing
{
    private const int MaxInFlight = 8;

    private readonly LodWorld _world;
    private readonly StreamedWorld _streamed;
    private readonly JobPool _pool;
    private readonly HashSet<long> _finished = [];
    private readonly HashSet<long> _flying = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<(long Key, bool Ok)> _results = new();
    private readonly Queue<(int Sx, int Sz, long Distance)> _plan = [];
    private readonly List<(int Sx, int Sz, long Distance)> _found = [];
    private (double X, double Z, int Radius) _plannedFor = (double.NaN, 0, 0);
    private int _inFlight;
    private long _done;

    /// <summary>Creates the ring of a far world.</summary>
    /// <param name="world">The far world the chunks are ingested into.</param>
    /// <param name="streamed">The world whose chunks they are.</param>
    /// <param name="pool">The workers.</param>
    public LodRing(LodWorld world, StreamedWorld streamed, JobPool pool)
    {
        _world = world;
        _streamed = streamed;
        _pool = pool;
    }

    /// <summary>How many footprints the ring has made real this run.</summary>
    public long Done => Interlocked.Read(ref _done);

    /// <summary>How many footprints are planned or being made.</summary>
    public int Pending => _plan.Count + Volatile.Read(ref _inFlight);

    private static long KeyOf(int sx, int sz) => ((long)sx << 32) ^ (uint)sz;

    /// <summary>Whether the ring has nothing left to do within its radius.</summary>
    public bool IsIdle => Pending == 0;

    /// <summary>One frame: plan when the eye has moved, and keep a few jobs going.</summary>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="radius">The radius in blocks (0 for no ring).</param>
    public void Update(double eyeX, double eyeZ, int radius)
    {
        while (_results.TryDequeue(out var result))
        {
            _ = _flying.Remove(result.Key);
            if (result.Ok)
            {
                _ = _finished.Add(result.Key);
            }
        }

        if (radius <= 0)
        {
            _plan.Clear();
            return;
        }

        if (double.IsNaN(_plannedFor.X) || Math.Abs(eyeX - _plannedFor.X) >= LodSection.Size || Math.Abs(eyeZ - _plannedFor.Z) >= LodSection.Size || radius != _plannedFor.Radius)
        {
            Plan(eyeX, eyeZ, radius);
        }

        while (Volatile.Read(ref _inFlight) < MaxInFlight && _plan.TryDequeue(out var next))
        {
            _ = Interlocked.Increment(ref _inFlight);
            var (sx, sz) = (next.Sx, next.Sz);
            var key = KeyOf(sx, sz);
            _ = _flying.Add(key);
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

    // The footprints within the radius that are not made yet and not in flight, nearest first: a plan is made again from what is left, so a move never drops a footprint that is still wanted.
    private void Plan(double eyeX, double eyeZ, int radius)
    {
        _plannedFor = (eyeX, eyeZ, radius);
        _found.Clear();
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
                if (distance > radius || _finished.Contains(key) || _flying.Contains(key))
                {
                    continue;
                }

                if (_world.Store.Entry(new LodSectionKey(0, sx, lowest, sz)) is { IsWhollyReal: true })
                {
                    _ = _finished.Add(key); // made in an earlier run
                    continue;
                }

                _found.Add((sx, sz, distance));
            }
        }

        _found.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        _plan.Clear();
        foreach (var item in _found)
        {
            _plan.Enqueue(item);
        }
    }

    // On a worker: the four chunk columns of a footprint, every layer, into real level-0 sections.
    private void Ingest(int sx, int sz)
    {
        var source = _streamed.Source;
        var (min, max) = (source.MinChunkY, source.MaxChunkY - 1);
        var columns = new IReadOnlyList<ChunkData>[4];
        for (var k = 0; k < 4; k++)
        {
            columns[k] = _streamed.GenerateLayers((2 * sx) + (k & 1), (2 * sz) + (k >> 1), min, max);
        }

        _world.IngestStack(sx, sz, (cx, cy, cz) =>
        {
            var column = columns[((cz - (2 * sz)) << 1) | (cx - (2 * sx))];
            var index = cy - min;
            return (uint)index < (uint)column.Count ? column[index] : null;
        });
        _ = Interlocked.Increment(ref _done);
    }
}
