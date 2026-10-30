using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Tests.Dialogue;

/// <summary>The dialogue panel's behaviour: T opens the text box and its letter is not typed, Esc undoes the newest thing first, and closing ends the talk.</summary>
public sealed class DialogueScreenTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    [Fact]
    public void TOpensTheTextBoxAndItsLetterIsSwallowedOnce()
    {
        var (screen, dialogue, _, lines) = Make();
        screen.Open("shopkeeper", 't');
        Assert.True(dialogue.IsOpen);
        Assert.True(dialogue.IsTyping);
        Assert.StartsWith("talk: shopkeeper (", Assert.Single(lines));

        screen.Type('t', alt: false); // the platform types the T that opened the panel
        screen.Type('a', alt: false);
        screen.Type('t', alt: false);
        Assert.Equal("at", dialogue.Entry.Text);
    }

    [Fact]
    public void EscEndsTheTypingFirstThenClosesThePanelAndTheTalk()
    {
        var (screen, dialogue, session, _) = Make();
        screen.Open("villager", 't');
        screen.HandleInput(Pressed(GameKey.Escape));
        Assert.True(dialogue.IsOpen);
        Assert.False(dialogue.IsTyping);

        screen.HandleInput(Pressed(GameKey.Escape));
        Assert.False(dialogue.IsOpen);
        Assert.NotEqual("villager", session.CurrentNpc); // the talk ended (the session falls back to the guide)
    }

    [Fact]
    public void LettersAreNotTypedWhileTheButtonsHaveTheKeyboard()
    {
        var (screen, dialogue, _, _) = Make();
        screen.Open("guide", 'e');
        screen.Type('e', alt: false);
        screen.Type('x', alt: false);
        Assert.False(dialogue.IsTyping);
        Assert.Equal(string.Empty, dialogue.Entry.Text);
    }

    private static (DialogueScreen Screen, DialogueModel Dialogue, VoxelSession Session, List<string> Lines) Make()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
        var voice = new NpcVoice(Path.GetTempPath(), new SpeechIndex([]), new SilentOutput(), muted: true, _ => { });
        var lines = new List<string>();
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var screen = new DialogueScreen(
            dialogue,
            session,
            voice,
            new SpeechActionRunner(session, dialogue, voice, () => null),
            new LessonFrontend(session, dialogue, _ => { }),
            new ListenSession(new PushToTalkSpeechCapture(null)),
            new OpenKeySwallow(),
            new TextEditKeys(),
            sound,
            static (_, _) => throw new InvalidOperationException("no view in this test"),
            static (_, _) => throw new InvalidOperationException("no font in this test"),
            static () => null,
            scriptedRun: true,
            lines.Add);
        return (screen, dialogue, session, lines);
    }

    private static TestInputFrame Pressed(params GameKey[] keys) => TestInputFrame.Pressing(keys);

    private sealed class SilentOutput : IAudioOutput
    {
        public bool Available => false;

        public bool IsPlaying => false;

        public bool Start(AudioBuffer clip) => false;

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
