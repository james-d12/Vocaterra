using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--weather</c> launch option (E05): it holds a weather kind of the loaded bundle, so a screenshot run can show any of them.</summary>
public sealed class WeatherLaunchOptionsTests
{
    private static readonly string[] Kinds = ["sunny", "cloudy", "rain"];

    [Fact]
    public void Given_NoOption_When_Parsed_Then_TheWeatherIsNotHeld()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var options, out var error), error);

        Assert.Null(options.WeatherId);
        Assert.True(options.CheckWeather(Kinds, out _));
    }

    [Theory]
    [InlineData("sunny")]
    [InlineData("rain")]
    [InlineData("anything-the-bundle-has")]
    public void Given_AnId_When_Parsed_Then_TheOptionHoldsItWhateverItIs(string id)
    {
        Assert.True(LaunchOptions.TryParse(["--weather", id], out var options, out var error), error);

        Assert.Equal(id, options.WeatherId);
    }

    [Fact]
    public void Given_AnIdOfTheBundle_When_Checked_Then_ItIsAccepted()
    {
        Assert.True(LaunchOptions.TryParse(["--weather", "cloudy", "--time", "13"], out var options, out var error), error);

        Assert.True(options.CheckWeather(Kinds, out var message));
        Assert.Equal(string.Empty, message);
        Assert.True(options.CheckWeather(["fog", "cloudy"], out _)); // another bundle, another ids
    }

    [Fact]
    public void Given_AnUnknownId_When_Checked_Then_ItIsRefusedAndListsTheIds()
    {
        Assert.True(LaunchOptions.TryParse(["--weather", "snow"], out var options, out var error), error);

        Assert.False(options.CheckWeather(Kinds, out var message));
        Assert.Contains("sunny, cloudy, rain", message, StringComparison.Ordinal);
        Assert.Contains("'snow'", message, StringComparison.Ordinal);
        Assert.False(options.CheckWeather(["Sunny"], out _)); // ids are exact
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Given_NoId_When_Parsed_Then_ItIsRefused(string id)
    {
        Assert.False(LaunchOptions.TryParse(["--weather", id], out _, out var error));

        Assert.Contains("--weather needs the id of a weather kind", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NoValue_When_Parsed_Then_ItIsRefused()
    {
        Assert.False(LaunchOptions.TryParse(["--weather"], out _, out var error));

        Assert.Contains("--weather needs a value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheUsageText_When_Read_Then_ItNamesTheFlagAfterTime()
    {
        var usage = LaunchOptions.Usage;

        Assert.Contains("--weather <id>", usage, StringComparison.Ordinal);
        Assert.True(usage.IndexOf("--weather <id>", StringComparison.Ordinal) > usage.IndexOf("--time <hour>", StringComparison.Ordinal));
    }
}
