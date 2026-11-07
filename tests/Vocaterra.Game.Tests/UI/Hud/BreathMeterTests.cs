using Vocaterra.Game.Core.Water;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>When the bubble meter shows, fades and pops (<see cref="BreathMeter"/>, H04): the state the layer draws.</summary>
public sealed class BreathMeterTests
{
    private static readonly BreathReading Land900 = new(Breath.MaxAir, false);

    private static BreathReading Under(int air) => new(air, true);

    private static BreathReading Land(int air) => new(air, false);

    [Theory]
    [InlineData(900, 10)]
    [InlineData(811, 10)]
    [InlineData(810, 9)]
    [InlineData(90, 1)]
    [InlineData(89, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Given_Some_Air_When_Read_Then_The_Bubbles_Are_One_Per_Tenth(int air, int bubbles) => Assert.Equal(bubbles, Under(air).Bubbles);

    [Fact]
    public void Given_Full_Air_On_Land_When_Updated_Then_The_Meter_Is_Hidden()
    {
        var meter = new BreathMeter();

        Assert.Equal(0f, meter.Update(Land900, 0).Alpha);
    }

    [Fact]
    public void Given_The_Eye_Under_Water_When_Updated_Then_The_Meter_Is_Shown_With_Ten_Full_Bubbles()
    {
        var meter = new BreathMeter();

        var frame = meter.Update(Under(Breath.MaxAir), 0);

        Assert.Equal(1f, frame.Alpha);
        Assert.All(frame.Sizes, size => Assert.Equal(1f, size));
    }

    [Fact]
    public void Given_Air_Below_Full_On_Land_When_Updated_Then_The_Meter_Is_Shown()
    {
        var meter = new BreathMeter();

        Assert.Equal(1f, meter.Update(Land(800), 0).Alpha);
    }

    [Fact]
    public void Given_The_Air_Refills_When_Less_Than_A_Second_Passes_Then_The_Meter_Stays_Shown()
    {
        var meter = new BreathMeter();
        meter.Update(Under(100), 0);
        meter.Update(Land900, 10);

        Assert.Equal(1f, meter.Update(Land900, 10 + BreathMeter.FadeDelayTicks).Alpha);
    }

    [Fact]
    public void Given_The_Air_Refilled_When_The_Delay_Passes_Then_The_Meter_Fades_Out_And_Is_Hidden()
    {
        var meter = new BreathMeter();
        meter.Update(Under(100), 0);
        meter.Update(Land900, 10);

        var halfway = meter.Update(Land900, 10 + BreathMeter.FadeDelayTicks + (BreathMeter.FadeTicks / 2)).Alpha;
        var gone = meter.Update(Land900, 10 + BreathMeter.FadeDelayTicks + BreathMeter.FadeTicks).Alpha;

        Assert.Equal(0.5f, halfway, 3);
        Assert.Equal(0f, gone);
    }

    [Fact]
    public void Given_A_Tenth_Of_The_Air_Used_When_Updated_Then_The_Bubble_Pops_Over_Three_Frames()
    {
        var meter = new BreathMeter();
        meter.Update(Under(Breath.MaxAir), 0);

        var sizes = new[]
        {
            meter.Update(Under(810), 1).Sizes[9],
            meter.Update(Under(810), 2).Sizes[9],
            meter.Update(Under(810), 3).Sizes[9],
            meter.Update(Under(810), 4).Sizes[9],
        };

        Assert.Equal(1f, sizes[0]);
        Assert.Equal(2f / 3, sizes[1], 3);
        Assert.Equal(1f / 3, sizes[2], 3);
        Assert.Equal(0f, sizes[3]);
    }

    [Fact]
    public void Given_Two_Tenths_Lost_At_Once_When_Updated_Then_Both_Bubbles_Pop()
    {
        var meter = new BreathMeter();
        meter.Update(Under(Breath.MaxAir), 0);

        var frame = meter.Update(Under(720), 1);

        Assert.Equal(1f, frame.Sizes[8]);
        Assert.Equal(1f, frame.Sizes[9]);
        Assert.Equal(1f, frame.Sizes[7]);

        var next = meter.Update(Under(720), 2);
        Assert.True(next.Sizes[8] < 1f && next.Sizes[9] < 1f, "both popping bubbles should be shrinking");
    }

    [Fact]
    public void Given_The_First_Reading_Is_Low_When_Updated_Then_No_Bubble_Pops()
    {
        var meter = new BreathMeter();

        var frame = meter.Update(Under(300), 0);

        Assert.Equal(4, frame.Sizes.Count(size => size == 1f));
        Assert.Equal(6, frame.Sizes.Count(size => size == 0f));
    }

    [Fact]
    public void Given_Air_Under_Water_At_Full_When_Updated_Then_Nothing_Pops()
    {
        var meter = new BreathMeter();
        meter.Update(Under(Breath.MaxAir), 0);

        var frames = Enumerable.Range(1, 10).Select(tick => meter.Update(Under(Breath.MaxAir), tick)).ToList();

        Assert.All(frames, frame => Assert.All(frame.Sizes, size => Assert.Equal(1f, size)));
    }

    [Fact]
    public void Given_A_Popping_Bubble_When_The_Air_Refills_Then_The_Bubble_Is_Full_Again()
    {
        var meter = new BreathMeter();
        meter.Update(Under(Breath.MaxAir), 0);
        meter.Update(Under(810), 1);

        var frame = meter.Update(Land900, 2);

        Assert.Equal(1f, frame.Sizes[9]);
        Assert.Equal(1f, meter.Update(Land900, 3).Sizes[9]);
    }
}
