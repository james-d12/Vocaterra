using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The scripted <c>--set-time</c> and <c>--set-weather</c> steps on the command line (E03): their values, their order among the steps, and the refusals.</summary>
public sealed class ClockAndWeatherLaunchOptionsTests
{
    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        return error;
    }

    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("13", 13.0)]
    [InlineData("7.5", 7.5)]
    [InlineData("24", 24.0)]
    public void Given_AnHourFrom0To24_When_Parsed_Then_TheStepHoldsTheClockAtIt(string text, double hour)
    {
        var options = Parse("--screenshot", "x.png", "--set-time", text);

        var step = Assert.Single(options.VoxelSteps);
        Assert.Equal(VoxelStepKind.SetTime, step.Kind);
        Assert.Equal(hour, step.Hour);
    }

    [Fact]
    public void Given_RunForTheTime_When_Parsed_Then_TheStepLetsTheClockRun()
    {
        var options = Parse("--screenshot", "x.png", "--set-time", "run");

        Assert.Equal(VoxelStepKind.RunTime, Assert.Single(options.VoxelSteps).Kind);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("24.5")]
    [InlineData("noon")]
    [InlineData("NaN")]
    public void Given_AnHourOutsideTheDial_When_Parsed_Then_ItIsRefusedAndNamesTheValues(string text)
    {
        var error = Refuse("--screenshot", "x.png", "--set-time", text);

        Assert.Contains("--set-time needs an hour from 0 to 24", error, StringComparison.Ordinal);
        Assert.Contains("or 'run'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NoValueForSetTime_When_Parsed_Then_ItIsRefused()
    {
        Assert.Contains("--set-time needs a value", Refuse("--screenshot", "x.png", "--set-time"), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ASetWeatherId_When_Parsed_Then_TheStepHoldsThatKind()
    {
        var options = Parse("--screenshot", "x.png", "--set-weather", "first");

        var step = Assert.Single(options.VoxelSteps);
        Assert.Equal(VoxelStepKind.SetWeather, step.Kind);
        Assert.Equal("first", step.Item);
    }

    [Fact]
    public void Given_RunForTheWeather_When_Parsed_Then_TheStepLetsItRun()
    {
        var options = Parse("--screenshot", "x.png", "--set-weather", "run");

        Assert.Equal(VoxelStepKind.RunWeather, Assert.Single(options.VoxelSteps).Kind);
    }

    [Fact]
    public void Given_NoValueForSetWeather_When_Parsed_Then_ItIsRefused()
    {
        Assert.Contains("--set-weather needs", Refuse("--screenshot", "x.png", "--set-weather"), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheSteps_When_Parsed_Then_TheyKeepTheOrderGiven()
    {
        var options = Parse("--screenshot", "x.png", "--set-time", "13", "--set-weather", "first", "--set-weather", "run", "--set-time", "run");

        Assert.Equal(
            [VoxelStepKind.SetTime, VoxelStepKind.SetWeather, VoxelStepKind.RunWeather, VoxelStepKind.RunTime],
            options.VoxelSteps.Select(static s => s.Kind));
    }

    [Fact]
    public void Given_TheStepsWithoutAScreenshot_When_Parsed_Then_TheyAreRefusedAsTheOtherScriptSteps()
    {
        Assert.Contains("--set-time", Refuse("--set-time", "13"), StringComparison.Ordinal);
        Assert.Contains("--set-weather", Refuse("--set-weather", "first"), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AWeatherIdTheBundleLacks_When_Checked_Then_TheCheckRefusesItAndNamesTheKinds()
    {
        var options = Parse("--screenshot", "x.png", "--set-weather", "third");

        Assert.False(options.CheckWeather(["first", "second"], out var error));
        Assert.Equal("--set-weather needs a weather kind (first, second) or 'run', got 'third'.", error);
    }

    [Fact]
    public void Given_KnownIdsAndRun_When_Checked_Then_TheCheckPasses()
    {
        var options = Parse("--screenshot", "x.png", "--set-weather", "second", "--set-weather", "run", "--set-time", "13");

        Assert.True(options.CheckWeather(["first", "second"], out var error), error);
        Assert.Empty(error);
    }

    [Fact]
    public void Given_TheHelp_When_Read_Then_ItDescribesTheTwoSteps()
    {
        Assert.Contains("--set-time <hour|run>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--set-weather <id|run>", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
