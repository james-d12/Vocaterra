namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// Turns real frame time into whole simulation ticks. The front-end owns the clock; the simulation only ever sees "step once". Time is kept
/// in integer micro-ticks (a millionth of a tick) so that a frame of exactly 1/60 s is exactly one tick and rounding never skips or doubles
/// one. At most <see cref="MaxStepsPerFrame"/> ticks run per frame; when the front-end falls further behind (a pause, a debugger, a slow
/// start) the surplus whole ticks are dropped rather than replayed, so the game never enters a spiral of catching up.
/// </summary>
public sealed class TickAccumulator
{
    private const long MicroPerTick = 1_000_000;
    private const double MaxFrameSeconds = 3600;
    private readonly int _ticksPerSecond;
    private long _micro;

    /// <summary>Creates an accumulator for the given tick rate.</summary>
    /// <param name="ticksPerSecond">The simulation rate (<c>VoxelSimulation.TicksPerSecond</c>).</param>
    /// <param name="maxStepsPerFrame">The cap on ticks per frame.</param>
    public TickAccumulator(int ticksPerSecond, int maxStepsPerFrame = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStepsPerFrame, 1);
        _ticksPerSecond = ticksPerSecond;
        MaxStepsPerFrame = maxStepsPerFrame;
    }

    /// <summary>The most ticks <see cref="Advance"/> returns for one frame.</summary>
    public int MaxStepsPerFrame { get; }

    /// <summary>The whole ticks dropped so far because a frame was too long.</summary>
    public long DroppedTicks { get; private set; }

    /// <summary>The fraction of a tick still pending, in [0, 1): the render interpolation factor between the last two ticks.</summary>
    public double Alpha => (double)_micro / MicroPerTick;

    /// <summary>Adds a frame's elapsed real time and returns how many ticks to run now (0 to <see cref="MaxStepsPerFrame"/>).</summary>
    /// <param name="elapsedSeconds">The real time since the previous frame; negative and non-finite values count as zero.</param>
    public int Advance(double elapsedSeconds)
    {
        if (double.IsFinite(elapsedSeconds) && elapsedSeconds > 0)
        {
            _micro += (long)Math.Round(Math.Min(elapsedSeconds, MaxFrameSeconds) * _ticksPerSecond * MicroPerTick);
        }

        var due = _micro / MicroPerTick;
        var steps = (int)Math.Min(due, MaxStepsPerFrame);
        _micro -= steps * MicroPerTick;
        if (due > steps)
        {
            DroppedTicks += due - steps;
            _micro %= MicroPerTick;
        }

        return steps;
    }

    /// <summary>Forgets the pending time and the dropped count.</summary>
    public void Reset()
    {
        _micro = 0;
        DroppedTicks = 0;
    }
}
