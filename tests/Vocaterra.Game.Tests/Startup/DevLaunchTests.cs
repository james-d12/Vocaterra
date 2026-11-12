using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The start states of the developer aids (U01): the debug overlay and the dialogue's developer note start off unless <c>--dev</c> is given.</summary>
public sealed class DevLaunchTests
{
    [Fact]
    public void WithoutTheFlagTheDeveloperAidsStartOffAndWithItOn()
    {
        Assert.True(LaunchOptions.TryParse([], out var plain, out _));
        Assert.False(plain.Dev);
        Assert.True(LaunchOptions.TryParse(["--dev"], out var dev, out _));
        Assert.True(dev.Dev);
        Assert.True(LaunchOptions.TryParse(["--playtest"], out var playtest, out _));
        Assert.False(playtest.Dev); // a playtest stays clean
    }

    [Fact]
    public void Given_TheLodgingFlag_When_ItIsParsed_Then_ItNeedsDevAndIsListedInTheHelp()
    {
        Assert.True(LaunchOptions.TryParse(["--dev", "--lodging"], out var given, out _));
        Assert.True(given.Lodging);
        Assert.True(LaunchOptions.TryParse(["--dev"], out var none, out _));
        Assert.False(none.Lodging);
        Assert.False(LaunchOptions.TryParse(["--lodging"], out _, out var error));
        Assert.Contains("--dev", error, StringComparison.Ordinal);
        Assert.Contains("--lodging", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheOwnPlotFlag_When_ItIsParsed_Then_ItNeedsDevTakesAColumnAndIsListedInTheHelp()
    {
        Assert.True(LaunchOptions.TryParse(["--dev", "--own-plot", "261,160"], out var given, out _));
        Assert.Equal((261, 160), given.OwnPlot);
        Assert.True(LaunchOptions.TryParse(["--dev"], out var none, out _));
        Assert.Null(none.OwnPlot);
        Assert.False(LaunchOptions.TryParse(["--own-plot", "261,160"], out _, out var error));
        Assert.Contains("--dev", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--dev", "--own-plot", "north"], out _, out var shape));
        Assert.Contains("--own-plot", shape, StringComparison.Ordinal);
        Assert.Contains("--own-plot", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheMarkPlotFlag_When_ItIsParsed_Then_ItNeedsDevTakesAColumnAndIsListedInTheHelp()
    {
        Assert.True(LaunchOptions.TryParse(["--dev", "--mark-plot", "266,165"], out var given, out _));
        Assert.Equal((266, 165), given.MarkPlot);
        Assert.True(LaunchOptions.TryParse(["--dev"], out var none, out _));
        Assert.Null(none.MarkPlot);
        Assert.False(LaunchOptions.TryParse(["--mark-plot", "266,165"], out _, out var error));
        Assert.Contains("--dev", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--dev", "--mark-plot", "north"], out _, out var shape));
        Assert.Contains("--mark-plot", shape, StringComparison.Ordinal);
        Assert.Contains("--mark-plot", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOverlayIsOffUntilItIsSwitchedOnAndTheNoteFollowsTheSameSwitch()
    {
        Assert.False(new VoxelDebugOverlay().Visible);
        var note = new DialogueModel(new InputFocus(), "Hola", RouteScene.Shipped, showVerdictNotes: false);
        Assert.False(note.ShowVerdictNotes);
        note.ShowVerdictNotes = true; // what F3 does with the overlay (VoxelGame)
        Assert.True(note.ShowVerdictNotes);
    }

    [Fact]
    public void TheFooterStopsRepeatingHowToSpeakOnceTheSpeakTipWasShownAndKeepsTheOtherKeys()
    {
        var ui = RouteScene.Shipped.Locale;
        Assert.Contains("speak", DialoguePanel.HintsFor(ui, typing: false, speechUnavailable: false), StringComparison.Ordinal);
        var quiet = DialoguePanel.HintsFor(ui, typing: false, speechUnavailable: false, speakHintShown: true);
        Assert.DoesNotContain("speak", quiet, StringComparison.Ordinal);
        Assert.Contains("Esc", quiet, StringComparison.Ordinal);
        var typing = DialoguePanel.HintsFor(ui, typing: true, speechUnavailable: false, speakHintShown: true);
        Assert.DoesNotContain("speak", typing, StringComparison.Ordinal);
        Assert.Contains("Enter: send", typing, StringComparison.Ordinal);
    }
}
