using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--underwater-blur</c> launch option (H05).</summary>
public sealed class UnderwaterBlurLaunchOptionsTests
{
    [Fact]
    public void Given_NoOption_When_Parsed_Then_TheBlurIsLow()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var options, out _));

        Assert.Equal(UnderwaterBlur.Low, options.UnderwaterBlur);
    }

    [Theory]
    [InlineData("off", UnderwaterBlur.Off)]
    [InlineData("low", UnderwaterBlur.Low)]
    [InlineData("medium", UnderwaterBlur.Medium)]
    [InlineData("high", UnderwaterBlur.High)]
    [InlineData("High", UnderwaterBlur.High)]
    public void Given_ALevel_When_Parsed_Then_TheOptionHoldsIt(string text, UnderwaterBlur expected)
    {
        Assert.True(LaunchOptions.TryParse(["--underwater-blur", text], out var options, out var error), error);

        Assert.Equal(expected, options.UnderwaterBlur);
    }

    [Fact]
    public void Given_AnUnknownLevel_When_Parsed_Then_ItIsRefusedAndNamesTheLevels()
    {
        Assert.False(LaunchOptions.TryParse(["--underwater-blur", "strong"], out _, out var error));

        Assert.Contains("--underwater-blur needs off, low, medium or high", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheHelp_When_Read_Then_ItDescribesTheOption()
    {
        Assert.Contains("--underwater-blur <off|low|medium|high>", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
