using System.Globalization;
using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The colour family of a book line: the layer maps it to a colour.</summary>
public enum BookTone
{
    /// <summary>A bright line: a known word, a completed lesson, an understood line.</summary>
    Known,

    /// <summary>A line in progress: a word with some credit, a lesson under way.</summary>
    Learning,

    /// <summary>A line the learner has met.</summary>
    Seen,

    /// <summary>A line the learner has not met yet.</summary>
    Unseen,

    /// <summary>A dim line: counts, help, feedback.</summary>
    Hint,

    /// <summary>A gold line: a summary, a due word.</summary>
    Gold,
}

/// <summary>
/// One line of a book page (S20). A page is a list of these, drawn from the top with the scroll applied; a heading line may carry a noun's icon, its
/// pips, lamps (the phrases' help) or a tick box (the lessons' units). Plain values.
/// </summary>
/// <param name="Tone">The colour family.</param>
/// <param name="Text">The text, in English or Spanish as the learner reads it.</param>
/// <param name="Aside">Text after <paramref name="Text"/> in the dim colour: a phrase's English, a lesson's state.</param>
/// <param name="Right">Text at the right edge.</param>
/// <param name="Lemma">The noun whose icon the line shows at its left, or <see langword="null"/>.</param>
/// <param name="Pips">The three progress pips at the right (catalogue nouns), or <see langword="null"/>.</param>
/// <param name="Tick">A tick box at the left, ticked or not, or <see langword="null"/> for none.</param>
/// <param name="Lamps">Two small boxes at the right, lit or not (the card and the audio of a phrase's help), or <see langword="null"/>.</param>
/// <param name="Indent">Whether the line sits under its heading.</param>
public sealed record BookLine(
    BookTone Tone,
    string Text,
    string? Aside = null,
    string? Right = null,
    string? Lemma = null,
    int? Pips = null,
    bool? Tick = null,
    IReadOnlyList<bool>? Lamps = null,
    bool Indent = false)
{
    /// <summary>The empty line between two entries.</summary>
    public static BookLine Gap { get; } = new(BookTone.Hint, string.Empty);
}

/// <summary>
/// The lines of the book's pages (S20), built from the learner's record, the word progress, the phrase progress, the lessons and the English text.
/// Each builder is a pure function of its inputs: the same record gives the same lines, in the same order.
/// </summary>
public static class BookPages
{
    /// <summary>The tag of a known noun or verb.</summary>
    public const string KnownTag = "known";

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

    /// <summary>
    /// The nouns page: every catalogue noun (the words page as it was: heard, read, said and correct, the three pips, <c>¿?</c> before it is seen), then
    /// every other noun the learner used (a lemma with no space: a fixed expression such as <c>por favor</c> is not a noun), each with its article, its
    /// forms, its counts and its last sentence.
    /// </summary>
    /// <param name="words">The word progress (the catalogue's counts).</param>
    /// <param name="learnerNouns">The noun rows of the learner's record (<c>session.Learner.Nouns</c>).</param>
    /// <param name="now">The stored time in Unix seconds (the due text).</param>
    /// <param name="definite">The definite article of a noun from its gender and lemma (<c>el</c> for <c>agua</c>).</param>
    /// <param name="sheet">The phrase sheet (for the English of a sentence that is a phrase).</param>
    public static IReadOnlyList<BookLine> Nouns(WordProgress words, IReadOnlyList<WordRow> learnerNouns, long now, Func<string, string, string> definite, PhraseSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(learnerNouns);
        ArgumentNullException.ThrowIfNull(definite);
        ArgumentNullException.ThrowIfNull(sheet);
        var lines = new List<BookLine>();
        var learner = learnerNouns.Where(static row => !row.Lemma.Contains(' ', StringComparison.Ordinal)).ToDictionary(static row => row.Lemma, StringComparer.Ordinal);
        var catalogue = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in JournalRows.Build(words, now))
        {
            catalogue.Add(row.Lemma);
            var tone = row.Known ? BookTone.Known : row.Record.CreditUnits > 0 ? BookTone.Learning : row.Seen ? BookTone.Seen : BookTone.Unseen;
            lines.Add(new BookLine(tone, row.Label, Lemma: row.Lemma, Pips: row.Pips, Right: row.Known ? KnownTag : null));
            var counts = $"heard {row.Record.Heard}   read {row.Record.Read}   said {row.Record.Spoken}   correct {row.Record.SpokenCorrect}";
            lines.Add(new BookLine(BookTone.Hint, counts, Right: row.DueText, Indent: true));
            if (learner.TryGetValue(row.Lemma, out var used))
            {
                AddUse(lines, used, sheet);
            }

            lines.Add(BookLine.Gap);
        }

        foreach (var row in learner.Values.Where(row => !catalogue.Contains(row.Lemma)).OrderBy(static row => row.Lemma, StringComparer.Ordinal))
        {
            var label = row.Gender is null ? row.Lemma : $"{definite(row.Gender, row.Lemma)} {row.Lemma}";
            lines.Add(new BookLine(BookTone.Seen, label, Lemma: row.Lemma));
            lines.Add(new BookLine(BookTone.Hint, $"used {row.Times}   correct {row.TimesCorrect}", Indent: true));
            AddUse(lines, row, sheet);
            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>
    /// The verbs page: every verb the learner used (a lemma with no space), its infinitive with its English (<c>querer — to want</c>) and each form with its
    /// label (<see cref="VerbLabels"/>), its counts and its last sentence.
    /// </summary>
    /// <param name="learnerVerbs">The verb rows of the learner's record (<c>session.Learner.Verbs</c>).</param>
    /// <param name="glosses">The verbs' English.</param>
    /// <param name="sheet">The phrase sheet (for the English of a sentence that is a phrase).</param>
    public static IReadOnlyList<BookLine> Verbs(IReadOnlyList<WordRow> learnerVerbs, VerbGlosses glosses, PhraseSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(learnerVerbs);
        ArgumentNullException.ThrowIfNull(glosses);
        ArgumentNullException.ThrowIfNull(sheet);
        var lines = new List<BookLine>();
        foreach (var row in learnerVerbs.Where(static row => !row.Lemma.Contains(' ', StringComparison.Ordinal)).OrderBy(static row => row.Lemma, StringComparer.Ordinal))
        {
            var gloss = glosses.ForLemma(row.Lemma);
            var heading = gloss is null ? row.Lemma : row.Lemma + VerbLabels.Separator + gloss.English;
            lines.Add(new BookLine(BookTone.Known, heading, Right: $"used {row.Times}   correct {row.TimesCorrect}"));
            foreach (var form in row.Forms)
            {
                var label = VerbLabels.Label(form.Form, row.Lemma, gloss, form.Person, form.Number, form.Tense, form.Mood, form.Question);
                lines.Add(new BookLine(BookTone.Seen, label, Right: $"{form.Times} times   {form.TimesCorrect} correct", Indent: true));
                if (form.Last is { } last)
                {
                    lines.Add(new BookLine(BookTone.Hint, $"last: \"{last.Sentence}\"{EnglishSuffix(sheet, last.Sentence)}", Indent: true));
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
    public static IReadOnlyList<BookLine> Phrases(IReadOnlyList<PhraseJournalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var lines = new List<BookLine>();
        foreach (var row in rows)
        {
            var tone = row.HasScaffold && row.Level == SupportLevel.None && row.Record.CreditedUses > 0 ? BookTone.Known : row.Record.Seen ? BookTone.Learning : BookTone.Unseen;
            var lamps = row.HasScaffold ? new[] { SupportLevels.ShowsCard(row.Level), row.Level != SupportLevel.None } : null;
            lines.Add(new BookLine(tone, row.Spanish, Aside: row.English, Right: row.LevelText, Lamps: lamps));
            lines.Add(new BookLine(BookTone.Hint, row.CountsText, Right: row.DueText, Indent: true));
        }

        return lines;
    }

    /// <summary>
    /// The lessons page: every lesson of the bundle in its order, with its state (completed in session n, in progress with the units done, not
    /// started), its units with a tick each, the phrases it taught (S17's recap), the help used and, once completed, its English summary.
    /// </summary>
    /// <param name="lessons">The lesson record (<c>session.Lessons.Record</c>).</param>
    /// <param name="sheet">The phrase sheet (the English and the Spanish of the phrases).</param>
    public static IReadOnlyList<BookLine> Lessons(IReadOnlyList<LessonRecordRow> lessons, PhraseSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(sheet);
        var lines = new List<BookLine>();
        foreach (var lesson in lessons)
        {
            var units = UnitsOf(lesson);
            var doneUnits = units.Count(static unit => unit.Ticked);
            var (tone, state) = lesson.Status switch
            {
                LessonStatus.Completed => (BookTone.Known, lesson.CompletedSession is { } session ? $"completed in session {session}" : "completed"),
                LessonStatus.InProgress => (BookTone.Learning, $"in progress: {doneUnits} of {units.Count} units done"),
                _ => (BookTone.Unseen, "not started"),
            };
            lines.Add(new BookLine(tone, lesson.Title, Aside: state));
            if (lesson.Status == LessonStatus.Completed && lesson.Summary is { } summary)
            {
                lines.Add(new BookLine(BookTone.Gold, summary, Indent: true));
            }

            foreach (var unit in units)
            {
                lines.Add(new BookLine(unit.Ticked ? BookTone.Seen : BookTone.Unseen, $"unit {unit.Number}", Right: $"{unit.Finished} of {unit.Steps} steps", Tick: unit.Ticked, Indent: true));
            }

            foreach (var phrase in lesson.Phrases)
            {
                var row = sheet.Find(phrase);
                lines.Add(new BookLine(BookTone.Seen, row?.Spanish ?? phrase, Aside: row?.English, Indent: true));
            }

            lines.Add(new BookLine(
                BookTone.Hint,
                $"help used: {lesson.HelpUses}   I don't know: {lesson.DontKnowUses}   repeats asked: {lesson.RepeatUses}   turns: {lesson.Turns}",
                Indent: true));
            lines.Add(BookLine.Gap);
        }

        return lines;
    }

    /// <summary>
    /// The "What I said" page: the newest lines the learner gave (<c>session.Learner.Recent(n)</c>, newest first): the line, how it came (said or
    /// typed, or a button), where it was said, and the core's feedback in English (S16's text).
    /// </summary>
    /// <param name="recent">The entries, newest first.</param>
    /// <param name="english">The English text (the feedback and the NPCs' names).</param>
    /// <param name="lessons">The lesson record, for a lesson line's title.</param>
    public static IReadOnlyList<BookLine> Said(IReadOnlyList<InputEntry> recent, EnglishText english, IReadOnlyList<LessonRecordRow> lessons)
    {
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(english);
        ArgumentNullException.ThrowIfNull(lessons);
        var lines = new List<BookLine>();
        foreach (var entry in recent)
        {
            var tone = entry.Communicated && !entry.Held ? BookTone.Known : BookTone.Hint;
            lines.Add(new BookLine(tone, $"\"{entry.Text}\"", Aside: $"session {entry.Session}", Right: $"{SourceText(entry.Source)}, {PlaceText(entry, english, lessons)}"));
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

    private static void AddUse(List<BookLine> lines, WordRow row, PhraseSheet sheet)
    {
        var forms = string.Join("   ", row.Forms.Select(static form => $"{form.Form} {form.Times}"));
        if (forms.Length > 0)
        {
            lines.Add(new BookLine(BookTone.Hint, $"forms: {forms}", Indent: true));
        }

        var last = row.Last;
        lines.Add(new BookLine(BookTone.Hint, $"last: \"{last.Sentence}\"{EnglishSuffix(sheet, last.Sentence)}", Indent: true));
    }

    private static string EnglishSuffix(PhraseSheet sheet, string sentence) =>
        SheetEnglish(sheet, sentence) is { } english ? " = " + english : string.Empty;

    private static string SourceText(InputSource source) => source switch
    {
        InputSource.Voice => "said",
        InputSource.Typed => "typed",
        _ => "button",
    };

    private static string PlaceText(InputEntry entry, EnglishText english, IReadOnlyList<LessonRecordRow> lessons)
    {
        switch (entry.Place)
        {
            case InputPlace.Lesson:
                var lessonId = entry.Unit is { } unit ? unit.Split('/')[0] : string.Empty;
                var title = lessons.FirstOrDefault(row => string.Equals(row.LessonId, lessonId, StringComparison.Ordinal))?.Title ?? lessonId;
                var step = entry.Unit is { } full && full.Contains('/', StringComparison.Ordinal) ? UnitOf(full[(full.IndexOf('/', StringComparison.Ordinal) + 1)..]) : string.Empty;
                return step.Length > 0 ? $"lesson {title}, {step}" : $"lesson {title}";
            case InputPlace.Check:
                return "a check";
            default:
                return entry.Npc is null ? "talk" : "talk to " + english.NpcName(entry.Npc);
        }
    }

    private sealed record UnitState(int Number, int Steps, int Finished, bool Ticked);
}
