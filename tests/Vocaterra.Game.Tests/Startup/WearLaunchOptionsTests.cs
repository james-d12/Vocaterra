using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--wear</c> and <c>--repair</c> launch options (I07): scripted steps in the order given.</summary>
public sealed class WearLaunchOptionsTests
{
    [Fact]
    public void Given_WearAndRepairOptions_When_Parsed_Then_TheyAreStepsInOrder()
    {
        Assert.True(LaunchOptions.TryParse(["--wear", "hacha=200", "--equip", "camisa", "--wear", "camisa=120", "--repair", "hacha", "--screenshot", "x.png"], out var options, out var error), error);

        Assert.Equal(
            [ScriptStepKind.Wear, ScriptStepKind.Equip, ScriptStepKind.Wear, ScriptStepKind.Move],
            options.Script.Select(static s => s.Kind));
        Assert.Equal(["hacha=200", "camisa", "camisa=120"], options.Script.Where(static s => s.Kind != ScriptStepKind.Move).Select(static s => s.Value));
    }

    [Theory]
    [InlineData("hacha")]
    [InlineData("hacha=")]
    [InlineData("=5")]
    [InlineData("hacha=-5")]
    [InlineData("hacha=lots")]
    public void Given_ABadWear_When_Parsed_Then_TheUsageErrorSaysSo(string value)
    {
        Assert.False(LaunchOptions.TryParse(["--wear", value, "--screenshot", "x.png"], out _, out var error));

        Assert.Contains("--wear needs an item and a wear", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ARepairWithNoItem_When_Parsed_Then_TheUsageErrorSaysSo()
    {
        Assert.False(LaunchOptions.TryParse(["--repair", " ", "--screenshot", "x.png"], out _, out var error));

        Assert.Contains("--repair needs an item", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_WearWithoutAScreenshotRun_When_Parsed_Then_ItIsRefusedLikeTheOtherSteps()
    {
        Assert.False(LaunchOptions.TryParse(["--wear", "hacha=3"], out _, out var error));

        Assert.Contains("need --screenshot", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheHelp_When_ItIsRead_Then_ItListsWearAndRepair()
    {
        Assert.Contains("--wear <item>=<n>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--repair <item>", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
