using Vocaterra.Game.Graphics.Water;

namespace Vocaterra.Game.Tests.Graphics.Water;

/// <summary>Which animation frame the water shows at a tick: a pure function of the tick.</summary>
public sealed class WaterFramesTests
{
    [Fact]
    public void Given_TickZero_When_TheFrameIsAsked_Then_ItIsTheFirst()
    {
        Assert.Equal(0, WaterFrames.IndexAt(0));
    }

    [Fact]
    public void Given_TicksWithinOneHold_When_TheFrameIsAsked_Then_TheFrameDoesNotChange()
    {
        for (var tick = 0; tick < WaterFrames.TicksPerFrame; tick++)
        {
            Assert.Equal(0, WaterFrames.IndexAt(tick));
        }

        Assert.Equal(1, WaterFrames.IndexAt(WaterFrames.TicksPerFrame));
    }

    [Fact]
    public void Given_ALoopOfTicks_When_TheFrameIsAsked_Then_ItReturnsToTheStart()
    {
        var loop = WaterFrames.Count * WaterFrames.TicksPerFrame;

        Assert.Equal(0, WaterFrames.IndexAt(loop));
        Assert.Equal(WaterFrames.Count - 1, WaterFrames.IndexAt(loop - 1));
        Assert.Equal(WaterFrames.IndexAt(123), WaterFrames.IndexAt(123 + (7L * loop)));
    }

    [Fact]
    public void Given_ANegativeTick_When_TheFrameIsAsked_Then_ItStaysInRange()
    {
        Assert.InRange(WaterFrames.IndexAt(-1), 0, WaterFrames.Count - 1);
        Assert.Equal(WaterFrames.Count - 1, WaterFrames.IndexAt(-1));
    }

    [Fact]
    public void Given_AWholeLoop_When_EveryFrameIsAsked_Then_EachShowsOnce()
    {
        var seen = Enumerable.Range(0, WaterFrames.Count * WaterFrames.TicksPerFrame).Select(static t => WaterFrames.IndexAt(t)).Distinct().Order().ToList();

        Assert.Equal(Enumerable.Range(0, WaterFrames.Count), seen);
    }
}
