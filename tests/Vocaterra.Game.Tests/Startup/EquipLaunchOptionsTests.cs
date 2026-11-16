using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--equip</c> launch option (I03a): a scripted step like <c>--give</c>'s that wears the item, repeatable and in the order given.</summary>
public sealed class EquipLaunchOptionsTests
{
    [Fact]
    public void Given_TwoEquipOptions_When_Parsed_Then_TheyAreTwoStepsInOrder()
    {
        Assert.True(LaunchOptions.TryParse(["--equip", "camisa", "--give", "pan", "--equip", "gorro", "--screenshot", "x.png"], out var options, out var error), error);

        Assert.Equal([(ScriptStepKind.Equip, "camisa"), (ScriptStepKind.Give, "pan"), (ScriptStepKind.Equip, "gorro")], options.Script.Select(static s => (s.Kind, s.Value)));
    }

    [Fact]
    public void Given_AnEquipWithNoItem_When_Parsed_Then_TheUsageErrorSaysSo()
    {
        Assert.False(LaunchOptions.TryParse(["--equip", " ", "--screenshot", "x.png"], out _, out var error));

        Assert.Contains("--equip needs an item", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnEquipWithoutAScreenshotRun_When_Parsed_Then_ItIsRefusedLikeTheOtherSteps()
    {
        Assert.False(LaunchOptions.TryParse(["--equip", "camisa"], out _, out var error));

        Assert.Contains("--equip", error, StringComparison.Ordinal);
        Assert.Contains("need --screenshot", error, StringComparison.Ordinal);
    }
}
