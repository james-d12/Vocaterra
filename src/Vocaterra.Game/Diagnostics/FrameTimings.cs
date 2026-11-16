using Vocaterra.Game.Core.Reporting;

namespace Vocaterra.Game.Diagnostics;

/// <summary>The time each frame's drawing took and the time from one draw's end to the next, for the summary a scripted run prints at its end.</summary>
public sealed class FrameTimings
{
    private readonly BusyFrameStats _draws = new();
    private readonly BusyFrameStats _frames = new();
    private double _lastDrawEnd = -1;

    /// <summary>One frame's draw, from <paramref name="drawStart"/> to <paramref name="drawEnd"/> (milliseconds); the first ticks' frames are not timed.</summary>
    public void Record(double drawStart, double drawEnd, long tick)
    {
        _draws.Record(drawEnd - drawStart);
        if (_lastDrawEnd >= 0 && tick > 2)
        {
            _frames.Record(drawEnd - _lastDrawEnd);
        }

        _lastDrawEnd = drawEnd;
    }

    /// <summary>The frames' statistics (the end of one draw to the next).</summary>
    public BusyFrameStats Frames => _frames;

    /// <summary>The draws: the scene, the layers and the flush.</summary>
    public string DrawSummary() => _draws.Summary();

    /// <summary>The frames: the end of one draw to the next.</summary>
    public string FrameSummary() => _frames.Summary();
}
