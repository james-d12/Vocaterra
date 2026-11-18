using System.Globalization;

namespace Vocaterra.Game.Core.Reporting;

/// <summary>
/// The lengths of the frames drawn while the turn worker was busy (loading the models, or recognizing and evaluating a turn) or the
/// microphone was recording. The frame must not block on the worker, so these stay near the length of an ordinary frame; the scripted run
/// prints the summary at its end so that the claim can be read off a run.
/// </summary>
public sealed class BusyFrameStats
{
    private readonly List<double> _milliseconds = [];

    /// <summary>The number of frames recorded.</summary>
    public int Count => _milliseconds.Count;

    /// <summary>The longest frame recorded, in milliseconds (zero before any).</summary>
    public double Longest => _milliseconds.Count == 0 ? 0 : _milliseconds.Max();

    /// <summary>The median frame, in milliseconds (zero before any).</summary>
    public double Median
    {
        get
        {
            if (_milliseconds.Count == 0)
            {
                return 0;
            }

            var sorted = _milliseconds.Order().ToList();
            return sorted[sorted.Count / 2];
        }
    }

    /// <summary>The frame at a percentile (0 to 100), in milliseconds (zero before any): 99 is the frame 99 in 100 are shorter than.</summary>
    /// <param name="percent">The percentile.</param>
    public double Percentile(double percent)
    {
        if (_milliseconds.Count == 0)
        {
            return 0;
        }

        var sorted = _milliseconds.Order().ToList();
        return sorted[Math.Clamp((int)(sorted.Count * percent / 100.0), 0, sorted.Count - 1)];
    }

    /// <summary>How many frames took longer than a limit, in milliseconds.</summary>
    /// <param name="limitMilliseconds">The limit.</param>
    public int CountOver(double limitMilliseconds) => _milliseconds.Count(ms => ms > limitMilliseconds);

    /// <summary>Records the length of one frame.</summary>
    public void Record(double frameMilliseconds)
    {
        if (frameMilliseconds >= 0)
        {
            _milliseconds.Add(frameMilliseconds);
        }
    }

    /// <summary>A one-line summary: the number of frames, the median and the longest.</summary>
    public string Summary() =>
        string.Create(CultureInfo.InvariantCulture, $"{Count} frames, median {Median:0.0} ms, longest {Longest:0.0} ms");
}
