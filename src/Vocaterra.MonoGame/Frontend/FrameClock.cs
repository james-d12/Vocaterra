using System.Diagnostics;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The front-end's clock: the real time since the previous frame. The simulation never sees it, only the ticks it produces.</summary>
public interface IFrameClock
{
    /// <summary>The seconds since the previous call (the first call returns zero).</summary>
    double NextFrameSeconds();
}

/// <summary>The real clock, from a <see cref="Stopwatch"/> (monotonic, unaffected by wall-clock changes).</summary>
public sealed class RealFrameClock : IFrameClock
{
    private long _last = Stopwatch.GetTimestamp();

    /// <inheritdoc />
    public double NextFrameSeconds()
    {
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(_last, now).TotalSeconds;
        _last = now;
        return seconds;
    }
}

/// <summary>A clock that reports exactly one tick of time per frame, for the scripted screenshot run (the same code path, no real time).</summary>
public sealed class FixedFrameClock : IFrameClock
{
    private readonly double _seconds;

    /// <summary>Creates a clock that reports 1 / <paramref name="ticksPerSecond"/> seconds per frame.</summary>
    public FixedFrameClock(int ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        _seconds = 1.0 / ticksPerSecond;
    }

    /// <inheritdoc />
    public double NextFrameSeconds() => _seconds;
}
