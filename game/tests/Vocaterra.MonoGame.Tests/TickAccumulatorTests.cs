using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;

namespace Vocaterra.MonoGame.Tests;

public sealed class TickAccumulatorTests
{
    private static readonly int Rate = GameSimulation.TicksPerSecond;

    [Fact]
    public void AFrameOfExactlyOneTickRunsExactlyOneTickForever()
    {
        var accumulator = new TickAccumulator(Rate);
        var total = 0;
        for (var i = 0; i < 6000; i++)
        {
            Assert.Equal(1, accumulator.Advance(1.0 / Rate));
            total++;
        }

        Assert.Equal(6000, total);
        Assert.Equal(0, accumulator.DroppedTicks);
    }

    [Fact]
    public void ASlowFrameCatchesUpAndAFastFrameWaits()
    {
        var accumulator = new TickAccumulator(Rate);
        Assert.Equal(2, accumulator.Advance(2.0 / Rate));
        Assert.Equal(0, accumulator.Advance(0.4 / Rate));
        Assert.Equal(0, accumulator.Advance(0.4 / Rate));
        Assert.Equal(1, accumulator.Advance(0.4 / Rate)); // 1.2 ticks accumulated in three frames
    }

    [Fact]
    public void AFastDisplayStillAveragesTheTickRate()
    {
        var accumulator = new TickAccumulator(Rate);
        var total = 0;
        for (var i = 0; i < 240; i++)
        {
            total += accumulator.Advance(1.0 / 240); // four frames per tick
        }

        Assert.Equal(Rate, total);
    }

    [Fact]
    public void CatchUpIsCappedAndTheSurplusIsDroppedNotReplayed()
    {
        var accumulator = new TickAccumulator(Rate, maxStepsPerFrame: 5);
        Assert.Equal(5, accumulator.Advance(1.0)); // 60 ticks due
        Assert.Equal(55, accumulator.DroppedTicks);
        Assert.Equal(0, accumulator.Advance(0.0)); // nothing left to run
        Assert.Equal(1, accumulator.Advance(1.0 / Rate));
    }

    [Fact]
    public void AHugePauseDoesNotOverflowOrSpiral()
    {
        var accumulator = new TickAccumulator(Rate);
        Assert.Equal(accumulator.MaxStepsPerFrame, accumulator.Advance(double.MaxValue));
        Assert.Equal(1, accumulator.Advance(1.0 / Rate));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NegativeAndNonFiniteTimeCountsAsZero(double seconds)
    {
        var accumulator = new TickAccumulator(Rate);
        Assert.Equal(0, accumulator.Advance(seconds));
        Assert.Equal(0.0, accumulator.Alpha);
    }

    [Fact]
    public void AlphaIsTheFractionOfATickStillPending()
    {
        var accumulator = new TickAccumulator(Rate);
        Assert.Equal(1, accumulator.Advance(1.25 / Rate));
        Assert.Equal(0.25, accumulator.Alpha, 6);
        Assert.InRange(accumulator.Alpha, 0.0, 1.0);
    }

    [Fact]
    public void ResetForgetsPendingTimeAndDroppedTicks()
    {
        var accumulator = new TickAccumulator(Rate);
        accumulator.Advance(1.0);
        accumulator.Reset();
        Assert.Equal(0, accumulator.DroppedTicks);
        Assert.Equal(0.0, accumulator.Alpha);
    }

    [Fact]
    public void ARateOrCapBelowOneIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickAccumulator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickAccumulator(60, 0));
    }
}
