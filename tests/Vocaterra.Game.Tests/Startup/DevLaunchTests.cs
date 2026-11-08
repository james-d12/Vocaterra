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
