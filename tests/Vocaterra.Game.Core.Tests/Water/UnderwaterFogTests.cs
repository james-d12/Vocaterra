using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The blend of the dry fog towards the water's.</summary>
public sealed class UnderwaterFogTests
{
    // Today's fog on the high preset: the horizon haze, from 0.8 of the end (400 * 0.94 = 376) to the end.
    private static readonly FogParameters Dry = new(0.71f, 0.82f, 0.95f, 300.8f, 376f);

    [Fact]
    public void Given_AnAmountOfZero_When_Blended_Then_TheNumbersAreExactlyTheDryOnes()
    {
        Assert.Equal(Dry, UnderwaterFog.Blend(Dry, 0f));
        Assert.Equal(Dry, UnderwaterFog.Blend(Dry, -3f));
    }

    [Fact]
    public void Given_AnAmountOfOne_When_Blended_Then_TheNumbersAreTheWaters()
    {
        Assert.Equal(UnderwaterFog.Water, UnderwaterFog.Blend(Dry, 1f));
        Assert.Equal(UnderwaterFog.Water, UnderwaterFog.Blend(Dry, 7f));
        Assert.Equal(0f, UnderwaterFog.Water.Start);
        Assert.Equal(14f, UnderwaterFog.Water.End);
    }

    [Fact]
    public void Given_HalfAnAmount_When_Blended_Then_EachNumberIsHalfwayBetween()
    {
        var half = UnderwaterFog.Blend(Dry, 0.5f);
        var wet = UnderwaterFog.Water;

        Assert.Equal((Dry.Red + wet.Red) / 2, half.Red, 5);
        Assert.Equal((Dry.Green + wet.Green) / 2, half.Green, 5);
        Assert.Equal((Dry.Blue + wet.Blue) / 2, half.Blue, 5);
        Assert.Equal((Dry.Start + wet.Start) / 2, half.Start, 3);
        Assert.Equal((Dry.End + wet.End) / 2, half.End, 3);
    }

    [Fact]
    public void Given_RisingAmounts_When_Blended_Then_TheEndOnlyEverComesCloser()
    {
        var previous = float.MaxValue;
        for (var i = 0; i <= 10; i++)
        {
            var end = UnderwaterFog.Blend(Dry, i / 10f).End;
            Assert.True(end <= previous);
            previous = end;
        }
    }
}
