using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Es.Morphology;

namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// The lines of the book's pages (S20), built from the learner's record, the word progress, the phrase progress, the lessons and the English text.
/// Each builder is a pure function of its inputs: the same record gives the same lines, in the same order.
/// </summary>
public static class BookPages
{
    /// <summary>The tag of a known noun or verb.</summary>
    public const string KnownTagId = "bookKnown";

    /// <summary>The sentence's English when the sentence is a row of the phrase sheet (either side), or <see langword="null"/>.</summary>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="sentence">The line as the learner said or typed it.</param>
    public static string? SheetEnglish(PhraseSheet sheet, string sentence)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(sentence);
        var plain = PhraseSheet.Plain(sentence);
        return sheet.Rows.FirstOrDefault(row => row.Texts.Any(text => string.Equals(PhraseSheet.Plain(text), plain, StringComparison.Ordinal)))?.English;
    }

    /// <summary>The lines of a page, built from the session's record (no drawing here). The book's layer and its speaker controls read the same lines (S20b).</summary>
    /// <param name="session">The session.</param>
    /// <param name="page">The page.</param>
    /// <param name="recordings">Which recording each row plays.</param>
    public static IReadOnlyList<BookLine> ForPage(GuideSession session, BookPage page, BookRecordings recordings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(recordings);
        return page switch
        {
            BookPage.Nouns => Nouns(session.Locale, session.Words, session.Learner.Nouns, session.StoredTime.UnixSeconds, session.DefiniteArticle, session.Phrases, session.Data.Words.Unseen, recordings),
            BookPage.Verbs => Verbs(session.Locale, session.Learner.Verbs, session.Glosses, session.Phrases, session.Bundle.SpanishMorphology()),
            BookPage.Phrases => Phrases(PhraseJournalRows.Build(session.PhraseProgress, session.StoredTime.UnixSeconds, session.Credit, session.Locale), recordings, ReviewLine(session)),
            BookPage.Lessons => Lessons(session.Locale, session.Lessons.Record, session.Phrases, recordings, Waits(session.Lessons)),
            _ => Said(session.Learner.Recent(LearnerRecord.SavedEntries), session.Locale, session.Lessons.Record),
        };
    }

    /// <summary>
    /// The left page's list (S28): the page's lines, except on the verbs page, where only the verbs themselves are listed (one line each) and the right page shows
    /// the selected verb's conjugation table and the other forms. The book's layer and its controls read the same list.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="page">The page.</param>
    /// <param name="recordings">Which recording each row plays.</param>
    public static IReadOnlyList<BookLine> ListFor(GuideSession session, BookPage page, BookRecordings recordings)
    {
        var lines = ForPage(session, page, recordings);
        return page == BookPage.Verbs ? lines.Where(static line => line.Verb is not null).ToList() : lines;
    }

    /// <summary>The verb forms taught by the lessons that are completed or under way (the book's tables show these plainly).</summary>
    /// <param name="session">The session.</param>
    public static IReadOnlyList<TaughtVerbForm> TaughtForms(GuideSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Lessons.Book.Lessons
            .Where(lesson => session.Lessons.StatusOf(lesson.Id) is LessonStatus.Completed or LessonStatus.InProgress)
            .SelectMany(static lesson => lesson.TaughtVerbs)
            .ToList();
    }

    /// <summary>The conjugation table of a verb the learner has met (S28), or <see langword="null"/> when the learner has not used it.</summary>
    /// <param name="session">The session.</param>
    /// <param name="lemma">The infinitive.</param>
    public static ConjugationTable? VerbTable(GuideSession session, string lemma)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lemma);
        var row = session.Learner.Verbs.FirstOrDefault(r => string.Equals(r.Lemma, lemma, StringComparison.Ordinal));
        return row is null ? null : ConjugationTable.Build(row, session.Learner.History, TaughtForms(session), session.Bundle.SpanishMorphology(), session.Glosses.ForLemma(lemma), session.Data.Words);
    }

    /// <summary>
    /// The lines of the item the right page details (S28): the block of lines that holds <paramref name="line"/>: its heading (a line that is not indented)
    /// and the indented lines under it, up to the next gap or heading. Empty when the line is outside the list or is a gap.
    /// </summary>
    /// <param name="lines">The page's lines.</param>
    /// <param name="line">The index of a line of the item (the selected row, or the first line on screen).</param>
    public static IReadOnlyList<BookLine> BlockOf(IReadOnlyList<BookLine> lines, int line)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (line < 0 || line >= lines.Count || lines[line].Text.Length == 0)
        {
            return [];
        }

        var start = line;
        while (start > 0 && lines[start].Indent && lines[start - 1].Text.Length > 0)
        {
            start--;
        }

        var end = start + 1;
        while (end < lines.Count && lines[end].Indent && lines[end].Text.Length > 0)
        {
            end++;
        }

        return lines.Skip(start).Take(end - start).ToList();
    }

    /// <summary>
    /// The nouns page: every catalogue noun (the words page as it was: heard, read, said and correct, the three pips, <c>¿?</c> before it is seen), then
    /// every other noun the learner used (a lemma with no space: a fixed expression such as <c>por favor</c> is not a noun), each with its article, its
    /// forms, its counts and its last sentence.
    /// </summary>
    /// <param name="ui">The support language (the page's words, C04).</param>
    /// <param name="words">The word progress (the catalogue's counts).</param>
    /// <param name="learnerNouns">The noun rows of the learner's record (<c>session.Learner.Nouns</c>).</param>
    /// <param name="now">The stored time in Unix seconds (the due text).</param>
    /// <param name="definite">The definite article of a noun from its gender and lemma (<c>el</c> for <c>agua</c>).</param>
    /// <param name="sheet">The phrase sheet (for the English of a sentence that is a phrase).</param>
    /// <param name="unseen">The label of a noun not met yet (<c>¿?</c>, <c>game/words.json</c>).</param>
    /// <param name="recordings">The recordings (S20b): a seen catalogue noun gets a speaker button for its word card. An unmet noun (<c>¿?</c>) and a noun outside the catalogue get none.</param>
    public static IReadOnlyList<BookLine> Nouns(LocaleText ui, WordProgress words, IReadOnlyList<WordRow> learnerNouns, long now, Func<string, string, string> definite, PhraseSheet sheet, string unseen, BookRecordings? recordings = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(learnerNouns);
        ArgumentNullException.ThrowIfNull(definite);
        ArgumentNullException.ThrowIfNull(sheet);
        var lines = new List<BookLine>();
        var learner = learnerNouns.Where(static row => !row.Lemma.Contains(' ', StringComparison.Ordinal)).ToDictionary(static row => row.Lemma, StringComparer.Ordinal);
        var catalogue = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in JournalRows.Build(words, now, unseen, ui))
        {
            catalogue.Add(row.Lemma);
            var tone = row.Known ? BookTone.Known : row.Record.CreditUnits > 0 ? BookTone.Learning : row.Seen ? BookTone.Seen : BookTone.Unseen;
            var speak = row.Seen ? recordings?.ForNoun(row.Lemma) : null;
            lines.Add(new BookLine(tone, row.Label, Lemma: row.Lemma, Pips: row.Pips, Right: row.Known ? ui.Ui(KnownTagId) : null, Speak: speak));
            var counts = ui.Ui("bookNounCounts", ("heard", N(row.Record.Heard)), ("read", N(row.Record.Read)), ("said", N(row.Record.Spoken)), ("correct", N(row.Record.SpokenCorrect)));
            lines.Add(new BookLine(BookTone.Hint, counts, Right: row.DueText, Indent: true));
            if (learner.TryGetValue(row.Lemma, out var used))
            {
                AddUse(ui, lines, used, sheet);
            }

            lines.Add(BookLine.Gap);
        }

        foreach (var row in learner.Values.Where(row => !catalogue.Contains(row.Lemma)).OrderBy(static row => row.Lemma, StringComparer.Ordinal))
        {
            var label = row.Gender is null ? row.Lemma : $"{definite(row.Gender, row.Lemma)} {row.Lemma}";
            lines.Add(new BookLine(BookTone.Seen, label, Lemma: row.Lemma));
            lines.Add(new BookLine(BookTone.Hint, ui.Ui("bookUsed", ("times", N(row.Times)), ("correct", N(row.TimesCorrect))), Indent: true));
            AddUse(ui, lines, row, sheet);
            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>
    /// The verbs page: every verb the learner used (a lemma with no space), its infinitive with its English (<c>querer — to want</c>) and each form with its
    /// label (<see cref="BookVerbLabels"/>), its counts and its last sentence.
    /// </summary>
    /// <param name="ui">The support language (the page's words, C04).</param>
    /// <param name="learnerVerbs">The verb rows of the learner's record (<c>session.Learner.Verbs</c>).</param>
    /// <param name="glosses">The verbs' English.</param>
    /// <param name="sheet">The phrase sheet (for the English of a sentence that is a phrase).</param>
    /// <param name="morphology">The bundle's morphology: an imperative reading that is an indicative present is labelled as that present (<see cref="VerbReading"/>).</param>
    public static IReadOnlyList<BookLine> Verbs(LocaleText ui, IReadOnlyList<WordRow> learnerVerbs, VerbGlosses glosses, PhraseSheet sheet, MorphologyGenerator morphology)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(learnerVerbs);
        ArgumentNullException.ThrowIfNull(glosses);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(morphology);
        var lines = new List<BookLine>();
        foreach (var row in learnerVerbs.Where(static row => !row.Lemma.Contains(' ', StringComparison.Ordinal)).OrderBy(static row => row.Lemma, StringComparer.Ordinal))
        {
            var gloss = glosses.ForLemma(row.Lemma);
            var heading = gloss is null ? row.Lemma : row.Lemma + BookVerbLabels.Separator + gloss.English;
            lines.Add(new BookLine(BookTone.Known, heading, Right: ui.Ui("bookUsed", ("times", N(row.Times)), ("correct", N(row.TimesCorrect))), Verb: row.Lemma));
            foreach (var form in row.Forms)
            {
                var (person, number, tense, mood) = VerbReading.Normalize(row.Lemma, form.Form, form.Person, form.Number, form.Tense, form.Mood, morphology);
                var label = BookVerbLabels.Label(form.Form, row.Lemma, gloss, person, number, tense, mood, form.Question);
                lines.Add(new BookLine(BookTone.Seen, label, Right: ui.Ui("bookFormCounts", ("uses", ui.Plural("ui.bookUses", form.Times)), ("correct", N(form.TimesCorrect))), Indent: true));
                if (form.Last is { } last)
                {
                    lines.Add(new BookLine(BookTone.Hint, Last(ui, sheet, last.Sentence), Indent: true));
                }
            }

            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>
    /// The phrases page (S8, unchanged in meaning): the phrases the learner has met first, each with its Spanish, its English, its help now (the lamps:
    /// card and audio) and its counts.
    /// </summary>
    /// <param name="rows">The rows from <see cref="PhraseJournalRows.Build"/>.</param>
    /// <param name="recordings">The recordings (S20b): every phrase row gets a speaker button for its recording, when it has one.</param>
    /// <param name="review">The line above the rows that carries the "Review now" button (X06), or <see langword="null"/> when nothing the guide can ask is due.</param>
    public static IReadOnlyList<BookLine> Phrases(IReadOnlyList<PhraseJournalRow> rows, BookRecordings? recordings = null, string? review = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var lines = new List<BookLine>();
        if (review is not null)
        {
            lines.Add(new BookLine(BookTone.Learning, review, Practise: BookPractise.ReviewNow));
            lines.Add(BookLine.Gap);
        }

        foreach (var row in rows)
        {
            var tone = row.HasScaffold && row.Level == SupportLevel.None && row.Record.CreditedUses > 0 ? BookTone.Known : row.Record.Seen ? BookTone.Learning : BookTone.Unseen;
            var lamps = row.HasScaffold ? new[] { SupportLevels.ShowsCard(row.Level), row.Level != SupportLevel.None } : null;
            lines.Add(new BookLine(tone, row.Spanish, Aside: row.English, Right: row.LevelText, Lamps: lamps, Speak: recordings?.ForPhrase(row.Id)));
            lines.Add(new BookLine(BookTone.Hint, row.CountsText, Right: row.DueText, Indent: true));
        }

        return lines;
    }

    // The Phrases page's "Review now" line (X06): how many phrases the warm-up would ask, while some are due and no warm-up is under way.
    private static string? ReviewLine(GuideSession session) =>
        session.Deck.Ready is > 0 and var ready && session.Deck.Status != DeckStatus.Running ? session.Locale.Plural("ui.bookReviewDue", ready) : null;

    /// <summary>
    /// The lessons page: every lesson of the bundle in its order, with its state (completed in session n, in progress with the units done, not
    /// started), its units with a tick each, the phrases it taught (S17's recap), the help used and, once completed, its English summary. A lesson not begun
    /// says on its own line, under its title, why it waits and where it starts (V12c): <c>next: talk to Luna</c> for the one the objective sends the learner to (in gold,
    /// title and line, so the eye finds it first), <c>open: talk to Luna</c> for another unlocked one, <c>opens in session 4, Mar</c> for one whose session has not come,
    /// <c>after “El pueblo”</c> for one whose earlier lesson is not done. Its own line, not the title's note: at 150 % text the left page is narrow, and a note after a title is cut first.
    /// Once every lesson is completed (after session 10's market, VILLAGE_PLAN V15) the page opens with a line that says the series is done, and no lesson says <c>next:</c>.
    /// </summary>
    /// <param name="ui">The support language (the page's words, C04).</param>
    /// <param name="lessons">The lesson record (<c>session.Lessons.Record</c>).</param>
    /// <param name="sheet">The phrase sheet (the English and the Spanish of the phrases).</param>
    /// <param name="recordings">The recordings (S20b): each phrase a lesson taught gets a speaker button for its recording.</param>
    /// <param name="waits">Why each lesson not begun waits, by lesson id (<see cref="LessonRunner.WaitOf"/>); a lesson not listed says only that it is not started.</param>
    public static IReadOnlyList<BookLine> Lessons(LocaleText ui, IReadOnlyList<LessonRecordRow> lessons, PhraseSheet sheet, BookRecordings? recordings = null, IReadOnlyDictionary<string, LessonWait>? waits = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(sheet);
        var lines = new List<BookLine>();
        if (lessons.Count > 0 && lessons.All(static l => l.Status == LessonStatus.Completed))
        {
            lines.Add(new BookLine(BookTone.Gold, ui.Ui("bookLessonsAllDone", ("count", N(lessons.Count)))));
            lines.Add(BookLine.Gap);
        }

        foreach (var lesson in lessons)
        {
            var units = UnitsOf(lesson);
            var doneUnits = units.Count(static unit => unit.Ticked);
            var wait = lesson.Status == LessonStatus.NotStarted && waits is not null && waits.TryGetValue(lesson.LessonId, out var found) ? found : null;
            var (tone, state) = lesson.Status switch
            {
                LessonStatus.Completed => (BookTone.Known, lesson.CompletedSession is { } session ? ui.Ui("bookLessonCompletedIn", ("session", N(session))) : ui.Ui("bookLessonCompleted")),
                LessonStatus.InProgress => (BookTone.Learning, ui.Ui("bookLessonInProgress", ("done", N(doneUnits)), ("units", N(units.Count)))),
                _ => (wait is { Kind: LessonWaitKind.Next } ? BookTone.Gold : BookTone.Unseen, wait is null ? ui.Ui("bookLessonNotStarted") : null),
            };
            lines.Add(new BookLine(tone, lesson.Title, Aside: state, Practise: lesson.Status == LessonStatus.Completed ? lesson.LessonId : null));
            if (wait is not null)
            {
                lines.Add(new BookLine(wait.Kind switch { LessonWaitKind.Next => BookTone.Gold, LessonWaitKind.Open => BookTone.Seen, _ => BookTone.Hint }, WaitText(ui, wait, lessons), Indent: true));
            }

            if (lesson.Status == LessonStatus.Completed && lesson.Summary is { } summary)
            {
                lines.Add(new BookLine(BookTone.Gold, summary, Indent: true));
            }

            foreach (var unit in units)
            {
                lines.Add(new BookLine(unit.Ticked ? BookTone.Seen : BookTone.Unseen, ui.Ui("bookUnit", ("number", N(unit.Number))), Right: ui.Ui("bookUnitSteps", ("finished", N(unit.Finished)), ("steps", N(unit.Steps))), Tick: unit.Ticked, Indent: true));
            }

            foreach (var phrase in lesson.Phrases)
            {
                var row = sheet.Find(phrase);
                lines.Add(new BookLine(BookTone.Seen, row?.Spanish ?? phrase, Aside: row?.English, Indent: true, Speak: recordings?.ForPhrase(phrase)));
            }

            lines.Add(new BookLine(
                BookTone.Hint,
                ui.Ui("bookLessonCounts", ("help", N(lesson.HelpUses)), ("dontKnow", N(lesson.DontKnowUses)), ("repeats", N(lesson.RepeatUses)), ("turns", N(lesson.Turns))),
                Indent: true));
            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>Why each lesson that is not begun waits (V12c), by lesson id: the lessons page's lines under their titles.</summary>
    /// <param name="runner">The lessons.</param>
    public static IReadOnlyDictionary<string, LessonWait> Waits(LessonRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var waits = new Dictionary<string, LessonWait>(StringComparer.Ordinal);
        foreach (var lesson in runner.Book.Lessons)
        {
            if (runner.WaitOf(lesson.Id) is { } wait)
            {
                waits[lesson.Id] = wait;
            }
        }

        return waits;
    }

    // The line under a lesson not begun (V12c): the NPC by what it is called (game/npcs/<id>.json, through the locale), the earlier lesson by its short title.
    private static string WaitText(LocaleText ui, LessonWait wait, IReadOnlyList<LessonRecordRow> lessons) => wait.Kind switch
    {
        LessonWaitKind.Next => ui.Ui("bookLessonNext", ("npc", ui.Called(wait.Npc))),
        LessonWaitKind.Open => ui.Ui("bookLessonOpen", ("npc", ui.Called(wait.Npc))),
        LessonWaitKind.Session => ui.Ui("bookLessonOpensIn", ("session", N(wait.Session)), ("npc", ui.Called(wait.Npc))),
        _ => ui.Ui("bookLessonAfter", ("title", LessonDefinition.ShortOf(lessons.FirstOrDefault(l => string.Equals(l.LessonId, wait.After, StringComparison.Ordinal))?.Title ?? wait.After ?? string.Empty))),
    };

    /// <summary>
    /// The "What I said" page: the newest lines the learner gave (<c>session.Learner.Recent(n)</c>, newest first): the line, how it came (said or
    /// typed, or a button), where it was said, and the core's feedback in English (S16's text).
    /// </summary>
    /// <param name="recent">The entries, newest first.</param>
    /// <param name="english">The English text (the feedback and the NPCs' names).</param>
    /// <param name="lessons">The lesson record, for a lesson line's title.</param>
    public static IReadOnlyList<BookLine> Said(IReadOnlyList<InputEntry> recent, LocaleText english, IReadOnlyList<LessonRecordRow> lessons)
    {
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(english);
        ArgumentNullException.ThrowIfNull(lessons);
        var lines = new List<BookLine>();
        foreach (var entry in recent)
        {
            var tone = entry.Communicated && !entry.Held ? BookTone.Known : BookTone.Hint;
            lines.Add(new BookLine(tone, $"\"{entry.Text}\"", Aside: english.Ui("bookSaidSession", ("session", N(entry.Session))), Right: english.Ui("bookSaidWhere", ("source", SourceText(english, entry.Source)), ("place", PlaceText(entry, english, lessons)))));
            foreach (var feedback in english.FeedbackFor(entry))
            {
                lines.Add(new BookLine(BookTone.Hint, feedback, Indent: true));
            }

            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>The unit a lesson step belongs to: <c>u1-hello</c> is unit <c>u1</c>; a step with no unit prefix is its own unit.</summary>
    /// <param name="stepId">The step's id.</param>
    public static string UnitOf(string stepId)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        var dash = stepId.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? stepId[..dash] : stepId;
    }

    private static IReadOnlyList<UnitState> UnitsOf(LessonRecordRow lesson)
    {
        var finished = lesson.Units.Select(static unit => unit.StepId).ToHashSet(StringComparer.Ordinal);
        var groups = (lesson.StepIds ?? []).GroupBy(UnitOf, StringComparer.Ordinal).ToList();
        var units = new List<UnitState>();
        for (var i = 0; i < groups.Count; i++)
        {
            var steps = groups[i].ToList();
            var done = steps.Count(finished.Contains);
            units.Add(new UnitState(i + 1, steps.Count, done, done == steps.Count));
        }

        return units;
    }

    private static void AddUse(LocaleText ui, List<BookLine> lines, WordRow row, PhraseSheet sheet)
    {
        var forms = string.Join("   ", row.Forms.Select(static form => $"{form.Form} {form.Times.ToString(CultureInfo.InvariantCulture)}"));
        if (forms.Length > 0)
        {
            lines.Add(new BookLine(BookTone.Hint, ui.Ui("bookForms", ("forms", forms)), Indent: true));
        }

        lines.Add(new BookLine(BookTone.Hint, Last(ui, sheet, row.Last.Sentence), Indent: true));
    }

    // last: "quiero agua" = I want water: the last sentence, with the sheet's English when it is a phrase.
    private static string Last(LocaleText ui, PhraseSheet sheet, string sentence) =>
        ui.Ui("bookLast", ("sentence", sentence), ("meaning", SheetEnglish(sheet, sentence) is { } english ? ui.Ui("bookMeaning", ("english", english)) : string.Empty));

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string SourceText(LocaleText ui, InputSource source) => ui.Ui(source switch
    {
        InputSource.Voice => "bookSaidVoice",
        InputSource.Typed => "bookSaidTyped",
        _ => "bookSaidButton",
    });

    private static string PlaceText(InputEntry entry, LocaleText english, IReadOnlyList<LessonRecordRow> lessons)
    {
        switch (entry.Place)
        {
            case InputPlace.Lesson:
                var lessonId = entry.Unit is { } unit ? unit.Split('/')[0] : string.Empty;
                var title = lessons.FirstOrDefault(row => string.Equals(row.LessonId, lessonId, StringComparison.Ordinal))?.Title ?? lessonId;
                var step = entry.Unit is { } full && full.Contains('/', StringComparison.Ordinal) ? UnitOf(full[(full.IndexOf('/', StringComparison.Ordinal) + 1)..]) : string.Empty;
                return step.Length > 0 ? english.Ui("bookSaidLessonStep", ("title", title), ("step", step)) : english.Ui("bookSaidLesson", ("title", title));
            case InputPlace.Check:
                return english.Ui("bookSaidCheck");
            default:
                return entry.Npc is null ? english.Ui("bookSaidTalk") : english.Ui("bookSaidTalkTo", ("npc", english.NpcName(entry.Npc)));
        }
    }

    private sealed record UnitState(int Number, int Steps, int Finished, bool Ticked);
}
