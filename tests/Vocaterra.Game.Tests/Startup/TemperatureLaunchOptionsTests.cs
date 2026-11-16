using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--temperature</c> launch option (I06): it holds the temperature where the player stands, in degrees Celsius, as <c>--time</c> holds the clock.</summary>
public sealed class TemperatureLaunchOptionsTests
{
    [Fact]
    public void Given_NoOption_When_Parsed_Then_TheTemperatureIsNotHeld()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var options, out var error), error);

        Assert.Null(options.TemperatureTenths);
    }

    [Theory]
    [InlineData("12", 120)]
    [InlineData("12.5", 125)]
    [InlineData("0", 0)]
    [InlineData("-5", -50)]
    [InlineData("-0.5", -5)]
    [InlineData("-12.3", -123)]
    [InlineData("12.55", 126)]
    [InlineData("100", 1000)]
    [InlineData("-100", -1000)]
    public void Given_Degrees_When_Parsed_Then_TheOptionHoldsTenthsOfADegree(string text, int tenths)
    {
        Assert.True(LaunchOptions.TryParse(["--temperature", text], out var options, out var error), error);

        Assert.Equal(tenths, options.TemperatureTenths);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-100.5")]
    [InlineData("cold")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("")]
    [InlineData("12,5")]
    public void Given_ANumberThatIsNoTemperature_When_Parsed_Then_ItIsRefusedAndNamesTheRange(string text)
    {
        Assert.False(LaunchOptions.TryParse(["--temperature", text], out _, out var error));

        Assert.Contains("--temperature needs degrees Celsius from -100 to 100", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NoValue_When_Parsed_Then_ItIsRefused()
    {
        Assert.False(LaunchOptions.TryParse(["--temperature"], out _, out var error));

        Assert.Contains("--temperature needs a value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheOptionWithTheClockAndTheWeather_When_Parsed_Then_AllThreeAreHeldAndTheUsageNamesIt()
    {
        Assert.True(LaunchOptions.TryParse(["--time", "0", "--weather", "rain", "--temperature", "-3"], out var options, out var error), error);

        Assert.Equal(0.0, options.TimeHour);
        Assert.Equal("rain", options.WeatherId);
        Assert.Equal(-30, options.TemperatureTenths);
        Assert.Contains("--temperature <n>", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
