using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The air counter: drain, refill, the HUD's bubbles and what it reports.</summary>
public sealed class BreathTests
{
    private static BreathEvents Run(Breath breath, int ticks, bool under, Action<BreathEvents, int>? each = null)
    {
        var all = BreathEvents.None;
        for (var i = 0; i < ticks; i++)
        {
            var e = breath.Step(under);
            each?.Invoke(e, i);
            all |= e;
        }

        return all;
    }

    [Fact]
    public void Given_AFullBreath_When_UnderWaterFor15Seconds_Then_TheAirIsOutOnTheLastTick()
    {
        var breath = new Breath();

        Run(breath, 899, true);
        Assert.Equal(1, breath.Air);
        Assert.False(breath.IsEmpty);

        var last = breath.Step(true);

        Assert.Equal(0, breath.Air);
        Assert.True(breath.IsEmpty);
        Assert.Equal(BreathEvents.OutOfAir, last);
    }

    [Fact]
    public void Given_AFullBreath_When_InTheOpen_Then_NothingHappens()
    {
        var breath = new Breath();

        Assert.Equal(BreathEvents.None, Run(breath, 100, false));
        Assert.True(breath.IsFull);
    }

    [Fact]
    public void Given_AnEmptyBreath_When_InTheOpen_Then_ItIsFullAgainInAboutTwoSecondsAndReportsRestoredOnce()
    {
        var breath = new Breath();
        breath.Restore(0);
        var restored = new List<int>();

        Run(breath, 200, false, (e, i) =>
        {
            if (e == BreathEvents.Restored)
            {
                restored.Add(i + 1);
            }
        });

        Assert.Equal([113], restored);
        Assert.True(breath.IsFull);
    }

    [Theory]
    [InlineData(900, 10)]
    [InlineData(899, 10)]
    [InlineData(811, 10)]
    [InlineData(810, 9)]
    [InlineData(90, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Given_SomeAir_When_BubblesAreCounted_Then_OneGoesWithEachTenth(int air, int bubbles)
    {
        var breath = new Breath();
        breath.Restore(air);

        Assert.Equal(bubbles, breath.Bubbles);
    }

    [Fact]
    public void Given_AirAtZeroUnderWater_When_Stepping_Then_OutOfAirComesEverySecond()
    {
        var breath = new Breath();
        breath.Restore(1);
        var ticks = new List<int>();

        Run(breath, 200, true, (e, i) =>
        {
            if (e == BreathEvents.OutOfAir)
            {
                ticks.Add(i + 1);
            }
        });

        Assert.Equal([1, 61, 121, 181], ticks);
    }

    [Fact]
    public void Given_AirAtZero_When_TheBodySurfaces_Then_NoMoreOutOfAirAndTheCadenceStartsAgain()
    {
        var breath = new Breath();
        breath.Restore(1);
        Run(breath, 30, true);

        Assert.Equal(BreathEvents.None, Run(breath, 1, false));
        Assert.Equal(BreathEvents.None, Run(breath, 20, false) & BreathEvents.OutOfAir);
        breath.Restore(0);
        var first = new List<int>();
        Run(breath, 130, true, (e, i) =>
        {
            if (e == BreathEvents.OutOfAir)
            {
                first.Add(i + 1);
            }
        });
        Assert.Equal([60, 120], first);
    }

    [Fact]
    public void Given_ABreathBelowFull_When_ItFills_Then_RestoredIsReportedAndOnlyThen()
    {
        var breath = new Breath();
        Run(breath, 10, true);

        var events = Run(breath, 5, false);

        Assert.Equal(BreathEvents.Restored, events);
        Assert.Equal(BreathEvents.None, Run(breath, 5, false));
    }

    [Fact]
    public void Given_AnAmountOutOfRange_When_Restored_Then_ItIsClamped()
    {
        var breath = new Breath();

        breath.Restore(5000);
        Assert.Equal(Breath.MaxAir, breath.Air);
        breath.Restore(-4);
        Assert.Equal(0, breath.Air);
    }
}
