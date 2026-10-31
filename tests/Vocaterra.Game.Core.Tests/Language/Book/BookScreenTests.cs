using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Tests.Language.Book;

/// <summary>The book's behaviour: it takes the keyboard while open, J and Esc close it, and the recap's phrases page waits for a live run.</summary>
public sealed class BookScreenTests
{
    [Theory]
    [InlineData(GameKey.J)]
    [InlineData(GameKey.Escape)]
    public void TheBookTakesTheKeyboardWhileOpenAndJOrEscClosesIt(GameKey key)
    {
        var (screen, book, focus, _) = Make(scripted: false);
        screen.Open();
        Assert.True(book.IsOpen);
        Assert.True(focus.BookOpen);

        screen.HandleInput(Pressed(key));
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
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var book = new BookModel();
        var focus = new InputFocus();
        var mixer = new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1));
        var voice = new NpcVoice(Path.GetTempPath(), new SpeechIndex([]), new SilentOutput(), muted: true, _ => { });
        var controls = new BookSpeechControls(book, new BookSpeaker(voice, mixer), session, new BookRecordings([], session.Phrases));
        var closed = new Counter();
        var screen = new BookScreen(book, controls, focus, session, null, null, 0, scripted, new ToastQueue(), new SoundFrontend(mixer, new SoundEventMapper()), Geometry, () => closed.Count++, _ => { });
        return (screen, book, focus, closed);
    }

    private static BookGeometry Geometry() => new(1280, 720); // the canvas of a 1280x720 window at 100 % text

    private static TestInputFrame Pressed(params GameKey[] keys) => TestInputFrame.Pressing(keys);

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
