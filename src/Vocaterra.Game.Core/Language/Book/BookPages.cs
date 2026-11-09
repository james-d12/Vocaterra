using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
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

    /// <summary>The lines of a page, built from the session's record (no drawing here): the list's lines, each entry followed by its <see cref="BookLineKind.Detail"/> lines. The book's layer and its speaker controls read the same lines (S20b).</summary>
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
            BookPage.Phrases => Phrases(session.Locale, PhraseJournalRows.Build(session.PhraseProgress, session.StoredTime.UnixSeconds, session.Credit, session.Locale), recordings, ReviewLine(session)),
            BookPage.Lessons => Lessons(session.Locale, session.Lessons.Record, session.Phrases, recordings, Waits(session.Lessons)),
            _ => Said(session.Learner.Recent(LearnerRecord.SavedEntries), session.Locale, session.Lessons.Record),
        };
    }

    /// <summary>
    /// The left page's list (S28, U07): the page's lines without the <see cref="BookLineKind.Detail"/> lines, which only the right page shows. A section is one
    /// row, so a row's index here is its line's index: the scroll and the buttons' hit tests count the same rows the layer draws. The book's layer and its controls read the same list.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="page">The page.</param>
    /// <param name="recordings">Which recording each row plays.</param>
    public static IReadOnlyList<BookLine> ListFor(GuideSession session, BookPage page, BookRecordings recordings) =>
        InListOnly(ForPage(session, page, recordings));

    /// <summary>The lines of a page that belong to the left list: every line but a <see cref="BookLineKind.Detail"/>.</summary>
    /// <param name="lines">The page's lines, with their details (<see cref="ForPage"/>).</param>
    public static IReadOnlyList<BookLine> InListOnly(IReadOnlyList<BookLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Where(static line => line.InList).ToList();
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
    /// The lines of the item the right page details (S28, U07): an entry and the <see cref="BookLineKind.Detail"/> lines after it. Empty when the line is outside the lines
    /// or is a section, a gap, a note or a row of tiles.
    /// </summary>
    /// <param name="lines">The page's lines with their details (<see cref="ForPage"/>).</param>
    /// <param name="line">The index of the entry, or of one of its details.</param>
    public static IReadOnlyList<BookLine> BlockOf(IReadOnlyList<BookLine> lines, int line)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (line < 0 || line >= lines.Count)
        {
            return [];
        }

        var start = line;
        while (start > 0 && lines[start].Kind == BookLineKind.Detail)
        {
            start--;
        }

        if (lines[start].Kind != BookLineKind.Entry)
        {
            return [];
        }

        var end = start + 1;
        while (end < lines.Count && lines[end].Kind == BookLineKind.Detail)
        {
            end++;
        }

        return lines.Skip(start).Take(end - start).ToList();
    }

    /// <summary>The block of the entry at a row of the left list: <see cref="BlockOf"/> for the row's index among the lines that are in the list.</summary>
    /// <param name="lines">The page's lines with their details (<see cref="ForPage"/>).</param>
    /// <param name="row">The row's index in the list (<see cref="InListOnly"/>).</param>
    public static IReadOnlyList<BookLine> BlockOfRow(IReadOnlyList<BookLine> lines, int row)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var seen = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].InList && ++seen == row)
            {
                return BlockOf(lines, i);
            }
        }

        return [];
    }

    /// <summary>
    /// The nouns page (U07): the catalogue's nouns in three sections, Known, Learning (a noun with credit or met) and Not met yet (an empty section is left out). A noun
    /// the learner has met is one entry (its icon, its word with its article, the three pips, a speaker button) whose details are the right page: its progress in words, what
    /// the learner did with it (heard, read, said, correct), when its next credited use is possible and the forms and last sentence it was used in. The nouns not met yet are
    /// only grey icons, twelve to a row. A noun outside the catalogue that the learner used (a lemma with no space) is an entry of Learning with no pips.
    /// </summary>
    /// <param name="ui">The support language (the page's words, C04).</param>
    /// <param name="words">The word progress (the catalogue's counts).</param>
    /// <param name="learnerNouns">The noun rows of the learner's record (<c>session.Learner.Nouns</c>).</param>
    /// <param name="now">The stored time in Unix seconds (the due text).</param>
    /// <param name="definite">The definite article of a noun from its gender and lemma (<c>el</c> for <c>agua</c>).</param>
    /// <param name="sheet">The phrase sheet (for the English of a sentence that is a phrase).</param>
    /// <param name="unseen">The label of a noun not met yet (<c>¿?</c>, <c>game/words.json</c>); no longer written on the page, which shows the noun's icon in grey.</param>
    /// <param name="recordings">The recordings (S20b): a seen catalogue noun gets a speaker button for its word card. A noun outside the catalogue gets none.</param>
    public static IReadOnlyList<BookLine> Nouns(LocaleText ui, WordProgress words, IReadOnlyList<WordRow> learnerNouns, long now, Func<string, string, string> definite, PhraseSheet sheet, string unseen, BookRecordings? recordings = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(learnerNouns);
        ArgumentNullException.ThrowIfNull(definite);
        ArgumentNullException.ThrowIfNull(sheet);
        var learner = learnerNouns.Where(static row => !row.Lemma.Contains(' ', StringComparison.Ordinal)).ToDictionary(static row => row.Lemma, StringComparer.Ordinal);
        var catalogue = new HashSet<string>(StringComparer.Ordinal);
        var rows = JournalRows.Build(words, now, unseen, ui);
        foreach (var row in rows)
        {
            catalogue.Add(row.Lemma);
        }

        var known = rows.Where(static row => row.Known).ToList();
        var learning = rows.Where(static row => !row.Known && (row.Seen || row.Record.CreditUnits > 0)).ToList();
        var notMet = rows.Where(static row => !row.Known && !row.Seen && row.Record.CreditUnits == 0).ToList();
        var others = learner.Values.Where(row => !catalogue.Contains(row.Lemma)).OrderBy(static row => row.Lemma, StringComparer.Ordinal).ToList();
        var lines = new List<BookLine>();
        if (known.Count > 0)
        {
            AddSection(lines, ui, BookTone.Known, "bookSectionKnown", known.Count);
            foreach (var row in known)
            {
                AddNoun(lines, ui, row, learner, sheet, recordings);
            }
        }

        if (learning.Count + others.Count > 0)
        {
            AddSection(lines, ui, BookTone.Learning, "bookSectionLearning", learning.Count + others.Count);
            foreach (var row in learning)
            {
                AddNoun(lines, ui, row, learner, sheet, recordings);
            }

            foreach (var row in others)
            {
                var label = row.Gender is null ? row.Lemma : $"{definite(row.Gender, row.Lemma)} {row.Lemma}";
                lines.Add(new BookLine(BookTone.Seen, label, Lemma: row.Lemma));
                lines.Add(Detail(BookTone.Hint, ui.Ui("bookUsed", ("times", N(row.Times)), ("correct", N(row.TimesCorrect))), BookDetailRole.Sub));
                AddUse(ui, lines, row, sheet);
            }
        }

        if (notMet.Count > 0)
        {
            AddSection(lines, ui, BookTone.Unseen, "bookSectionNotMet", notMet.Count);
            for (var i = 0; i < notMet.Count; i += BookLine.TilesPerRow)
            {
                lines.Add(new BookLine(BookTone.Unseen, string.Empty, Kind: BookLineKind.Tiles, Lemmas: notMet.Skip(i).Take(BookLine.TilesPerRow).Select(static row => row.Lemma).ToList()));
            }
        }

        return lines;
    }

    // A section of the list: a gap before it (none at the page's top), then its band with the count.
    private static void AddSection(List<BookLine> lines, LocaleText ui, BookTone tone, string key, int count, params (string Name, string Value)[] arguments)
    {
        if (lines.Count > 0)
        {
            lines.Add(BookLine.Gap);
        }

        lines.Add(new BookLine(tone, ui.Ui(key, arguments), Right: N(count), Kind: BookLineKind.Section));
    }

    private static BookLine Detail(BookTone tone, string text, BookDetailRole role = BookDetailRole.Text, string? aside = null, string? right = null) =>
        new(tone, text, Aside: aside, Right: right, Kind: BookLineKind.Detail, Role: role);

    private static BookLine Label(string text, string? count = null) => Detail(BookTone.Hint, text, BookDetailRole.Label, right: count);

    private static BookLine StatsLine(params (string Number, string Caption)[] stats) =>
        new(BookTone.Hint, string.Empty, Kind: BookLineKind.Detail, Role: BookDetailRole.Stats, Stats: stats.Select(static s => new BookStat(s.Number, s.Caption)).ToList());

    private static void AddNoun(List<BookLine> lines, LocaleText ui, JournalRow row, Dictionary<string, WordRow> learner, PhraseSheet sheet, BookRecordings? recordings)
    {
        var tone = row.Known ? BookTone.Known : row.Record.CreditUnits > 0 ? BookTone.Learning : BookTone.Seen;
        lines.Add(new BookLine(tone, row.Label, Lemma: row.Lemma, Pips: row.Pips, Speak: row.Seen ? recordings?.ForNoun(row.Lemma) : null));
        var progress = row.Known ? ui.Ui(KnownTagId) : ui.Ui("bookNounProgress", ("n", N(Math.Min(row.Pips, WordProgress.UsesToKnow))), ("of", N(WordProgress.UsesToKnow)));
        lines.Add(new BookLine(row.Known ? BookTone.Known : BookTone.Hint, progress, Pips: row.Pips, Kind: BookLineKind.Detail, Role: BookDetailRole.Sub));
        lines.Add(Label(ui.Ui("bookDidLabel")));
        lines.Add(StatsLine(
            (N(row.Record.Heard), ui.Ui("bookStatHeard")),
            (N(row.Record.Read), ui.Ui("bookStatRead")),
            (N(row.Record.Spoken), ui.Ui("bookStatSaid")),
            (N(row.Record.SpokenCorrect), ui.Ui("bookStatCorrect"))));
        if (row.DueText is not null)
        {
            lines.Add(Detail(BookTone.Hint, row.DueText));
        }
        else if (!row.Known)
        {
            lines.Add(Detail(BookTone.Hint, ui.Ui("bookNounNote", ("of", N(WordProgress.UsesToKnow)))));
        }

        if (learner.TryGetValue(row.Lemma, out var used))
        {
            AddUse(ui, lines, used, sheet);
        }
    }

    /// <summary>
    /// The verbs page (U07): one entry for every verb the learner used (a lemma with no space), its infinitive with its English as the aside and its uses at the right, then a
    /// quiet note that more verbs come with use. The entry's details are the right page's "What you said": each form with its label (<see cref="BookVerbLabels"/>), its counts and
    /// its last sentence. The right page's table is built from the learner's record by the layer (<see cref="VerbTable"/>).
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
            lines.Add(new BookLine(BookTone.Known, row.Lemma, Aside: gloss?.English, Right: ui.Ui("bookUsed", ("times", N(row.Times)), ("correct", N(row.TimesCorrect))), Verb: row.Lemma));
            foreach (var form in row.Forms)
            {
                var (person, number, tense, mood) = VerbReading.Normalize(row.Lemma, form.Form, form.Person, form.Number, form.Tense, form.Mood, morphology);
                var label = BookVerbLabels.Label(form.Form, row.Lemma, gloss, person, number, tense, mood, form.Question);
                lines.Add(Detail(BookTone.Seen, label, right: ui.Ui("bookFormCounts", ("uses", ui.Plural("ui.bookUses", form.Times)), ("correct", N(form.TimesCorrect)))));
                if (form.Last is { } last)
                {
                    lines.Add(Detail(BookTone.Hint, Last(ui, sheet, last.Sentence)));
                }
            }
        }

        if (lines.Count > 0)
        {
            lines.Add(BookLine.Gap);
            lines.Add(new BookLine(BookTone.Hint, ui.Ui("bookVerbsNote"), Kind: BookLineKind.Meta));
        }

        return lines;
    }

    /// <summary>
    /// The phrases page (U07): grouped by help, in three sections: Without help (a phrase met that has no help to fade), With help (a phrase met whose card or audio still shows) and
    /// Not met yet. Each phrase is one entry: its Spanish, its English as the aside, its lamps (the card and the audio, for a phrase met that has them) and a speaker button. The
    /// entry's details are the right page: its English, the help now named, what the learner did with it (the counts as numbers) and when its next credited use is possible. The
    /// "Review now" line, when something is due, is the top entry, with its button.
    /// </summary>
    /// <param name="ui">The support language (the sections' and the numbers' words, C04).</param>
    /// <param name="rows">The rows from <see cref="PhraseJournalRows.Build"/>.</param>
    /// <param name="recordings">The recordings (S20b): every phrase row gets a speaker button for its recording, when it has one.</param>
    /// <param name="review">The line above the rows that carries the "Review now" button (X06), or <see langword="null"/> when nothing the guide can ask is due.</param>
    public static IReadOnlyList<BookLine> Phrases(LocaleText ui, IReadOnlyList<PhraseJournalRow> rows, BookRecordings? recordings = null, string? review = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(rows);
        var lines = new List<BookLine>();
        if (review is not null)
        {
            lines.Add(new BookLine(BookTone.Gold, review, Practise: BookPractise.ReviewNow));
        }

        var without = rows.Where(static row => row.Record.Seen && !(row.HasScaffold && row.Level != SupportLevel.None)).ToList();
        var with = rows.Where(static row => row.Record.Seen && row.HasScaffold && row.Level != SupportLevel.None).ToList();
        var notMet = rows.Where(static row => !row.Record.Seen).ToList();
        foreach (var (key, tone, group) in new[] { ("bookSectionWithoutHelp", BookTone.Known, without), ("bookSectionWithHelp", BookTone.Learning, with), ("bookSectionNotMet", BookTone.Unseen, notMet) })
        {
            if (group.Count == 0)
            {
                continue;
            }

            AddSection(lines, ui, tone, key, group.Count);
            foreach (var row in group)
            {
                AddPhrase(lines, ui, row, recordings);
            }
        }

        return lines;
    }

    private static void AddPhrase(List<BookLine> lines, LocaleText ui, PhraseJournalRow row, BookRecordings? recordings)
    {
        var tone = row.HasScaffold && row.Level == SupportLevel.None && row.Record.CreditedUses > 0 ? BookTone.Known : row.Record.Seen ? BookTone.Learning : BookTone.Unseen;
        var lamps = row.Record.Seen && row.HasScaffold ? new[] { SupportLevels.ShowsCard(row.Level), row.Level != SupportLevel.None } : null;
        lines.Add(new BookLine(tone, row.Spanish, Aside: row.English, Lamps: lamps, Speak: recordings?.ForPhrase(row.Id)));
        lines.Add(Detail(BookTone.Seen, row.English, BookDetailRole.Sub));
        if (lamps is not null)
        {
            lines.Add(Label(ui.Ui("bookHelpNow")));
            lines.Add(new BookLine(BookTone.Known, ui.Ui("bookLampCard"), Aside: ui.Ui("bookLampCardNote"), Lamps: [lamps[0]], Kind: BookLineKind.Detail));
            lines.Add(new BookLine(BookTone.Known, ui.Ui("bookLampAudio"), Aside: ui.Ui("bookLampAudioNote"), Lamps: [lamps[1]], Kind: BookLineKind.Detail));
            lines.Add(Detail(BookTone.Hint, ui.Ui("bookHelpFades")));
        }

        lines.Add(Label(ui.Ui("bookDidLabel")));
        lines.Add(row.Hears
            ? StatsLine((N(row.Record.Heard), ui.Ui("bookStatHeard")), (N(row.Record.Answered), ui.Ui("bookStatAnswered")))
            : StatsLine(
                (PhraseProgress.Uses(row.Record.Supported), ui.Ui("bookStatWithHelp")),
                (PhraseProgress.Uses(row.Record.Unaided), ui.Ui("bookStatWithoutHelp")),
                (N(row.Record.ChoiceOnly), ui.Ui("bookStatChoices")),
                (N(row.Record.DontKnow), ui.Ui("bookStatDontKnow")),
                (PhraseProgress.Uses(row.Record.Transfer), ui.Ui("bookStatOther"))));
        if (row.DueText is not null)
        {
            lines.Add(Detail(BookTone.Hint, row.DueText));
        }
    }

    // The Phrases page's "Review now" line (X06): how many phrases the warm-up would ask, while some are due and no warm-up is under way.
    private static string? ReviewLine(GuideSession session) =>
        session.Deck.Ready is > 0 and var ready && session.Deck.Status != DeckStatus.Running ? session.Locale.Plural("ui.bookReviewDue", ready) : null;

    /// <summary>
    /// The lessons page (U07): one entry for every lesson of the bundle in its order: a mark (a ticked box for a lesson done, a half box under way, an arrow for the one the
    /// objective sends the learner to, a dashed box for one that waits), its short title, and its state as the aside: <c>completed in session 1</c>, <c>in progress</c>, <c>next:
    /// talk to Luna</c> (in gold, title and line, so the eye finds it first), <c>open: talk to Luna</c> for another unlocked one, <c>opens in session 4, Mar</c> for one whose session has
    /// not come, <c>after “El pueblo”</c> for one whose earlier lesson is not done (V12c). The entry's details are the right page: the full title, the state, the summary of a lesson done,
    /// its units as a strip of marks (with the steps of an unfinished unit), the help it used as numbers and the phrases it taught, each with its speaker button. Once every lesson is
    /// completed (after session 10's market, village-plan V15) the page opens with a note that says the series is done, and no lesson says <c>next:</c>.
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
            lines.Add(new BookLine(BookTone.Gold, ui.Ui("bookLessonsAllDone", ("count", N(lessons.Count))), Kind: BookLineKind.Meta));
            lines.Add(BookLine.Gap);
        }

        foreach (var lesson in lessons)
        {
            var units = UnitsOf(lesson);
            var doneUnits = units.Count(static unit => unit.Ticked);
            var wait = lesson.Status == LessonStatus.NotStarted && waits is not null && waits.TryGetValue(lesson.LessonId, out var found) ? found : null;
            var (tone, mark, state) = lesson.Status switch
            {
                LessonStatus.Completed => (BookTone.Known, BookMark.Done, lesson.CompletedSession is { } session ? ui.Ui("bookLessonCompletedIn", ("session", N(session))) : ui.Ui("bookLessonCompleted")),
                LessonStatus.InProgress => (BookTone.Learning, BookMark.Half, ui.Ui("bookLessonInProgress", ("done", N(doneUnits)), ("units", N(units.Count)))),
                _ => (
                    wait is { Kind: LessonWaitKind.Next } ? BookTone.Gold : wait is { Kind: LessonWaitKind.Open } ? BookTone.Seen : BookTone.Unseen,
                    wait is { Kind: LessonWaitKind.Next } ? BookMark.Next : BookMark.Wait,
                    wait is null ? ui.Ui("bookLessonNotStarted") : WaitText(ui, wait, lessons)),
            };
            var practise = lesson.Status == LessonStatus.Completed || wait is { Kind: LessonWaitKind.Open } || (lesson.Class && lesson.Status == LessonStatus.InProgress) ? lesson.LessonId : null;
            lines.Add(new BookLine(tone, LessonDefinition.ShortOf(lesson.Title), Aside: state, Mark: mark, Practise: practise, Next: wait is { Kind: LessonWaitKind.Next }, Ask: lesson.Status != LessonStatus.Completed));
            lines.Add(Detail(tone, lesson.Title, BookDetailRole.Title));
            var subTone = mark switch { BookMark.Next => BookTone.Gold, BookMark.Half => BookTone.Seen, _ => BookTone.Hint }; // a lesson done or waiting is quiet; the one to go to is gold
            lines.Add(new BookLine(subTone, state, Mark: mark, Kind: BookLineKind.Detail, Role: BookDetailRole.Sub));
            if (lesson.Status == LessonStatus.Completed && lesson.Summary is { } summary)
            {
                lines.Add(Detail(BookTone.Seen, summary));
            }

            if (units.Count > 0)
            {
                lines.Add(Label(ui.Ui("bookUnitsLabel"), ui.Ui("bookUnitsDone", ("done", N(doneUnits)), ("units", N(units.Count)))));
                lines.Add(new BookLine(BookTone.Seen, string.Empty, Kind: BookLineKind.Detail, Role: BookDetailRole.Units, Marks: units.Select(static unit => unit.Ticked ? BookMark.Done : unit.Finished > 0 ? BookMark.Half : BookMark.Empty).ToList()));
                var open = units.Where(static unit => !unit.Ticked).ToList();
                if (open.Count > 0)
                {
                    lines.Add(Detail(BookTone.Hint, string.Join("   ", open.Select(unit => ui.Ui("bookUnit", ("number", N(unit.Number))) + ": " + ui.Ui("bookUnitSteps", ("finished", N(unit.Finished)), ("steps", N(unit.Steps)))))));
                }
            }

            if (lesson.Status != LessonStatus.NotStarted)
            {
                lines.Add(Label(ui.Ui("bookHelpLabel")));
                lines.Add(StatsLine(
                    (N(lesson.HelpUses), ui.Ui("bookStatHelpUsed")),
                    (N(lesson.DontKnowUses), ui.Ui("bookStatDontKnowI")),
                    (N(lesson.RepeatUses), ui.Ui("bookStatRepeats")),
                    (N(lesson.Turns), ui.Ui("bookStatTurns"))));
            }

            if (lesson.Phrases.Count > 0)
            {
                lines.Add(Label(ui.Ui("bookPhrasesLabel"), N(lesson.Phrases.Count)));
                foreach (var phrase in lesson.Phrases)
                {
                    var row = sheet.Find(phrase);
                    lines.Add(new BookLine(BookTone.Known, row?.Spanish ?? phrase, Aside: row?.English, Speak: recordings?.ForPhrase(phrase), Kind: BookLineKind.Detail));
                }
            }
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
        LessonWaitKind.Meet => ui.Ui("bookLessonMeet", ("npc", ui.Called(wait.Npc))),
        _ => ui.Ui("bookLessonAfter", ("title", LessonDefinition.ShortOf(lessons.FirstOrDefault(l => string.Equals(l.LessonId, wait.After, StringComparison.Ordinal))?.Title ?? wait.After ?? string.Empty))),
    };

    /// <summary>
    /// The "What I said" page (U07): the newest lines the learner gave (<c>session.Learner.Recent(n)</c>, newest first), in a section for each run of one play session. An entry is
    /// a mark (how the core took the line: understood, understood with a small mistake, not understood, or a dot for a line held back or a request for help), the line in quotes
    /// (<c>(nothing heard)</c> for a blank one) and where it was said in a word or two. Its details are the right page: the line, the outcome in words, the session, how it came and
    /// where, and the core's feedback in English (S16's text), uncut.
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
        for (var i = 0; i < recent.Count;)
        {
            var run = recent.Skip(i).TakeWhile(entry => entry.Session == recent[i].Session).Count();
            AddSection(lines, english, BookTone.Seen, "bookSession", run, ("session", N(recent[i].Session)));
            foreach (var entry in recent.Skip(i).Take(run))
            {
                var mark = MarkOf(entry);
                var blank = string.IsNullOrWhiteSpace(entry.Text);
                var text = blank ? english.Ui("bookNothingHeard") : $"\"{entry.Text}\"";
                lines.Add(new BookLine(blank ? BookTone.Hint : BookTone.Known, text, Right: ShortPlace(entry, english), Mark: mark));
                lines.Add(new BookLine(BookTone.Known, OutcomeText(english, entry, mark), Mark: mark, Kind: BookLineKind.Detail, Role: BookDetailRole.Sub));
                lines.Add(Detail(BookTone.Hint, english.Ui("bookSaidMeta", ("session", N(entry.Session)), ("source", SourceText(english, entry.Source)), ("place", PlaceText(entry, english, lessons)))));
                var feedback = english.FeedbackFor(entry);
                if (feedback.Count > 0)
                {
                    lines.Add(Label(english.Ui("bookFeedbackLabel")));
                    foreach (var line in feedback)
                    {
                        lines.Add(Detail(BookTone.Seen, line));
                    }
                }
            }

            i += run;
        }

        return lines;
    }

    // How the core took a line (the mark): held back or a request for help says nothing about the word (a dot); otherwise understood (a tick), understood with errors (an
    // exclamation mark) or not understood (a cross).
    private static BookMark MarkOf(InputEntry entry) =>
        entry.Held || string.Equals(entry.Intent, "ask_help", StringComparison.Ordinal) ? BookMark.Dot
        : entry.Communicated ? (entry.Errors.Count > 0 ? BookMark.Slip : BookMark.Understood)
        : BookMark.Missed;

    private static string OutcomeText(LocaleText ui, InputEntry entry, BookMark mark) => ui.Ui(mark switch
    {
        BookMark.Understood => "bookOutcomeGood",
        BookMark.Slip => "bookOutcomeSlip",
        BookMark.Missed => "bookOutcomeMiss",
        _ => entry.Held ? "bookOutcomeHeld" : "bookOutcomeHelp",
    });

    // Where a line was said in a word or two, for the row: talk, lesson · u3, a check.
    private static string ShortPlace(InputEntry entry, LocaleText ui)
    {
        switch (entry.Place)
        {
            case InputPlace.Lesson:
                var step = entry.Unit is { } full && full.Contains('/', StringComparison.Ordinal) ? UnitOf(full[(full.IndexOf('/', StringComparison.Ordinal) + 1)..]) : string.Empty;
                return step.Length > 0 ? ui.Ui("bookSaidPlaceLesson", ("step", step)) : ui.Ui("bookSaidPlaceLessonOnly");
            case InputPlace.Check:
                return ui.Ui("bookSaidCheck");
            default:
                return ui.Ui("bookSaidTalk");
        }
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

    // What the learner said of a noun: the forms with their counts (agua 5   aguas 1), then the last sentence with the sheet's English when it is a phrase.
    private static void AddUse(LocaleText ui, List<BookLine> lines, WordRow row, PhraseSheet sheet)
    {
        lines.Add(Label(ui.Ui("bookWhatYouSaid")));
        var forms = string.Join("   ", row.Forms.Select(static form => $"{form.Form} {form.Times.ToString(CultureInfo.InvariantCulture)}"));
        if (forms.Length > 0)
        {
            lines.Add(Detail(BookTone.Seen, forms));
        }

        lines.Add(Detail(BookTone.Hint, Last(ui, sheet, row.Last.Sentence)));
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
