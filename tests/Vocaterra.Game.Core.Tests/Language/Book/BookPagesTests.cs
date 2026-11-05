using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Core.Tests.Language.Book;

// What each page of the book lists for a fixed record (S20): built from history entries folded by the learner's record, and from fixed lesson rows.
public sealed class BookPagesTests
{
    private static readonly Lazy<PhraseSheet> Sheet = new(() => TestGame.Data.Phrases);
    private static readonly Lazy<LocaleText> English = new(() => TestGame.Data.Locale);
    private static readonly Lazy<VerbGlosses> Glosses = new(() => TestGame.Data.Glosses);

    private static InputEntry Entry(long id, string text, IReadOnlyList<GuideWord> words, InputSource source = InputSource.Voice, string? npc = "guide",
        InputPlace place = InputPlace.Dialogue, string? unit = null, bool communicated = true, IReadOnlyList<GuideError>? errors = null, string intent = "ask_directions") =>
        new(id, 1, GuideSession.DefaultStartedAt + id, source, text, npc, "guide-open", place, unit, intent, communicated, false, errors ?? [], words);

    private static GuideWord Word(string text, string lemma, string pos, int start, string? person = null, string? number = null, string? tense = null,
        string? mood = null, string? gender = null) =>
        new(text, new TextSpan(start, text.Length), lemma, pos, WordRole.Word, person, number, tense, mood, gender);

    // The fixed record: four lines, one of them a question, a fixed expression read as a noun (which the book must not list), an interjection, and a
    // noun the catalogue does not have (vaso, masculine: el vaso).
    private static LearnerRecord FixedRecord()
    {
        var record = new LearnerRecord();
        record.Append(Entry(1, "quiero agua, por favor", [
            Word("quiero", "querer", "verb", 0, "1", "sg", "pres", "ind"),
            Word("agua", "agua", "noun", 7, null, "sg", null, null, "f"),
            Word("por favor", "por favor", "noun", 13),
        ]));
        record.Append(Entry(2, "¿quieres pan?", [
            Word("quieres", "querer", "verb", 0, "2", "sg", "pres", "ind"),
            Word("pan", "pan", "noun", 9, null, "sg", null, null, "m"),
        ], InputSource.Typed));
        record.Append(Entry(3, "gracias, quiero un vaso", [
            Word("gracias", "gracias", "interj", 0),
            Word("quiero", "querer", "verb", 10, "1", "sg", "pres", "ind"),
            Word("vaso", "vaso", "noun", 20, null, "sg", null, null, "m"),
        ]));
        record.Append(Entry(4, "fue el vaso", [
            Word("fue", "ser", "verb", 0, "3", "sg", "pret", "ind"),
        ], InputSource.Voice, npc: null, place: InputPlace.Lesson, unit: "first-words/u1-hello"));
        return record;
    }

    private static WordProgress Words()
    {
        var words = new WordProgress(TestGame.Nouns);
        words.MarkHeard("agua");
        return words;
    }

    private static string Article(string gender, string lemma) => gender == "m" ? "el" : "la";

    [Fact]
    public void TheNounsPageListsTheCatalogueWordsAndThenTheOtherNounsWithTheirArticlesAndUses()
    {
        var record = FixedRecord();
        var lines = BookPages.Nouns(TestGame.Data.Locale, Words(), record.Nouns, 0, Article, Sheet.Value, "¿?");
        var texts = lines.Select(static l => l.Text).ToList();

        Assert.Contains("el agua", texts); // a catalogue noun, seen: its article from the catalogue
        Assert.Contains("¿?", texts); // a catalogue noun not met yet
        var agua = lines.ToList().IndexOf(lines.First(static l => l.Text == "el agua"));
        Assert.StartsWith("heard 1", lines[agua + 1].Text, StringComparison.Ordinal); // heard once, then read and said at none

        var vaso = lines.ToList().IndexOf(lines.First(static l => l.Text == "el vaso"));
        Assert.True(vaso > 0);
        Assert.Equal("used 1   correct 1", lines[vaso + 1].Text);
        Assert.Equal("forms: vaso 1", lines[vaso + 2].Text);
        Assert.Equal("last: \"gracias, quiero un vaso\"", lines[vaso + 3].Text);
    }

    [Fact]
    public void TheNounsPageNeverListsAFixedExpressionOrAnInterjection()
    {
        var lines = BookPages.Nouns(TestGame.Data.Locale, Words(), FixedRecord().Nouns, 0, Article, Sheet.Value, "¿?");
        // No heading is the expression, and no form of it is listed; a last sentence may quote it, as any sentence does.
        Assert.DoesNotContain(lines, static l => l.Text.StartsWith("por favor", StringComparison.Ordinal) || l.Text.StartsWith("gracias", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static l => l.Text.StartsWith("forms: ", StringComparison.Ordinal) && (l.Text.Contains("favor", StringComparison.Ordinal) || l.Text.Contains("gracias", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheVersesPageGivesEachInfinitiveWithItsEnglishAndEachFormWithItsLabel()
    {
        var lines = BookPages.Verbs(TestGame.Data.Locale, FixedRecord().Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology());
        var texts = lines.Select(static l => l.Text).ToList();

        Assert.Equal("querer — to want", texts[0]);
        Assert.Contains("quiero — I want", texts);
        Assert.Contains("¿quieres? — do you want?", texts); // the question is a form of its own
        Assert.Contains("fue — he/she/it was", texts);
        var quiero = lines.First(static l => l.Text == "quiero — I want");
        Assert.Equal("2 times   2 correct", quiero.Right);
        Assert.Equal("1 time   1 correct", lines.First(static l => l.Text == "fue — he/she/it was").Right); // one use reads in the singular
    }

    [Fact]
    public void WhatYouSaidGivesVamosAsWeGoAndSerAndEstarTheirOwnGlosses()
    {
        var record = new LearnerRecord();
        record.Append(Entry(1, "vamos a comer fruta", [
            Word("vamos", "ir", "verb", 0, "1", "pl", null, "imp"),
            Word("comer", "comer", "verb", 9),
        ]));
        record.Append(Entry(2, "soy Luna, estoy aquí", [
            Word("soy", "ser", "verb", 0, "1", "sg", "pres", "ind"),
            Word("estoy", "estar", "verb", 13, "1", "sg", "pres", "ind"),
        ]));
        record.Append(Entry(3, "Está aquí.", [
            Word("está", "estar", "verb", 0, "2", "sg", null, "imp"),
        ]));
        var texts = BookPages.Verbs(TestGame.Data.Locale, record.Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology()).Select(static l => l.Text).ToList();

        Assert.Contains("vamos — we go", texts);
        Assert.Contains("está — he/she/it is", texts); // an imperative reading that is the present of estar
        Assert.DoesNotContain(texts, static t => t.Contains("let's", StringComparison.Ordinal));
        Assert.Contains("ser — to be: what", texts);
        Assert.Contains("estar — to be: where or how", texts);
        Assert.Contains("soy — I am", texts);
        Assert.Contains("estoy — I am", texts);
    }

    [Fact]
    public void TheVersesPageNeverListsAFixedExpressionOrAnInterjection()
    {
        var lines = BookPages.Verbs(TestGame.Data.Locale, FixedRecord().Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology());
        Assert.DoesNotContain(lines, static l => l.Text.StartsWith("por favor", StringComparison.Ordinal) || l.Text.StartsWith("gracias", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static l => l.Text.Contains(" — ", StringComparison.Ordinal) && (l.Text.StartsWith("por", StringComparison.Ordinal) || l.Text.StartsWith("gracias", StringComparison.Ordinal)));
    }

    [Fact]
    public void ALastSentenceThatIsAPhraseOfTheSheetShowsItsEnglish()
    {
        var sheetRow = Sheet.Value.Find("pedir-agua")!;
        var record = new LearnerRecord();
        record.Append(Entry(1, sheetRow.Spanish, [Word("quiero", "querer", "verb", 0, "1", "sg", "pres", "ind")]));
        var verbs = BookPages.Verbs(TestGame.Data.Locale, record.Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology());
        Assert.Contains(verbs, line => line.Text == $"last: \"{sheetRow.Spanish}\" = {sheetRow.English}");
    }

    [Fact]
    public void ThePhrasesPageListsEveryPhraseWithItsEnglishAndItsHelp()
    {
        var progress = new PhraseProgress(Sheet.Value, 1, TestGame.Data.Npcs.ScaffoldPhrases);
        var rows = PhraseJournalRows.Build(progress, 0, CreditRules.Default, TestGame.Data.Locale);
        var lines = BookPages.Phrases(rows);
        var first = rows[0];
        Assert.Equal(first.Spanish, lines[0].Text);
        Assert.Equal(first.English, lines[0].Aside);
        Assert.Equal(first.LevelText, lines[0].Right);
        Assert.Equal(first.CountsText, lines[1].Text);
        Assert.Equal(rows.Count * 2, lines.Count);
    }

    [Fact]
    public void TheLessonsPageGivesEachLessonItsStateUnitsTicksPhrasesHelpAndSummary()
    {
        var done = new LessonRecordRow(
            "first-words", "First words with Luna", "english", LessonStatus.Completed, 5,
            [new LessonUnitRow("u1-a", StepOutcome.Done), new LessonUnitRow("u1-b", StepOutcome.Done), new LessonUnitRow("u2-a", StepOutcome.Done)],
            2, 3, 2, 1, 0, ["pedir-agua"], "You can greet.", ["u1-a", "u1-b", "u2-a"]);
        var partial = new LessonRecordRow(
            "second", "Second", "english", LessonStatus.InProgress, 3,
            [new LessonUnitRow("u1-a", StepOutcome.Done)], null, 0, 0, 0, 0, [], null, ["u1-a", "u2-a"]);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [done, partial], Sheet.Value);
        var texts = lines.Select(static l => l.Text).ToList();

        Assert.Equal("First words with Luna", texts[0]);
        Assert.Equal("completed in session 2", lines[0].Aside);
        Assert.Equal("You can greet.", texts[1]);
        Assert.Equal("unit 1", texts[2]);
        Assert.True(lines[2].Tick);
        Assert.Equal("2 of 2 steps", lines[2].Right);
        Assert.Equal("unit 2", texts[3]);
        Assert.True(lines[3].Tick);
        Assert.Equal("Quiero agua, por favor.", texts[4]); // the phrase the lesson taught, in Spanish
        Assert.Equal("help used: 2   I don't know: 1   repeats asked: 0   turns: 3", texts[5]);

        var second = lines.First(static l => l.Text == "Second");
        Assert.Equal("in progress: 1 of 2 units done", second.Aside);
        Assert.Single(lines, static l => l.Tone == BookTone.Gold); // only the completed lesson has a summary
        Assert.Contains(lines, static l => l.Text == "unit 2" && l.Tick == false);
    }

    [Fact]
    public void ALessonNotStartedSaysSo()
    {
        var notStarted = new LessonRecordRow("third", "Third", "english", LessonStatus.NotStarted, 4, [], null, 0, 0, 0, 0, [], null, ["u1-a"]);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [notStarted], Sheet.Value);
        Assert.Equal("not started", lines[0].Aside);
        Assert.DoesNotContain(lines, static l => l.Tone == BookTone.Gold);
    }

    // The line under a lesson's title on the shipped lessons page, and the title's line.
    private static (BookLine Title, BookLine Why) LessonLines(VoxelSession session, string lessonId)
    {
        var lines = BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons));
        var title = session.Lessons.Book.Find(lessonId)!.Title;
        var at = lines.ToList().FindIndex(l => l.Text == title && !l.Indent);
        return (lines[at], lines[at + 1]);
    }

    [Fact]
    public void ALessonNotBegunSaysWhyItWaitsAndWhoTeachesItAndOnlyTheIntroductionIsNextInGold()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        var (title, why) = LessonLines(session, "first-words");
        Assert.Equal("next: talk to Luna", why.Text); // the introduction starts by itself at Luna: the NPC by what it is called (game/npcs/guide.json)
        Assert.True(why.Indent);
        Assert.Equal(BookTone.Gold, why.Tone);
        Assert.Equal(BookTone.Gold, title.Tone); // the eye finds it first
        Assert.Null(title.Aside);
        Assert.True(title.Next);
        Assert.Null(title.Practise); // nothing to ask for yet: a lesson that starts by itself, not met
        Assert.Equal("meet Luna first", LessonLines(session, "first-class").Why.Text); // her first class: Luna is not met yet

        (title, why) = LessonLines(session, "personas");
        Assert.Equal("meet Aldeano first", why.Text); // the villager has no name: his label; the day opens nothing (L01)
        Assert.Equal(BookTone.Hint, why.Tone);
        Assert.Equal(BookTone.Unseen, title.Tone);
        Assert.Equal("meet Mar first", LessonLines(session, "el-pueblo").Why.Text);

        session.Lessons.CountAsCompleted("first-words", 1);
        _ = session.OpenTalk("villager");
        _ = session.OpenTalk("fisher");
        (title, why) = LessonLines(session, "personas");
        Assert.Equal("open: ask Aldeano for a class", why.Text);
        Assert.Equal(BookTone.Seen, why.Tone);
        Assert.Equal("personas", title.Practise); // the "Ask for a class" button
        Assert.True(title.Ask);
        Assert.Equal("after “Ayer y mañana”", LessonLines(session, "el-pueblo").Why.Text);
        Assert.DoesNotContain(BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons)), static l => l.Text.StartsWith("next:", StringComparison.Ordinal));
    }

    [Fact]
    public void InSessionEightEveryOpenClassIsOnOfferAtItsTeacherAndNoneIsNext()
    {
        // The learner's route into session 8, as --skip-to-session 8 builds it: ¿Cómo es? completed in session 7, the villager met; Ayer en el pueblo is open, not "next".
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 8);

        var (title, why) = LessonLines(session, "ayer-en-el-pueblo");
        Assert.Equal("open: ask Aldeano for a class", why.Text);
        Assert.Equal(BookTone.Seen, why.Tone);
        Assert.Equal("completed in session 7", LessonLines(session, "como-es").Title.Aside);
        Assert.Equal("ayer-en-el-pueblo", title.Practise);
        Assert.DoesNotContain(BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons)), static l => l.Text.StartsWith("next:", StringComparison.Ordinal));
    }

    [Fact]
    public void CompletedAndBegunLessonsKeepTheirLinesAndSayNoWait()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1);
        session.Day = 2;
        session.Sitting = 2;
        Assert.True(session.Lessons.Start("personas"));

        Assert.Null(session.Lessons.WaitOf("first-words"));
        Assert.Null(session.Lessons.WaitOf("personas"));
        Assert.Equal("completed in session 1", LessonLines(session, "first-words").Title.Aside);
        var (title, next) = LessonLines(session, "personas");
        Assert.StartsWith("in progress: ", title.Aside, StringComparison.Ordinal);
        Assert.Equal("unit 1", next.Text); // its units follow at once: no wait line
        Assert.Equal(new LessonWait(LessonWaitKind.Meet, 3, "villager"), session.Lessons.WaitOf("ayer-y-manana"));
    }

    [Fact]
    public void WhatISaidListsTheNewestLinesFirstWithTheCoreFeedbackInEnglish()
    {
        var record = FixedRecord();
        var lines = BookPages.Said(record.Recent(10), English.Value, [new LessonRecordRow("first-words", "First words with Luna", "english", LessonStatus.InProgress, 34, [], null, 0, 0, 0, 0, [])]);
        var texts = lines.Select(static l => l.Text).ToList();

        Assert.Equal("\"fue el vaso\"", texts[0]);
        Assert.Contains("lesson First words with Luna, u1", lines[0].Right);
        Assert.Equal("\"gracias, quiero un vaso\"", texts[3]);
        Assert.Contains("said, talk to The guide", lines[3].Right);
        Assert.Equal(English.Value.FeedbackFor(record.Recent(10)[1])[0], texts[4]); // the newer entry is first; its feedback follows its line
        var typed = lines.First(static l => l.Text == "\"¿quieres pan?\"");
        Assert.Contains("typed", typed.Right);
    }

    [Fact]
    public void AHeldLineIsShownAsUncertainInEnglish()
    {
        var held = new InputEntry(1, 1, 0, InputSource.Voice, "quiero", "guide", "guide-open", InputPlace.Dialogue, null, "ask_directions", false, true, [], []);
        var lines = BookPages.Said([held], English.Value, []);
        Assert.Equal(English.Value.FeedbackFor(held)[0], lines[1].Text);
    }

    [Fact]
    public void TheUnitOfAStepIsItsPrefixBeforeTheFirstDash()
    {
        Assert.Equal("u1", BookPages.UnitOf("u1-hello"));
        Assert.Equal("u6", BookPages.UnitOf("u6-recap"));
        Assert.Equal("solo", BookPages.UnitOf("solo"));
    }

    [Fact]
    public void TheSheetEnglishIsFoundForAPhraseEitherSide()
    {
        var row = Sheet.Value.Find("pedir-agua")!;
        Assert.Equal(row.English, BookPages.SheetEnglish(Sheet.Value, row.Spanish));
        Assert.Null(BookPages.SheetEnglish(Sheet.Value, "una frase que no está en la hoja"));
    }

    [Fact]
    public void TheLessonsPageOpensAtTheTopWhenNoLessonIsNext()
    {
        // L01: only the introduction is "next" (it starts by itself); from session 9 on, no line is, and the page opens at the top.
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 9);
        var lines = BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons));
        Assert.DoesNotContain(lines, static l => l.Next);
        Assert.Equal(0, BookScroll.OpeningOffset(lines, 20));
    }

    [Fact]
    public void TheSelectionStartsAtTheRowThePageOpenedOn()
    {
        // V30: the first arrow key moves on from the next lesson's row; it does not jump back to the top of the page.
        var lines = new List<BookLine>();
        for (var lesson = 0; lesson < 30; lesson++)
        {
            lines.Add(new BookLine(BookTone.Known, "lesson " + lesson, Next: lesson == 20));
            lines.Add(new BookLine(BookTone.Known, "row " + lesson, Speak: "line." + lesson));
        }

        var rows = BookSpeakers.Selectable(lines);
        var expected = rows.ToList().FindIndex(row => row >= 40);
        Assert.Equal(20, expected);

        var model = new BookModel();
        model.Open(new Vocaterra.Game.Core.Language.Words.WordProgress(TestGame.Nouns));
        model.Show(BookPage.Lessons, animate: false);
        model.OpenAtNext(lines, 20);

        Assert.Equal(expected, model.SelectedIndex(rows.Count));
        model.MoveSelection(-1, rows.Count);
        Assert.Equal(expected - 1, model.SelectedIndex(rows.Count));
    }

    [Fact]
    public void ThePageOpensAtTheTopWhenNoLessonIsNext()
    {
        Assert.Equal(0, BookScroll.OpeningOffset([new BookLine(BookTone.Known, "a"), BookLine.Gap], 5));
    }
}
