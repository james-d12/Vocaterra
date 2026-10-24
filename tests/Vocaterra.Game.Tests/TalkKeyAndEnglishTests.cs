using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Input;
using Vocaterra.Game.Sound;
using Vocaterra.Game.UI;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.Tests;

/// <summary>
/// S17b: the talk key is named in both dialogue states (with and without speech), the game's own messages are English, and the words the learner
/// learns (<c>hola</c>, the noun <c>campamento</c>) stay Spanish. The panel's hint line is a pure function, so no window is needed.
/// </summary>
public sealed class TalkKeyAndEnglishTests
{
    [Fact]
    public void TheButtonsStateNamesTheTalkKeyWhenSpeechIsOn()
    {
        Assert.Equal("Hold V or Space: speak   Esc: cancel or close", DialoguePanel.HintsFor(typing: false, speechUnavailable: false));
        Assert.Equal(DialoguePanel.ButtonHints, DialoguePanel.HintsFor(false, false));
    }

    [Fact]
    public void TheTypingStateNamesTheTalkKeyWhenSpeechIsOn()
    {
        // V types a letter in the text box, so the typing hint gives the way back to the talk key rather than a hold that would do nothing.
        var hints = DialoguePanel.HintsFor(typing: true, speechUnavailable: false);
        Assert.Equal(DialoguePanel.KeyHints, hints);
        Assert.Contains("hold V or Space: speak", hints, StringComparison.Ordinal);
        Assert.Contains("Enter: send", hints, StringComparison.Ordinal);
    }

    [Fact]
    public void WithSpeechOffBothStatesSayToTypeTheAnswerInEnglish()
    {
        Assert.Equal(DialoguePanel.SpeechOffButtonHints, DialoguePanel.HintsFor(typing: false, speechUnavailable: true));
        Assert.StartsWith("Speech off: type your answer", DialoguePanel.HintsFor(false, true), StringComparison.Ordinal);
        Assert.StartsWith("Speech off: type your answer", DialoguePanel.HintsFor(true, true), StringComparison.Ordinal);
        Assert.DoesNotContain("hold V", DialoguePanel.HintsFor(false, true), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDialogueHintsHaveNoSpanish()
    {
        foreach (var hint in new[] { DialoguePanel.KeyHints, DialoguePanel.ButtonHints, DialoguePanel.SpeechOffButtonHints, DialoguePanel.SpeechOffKeyHints })
        {
            Assert.DoesNotMatch("Mantén|Espacio|hablar|cancelar|cerrar|enviar|botones", hint);
        }
    }

    [Fact]
    public void TheDialogueStatusWordsAreEnglish()
    {
        Assert.Equal("Listening…", DialoguePanel.ListeningText);
        Assert.Equal("Thinking…", DialoguePanel.ThinkingText);
        Assert.Equal("You (voice): ", DialoguePanel.SpokenPrefix);
        Assert.False(new DialogueModel(new InputFocus(), "Hola.").SpeechUnavailable);
    }

    [Fact]
    public void TheMicCheckScreenIsEnglishAndShowsTheWordToSay()
    {
        Assert.Equal("Microphone check", MicCheckModel.Title);
        Assert.Equal("Say \"hola\"", MicCheckModel.Instruction);
        Assert.Equal("Hold V or Space and say \"hola\"", MicCheckModel.TalkHint);
        Assert.Equal("Got it, I can hear you.", MicCheckModel.PassedText);
        Assert.Equal("I didn't catch that. Try again?", MicCheckModel.FailedText);
        Assert.Equal(["Continue", "Try again", "Skip"], new[] { MicCheckModel.ContinueLabel, MicCheckModel.RetryLabel, MicCheckModel.SkipLabel });
    }

    [Fact]
    public void TheMicCheckStatusLinesFollowThePhase()
    {
        var check = new MicCheckModel((_, _) => { });
        Assert.Equal(MicCheckModel.TalkHint, check.StatusText);
        check.BeginListening();
        Assert.Equal("Listening…", check.StatusText);
        check.EndListening();
        Assert.Equal("Thinking…", check.StatusText);
        check.Complete(new MicCheckResult("gola", false, 0.2));
        Assert.Equal(MicCheckModel.FailedText, check.StatusText);
    }

    [Fact]
    public void ANoMicrophoneMessageIsEnglishAndTheTypedFallbackIsNamed()
    {
        var talk = new PushToTalk(null);
        Assert.False(talk.Begin());
        Assert.Equal("No microphone: type your answer.", talk.Problem);
        Assert.Equal("No microphone: type your answer.", PushToTalk.NoMicrophone);
        Assert.Equal("I didn't hear anything.", PushToTalk.Silence);
    }

    [Fact]
    public void TheBeaconsDistanceIsInBlocksAndItsNounStaysSpanish()
    {
        Assert.Equal("campamento  74 blocks", BeaconArrowLayer.BeaconText("campamento", 74.4));
        Assert.Equal("74 blocks", BeaconArrowLayer.BeaconText(null, 73.6));
        Assert.Equal("río  12 blocks", BeaconArrowLayer.BeaconText("río", 12));
    }
}
