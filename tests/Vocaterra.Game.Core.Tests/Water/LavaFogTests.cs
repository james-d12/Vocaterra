using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The blend of the fog round the eye towards the lava's (the view through the rock from inside lava, 2026-11-19).</summary>
public sealed class LavaFogTests
{
    private static readonly FogParameters Dry = new(0.71f, 0.82f, 0.95f, 300.8f, 376f);

    [Fact]
    public void Given_AnAmountOfZero_When_Blended_Then_TheNumbersAreExactlyTheOnesOutside()
    {
        Assert.Equal(Dry, LavaFog.Blend(Dry, 0f));
        Assert.Equal(Dry, LavaFog.Blend(Dry, -1f));
        Assert.Equal(UnderwaterFog.Water, LavaFog.Blend(UnderwaterFog.Water, 0f));
    }

    [Fact]
    public void Given_AnAmountOfOne_When_Blended_Then_TheFogIsCompleteWithinTheBlockTheEyeIsIn()
    {
        Assert.Equal(LavaFog.Lava, LavaFog.Blend(Dry, 1f));
        Assert.Equal(LavaFog.Lava, LavaFog.Blend(UnderwaterFog.Water, 4f));
        Assert.True(LavaFog.Lava.End < 1f, "the fog ends inside the lava block: no face past it shows");
        Assert.True(LavaFog.Lava.Start <= LavaFog.Lava.End);
    }

    [Fact]
    public void Given_HalfAnAmount_When_Blended_Then_EachNumberIsHalfwayBetween()
    {
        var half = LavaFog.Blend(Dry, 0.5f);
        var lava = LavaFog.Lava;

        Assert.Equal((Dry.Red + lava.Red) / 2, half.Red, 5);
        Assert.Equal((Dry.Green + lava.Green) / 2, half.Green, 5);
        Assert.Equal((Dry.Blue + lava.Blue) / 2, half.Blue, 5);
        Assert.Equal((Dry.Start + lava.Start) / 2, half.Start, 3);
        Assert.Equal((Dry.End + lava.End) / 2, half.End, 3);
    }

    [Fact]
    public void Given_RisingAmounts_When_Blended_Then_TheEndOnlyEverComesCloser()
    {
        var previous = float.MaxValue;
        for (var i = 0; i <= 10; i++)
        {
            var end = LavaFog.Blend(Dry, i / 10f).End;
            Assert.True(end <= previous);
            previous = end;
        }
    }
}
