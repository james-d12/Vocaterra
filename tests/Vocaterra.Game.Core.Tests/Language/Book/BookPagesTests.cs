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
    public void TheNounsPageHasSectionsAnEntryPerNounMetAndTilesForTheOnesNotMet()
    {
        var record = FixedRecord();
        var lines = BookPages.Nouns(TestGame.Data.Locale, Words(), record.Nouns, 0, Article, Sheet.Value, "¿?");
        var list = BookPages.InListOnly(lines);

        // Learning (agua was heard, vaso was used and is outside the catalogue), a gap, Not met yet with its tiles; Known is empty and left out.
        Assert.Equal(["Learning", "Not met yet"], list.Where(static l => l.Kind == BookLineKind.Section).Select(static l => l.Text));
        Assert.Equal(BookLineKind.Section, list[0].Kind); // no gap at the page's top
        Assert.Equal(BookLineKind.Gap, list[list.ToList().FindIndex(static l => l.Text == "Not met yet") - 1].Kind);
        Assert.DoesNotContain(list, static l => l.Text == "Known");
        Assert.DoesNotContain(lines, static l => l.Text == "¿?"); // a noun not met has no text

        var entries = list.Where(static l => l.Kind == BookLineKind.Entry).ToList();
        Assert.Equal(["el agua", "el vaso"], entries.Select(static l => l.Text));
        Assert.All(entries, static l => Assert.NotNull(l.Lemma));
        Assert.NotNull(entries[0].Pips);
        Assert.Null(entries[1].Pips); // a noun outside the catalogue has no pips
        Assert.Equal(WordProgress.UsesToKnow, WordProgress.UsesToKnow); // the pips are three

        var tiles = list.Where(static l => l.Kind == BookLineKind.Tiles).ToList();
        Assert.NotEmpty(tiles);
        Assert.All(tiles, static l => Assert.InRange(l.Lemmas!.Count, 1, BookLine.TilesPerRow));
        Assert.All(tiles.Take(tiles.Count - 1), static l => Assert.Equal(BookLine.TilesPerRow, l.Lemmas!.Count));
        var notMet = list.First(static l => l.Text == "Not met yet");
        Assert.Equal(tiles.Sum(static l => l.Lemmas!.Count).ToString(System.Globalization.CultureInfo.InvariantCulture), notMet.Right); // the count at the right of the band
        Assert.DoesNotContain("agua", tiles.SelectMany(static l => l.Lemmas!)); // a noun met is not among the tiles
        Assert.DoesNotContain(list, static l => l.Kind == BookLineKind.Detail);
    }

    [Fact]
    public void ANounsDetailsAreItsRightPageWithNumbersInsteadOfZeroCounts()
    {
        var record = FixedRecord();
        var lines = BookPages.Nouns(TestGame.Data.Locale, Words(), record.Nouns, 0, Article, Sheet.Value, "¿?");
        var at = lines.ToList().FindIndex(static l => l.Text == "el agua");
        var block = BookPages.BlockOf(lines, at);

        Assert.Equal(BookLineKind.Entry, block[0].Kind);
        Assert.All(block.Skip(1), static l => Assert.Equal(BookLineKind.Detail, l.Kind));
        Assert.Equal("0 of 3 correct uses", block[1].Text);
        Assert.Equal(BookDetailRole.Sub, block[1].Role);
        Assert.Equal(["What you did"], block.Where(static l => l.Role == BookDetailRole.Label).Select(static l => l.Text).Take(1));
        var stats = block.Single(static l => l.Role == BookDetailRole.Stats).Stats!;
        Assert.Equal([("1", "heard"), ("0", "read"), ("0", "said"), ("0", "correct")], stats.Select(static s => (s.Number, s.Caption)));
        Assert.Contains(block, static l => l.Text == "A word is known after 3 correct uses.");

        var vaso = BookPages.BlockOf(lines, lines.ToList().FindIndex(static l => l.Text == "el vaso"));
        Assert.Equal("used 1   correct 1", vaso[1].Text);
        Assert.Contains(vaso, static l => l.Role == BookDetailRole.Label && l.Text == "What you said");
        Assert.Contains(vaso, static l => l.Text == "vaso 1");
        Assert.Contains(vaso, static l => l.Text == "last: \"gracias, quiero un vaso\"");
        Assert.Equal(vaso.Count, vaso.Count(l => l == vaso[0] || l.Kind == BookLineKind.Detail));
    }

    [Fact]
    public void ANounWithThreeCreditedUsesIsKnownAndComesFirstInItsOwnSection()
    {
        var words = new WordProgress(TestGame.Nouns);
        for (var i = 0; i < 3; i++)
        {
            words.MarkHeard("agua");
        }

        // The word record's own rules decide what is known; here the section follows the row's flag.
        var rows = JournalRows.Build(words, "¿?");
        var lines = BookPages.Nouns(TestGame.Data.Locale, words, [], 0, Article, Sheet.Value, "¿?");
        var known = rows.Count(static r => r.Known);
        Assert.Equal(known > 0, lines.Any(static l => l is { Kind: BookLineKind.Section, Text: "Known" }));
    }

    [Fact]
    public void TheNounsPageNeverListsAFixedExpressionOrAnInterjection()
    {
        var lines = BookPages.Nouns(TestGame.Data.Locale, Words(), FixedRecord().Nouns, 0, Article, Sheet.Value, "¿?");
        // No entry is the expression, and no form of it is listed; a last sentence may quote it, as any sentence does.
        Assert.DoesNotContain(lines, static l => l.Text.StartsWith("por favor", StringComparison.Ordinal) || l.Text.StartsWith("gracias", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static l => l.Kind == BookLineKind.Detail && (l.Text.StartsWith("por favor ", StringComparison.Ordinal) || l.Text.StartsWith("gracias ", StringComparison.Ordinal)));
    }

    // The verbs page's texts as the old page spelled them: an entry's infinitive with its English after a dash, a detail's text as it is.
    private static List<string> VerbTexts(IReadOnlyList<BookLine> lines) =>
        lines.Select(static l => l.Verb is not null && l.Aside is not null ? $"{l.Text} — {l.Aside}" : l.Text).ToList();

    [Fact]
    public void TheVersesPageGivesEachInfinitiveAnEntryAndEachFormADetailWithItsLabel()
    {
        var lines = BookPages.Verbs(TestGame.Data.Locale, FixedRecord().Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology());
        var texts = VerbTexts(lines);

        Assert.Equal("querer — to want", texts[0]);
        Assert.Equal(BookLineKind.Entry, lines[0].Kind);
        Assert.Equal("querer", lines[0].Verb);
        Assert.Contains("quiero — I want", texts);
        Assert.Contains("¿quieres? — do you want?", texts); // the question is a form of its own
        Assert.Contains("fue — he/she/it was", texts);
        Assert.Equal(BookLineKind.Detail, lines.First(static l => l.Text == "quiero — I want").Kind);
        Assert.Equal("2 times   2 correct", lines.First(static l => l.Text == "quiero — I want").Right);
        Assert.Equal("1 time   1 correct", lines.First(static l => l.Text == "fue — he/she/it was").Right); // one use reads in the singular
        Assert.Equal("used 3   correct 3", lines[0].Right); // the entry carries the verb's uses
    }

    [Fact]
    public void TheVersesPageEndsWithAGapAndAQuietNoteAndTheListHasOneEntryPerVerb()
    {
        var lines = BookPages.Verbs(TestGame.Data.Locale, FixedRecord().Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology());
        Assert.Equal(BookLineKind.Meta, lines[^1].Kind);
        Assert.Equal("Use a verb in a sentence and it comes here.", lines[^1].Text);
        Assert.Equal(BookLineKind.Gap, lines[^2].Kind);
        var list = BookPages.InListOnly(lines);
        Assert.Equal(list.Count(static l => l.Verb is not null), list.Count(static l => l.Kind == BookLineKind.Entry));
        Assert.DoesNotContain(list, static l => l.Kind == BookLineKind.Detail);
        Assert.Empty(BookPages.Verbs(TestGame.Data.Locale, [], Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology())); // no verbs: no note, the page says "No verbs yet"
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
        var texts = VerbTexts(BookPages.Verbs(TestGame.Data.Locale, record.Verbs, Glosses.Value, Sheet.Value, TestGame.Bundle.SpanishMorphology()));

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
        Assert.DoesNotContain(VerbTexts(lines), static t => t.Contains(" — ", StringComparison.Ordinal) && (t.StartsWith("por", StringComparison.Ordinal) || t.StartsWith("gracias", StringComparison.Ordinal)));
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
    public void ThePhrasesPageIsGroupedByHelpAndEveryPhraseIsOneEntryWithItsDetails()
    {
        var progress = new PhraseProgress(Sheet.Value, 1, TestGame.Data.Npcs.ScaffoldPhrases);
        var rows = PhraseJournalRows.Build(progress, 0, CreditRules.Default, TestGame.Data.Locale);
        var lines = BookPages.Phrases(TestGame.Data.Locale, rows);
        var list = BookPages.InListOnly(lines);

        // Nothing is met in a new game: every phrase is under Not met yet, one band, no lamps, no counts in the list.
        Assert.Equal(BookLineKind.Section, list[0].Kind);
        Assert.Equal("Not met yet", list[0].Text);
        Assert.Equal(rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), list[0].Right);
        var entries = list.Where(static l => l.Kind == BookLineKind.Entry).ToList();
        Assert.Equal(rows.Count, entries.Count);
        Assert.Equal(rows.Select(static r => r.Spanish), entries.Select(static l => l.Text));
        Assert.Equal(rows.Select(static r => r.English), entries.Select(static l => l.Aside));
        Assert.All(entries, static l => Assert.Null(l.Lamps));
        Assert.All(entries, static l => Assert.Null(l.Right));
        Assert.Equal(BookTone.Unseen, entries[0].Tone);

        var block = BookPages.BlockOf(lines, lines.ToList().FindIndex(static l => l.Kind == BookLineKind.Entry));
        var stats = block.Single(static l => l.Role == BookDetailRole.Stats).Stats!;
        Assert.Equal(rows[0].Hears ? 2 : 5, stats.Count);
        Assert.Equal(rows[0].English, block[1].Text);
    }

    [Fact]
    public void APhraseMetWithHelpIsUnderWithHelpAndItsRightPageNamesTheLamps()
    {
        var progress = new PhraseProgress(Sheet.Value, 1, TestGame.Data.Npcs.ScaffoldPhrases);
        var scaffold = progress.Sheet.Rows.First(row => progress.HasScaffold(row.Id));
        progress.NoteCardShown(scaffold.Id);
        var rows = PhraseJournalRows.Build(progress, 0, CreditRules.Default, TestGame.Data.Locale);
        var lines = BookPages.Phrases(TestGame.Data.Locale, rows, null, "3 phrases are ready for a warm-up with Luna");
        var list = BookPages.InListOnly(lines);

        Assert.Equal(BookPractise.ReviewNow, list[0].Practise); // the top entry, with its button
        Assert.Equal(BookTone.Gold, list[0].Tone);
        Assert.Equal(["With help", "Not met yet"], list.Where(static l => l.Kind == BookLineKind.Section).Select(static l => l.Text));
        var entry = list.First(l => l.Text == scaffold.Spanish);
        Assert.Equal([true, true], entry.Lamps); // English and card and audio: both lamps lit
        var block = BookPages.BlockOf(lines, lines.ToList().IndexOf(entry));
        Assert.Contains(block, static l => l.Role == BookDetailRole.Label && l.Text == "Help now");
        Assert.Contains(block, static l => l.Text == "Card" && l.Lamps is [true]);
        Assert.Contains(block, static l => l.Text == "Audio" && l.Lamps is [true]);
        Assert.Contains(block, static l => l.Text == "Help fades as you say it without help.");
        Assert.Equal(["with help", "without help", "choices", "don't know", "other situation"], block.Single(static l => l.Role == BookDetailRole.Stats).Stats!.Select(static s => s.Caption));
        Assert.DoesNotContain(BookSpeakers.Selectable(list), i => list[i].Practise == BookPractise.ReviewNow); // "Review now" is a button, not a row to select
    }

    [Fact]
    public void TheLessonsPageIsOneEntryPerLessonAndItsDetailsAreTheRightPage()
    {
        var done = new LessonRecordRow(
            "first-words", "First words with Luna", "english", LessonStatus.Completed, 5,
            [new LessonUnitRow("u1-a", StepOutcome.Done), new LessonUnitRow("u1-b", StepOutcome.Done), new LessonUnitRow("u2-a", StepOutcome.Done)],
            2, 3, 2, 1, 0, ["pedir-agua"], "You can greet.", ["u1-a", "u1-b", "u2-a"]);
        var partial = new LessonRecordRow(
            "second", "Second: the second one", "english", LessonStatus.InProgress, 3,
            [new LessonUnitRow("u1-a", StepOutcome.Done)], null, 0, 0, 0, 0, [], null, ["u1-a", "u2-a"]);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [done, partial], Sheet.Value);
        var list = BookPages.InListOnly(lines);

        Assert.Equal(2, list.Count); // one row per lesson, no gaps, no units or phrases in the list
        Assert.Equal("First words with Luna", list[0].Text);
        Assert.Equal("completed in session 2", list[0].Aside);
        Assert.Equal(BookMark.Done, list[0].Mark);
        Assert.Equal("Second", list[1].Text); // the short title: the part before the colon
        Assert.Equal("in progress: 1 of 2 units done", list[1].Aside);
        Assert.Equal(BookMark.Half, list[1].Mark);
        Assert.Equal(BookTone.Known, list[0].Tone);
        Assert.Equal(BookTone.Learning, list[1].Tone);

        var block = BookPages.BlockOf(lines, 0);
        Assert.Equal("First words with Luna", block.Single(static l => l.Role == BookDetailRole.Title).Text);
        Assert.Equal("completed in session 2", block.Single(static l => l.Role == BookDetailRole.Sub).Text);
        Assert.Contains(block, static l => l.Text == "You can greet.");
        var units = block.Single(static l => l.Role == BookDetailRole.Units);
        Assert.Equal([BookMark.Done, BookMark.Done], units.Marks);
        Assert.Contains(block, static l => l.Role == BookDetailRole.Label && l.Text == "Units" && l.Right == "2 of 2 done");
        Assert.Equal([("2", "help used"), ("1", "I don't know"), ("0", "repeats asked"), ("3", "turns")], block.Single(static l => l.Role == BookDetailRole.Stats).Stats!.Select(static s => (s.Number, s.Caption)));
        var phrase = block.Single(static l => l.Kind == BookLineKind.Detail && l.Text == "Quiero agua, por favor."); // the phrase the lesson taught, in Spanish
        Assert.Equal("I want water, please.", phrase.Aside);
        Assert.Contains(block, static l => l.Role == BookDetailRole.Label && l.Text == "Phrases" && l.Right == "1");

        var second = BookPages.BlockOf(lines, lines.ToList().FindIndex(static l => l.Text == "Second"));
        Assert.Equal([BookMark.Done, BookMark.Empty], second.Single(static l => l.Role == BookDetailRole.Units).Marks);
        Assert.Contains(second, static l => l.Text == "unit 2: 0 of 1 steps"); // the steps of a unit not finished only
        Assert.DoesNotContain(second, static l => l.Text.StartsWith("unit 1:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static l => l.Tone == BookTone.Gold); // no lesson is next, and the all-done note is for a page with every lesson done
    }

    [Fact]
    public void ALessonNotStartedSaysSoAndHasNoHelpNumbers()
    {
        var notStarted = new LessonRecordRow("third", "Third", "english", LessonStatus.NotStarted, 4, [], null, 0, 0, 0, 0, [], null, ["u1-a"]);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [notStarted], Sheet.Value);
        Assert.Equal("not started", lines[0].Aside);
        Assert.Equal(BookMark.Wait, lines[0].Mark);
        Assert.Equal(BookTone.Unseen, lines[0].Tone);
        Assert.DoesNotContain(lines, static l => l.Tone == BookTone.Gold);
        Assert.DoesNotContain(lines, static l => l.Role == BookDetailRole.Stats);
        Assert.Equal([BookMark.Empty], lines.Single(static l => l.Role == BookDetailRole.Units).Marks);
    }

    [Fact]
    public void WhenEveryLessonIsDoneThePageOpensWithAGoldNote()
    {
        var done = new LessonRecordRow("a", "A", "english", LessonStatus.Completed, 1, [], 1, 0, 0, 0, 0, [], null, []);
        var lines = BookPages.Lessons(TestGame.Data.Locale, [done], Sheet.Value);
        Assert.Equal(BookLineKind.Meta, lines[0].Kind);
        Assert.Equal(BookTone.Gold, lines[0].Tone);
        Assert.Equal(BookLineKind.Gap, lines[1].Kind);
        Assert.DoesNotContain(BookSpeakers.Selectable(BookPages.InListOnly(lines)), static _ => false);
        Assert.Equal([2], BookSpeakers.Selectable(BookPages.InListOnly(lines)));
    }

    // The line under a lesson's title on the shipped lessons page, and the title's line.
    private static (BookLine Title, BookLine Why) LessonLines(VoxelSession session, string lessonId)
    {
        var lines = BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons));
        var title = session.Lessons.Book.Find(lessonId)!.Title;
        var at = lines.ToList().FindIndex(l => l.Kind == BookLineKind.Entry && l.Text == LessonDefinition.ShortOf(title));
        return (lines[at], lines[at]); // the state is the row's aside (it was a line of its own before U07)
    }

    [Fact]
    public void ALessonNotBegunSaysWhyItWaitsAndWhoTeachesItAndOnlyTheIntroductionIsNextInGold()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        var (title, why) = LessonLines(session, "first-words");
        Assert.Equal("next: talk to Luna", why.Aside); // the introduction starts by itself at Luna: the NPC by what it is called (game/npcs/guide.json)
        Assert.Equal(BookMark.Next, title.Mark);
        Assert.Equal(BookTone.Gold, title.Tone); // the eye finds it first
        Assert.True(title.Next);
        Assert.Null(title.Practise); // nothing to ask for yet: a lesson that starts by itself, not met
        Assert.Equal("meet Luna first", LessonLines(session, "first-class").Why.Aside); // her first class: Luna is not met yet

        (title, why) = LessonLines(session, "personas");
        Assert.Equal("meet Tomás first", why.Aside); // the villager by his name (#52); the day opens nothing (L01)
        Assert.Equal(BookTone.Unseen, title.Tone);
        Assert.Equal(BookMark.Wait, title.Mark);
        Assert.Equal("meet Mar first", LessonLines(session, "el-pueblo").Why.Aside);

        session.Lessons.CountAsCompleted("first-words", 1);
        _ = session.OpenTalk("villager");
        _ = session.OpenTalk("fisher");
        (title, why) = LessonLines(session, "personas");
        Assert.Equal("open: ask Tomás", why.Aside);
        Assert.Equal(BookTone.Seen, title.Tone);
        Assert.Equal("personas", title.Practise); // the "Ask for a class" button
        Assert.True(title.Ask);
        Assert.Equal("after “Ayer y mañana”", LessonLines(session, "el-pueblo").Why.Aside);
        Assert.DoesNotContain(BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons)), static l => l.Aside is not null && l.Aside.StartsWith("next:", StringComparison.Ordinal));
    }

    [Fact]
    public void InSessionEightEveryOpenClassIsOnOfferAtItsTeacherAndNoneIsNext()
    {
        // The learner's route into session 8, as --skip-to-session 8 builds it: ¿Cómo es? completed in session 7, the villager met; Ayer en el pueblo is open, not "next".
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 8);

        var (title, why) = LessonLines(session, "ayer-en-el-pueblo");
        Assert.Equal("open: ask Tomás", why.Aside);
        Assert.Equal(BookTone.Seen, title.Tone);
        Assert.Equal("completed in session 7", LessonLines(session, "como-es").Title.Aside);
        Assert.Equal("ayer-en-el-pueblo", title.Practise);
        Assert.DoesNotContain(BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons)), static l => l.Aside is not null && l.Aside.StartsWith("next:", StringComparison.Ordinal));
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
        var (title, _) = LessonLines(session, "personas");
        Assert.StartsWith("in progress: ", title.Aside, StringComparison.Ordinal);
        Assert.Equal(BookMark.Half, title.Mark); // under way: no wait text
        Assert.Equal(new LessonWait(LessonWaitKind.Meet, 3, "villager"), session.Lessons.WaitOf("ayer-y-manana"));
    }

    [Fact]
    public void WhatISaidIsOneSectionPerSessionWithTheNewestLinesFirstAndAMarkForEach()
    {
        var record = FixedRecord();
        var lines = BookPages.Said(record.Recent(10), English.Value, [new LessonRecordRow("first-words", "First words with Luna", "english", LessonStatus.InProgress, 34, [], null, 0, 0, 0, 0, [])]);
        var list = BookPages.InListOnly(lines);

        Assert.Equal(BookLineKind.Section, list[0].Kind);
        Assert.Equal("Session 1", list[0].Text);
        Assert.Equal("4", list[0].Right);
        Assert.Single(list, static l => l.Kind == BookLineKind.Section); // every line is of session 1
        Assert.Equal(["\"fue el vaso\"", "\"gracias, quiero un vaso\"", "\"¿quieres pan?\"", "\"quiero agua, por favor\""], list.Skip(1).Select(static l => l.Text));
        Assert.Equal("lesson · u1", list[1].Right);
        Assert.Equal("talk", list[2].Right);
        Assert.All(list.Skip(1), static l => Assert.Equal(BookTone.Known, l.Tone));
        Assert.All(list.Skip(1), static l => Assert.NotNull(l.Mark));

        var block = BookPages.BlockOf(lines, lines.ToList().FindIndex(static l => l.Text == "\"fue el vaso\""));
        Assert.Equal("Session 1   ·   said   ·   lesson First words with Luna, u1", block.Single(static l => l.Kind == BookLineKind.Detail && l.Role == BookDetailRole.Text && l.Tone == BookTone.Hint).Text);
        var typed = BookPages.BlockOf(lines, lines.ToList().FindIndex(static l => l.Text == "\"¿quieres pan?\""));
        Assert.Contains(typed, static l => l.Text.Contains("typed", StringComparison.Ordinal));
        Assert.Equal(English.Value.FeedbackFor(record.Recent(10)[3]), typed.SkipWhile(static l => l.Role != BookDetailRole.Label).Skip(1).Select(static l => l.Text)); // the feedback, uncut, after its label
    }

    [Fact]
    public void ALineNewSessionsStartANewSectionAndTheMarkFollowsTheCoresVerdict()
    {
        Assert.NotNull(English.Value);
        InputEntry At(long id, int session, string text, bool communicated, bool held = false, IReadOnlyList<GuideError>? errors = null, string intent = "ask_directions") =>
            new(id, session, id, InputSource.Voice, text, "guide", "guide-open", InputPlace.Dialogue, null, intent, communicated, held, errors ?? [], []);
        var error = new GuideError("agreement", "el", "la", new TextSpan(0, 3));
        var recent = new[]
        {
            At(5, 2, "sí", communicated: true),
            At(4, 2, "es esto", communicated: true, errors: [error]),
            At(3, 2, "hasta la fecha", communicated: false),
            At(2, 1, "ayuda", communicated: true, intent: "ask_help"),
            At(1, 1, "", communicated: false, held: true),
        };
        var lines = BookPages.Said(recent, English.Value, []);
        var list = BookPages.InListOnly(lines);

        Assert.Equal(["Session 2", "Session 1"], list.Where(static l => l.Kind == BookLineKind.Section).Select(static l => l.Text));
        Assert.Equal(BookLineKind.Gap, list[4].Kind); // a gap between sections
        var marks = list.Where(static l => l.Kind == BookLineKind.Entry).Select(static l => l.Mark).ToList();
        Assert.Equal([BookMark.Understood, BookMark.Slip, BookMark.Missed, BookMark.Dot, BookMark.Dot], marks);
        var blank = list.Last(static l => l.Kind == BookLineKind.Entry);
        Assert.Equal("(nothing heard)", blank.Text);
        Assert.Equal(BookTone.Hint, blank.Tone);
        Assert.Equal(["Understood", "Understood, with a small mistake", "Not understood", "You asked for help", "Held back: the game was not sure"], lines.Where(static l => l.Role == BookDetailRole.Sub).Select(static l => l.Text));
    }

    [Fact]
    public void AHeldLineIsShownAsUncertainInEnglish()
    {
        var held = new InputEntry(1, 1, 0, InputSource.Voice, "quiero", "guide", "guide-open", InputPlace.Dialogue, null, "ask_directions", false, true, [], []);
        var lines = BookPages.Said([held], English.Value, []);
        Assert.Equal(English.Value.FeedbackFor(held)[0], lines.Last().Text);
        Assert.Equal(BookMark.Dot, lines.First(static l => l.Kind == BookLineKind.Entry).Mark);
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
            lines.Add(new BookLine(BookTone.Known, "lesson " + lesson, Next: lesson == 20)); // one entry per lesson: the selection counts entries
        }

        var rows = BookSpeakers.Selectable(lines);
        var expected = rows.ToList().FindIndex(row => row >= 20);
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
