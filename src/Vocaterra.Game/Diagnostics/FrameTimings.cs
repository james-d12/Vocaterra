using Vocaterra.Game.Core.Reporting;

namespace Vocaterra.Game.Diagnostics;

/// <summary>The time each frame's drawing took and the time from one draw's end to the next, for the summary a scripted run prints at its end.</summary>
public sealed class FrameTimings
{
    private readonly BusyFrameStats _draws = new();
    private readonly BusyFrameStats _frames = new();
    private double _lastDrawEnd = -1;
    private readonly double _createdAt = MonotonicClock.Milliseconds();
    private readonly List<(long Tick, double Milliseconds)> _worst = [];

    /// <summary>One frame's draw, from <paramref name="drawStart"/> to <paramref name="drawEnd"/> (milliseconds); the first ticks' frames are not timed.</summary>
    public void Record(double drawStart, double drawEnd, long tick)
    {
        _draws.Record(drawEnd - drawStart);
        NoteWorst(tick, drawEnd - (_lastDrawEnd >= 0 ? _lastDrawEnd : _createdAt));
        if (_lastDrawEnd >= 0 && tick > 2)
        {
            _frames.Record(drawEnd - _lastDrawEnd);
        }

        _lastDrawEnd = drawEnd;
    }

    private void NoteWorst(long tick, double milliseconds)
    {
        _worst.Add((tick, milliseconds));
        if (_worst.Count > 64)
        {
            _worst.Sort(static (a, b) => b.Milliseconds.CompareTo(a.Milliseconds));
            _worst.RemoveRange(5, _worst.Count - 5);
        }
    }

    /// <summary>The longest gaps between one draw's end and the next (the first from the start of the game), with the simulation tick each ended at, longest first, the first frames of a run included: where a start or a jump stalled.</summary>
    public string WorstFrames()
    {
        var top = _worst.OrderByDescending(static w => w.Milliseconds).Take(5);
        return string.Join(", ", top.Select(static w => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{w.Tick}:{w.Milliseconds:0}")));
    }

    /// <summary>The frames' statistics (the end of one draw to the next).</summary>
    public BusyFrameStats Frames => _frames;

    /// <summary>The draws: the scene, the layers and the flush.</summary>
    public string DrawSummary() => _draws.Summary();

    /// <summary>The frames: the end of one draw to the next.</summary>
    public string FrameSummary() => _frames.Summary();
}
