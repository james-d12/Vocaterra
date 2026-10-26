using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Utilities;
using Vocaterra.Game.Input;
using Vocaterra.Game.Screens;
using Vocaterra.Game.UI;
using Vocaterra.Game.UI.Book;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Screens;

/// <summary>The book's behaviour: it takes the keyboard while open, J and Esc close it, and the recap's phrases page waits for a live run.</summary>
public sealed class BookScreenTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    [Theory]
    [InlineData(Keys.J)]
    [InlineData(Keys.Escape)]
    public void TheBookTakesTheKeyboardWhileOpenAndJOrEscClosesIt(Keys key)
    {
        var (screen, book, focus, _) = Make(scripted: false);
        screen.Open();
        Assert.True(book.IsOpen);
        Assert.True(focus.BookOpen);

        screen.HandleKeys(Pressed(key));
        Assert.False(book.IsOpen);
        Assert.False(focus.BookOpen);
    }

    [Fact]
    public void TheRecapOpensThePhrasesPageAndClosesTheDialogueButNotInAScriptedRun()
    {
        var (live, book, _, closed) = Make(scripted: false);
        live.ShowPhrases();
        Assert.True(book.IsOpen);
        Assert.Equal(BookPage.Phrases, book.Page);
        Assert.Equal(1, closed.Count);

        var (scripted, scriptedBook, _, scriptedClosed) = Make(scripted: true);
        scripted.ShowPhrases();
        Assert.False(scriptedBook.IsOpen);
        Assert.Equal(0, scriptedClosed.Count);
    }

    private static (BookScreen Screen, BookModel Book, InputFocus Focus, Counter Closed) Make(bool scripted)
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var book = new BookModel();
        var focus = new InputFocus();
        var mixer = new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1));
        var voice = new NpcVoice(Path.GetTempPath(), new SpeechIndex([]), new SilentOutput(), muted: true, _ => { });
        var controls = new BookSpeechControls(book, new BookSpeaker(voice, mixer), session, new BookRecordings([], session.Phrases));
        var closed = new Counter();
        var screen = new BookScreen(book, controls, focus, session, null, null, 0, scripted, new ToastQueue(), new SoundFrontend(mixer, new SoundEventMapper()), new UiCanvas(100, static () => (1280, 720)), () => closed.Count++, _ => { });
        return (screen, book, focus, closed);
    }

    private static InputSnapshot Pressed(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(), false, false, 0);

    private sealed class Counter
    {
        public int Count { get; set; }
    }

    private sealed class SilentOutput : IAudioOutput
    {
        public bool Available => false;

        public bool IsPlaying => false;

        public bool Start(Vocaterra.Nlp.Core.Audio.AudioBuffer clip) => false;

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
