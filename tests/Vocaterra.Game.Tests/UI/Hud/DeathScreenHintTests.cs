using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>The wake card (N05) is not drawn over by the bed's prompt or the plot's line (Y14).</summary>
public sealed class DeathScreenHintTests
{
    private static readonly DeathScreenView Card = new("Estás en tu campamento.", "You are at your camp.", "You fainted from thirst.");

    [Fact]
    public void Given_TheWakeCardIsUp_When_TheGameHasASleepPrompt_Then_NoHintIsDrawn() =>
        Assert.Null(DeathScreenLayer.HintWhileUp(Card, "E: sleep until morning (session 4)"));

    [Fact]
    public void Given_NoWakeCard_When_TheGameHasAPlotLine_Then_TheLineIsKept() =>
        Assert.Equal("Your land", DeathScreenLayer.HintWhileUp(null, "Your land"));

    [Fact]
    public void Given_NoWakeCardAndNoHint_When_TheHintIsAsked_Then_ThereIsNone() =>
        Assert.Null(DeathScreenLayer.HintWhileUp(null, null));
}
