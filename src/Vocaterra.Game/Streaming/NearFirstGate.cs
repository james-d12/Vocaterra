using System.Globalization;

namespace Vocaterra.Game.Streaming;

/// <summary>What the near-first gate holds back while it is closed (B45).</summary>
public enum NearHold
{
    /// <summary>Nothing: the gate is never closed (a dev run that wants the old order).</summary>
    Off = 0,

    /// <summary>The ring's footprints and the far tiles of levels 0 to 3; the coarse tiles (level 4 and up) and B35's level-4 stand-ins go through, so the horizon is covered while the near view is made (the default).</summary>
    Fine = 1,

    /// <summary>Every ring job and every far tile job, stand-ins included.</summary>
    All = 2,
}

/// <summary>
/// The gate of far work (B45, the owner's rule that nearer chunks are more important): at the start and after a jump of the eye, the ring's footprints and the far view's fine tiles are not handed to the workers until the near view
/// round the eye, <see cref="NearViewBlocks"/> blocks, is made (every chunk the residency wants there is resident and every chunk in view there has its mesh). Until then the workers do the residency's and the meshes' jobs only, where far
/// jobs used to take a worker for a whole tile at a time between two of theirs and the game thread's time with them (B43 measured 145 to 160 worker-seconds of far work during the start, and the near view 3 s sooner with it held).
/// The gate closes by <see cref="Arm"/> (the host's frame calls it when the eye starts an episode), opens by <see cref="Observe"/> when the near view is done or <see cref="TimeoutMilliseconds"/> have passed (a chunk that never meshes cannot
/// hold the far view for ever), and is open for a host that nobody arms (the tests, <c>--pregen</c>, a scripted run that settles). It decides nothing about order once it is open: B35's coarse first and B38's nearest first stay as they were.
/// </summary>
public sealed class NearFirstGate
{
    /// <summary>The radius of the near view the gate waits for, in blocks: eight chunks, twice what the move probe (64 blocks) times, about where the sky exposure and the first fog bands end.</summary>
    public const int NearViewBlocks = 128;

    /// <summary>The same radius in chunks.</summary>
    public const int NearViewChunks = NearViewBlocks / 16;

    /// <summary>The longest the gate holds, in milliseconds from <see cref="Arm"/>: a near view that takes longer than this (a machine with one worker, a chunk that fails) lets the far work go on beside it.</summary>
    public const double TimeoutMilliseconds = 20_000;

    private volatile bool _closed;
    private double _armedAt;
    private int _armed;
    private int _timeouts;
    private double _lastHeld = double.NaN;

    /// <summary>Creates a gate that is open.</summary>
    /// <param name="mode">What it holds while it is closed; <see cref="NearHold.Fine"/> by default, or the value of <c>VOCATERRA_NEAR_GATE</c> (<c>off</c>, <c>fine</c>, <c>all</c>) for a dev run.</param>
    public NearFirstGate(NearHold? mode = null) => Mode = mode ?? FromEnvironment();

    /// <summary>What the gate holds back while it is closed.</summary>
    public NearHold Mode { get; set; }

    /// <summary>Whether the gate is closed now: far work waits. Read from the game thread (the ring's and the far view's frame).</summary>
    public bool IsClosed => _closed && Mode != NearHold.Off;

    /// <summary>Whether the far tiles of levels 0 to 3 and the ring wait now.</summary>
    public bool HoldsFine => IsClosed;

    /// <summary>Whether the coarse tiles and the stand-ins wait too now (<see cref="NearHold.All"/>).</summary>
    public bool HoldsCoarse => _closed && Mode == NearHold.All;

    /// <summary>How many times the gate was closed (the start, and each jump).</summary>
    public int Armed => Volatile.Read(ref _armed);

    /// <summary>How many times the timeout opened it.</summary>
    public int Timeouts => Volatile.Read(ref _timeouts);

    /// <summary>How long the gate held the last time it opened, in milliseconds, or <see langword="null"/> before it first opened after being closed.</summary>
    public double? LastHeldMilliseconds => double.IsNaN(_lastHeld) ? null : _lastHeld;

    /// <summary>Closes the gate: a start or a jump has begun at <paramref name="now"/>. A gate already closed stays closed and its clock starts again.</summary>
    /// <param name="now">The clock in milliseconds.</param>
    public void Arm(double now)
    {
        _armedAt = now;
        _closed = true;
        _ = Interlocked.Increment(ref _armed);
    }

    /// <summary>Opens the gate whatever the near view holds (a scripted run that settles the whole view; a host that asks for everything).</summary>
    public void Open() => _closed = false;

    /// <summary>
    /// One frame's look at the near view while the gate is closed: opens it when the near view is done or the timeout has passed. <paramref name="nearViewDone"/> is not called while the gate is open.
    /// </summary>
    /// <param name="now">The clock in milliseconds, after the frame's meshing.</param>
    /// <param name="nearViewDone">Whether the near view is resident and meshed (asked only while closed).</param>
    /// <returns>Whether the gate opened in this call.</returns>
    public bool Observe(double now, Func<bool> nearViewDone)
    {
        ArgumentNullException.ThrowIfNull(nearViewDone);
        if (!_closed)
        {
            return false;
        }

        var timedOut = now - _armedAt >= TimeoutMilliseconds;
        if (!timedOut && !nearViewDone())
        {
            return false;
        }

        _closed = false;
        _lastHeld = now - _armedAt;
        if (timedOut)
        {
            _ = Interlocked.Increment(ref _timeouts);
        }

        return true;
    }

    /// <summary>The line for <c>--stream-stats</c>.</summary>
    public string Line => string.Create(CultureInfo.InvariantCulture, $"near gate: {Mode.ToString().ToLowerInvariant()}, closed {Armed} time(s), last held {(LastHeldMilliseconds is { } held ? $"{held / 1000:0.0} s" : "n/a")}, {Timeouts} timeout(s), {(_closed ? "closed now" : "open")}");

    private static NearHold FromEnvironment() => Environment.GetEnvironmentVariable("VOCATERRA_NEAR_GATE")?.ToLowerInvariant() switch
    {
        "off" => NearHold.Off,
        "all" => NearHold.All,
        _ => NearHold.Fine,
    };
}
