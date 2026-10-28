using Vocaterra.Game.Diagnostics;

namespace Vocaterra.Game.Tests.Graphics;

/// <summary>The scripted run's frame timings: every draw is timed, and a frame is the time from one draw's end to the next once the world has ticked.</summary>
public sealed class FrameTimingsTests
{
    [Fact]
    public void EveryDrawIsTimedAndTheFirstTicksHaveNoFrameTime()
    {
        var timings = new FrameTimings();
        timings.Record(0, 2, tick: 1);
        timings.Record(10, 13, tick: 2);
        timings.Record(20, 24, tick: 3);
        timings.Record(30, 31, tick: 4);
        Assert.Equal("4 frames, median 3.0 ms, longest 4.0 ms", timings.DrawSummary());
        Assert.Equal("2 frames, median 11.0 ms, longest 11.0 ms", timings.FrameSummary()); // 24 - 13 and 31 - 24 (the first two ticks are not timed)
    }
}
