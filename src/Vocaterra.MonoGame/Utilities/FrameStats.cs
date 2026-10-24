namespace Vocaterra.MonoGame.Utilities;

/// <summary>Counts frames and reports frames per second over a window of real time (half a second by default).</summary>
public sealed class FrameStats
{
    private readonly double _windowSeconds;
    private double _elapsed;
    private int _frames;

    /// <summary>Creates the counter.</summary>
    public FrameStats(double windowSeconds = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(windowSeconds, 0);
        _windowSeconds = windowSeconds;
    }

    /// <summary>The frames per second measured over the last full window (0 before the first window ends).</summary>
    public double Fps { get; private set; }

    /// <summary>Records one frame that took <paramref name="frameSeconds"/> of real time.</summary>
    public void Record(double frameSeconds)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0)
        {
            return;
        }

        _frames++;
        _elapsed += frameSeconds;
        if (_elapsed >= _windowSeconds)
        {
            Fps = _frames / _elapsed;
            _frames = 0;
            _elapsed = 0;
        }
    }
}
