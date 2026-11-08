using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The scripted steps of the station (W07): <c>--station</c>, <c>--act</c> and <c>--proc-help</c>.</summary>
public sealed class StationLaunchOptionsTests
{
    [Fact]
    public void TheStationStepsKeepTheirOrderAndTheirValues()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--station", "horno", "--act", "harina, bol", "--act", "mano,horno,sacar", "--proc-help", "--shot", "b.png"], out var options, out var error), error);
        Assert.Equal(
            [(ScriptStepKind.Station, "horno"), (ScriptStepKind.Act, "harina,bol"), (ScriptStepKind.Act, "mano,horno,sacar"), (ScriptStepKind.ProcHelp, ""), (ScriptStepKind.Shot, "b.png")],
            options.Script.Select(static s => (s.Kind, s.Value)));
    }

    [Theory]
    [InlineData("harina")]
    [InlineData("a,b,c,d")]
    [InlineData("harina,")]
    [InlineData(",bol")]
    public void AnActNeedsTwoOrThreeNonEmptyParts(string act)
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--act", act], out _, out var error));
        Assert.Contains("--act", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AStationNeedsAnIdAndTheStepsNeedAScreenshot()
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--station", " "], out _, out var error));
        Assert.Contains("--station", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--station", "horno"], out _, out error));
        Assert.Contains("--screenshot", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--proc-help"], out _, out error));
        Assert.Contains("--screenshot", error, StringComparison.Ordinal);
    }
}
