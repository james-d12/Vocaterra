using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Language.Book;

// The book's speaker buttons (S20b) over the committed speech folder: every catalogue noun and every phrase resolves to a recording, the NPC lines that
// speak the heard-only phrases, no button on an unmet noun, the click, Enter and Shift+Enter, the mute, and the sound log's marks of the plays.
public sealed class BookSpeechTests
{
    private static readonly Lazy<PhraseSheet> Sheet = new(() => TestGame.Data.Phrases);
    private static readonly Lazy<IReadOnlyList<SpeechLine>> Lines = new(() => SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, Sheet.Value, TestGame.Data.Npcs));
    private static readonly Lazy<string> Folder = new(() => Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName));

    // The phrases that have no recording of their own on purpose: none (every phrase plays its own file or an NPC line).
    private static readonly string[] PhrasesWithoutARecording = [];

    private static readonly long Now = GuideSession.DefaultStartedAt;

    private static BookRecordings Committed()
    {
        var store = new SpeechStore(Folder.Value, Lines.Value);
        return new BookRecordings(Lines.Value.Select(static l => l.LineId).Where(store.Contains), Sheet.Value);
    }

    private static string Definite(string gender, string lemma) => gender == "m" ? "el" : "la";

    [Fact]
    public void EveryCatalogueNounResolvesToItsWordCard()
    {
        var recordings = Committed();
        foreach (var noun in TestGame.Nouns.Nouns)
        {
            var card = recordings.ForNoun(noun.Lemma);
            Assert.NotNull(card);
            Assert.Equal(SpeechFiles.CardId(noun.Lemma), card);
            Assert.True(File.Exists(Path.Combine(Folder.Value, SpeechFiles.CardsFolder, card + ".wav")), $"{card}.wav");
            Assert.True(File.Exists(Path.Combine(Folder.Value, SpeechFiles.CardsFolder, card + ".slow.wav")), $"{card}.slow.wav");
        }

        Assert.Equal("card.rio", recordings.ForNoun("río")); // the file name is plain ASCII; the card's text keeps the accent
        Assert.Null(recordings.ForNoun("vaso")); // a noun outside the catalogue has no card
    }

    [Fact]
    public void EveryPhraseResolvesToARecordingOrIsListedAsHavingNone()
    {
        var recordings = Committed();
        foreach (var row in Sheet.Value.Rows)
        {
            var recording = recordings.ForPhrase(row.Id);
            if (PhrasesWithoutARecording.Contains(row.Id))
            {
                Assert.Null(recording);
                continue;
            }

            Assert.True(recording is not null, $"the phrase '{row.Id}' has no recording");
            Assert.True(new SpeechStore(Folder.Value, Lines.Value).Contains(recording), $"{recording}.wav");
        }
    }

    [Fact]
    public void APhraseWithItsOwnFilePlaysItAndAHeardOnlyPhraseePlaysTheNpcLineThatSpeaksIt()
    {
        var recordings = Committed();
        Assert.Equal("phrase.pedir-agua", recordings.ForPhrase("pedir-agua"));
        Assert.Equal("phrase.hola", recordings.ForPhrase("hola"));

        // The seven phrases the learner only hears (no file of their own): the NPC line the phrase sheet names, with the same words.
        var heard = new (string Phrase, string Line)[]
        {
            ("que-necesitas", "shop_need.1"),
            ("quieres-pan", "shop_offer.1"),
            ("me-llamo", "guide.lesson.name.1"),
            ("es-madera", "guide.lesson.is_wood.1"),
            ("quieres-fruta", "guide.lesson.offer.1"),
            ("de-nada", "guide.lesson.welcome.1"),
            ("necesitas-madera", "guide.lesson.task.1"),
        };
        foreach (var (phrase, line) in heard)
        {
            Assert.Equal(line, recordings.ForPhrase(phrase));
            Assert.Equal(Sheet.Value.Find(phrase)!.Spanish, Lines.Value.Single(l => l.LineId == line).Text);
        }
    }

    [Fact]
    public void AnUnmetNounHasNoButtonAndASeenOneHasItsCard()
    {
        var words = new WordProgress(TestGame.Nouns);
        var none = BookPages.Nouns(TestGame.Data.Locale, words, [], Now, Definite, Sheet.Value, TestGame.Data.Words.Unseen, Committed());
        Assert.All(none.Where(static l => l.Lemma is not null), static l => Assert.Null(l.Speak)); // ¿? rows: nothing is given away
        Assert.DoesNotContain(none, static l => l.Text == TestGame.Data.Words.Unseen); // an unmet noun is a grey tile now: no text, no pips, nothing to give away
        Assert.DoesNotContain(none, static l => l.Lemma is not null); // and no entry at all in a new game

        words.MarkHeard("madera");
        var seen = BookPages.Nouns(TestGame.Data.Locale, words, [], Now, Definite, Sheet.Value, TestGame.Data.Words.Unseen, Committed());
        var row = seen.Single(static l => l.Lemma == "madera" && l.Speak is not null);
        Assert.Equal("card.madera", row.Speak);
        Assert.Equal(1, seen.Count(static l => l.Speak is not null)); // no other noun is seen
    }

    [Fact]
    public void ANounUsedOutsideTheCatalogueHasNoButton()
    {
        var record = new LearnerRecord();
        record.Append(new InputEntry(1, 1, GuideSession.DefaultStartedAt + 1, InputSource.Voice, "fue el vaso", "guide", "guide-open", InputPlace.Dialogue, null, "ask_directions", true, false, [],
            [new GuideWord("vaso", new TextSpan(7, 4), "vaso", "noun", WordRole.Word, null, "sg", null, null, "m")]));
        var words = new WordProgress(TestGame.Nouns);
        var lines = BookPages.Nouns(TestGame.Data.Locale, words, record.Nouns, Now, Definite, Sheet.Value, TestGame.Data.Words.Unseen, Committed());
        Assert.Contains(lines, static l => l.Lemma == "vaso");
        Assert.All(lines, static l => Assert.Null(l.Speak));
    }

    [Fact]
    public void TheVerbsPageHasNoButtonsAndThePhrasesPageHasOneForEveryPhrase()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var verbs = BookPages.ForPage(session, BookPage.Verbs, Committed());
        Assert.All(verbs, static l => Assert.Null(l.Speak));

        var phrases = BookPages.ForPage(session, BookPage.Phrases, Committed());
        Assert.Equal(Sheet.Value.Rows.Count - PhrasesWithoutARecording.Length, phrases.Count(static l => l.Speak is not null)); // every phrase row with a recording, the met and the unmet
    }

    [Fact]
    public void TheGeometryPlacesEachButtonAtItsRowAndHitsItOnly()
    {
        var geometry = new BookGeometry(1280, 720);
        var lines = new List<BookLine>
        {
            new(BookTone.Known, "madera", Speak: "card.madera"),
            new(BookTone.Hint, "counts"),
            new(BookTone.Known, "agua", Speak: "card.agua"),
        };
        var first = BookSpeakers.ButtonRect(geometry, geometry.ContentTop);
        Assert.Equal(0, BookSpeakers.At(geometry, lines, 0, first.X + 2, first.Y + 2));
        Assert.Null(BookSpeakers.At(geometry, lines, 0, geometry.ContentLeft + 4, geometry.ContentTop + 2)); // the text is not a button
        var third = BookSpeakers.ButtonRect(geometry, geometry.ContentTop + (2 * BookGeometry.LineHeight));
        Assert.Equal(1, BookSpeakers.At(geometry, lines, 0, third.X + 2, third.Y + 2)); // the second speakable row
        Assert.Null(BookSpeakers.At(geometry, lines, 1, first.X + 2, first.Y + 2)); // scrolled off: no button there
        Assert.Equal(new[] { 0, 2 }, BookSpeakers.Speakable(lines));
    }

    [Fact]
    public void TheSelectionStartsOnTheFirstRowAndMovesAndStaysInside()
    {
        var model = new BookModel();
        model.Open(new WordProgress(TestGame.Nouns));
        Assert.Equal(-1, model.SelectedIndex(0));
        Assert.Equal(0, model.SelectedIndex(3));
        model.MoveSelection(1, 3);
        model.MoveSelection(1, 3);
        model.MoveSelection(1, 3);
        Assert.Equal(2, model.SelectedIndex(3)); // the last row, not past it
        model.MoveSelection(-5, 3);
        Assert.Equal(0, model.SelectedIndex(3));

        model.Reveal(line: 30, visible: 10);
        Assert.Equal(21, model.Offset); // the row is the last one on screen
        model.Reveal(line: 5, visible: 10);
        Assert.Equal(5, model.Offset);
    }

    [Fact]
    public void AClickEnterAndShiftEnterPlayTheNormalAndTheSlowRecording()
    {
        var bench = new Bench(Lines.Value, muted: false);
        bench.Session.Words.MarkHeard("agua");
        bench.Session.Words.MarkHeard("madera");
        var geometry = new BookGeometry(1280, 720);

        // The catalogue is in lemma order, so the seen nouns are agua then madera. Enter plays the first (agua); Down moves to madera and Shift+Enter
        // plays its slow recording.
        Assert.True(bench.Controls.Enter(slow: false));
        Assert.Equal(["card.agua"], bench.Played());
        bench.Controls.Move(1, geometry.LinesVisible);
        Assert.True(bench.Controls.Enter(slow: true));
        Assert.Equal(["card.agua", "card.madera.slow"], bench.Played());

        // A click on the button of agua plays it normally; a Shift-click on the button of madera plays it slow; a click on the text plays nothing.
        var rows = BookSpeakers.Speakable(bench.Controls.Lines);
        var agua = BookSpeakers.ButtonRect(geometry, geometry.ContentTop + (rows[0] * BookGeometry.LineHeight));
        var madera = BookSpeakers.ButtonRect(geometry, geometry.ContentTop + (rows[1] * BookGeometry.LineHeight));
        Assert.True(bench.Controls.Click(geometry, agua.X + 2, agua.Y + 2, slow: false));
        Assert.True(bench.Controls.Click(geometry, madera.X + 2, madera.Y + 2, slow: true));
        Assert.False(bench.Controls.Click(geometry, geometry.ContentLeft + 4, geometry.ContentTop + 2, slow: false));
        Assert.Equal(["card.agua", "card.madera.slow", "card.agua", "card.madera.slow"], bench.Played());

        // The sound log shows each play as a mark: the voice ducks the other sounds through the mixer, as any voice line does.
        Assert.Equal(
            ["speak card.agua", "speak card.madera slow", "speak card.agua", "speak card.madera slow"],
            bench.Log.Where(static l => l.Contains(" speak ", StringComparison.Ordinal)).Select(static l => l[(l.IndexOf("speak", StringComparison.Ordinal))..]));
    }

    [Fact]
    public void AnUnmetNounPlaysNothingAndTheMuteKeepsEveryVoiceSilent()
    {
        var bench = new Bench(Lines.Value, muted: false);
        Assert.False(bench.Controls.Enter(slow: false)); // no noun is seen yet, so no row has a button
        Assert.Empty(bench.Played());

        var muted = new Bench(Lines.Value, muted: true);
        muted.Session.Words.MarkHeard("madera");
        Assert.False(muted.Controls.Enter(slow: false));
        Assert.Empty(muted.Played());
        Assert.DoesNotContain(muted.Log, static l => l.Contains("speak", StringComparison.Ordinal));
    }

    [Fact]
    public void AScriptedPlayOpensThePageOfTheTargetAndPlaysItAsEnterDoes()
    {
        var bench = new Bench(Lines.Value, muted: false);
        bench.Session.Words.MarkHeard("madera");
        Assert.Equal("book: play card.madera", bench.Controls.PlayTarget("madera", slow: false));
        Assert.Equal(BookPage.Nouns, bench.Book.Page);
        Assert.Equal(["card.madera"], bench.Played());

        Assert.Equal("book: play phrase.pedir-agua (slow)", bench.Controls.PlayTarget("phrase:pedir-agua", slow: true));
        Assert.Equal(BookPage.Phrases, bench.Book.Page);
        Assert.Equal(["card.madera", "phrase.pedir-agua.slow"], bench.Played());

        Assert.Equal("book: no recording for 'vaso'", bench.Controls.PlayTarget("vaso", slow: false));
        Assert.Equal(["card.madera", "phrase.pedir-agua.slow"], bench.Played());
    }

    // ---- U07: the right page's speaker buttons, the entries' selection and the rows' clicks

    [Fact]
    public void AClickOnASpeakerButtonOfTheRightPagePlaysItsRecordingNormalOrSlowAndLeavesTheSelectionWhereItWas()
    {
        var bench = new Bench(Lines.Value, muted: false, shippedLessons: true);
        bench.Book.Show(BookPage.Lessons, animate: false);
        var geometry = new BookGeometry(1280, 720);
        var before = bench.Book.SelectedIndex(bench.Controls.Lines.Count);

        // The layer says where it drew the buttons (a lesson's phrases are wrapped and scrolled on the right page, so the controls cannot compute them).
        bench.Book.SetRightButtons(
        [
            new BookRightButton(new BookRect(geometry.DetailRight - BookSpeakers.ButtonSize, geometry.ContentTop + 200, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize), "phrase.pedir-agua"),
            new BookRightButton(new BookRect(geometry.DetailRight - BookSpeakers.ButtonSize, geometry.ContentTop + 219, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize), "card.agua"),
        ]);
        var first = bench.Book.RightButtons[0].Rect;
        var second = bench.Book.RightButtons[1].Rect;

        Assert.True(bench.Controls.Click(geometry, first.X + 3, first.Y + 3, slow: false));
        Assert.True(bench.Controls.Click(geometry, second.X + 3, second.Y + 3, slow: true));
        Assert.False(bench.Controls.Click(geometry, first.X - 40, first.Y + 3, slow: false)); // beside the button
        Assert.False(bench.Controls.Click(geometry, first.X + 3, first.Y + 40, slow: false)); // under it
        Assert.Equal(["phrase.pedir-agua", "card.agua.slow"], bench.Played());
        Assert.Equal(before, bench.Book.SelectedIndex(bench.Controls.Lines.Count));
        Assert.Equal(["speak phrase.pedir-agua", "speak card.agua slow"], bench.Log.Where(static l => l.Contains(" speak ", StringComparison.Ordinal)).Select(static l => l[(l.IndexOf("speak", StringComparison.Ordinal))..]));

        bench.Book.SetRightButtons([]); // the page has none now (another page, or a leaf turning)
        Assert.False(bench.Controls.Click(geometry, first.X + 3, first.Y + 3, slow: false));
    }

    [Fact]
    public void ALessonsPhrasesAreDetailsWithTheirSpeakersAndTheLeftListHasNoSpeakerButton()
    {
        var ids = Sheet.Value.Rows.Where(row => Committed().ForPhrase(row.Id) is not null).Take(2).Select(static row => row.Id).ToList();
        Assert.Equal(2, ids.Count);
        var done = new LessonRecordRow(
            "first-words", "First words with Luna", "english", LessonStatus.Completed, 5,
            [new LessonUnitRow("u1-a", StepOutcome.Done)], 2, 3, 2, 1, 0, ids, "You can greet.", ["u1-a"]);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [done], Sheet.Value, Committed());
        var phrases = lines.Where(static l => l.Kind == BookLineKind.Detail && l.Speak is not null).ToList();

        Assert.Equal(ids.Select(id => Committed().ForPhrase(id)), phrases.Select(static l => l.Speak));
        Assert.All(phrases, static l => Assert.NotNull(l.Aside)); // each with its English
        var list = BookPages.InListOnly(lines);
        Assert.Empty(BookSpeakers.Speakable(list)); // none in the left page: the buttons are on the right, with hit tests of their own
        Assert.Equal([2], BookSpeakers.Selectable(list)); // the all-done note and its gap come first; the one lesson is the entry
    }

    [Fact]
    public void EnterPlaysTheSelectedEntrysRecordingAndNothingForAnEntryWithNone()
    {
        var bench = new Bench(Lines.Value, muted: false, shippedLessons: true);
        bench.Book.Show(BookPage.Lessons, animate: false);
        Assert.False(bench.Controls.Enter(slow: false)); // the lessons' rows have no recording: the selected entry is a lesson
        Assert.Empty(bench.Played());

        bench.Book.Show(BookPage.Nouns, animate: false);
        bench.Session.Words.MarkHeard("madera");
        Assert.True(bench.Controls.Enter(slow: false)); // the only entry is madera
        Assert.Equal(["card.madera"], bench.Played());
    }

    [Fact]
    public void AClickOnAnEntrysRowSelectsItAndAClickOnASectionOrAGapDoesNot()
    {
        var bench = new Bench(Lines.Value, muted: true, shippedLessons: true);
        bench.Book.Show(BookPage.Nouns, animate: false);
        bench.Session.Words.MarkHeard("agua");
        bench.Session.Words.MarkHeard("madera");
        var geometry = new BookGeometry(1280, 720);
        var lines = bench.Controls.Lines;
        Assert.Equal(BookLineKind.Section, lines[0].Kind);

        Assert.False(bench.Controls.ClickRow(geometry, geometry.ContentLeft + 4, geometry.ContentTop + 2)); // the band
        Assert.True(bench.Controls.ClickRow(geometry, geometry.ContentLeft + 4, geometry.ContentTop + BookGeometry.LineHeight + 2)); // the first entry
        Assert.Equal(0, bench.Book.SelectedIndex(2));
        Assert.True(bench.Controls.ClickRow(geometry, geometry.ContentLeft + 4, geometry.ContentTop + (2 * BookGeometry.LineHeight) + 2));
        Assert.Equal(1, bench.Book.SelectedIndex(2));
        Assert.False(bench.Controls.ClickRow(geometry, geometry.ContentLeft + 4, geometry.ContentTop + (3 * BookGeometry.LineHeight) + 2)); // the gap
    }

    [Fact]
    public void TheButtonsOfTheListsRowsAfterASectionAreWhereTheirRowsAre()
    {
        var bench = new Bench(Lines.Value, muted: true);
        bench.Session.Words.MarkHeard("agua");
        bench.Book.Show(BookPage.Nouns, animate: false);
        var geometry = new BookGeometry(1280, 720);
        var lines = bench.Controls.Lines;
        var agua = lines.ToList().FindIndex(static l => l.Lemma == "agua");
        Assert.Equal(1, agua); // under the Learning band: a section is one row
        var button = BookSpeakers.ButtonRect(geometry, geometry.ContentTop + (agua * BookGeometry.LineHeight));
        Assert.True(bench.Controls.Click(geometry, button.X + 2, button.Y + 2, slow: false));
        Assert.Equal(0, bench.Book.SelectedIndex(1));
    }

    // ---- "Practise again" (S26)

    [Fact]
    public void OnlyACompletedLessonHasAPractiseAgainButtonAtTheRightEdgeOfItsHeading()
    {
        var bench = new Bench(Lines.Value, muted: true, shippedLessons: true);
        bench.Book.Show(BookPage.Lessons);
        Assert.All(bench.Controls.Lines, static l => Assert.Null(l.Practise)); // nothing is completed yet

        Assert.True(bench.Session.Lessons.Start("first-words"));
        Assert.All(bench.Controls.Lines, static l => Assert.Null(l.Practise)); // under way is not completed
        while (bench.Session.Lessons.Active is not null)
        {
            _ = bench.Session.Lessons.Skip();
        }

        var lines = bench.Controls.Lines;
        var heading = lines.Select((line, index) => (line, index)).Single(static x => x.line.Practise is not null);
        Assert.Equal("first-words", heading.line.Practise);
        Assert.Equal("Welcome", heading.line.Text); // the short title: the lessons page lists one row per lesson
        Assert.Single(lines, static l => l.Practise is not null); // Personas is not completed

        var geometry = new BookGeometry(1280, 720);
        var top = geometry.ContentTop + (heading.index * BookGeometry.LineHeight);
        var button = BookPractise.ButtonRect(geometry, top);
        Assert.Equal(geometry.ContentRight, button.Right);
        Assert.Equal("first-words", BookPractise.At(geometry, lines, 0, button.X + 3, button.Y + 3));
        Assert.Null(BookPractise.At(geometry, lines, 0, geometry.ContentLeft + 4, top + 2)); // the title is no button
        Assert.Null(BookPractise.At(geometry, lines, heading.index + 1, button.X + 3, button.Y + 3)); // scrolled off: no button there
        Assert.Equal("first-words", BookPractise.FirstOnScreen(geometry, lines, 0));
        Assert.Null(BookPractise.FirstOnScreen(geometry, lines, heading.index + 1));
    }

    [Fact]
    public void AClickOnPractiseAgainAsksForTheReplayAndAClickElsewhereDoesNot()
    {
        var bench = new Bench(Lines.Value, muted: true, shippedLessons: true);
        bench.Book.Show(BookPage.Lessons);
        Assert.True(bench.Session.Lessons.Start("first-words"));
        while (bench.Session.Lessons.Active is not null)
        {
            _ = bench.Session.Lessons.Skip();
        }

        var geometry = new BookGeometry(1280, 720);
        Assert.Null(bench.Controls.ClickPractise(geometry, geometry.ContentLeft + 4, geometry.ContentTop + 2));
        Assert.Null(bench.Session.Lessons.ReplayPending);

        var button = BookPractise.ButtonRect(geometry, geometry.ContentTop);
        Assert.Equal("first-words", bench.Controls.ClickPractise(geometry, button.X + 3, button.Y + 3));
        Assert.Equal("first-words", bench.Session.Lessons.ReplayPending);
        Assert.Equal("Practise again: talk to the guide to start “Welcome”", bench.Session.ObjectiveHud()[0].Text);

        Assert.Null(bench.Controls.Practise("personas")); // never completed
        Assert.Equal("first-words", bench.Controls.PractiseFirst(geometry)); // the P key asks for the first lesson on screen
    }

    [Fact]
    public void ThePractiseAgainLabelIsEnglishAndTheButtonFitsTheContent()
    {
        var geometry = new BookGeometry(800, 600);
        var button = BookPractise.ButtonRect(geometry, geometry.ContentTop);

        Assert.Equal("Practise again", BookPractise.Label(TestGame.Data.Locale));
        Assert.True(button.X > geometry.ContentLeft + 200); // the heading keeps most of the row
        Assert.True(button.Right <= geometry.ContentRight && button.Bottom <= geometry.ContentTop + BookGeometry.LineHeight);
    }

    // A session, a book and the controls over the committed speech folder, with a fake sound device and a sound log.
    private sealed class Bench
    {
        private readonly List<string> _voiceLog = [];

        public Bench(IReadOnlyList<SpeechLine> lines, bool muted, bool shippedLessons = false)
        {
            var bundle = TestGame.Bundle;
            Session = shippedLessons ? RouteScene.NewSessionWithShippedLessons(bundle, TestGame.Nouns) : RouteScene.NewSession(bundle, TestGame.Nouns);
            Book = new BookModel();
            Log = [];
            var mixer = new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(42), new ListLog(Log));
            var voice = new NpcVoice(Folder.Value, new SpeechIndex(lines), new FakeDevice(), muted, _voiceLog.Add);
            Speaker = new BookSpeaker(voice, mixer);
            Controls = new BookSpeechControls(Book, Speaker, Session, Committed());
            Book.Open(Session.Words);
        }

        public VoxelSession Session { get; }

        public BookModel Book { get; }

        public BookSpeaker Speaker { get; }

        public BookSpeechControls Controls { get; }

        public List<string> Log { get; }

        // The line ids the voice started, from its log ("voice: <id>").
        public List<string> Played() => _voiceLog.Where(static l => l.StartsWith("voice: ", StringComparison.Ordinal)).Select(static l => l["voice: ".Length..]).ToList();

        private sealed class ListLog(List<string> lines) : ISoundLog
        {
            public void Write(string line) => lines.Add(line);
        }

        private sealed class FakeDevice : IAudioOutput
        {
            private bool _playing;

            public bool Available => true;

            public bool IsPlaying => _playing;

            public bool Start(AudioBuffer clip)
            {
                _playing = true;
                return true;
            }

            public void Stop() => _playing = false;

            public void Dispose()
            {
            }
        }
    }
}
