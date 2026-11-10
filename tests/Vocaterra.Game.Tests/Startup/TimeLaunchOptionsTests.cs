using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--time</c> launch option (E02): it holds the day clock at an hour, so a screenshot run can show any hour.</summary>
public sealed class TimeLaunchOptionsTests
{
    [Fact]
    public void Given_NoOption_When_Parsed_Then_TheClockIsNotHeld()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var options, out var error), error);

        Assert.Null(options.TimeHour);
    }

    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("5", 5.0)]
    [InlineData("7.5", 7.5)]
    [InlineData("13", 13.0)]
    [InlineData("23.25", 23.25)]
    [InlineData("24", 24.0)]
    public void Given_AnHourFrom0To24_When_Parsed_Then_TheOptionHoldsIt(string text, double expected)
    {
        Assert.True(LaunchOptions.TryParse(["--time", text], out var options, out var error), error);

        Assert.Equal(expected, options.TimeHour);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("24.5")]
    [InlineData("100")]
    [InlineData("noon")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("")]
    public void Given_AnHourOutsideTheDial_When_Parsed_Then_ItIsRefusedAndNamesTheRange(string text)
    {
        Assert.False(LaunchOptions.TryParse(["--time", text], out _, out var error));

        Assert.Contains("--time needs an hour from 0 to 24", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NoValue_When_Parsed_Then_ItIsRefused()
    {
        Assert.False(LaunchOptions.TryParse(["--time"], out _, out var error));

        Assert.Contains("--time needs a value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ADecimalCommaOrAnyCulture_When_Parsed_Then_ThePointIsTheSeparator()
    {
        Assert.False(LaunchOptions.TryParse(["--time", "7,5"], out _, out _)); // invariant culture, as the other numeric flags
        Assert.True(LaunchOptions.TryParse(["--time", "7.5", "--dusk"], out var options, out var error), error);
        Assert.Equal(7.5, options.TimeHour);
        Assert.True(options.Dusk); // both may be given: the hour wins for the clock
    }

    [Fact]
    public void Given_TheHelp_When_Read_Then_ItDescribesTheTimeAndTheDuskFlags()
    {
        Assert.Contains("--time <hour>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--dusk", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
