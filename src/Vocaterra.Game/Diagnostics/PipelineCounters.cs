using System.Globalization;
using System.Text;

namespace Vocaterra.Game.Diagnostics;

/// <summary>The stages a voxel chunk passes through on its way to the screen (B43), in order.</summary>
public enum PipelineStage
{
    /// <summary>The chunk's blocks were made by the generator (a worker's column job, or the game thread with no workers); counted when the column is made, whether or not the game loop installs it.</summary>
    Generated = 0,

    /// <summary>The chunk was put in the grid (the game loop installed it): its blocks can be read.</summary>
    Resident = 1,

    /// <summary>The chunk's first mesh was built and taken by the game loop (counted at the time the worker finished it); an all-air chunk has no mesh and is never counted.</summary>
    Meshed = 2,

    /// <summary>The chunk's first mesh with something to draw has its buffers on the card (a buried chunk is meshed but never uploaded).</summary>
    GpuReady = 3,

    /// <summary>The chunk was first drawn in a frame: it has a mesh on the card and its box is inside the camera's frustum.</summary>
    Visible = 4,
}

/// <summary>
/// The voxel zone's stage counters for <c>--stream-stats</c> (B43): how many chunks reached each <see cref="PipelineStage"/>, and when, since the first frame and since the start or the last jump of the eye (an <i>episode</i>).
/// Each stage counts a chunk once while the renderer and the grid hold it (a chunk evicted and made again later counts again); its times go in fixed buckets of <see cref="BucketMilliseconds"/>, so the counters
/// allocate nothing after they are made, are safe to call from the workers (<see cref="Add"/> is lock free) and stay on in every run. A line (<see cref="Line"/>) gives each stage's count, its rate and when 50%,
/// 90% and 100% of the episode's chunks reached it; it is printed when the whole view round the eye is made, and at the end of a scripted run.
/// </summary>
public sealed class PipelineCounters
{
    /// <summary>How many stages there are.</summary>
    public const int Stages = 5;

    /// <summary>The width of a time bucket: the resolution of the times the line gives.</summary>
    public const double BucketMilliseconds = 50;

    /// <summary>How many buckets an episode has (four minutes); later events go in the last one.</summary>
    public const int Buckets = 4800;

    /// <summary>The distance in blocks that makes a move of the eye a new episode (the same as <see cref="MoveProbe.JumpBlocks"/>).</summary>
    public const float JumpBlocks = MoveProbe.JumpBlocks;

    private static readonly string[] Names = ["generated", "resident", "meshed", "gpu-ready", "visible"];

    private readonly long[] _totals = new long[Stages];
    private readonly long[] _counts = new long[Stages];
    private readonly int[] _buckets = new int[Stages * Buckets];
    private double _origin = double.NaN;
    private double _firstFrame = double.NaN;
    private bool _seen;
    private float _lastX;
    private float _lastZ;
    private float _episodeX;
    private float _episodeZ;
    private int _episodes;

    /// <summary>How many episodes have begun (the start is the first).</summary>
    public int Episodes => _episodes;

    /// <summary>The clock at the start of the episode in progress, or <see langword="null"/> before the first frame.</summary>
    public double? Origin => double.IsNaN(_origin) ? null : _origin;

    /// <summary>
    /// Notes the eye of a frame, before the frame's streaming: the first frame begins the start episode, and an eye farther than <see cref="JumpBlocks"/> from the last frame's begins a new one (a jump). Returns
    /// whether an episode began.
    /// </summary>
    /// <param name="eyeX">The eye's x in blocks.</param>
    /// <param name="eyeZ">The eye's z in blocks.</param>
    /// <param name="now">The clock in milliseconds.</param>
    public bool Track(float eyeX, float eyeZ, double now)
    {
        var jumped = !_seen || MathF.Sqrt(((eyeX - _lastX) * (eyeX - _lastX)) + ((eyeZ - _lastZ) * (eyeZ - _lastZ))) > JumpBlocks;
        (_lastX, _lastZ) = (eyeX, eyeZ);
        if (!jumped)
        {
            return false;
        }

        if (_seen)
        {
            Array.Clear(_buckets); // a jump: the new place's times count from now (a worker's add racing this clear may be lost: a diagnostic)
            Array.Clear(_counts);
        }
        else
        {
            _firstFrame = now; // the start keeps what was counted before its first frame, in the first bucket
        }

        _seen = true;
        (_episodeX, _episodeZ) = (eyeX, eyeZ);
        Volatile.Write(ref _origin, now);
        _episodes++;
        return true;
    }

    /// <summary>Counts <paramref name="count"/> chunks reaching a stage at a time (any thread).</summary>
    /// <param name="stage">The stage.</param>
    /// <param name="count">How many chunks.</param>
    /// <param name="at">The clock in milliseconds when they reached it.</param>
    public void Add(PipelineStage stage, int count, double at)
    {
        if (count <= 0)
        {
            return;
        }

        var s = (int)stage;
        _ = Interlocked.Add(ref _totals[s], count);
        _ = Interlocked.Add(ref _counts[s], count);
        var origin = Volatile.Read(ref _origin);
        var bucket = double.IsNaN(origin) ? 0 : (int)Math.Clamp(Math.Floor((at - origin) / BucketMilliseconds), 0, Buckets - 1);
        _ = Interlocked.Add(ref _buckets[(s * Buckets) + bucket], count);
    }

    /// <summary>The chunks that reached a stage since the counters were made.</summary>
    /// <param name="stage">The stage.</param>
    public long Total(PipelineStage stage) => Interlocked.Read(ref _totals[(int)stage]);

    /// <summary>The chunks that reached a stage in the episode in progress.</summary>
    /// <param name="stage">The stage.</param>
    public long Count(PipelineStage stage) => Interlocked.Read(ref _counts[(int)stage]);

    /// <summary>
    /// When the stage held <paramref name="fraction"/> of the chunks it holds now in this episode, in milliseconds from the episode's start (the end of the bucket it was reached in), or <see langword="null"/>
    /// when it holds none.
    /// </summary>
    /// <param name="stage">The stage.</param>
    /// <param name="fraction">The share, above 0 and at most 1.</param>
    public double? TimeTo(PipelineStage stage, double fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fraction);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fraction, 1);
        var s = (int)stage;
        long total = 0;
        for (var i = 0; i < Buckets; i++)
        {
            total += Volatile.Read(ref _buckets[(s * Buckets) + i]);
        }

        if (total == 0)
        {
            return null;
        }

        var target = (long)Math.Ceiling(fraction * total);
        long sum = 0;
        for (var i = 0; i < Buckets; i++)
        {
            sum += Volatile.Read(ref _buckets[(s * Buckets) + i]);
            if (sum >= target)
            {
                return (i + 1) * BucketMilliseconds;
            }
        }

        return Buckets * BucketMilliseconds;
    }

    /// <summary>
    /// The line for <c>--stream-stats</c>: the episode (the start, or the jump's place) and how long it has run, then per stage the chunks it holds, when it reached 50%, 90% and 100% of them (seconds from the
    /// episode's start), its rate over the whole episode (the chunks over the time to 100%) and over the middle of it (10% to 90%: the throughput once it runs), then the totals since the first frame.
    /// </summary>
    /// <param name="now">The clock in milliseconds.</param>
    public string Line(double now)
    {
        var text = new StringBuilder(512);
        var inv = CultureInfo.InvariantCulture;
        var label = _episodes <= 1 ? "start" : string.Create(inv, $"jump to {_episodeX:0},{_episodeZ:0}");
        _ = text.Append(inv, $"pipeline: {label}, {(double.IsNaN(_origin) ? 0 : (now - _origin) / 1000):0.0} s in:");
        for (var s = 0; s < Stages; s++)
        {
            var stage = (PipelineStage)s;
            var count = Count(stage);
            _ = text.Append(inv, $"{(s == 0 ? " " : "; ")}{Names[s]} {count}");
            if (TimeTo(stage, 0.5) is not { } half || TimeTo(stage, 0.9) is not { } most || TimeTo(stage, 1) is not { } all)
            {
                continue;
            }

            var tenth = TimeTo(stage, 0.1) ?? 0;
            var whole = count / Math.Max(all / 1000, BucketMilliseconds / 1000);
            var middle = 0.8 * count / Math.Max((most - tenth) / 1000, BucketMilliseconds / 1000);
            _ = text.Append(inv, $" (50/90/100% at {half / 1000:0.0}/{most / 1000:0.0}/{all / 1000:0.0} s, {whole:0}/s, 10-90% {middle:0}/s)");
        }

        _ = text.Append(inv, $"; since the first frame ({(double.IsNaN(_firstFrame) ? 0 : (now - _firstFrame) / 1000):0.0} s): {Total(PipelineStage.Generated)}/{Total(PipelineStage.Resident)}/{Total(PipelineStage.Meshed)}/{Total(PipelineStage.GpuReady)}/{Total(PipelineStage.Visible)}");
        return text.ToString();
    }
}
