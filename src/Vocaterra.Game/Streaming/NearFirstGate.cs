using System.Globalization;

namespace Vocaterra.Game.Streaming;

/// <summary>What the near-first gate holds back while it is closed (B45).</summary>
public enum NearHold
{
    /// <summary>Nothing: the gate is never closed (a dev run that wants the old order).</summary>
    Off = 0,

    /// <summary>The ring's footprints and the far tiles of levels 0 to 3; the coarse tiles (level 4 and up) and B35's level-4 stand-ins go through. Measured (B45): no gain over no gate, because the coarse tiles and stand-ins are most of the far work the workers take during the start.</summary>
    Fine = 1,

    /// <summary>Every ring job and every far tile job, stand-ins included (the default: B45 measured the residency done at 5 s instead of 9 s, the 128-block view 1 to 3 s sooner, the sky gap 2 s longer at a start and the same after a jump).</summary>
    All = 2,
}

/// <summary>
/// The gate of far work (B45, the owner's rule that nearer chunks are more important): at the start and after a jump of the eye, the ring's footprints and the far view's tiles (the stand-ins and coarse tiles too, see <see cref="NearHold"/>) are not handed to the workers until the near view
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

    /// <summary>B47: how far the eye may go from where the gate was closed, in blocks, before the gate opens: the near view it waits for is the view of a place the eye has left (flying at 190 blocks a second the near view is never done, and every far tile, the stand-ins too, was held for the whole timeout: sky through the far land). Walking never reaches it within the timeout.</summary>
    public const double LeaveBlocks = NearViewBlocks;

    /// <summary>The longest the gate holds, in milliseconds from <see cref="Arm"/>: a near view that takes longer than this (a machine with one worker, a chunk that fails) lets the far work go on beside it.</summary>
    public const double TimeoutMilliseconds = 20_000;

    private volatile bool _closed;
    private double _armedAt;
    private double _armedX;
    private double _armedZ;
    private int _left;
    private int _armed;
    private int _timeouts;
    private double _lastHeld = double.NaN;
    private readonly List<double> _held = [];

    /// <summary>Creates a gate that is open.</summary>
    /// <param name="mode">What it holds while it is closed; <see cref="NearHold.All"/> by default, or the value of <c>VOCATERRA_NEAR_GATE</c> (<c>off</c>, <c>fine</c>, <c>all</c>) for a dev run.</param>
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

    /// <summary>How many times the eye leaving the place the gate was closed at opened it (B47).</summary>
    public int Left => Volatile.Read(ref _left);

    /// <summary>How many times the timeout opened it.</summary>
    public int Timeouts => Volatile.Read(ref _timeouts);

    /// <summary>How long the gate held the last time it opened, in milliseconds, or <see langword="null"/> before it first opened after being closed.</summary>
    public double? LastHeldMilliseconds => double.IsNaN(_lastHeld) ? null : _lastHeld;

    /// <summary>Closes the gate: a start or a jump has begun at <paramref name="now"/>. A gate already closed stays closed and its clock starts again.</summary>
    /// <param name="now">The clock in milliseconds.</param>
    /// <param name="eyeX">The eye's x in blocks (B47: where the gate is closed; with <see cref="Observe"/>'s eye it opens the gate when the eye leaves by <see cref="LeaveBlocks"/>).</param>
    /// <param name="eyeZ">The eye's z in blocks.</param>
    public void Arm(double now, double eyeX = 0, double eyeZ = 0)
    {
        _armedAt = now;
        _armedX = eyeX;
        _armedZ = eyeZ;
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
    /// <param name="eye">B47: the eye's x and z in blocks, or <see langword="null"/> for a caller that does not say (the gate then opens only when the near view is done or the time is out).</param>
    /// <returns>Whether the gate opened in this call.</returns>
    public bool Observe(double now, Func<bool> nearViewDone, (double X, double Z)? eye = null)
    {
        ArgumentNullException.ThrowIfNull(nearViewDone);
        if (!_closed)
        {
            return false;
        }

        var timedOut = now - _armedAt >= TimeoutMilliseconds;
        var left = eye is { } at && Math.Sqrt(((at.X - _armedX) * (at.X - _armedX)) + ((at.Z - _armedZ) * (at.Z - _armedZ))) >= LeaveBlocks;
        if (!timedOut && !left && !nearViewDone())
        {
            return false;
        }

        _closed = false;
        _lastHeld = now - _armedAt;
        _held.Add(_lastHeld);
        if (timedOut)
        {
            _ = Interlocked.Increment(ref _timeouts);
        }
        else if (left)
        {
            _ = Interlocked.Increment(ref _left);
        }

        return true;
    }

    /// <summary>The line for <c>--stream-stats</c>.</summary>
    public string Line => string.Create(CultureInfo.InvariantCulture, $"near gate: {Mode.ToString().ToLowerInvariant()}, closed {Armed} time(s), held [{string.Join(", ", _held.Select(static ms => $"{ms / 1000:0.0} s"))}], {Timeouts} timeout(s), {Left} left behind, {(_closed ? "closed now" : "open")}");

    private static NearHold FromEnvironment() => Environment.GetEnvironmentVariable("VOCATERRA_NEAR_GATE")?.ToLowerInvariant() switch
    {
        "off" => NearHold.Off,
        "fine" => NearHold.Fine,
        _ => NearHold.All,
    };
}
