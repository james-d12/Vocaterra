using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Dialogue;

/// <summary>
/// S17b: the talk key is named in both dialogue states (with and without speech), the game's own messages are English, and the words the learner
/// learns (<c>hola</c>, the noun <c>campamento</c>) stay Spanish. The panel's hint line is a pure function, so no window is needed.
/// </summary>
public sealed class TalkKeyAndEnglishTests
{
    [Fact]
    public void TheButtonsStateNamesTheTalkKeyWhenSpeechIsOn()
    {
        Assert.Equal("Hold V or Space: speak   Esc: cancel or close", DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, typing: false, speechUnavailable: false));
        Assert.Equal(DialoguePanel.ButtonHints(RouteScene.Shipped.Locale), DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, false, false));
    }

    [Fact]
    public void TheTypingStateNamesTheTalkKeyWhenSpeechIsOn()
    {
        // V types a letter in the text box, so the typing hint gives the way back to the talk key rather than a hold that would do nothing.
        var hints = DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, typing: true, speechUnavailable: false);
        Assert.Equal(DialoguePanel.KeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing), hints);
        Assert.Contains("hold V or Space: speak", hints, StringComparison.Ordinal);
        Assert.Contains("Enter: send", hints, StringComparison.Ordinal);
    }

    [Fact]
    public void WithSpeechOffBothStatesSayToTypeTheAnswerInEnglish()
    {
        Assert.Equal(DialoguePanel.SpeechOffButtonHints(RouteScene.Shipped.Locale), DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, typing: false, speechUnavailable: true));
        Assert.StartsWith("Speech off: type your answer", DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, false, true), StringComparison.Ordinal);
        Assert.StartsWith("Speech off: type your answer", DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, true, true), StringComparison.Ordinal);
        Assert.DoesNotContain("hold V", DialoguePanel.HintsFor(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing, false, true), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDialogueHintsHaveNoSpanish()
    {
        foreach (var hint in new[] { DialoguePanel.KeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing), DialoguePanel.ButtonHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffButtonHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffKeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing) })
        {
            Assert.DoesNotMatch("Mantén|Espacio|hablar|cancelar|cerrar|enviar|botones", hint);
        }
    }

    [Fact]
    public void TheDialogueStatusWordsAreEnglish()
    {
        Assert.Equal("Listening…", DialoguePanel.ListeningText(RouteScene.Shipped.Locale));
        Assert.Equal("Thinking…", DialoguePanel.ThinkingText(RouteScene.Shipped.Locale));
        Assert.Equal("You (voice): ", DialoguePanel.SpokenPrefix(RouteScene.Shipped.Locale));
        Assert.False(new DialogueModel(new InputFocus(), "Hola.", RouteScene.Shipped).SpeechUnavailable);
    }

    [Fact]
    public void TheMicCheckScreenIsEnglishAndShowsTheWordToSay()
    {
        var model = new MicCheckModel((_, _) => { }, RouteScene.Shipped.Locale, "hola");
        Assert.Equal("Microphone check", model.Title);
        Assert.Equal("Say \"hola\"", model.Instruction);
        Assert.Equal("Hold V or Space and say \"hola\"", model.TalkHint); // {key:listen} and {key:listenAlt} spelled from the bindings (C04)
        Assert.Equal("Got it, I can hear you.", model.PassedText);
        Assert.Equal("I didn't catch that. Try again?", model.FailedText);
        Assert.Equal(["Continue", "Try again", "Skip"], new[] { model.ContinueLabel, model.RetryLabel, model.SkipLabel });
    }

    [Fact]
    public void TheMicCheckStatusLinesFollowThePhase()
    {
        var check = new MicCheckModel((_, _) => { }, RouteScene.Shipped.Locale, "hola");
        Assert.Equal(check.TalkHint, check.StatusText);
        check.BeginListening();
        Assert.Equal("Listening…", check.StatusText);
        check.EndListening();
        Assert.Equal("Thinking…", check.StatusText);
        check.Complete(new MicCheckResult("gola", false, 0.2));
        Assert.Equal(check.FailedText, check.StatusText);
    }

    [Fact]
    public void ANoMicrophoneMessageIsEnglishAndTheTypedFallbackIsNamed()
    {
        var talk = new PushToTalkSpeechCapture(null);
        Assert.False(talk.Begin());
        Assert.Equal("No microphone: type your answer.", talk.Problem);
        Assert.Equal("No microphone: type your answer.", PushToTalkSpeechCapture.NoMicrophone);
        Assert.Equal("I didn't hear anything.", PushToTalkSpeechCapture.Silence);
    }

    [Fact]
    public void TheBeaconsDistanceIsInBlocksAndItsNounStaysSpanish()
    {
        Assert.Equal("campamento  74 blocks", BeaconArrowLayer.BeaconText(RouteScene.Shipped.Locale, "campamento", 74.4));
        Assert.Equal("74 blocks", BeaconArrowLayer.BeaconText(RouteScene.Shipped.Locale, null, 73.6));
        Assert.Equal("río  12 blocks", BeaconArrowLayer.BeaconText(RouteScene.Shipped.Locale, "río", 12));
    }
}
