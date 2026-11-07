using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The blur levels, their numbers and their names.</summary>
public sealed class UnderwaterBlurSettingsTests
{
    [Fact]
    public void Given_TheDefault_When_Read_Then_ItIsLow()
    {
        Assert.Equal(UnderwaterBlur.Low, UnderwaterBlurSettings.Default);
    }

    [Fact]
    public void Given_Off_When_Mapped_Then_ThereIsNoBlur()
    {
        var off = UnderwaterBlurSettings.For(UnderwaterBlur.Off);

        Assert.False(off.Enabled);
        Assert.Equal(0f, off.Weight);
    }

    [Theory]
    [InlineData(UnderwaterBlur.Low, 2, 0.4f)]
    [InlineData(UnderwaterBlur.Medium, 4, 0.6f)]
    [InlineData(UnderwaterBlur.High, 4, 0.8f)]
    public void Given_ALevel_When_Mapped_Then_TheCopyIsThatSmallAtThatWeight(UnderwaterBlur level, int downscale, float weight)
    {
        var settings = UnderwaterBlurSettings.For(level);

        Assert.True(settings.Enabled);
        Assert.Equal(downscale, settings.Downscale);
        Assert.Equal(weight, settings.Weight);
    }

    [Fact]
    public void Given_TheLevels_When_Compared_Then_EachIsStrongerThanTheLast()
    {
        var weights = new[] { UnderwaterBlur.Off, UnderwaterBlur.Low, UnderwaterBlur.Medium, UnderwaterBlur.High }
            .Select(level => UnderwaterBlurSettings.For(level).Weight)
            .ToArray();

        Assert.Equal(weights.OrderBy(w => w), weights);
        Assert.Equal(weights.Distinct().Count(), weights.Length);
    }

    [Theory]
    [InlineData("off", UnderwaterBlur.Off)]
    [InlineData("low", UnderwaterBlur.Low)]
    [InlineData("medium", UnderwaterBlur.Medium)]
    [InlineData("high", UnderwaterBlur.High)]
    [InlineData("HIGH", UnderwaterBlur.High)]
    [InlineData(" Medium ", UnderwaterBlur.Medium)]
    public void Given_Text_When_Parsed_Then_ItIsTheLevel(string text, UnderwaterBlur expected)
    {
        Assert.Equal(expected, UnderwaterBlurSettings.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("strong")]
    [InlineData("1")]
    [InlineData(null)]
    public void Given_TextThatIsNoLevel_When_Parsed_Then_ItIsNull(string? text)
    {
        Assert.Null(UnderwaterBlurSettings.Parse(text));
    }

    [Theory]
    [InlineData(UnderwaterBlur.Low)]
    [InlineData(UnderwaterBlur.Medium)]
    [InlineData(UnderwaterBlur.High)]
    public void Given_ALevel_When_TheTapsAreDrawnOverThePicture_Then_EachCopySuppliesAnEqualShareAndThePictureTheRest(UnderwaterBlur level)
    {
        var settings = UnderwaterBlurSettings.For(level);
        var opacities = settings.TapOpacities();
        Assert.Equal(UnderwaterBlurSettings.Taps, opacities.Length);

        // Over-blend each copy in turn on a picture of weight 1 and see what each source ends up with.
        var picture = 1.0;
        var copies = new double[opacities.Length];
        for (var i = 0; i < opacities.Length; i++)
        {
            picture *= 1 - opacities[i];
            for (var j = 0; j < i; j++)
            {
                copies[j] *= 1 - opacities[i];
            }

            copies[i] = opacities[i];
        }

        Assert.Equal(1 - settings.Weight, picture, 5);
        foreach (var copy in copies)
        {
            Assert.Equal(settings.Weight / UnderwaterBlurSettings.Taps, copy, 5);
        }
    }

    [Fact]
    public void Given_Off_When_TheTapsAreAsked_Then_NoneIsVisible()
    {
        Assert.All(UnderwaterBlurSettings.For(UnderwaterBlur.Off).TapOpacities(), opacity => Assert.Equal(0f, opacity));
    }
}
