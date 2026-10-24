using System.Globalization;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game;

/// <summary>
/// What the session summary is built from (X04, R01): this session's lines and events, the lesson record, the phrase sheet and the phrase records. Plain values, so the
/// summary of a fixed record is a fixed text (the tests build one by hand).
/// </summary>
/// <param name="Session">The play session the summary is of.</param>
/// <param name="History">The learner's history, every session (<see cref="LearnerRecord.History"/>); the session's own lines are the ones of <paramref name="Session"/>.</param>
/// <param name="Events">The game events of this run (<see cref="GuideSession.Events"/>): the phrase uses and the lesson events.</param>
/// <param name="Lessons">The lesson record, in the bundle's order (<see cref="LessonRunner.Record"/>).</param>
/// <param name="Phrases">The phrase sheet, for the Spanish of each phrase.</param>
/// <param name="PhraseRecords">The phrase records (<see cref="PhraseProgress.Records"/>): the support level each phrase stands at.</param>
public sealed record SummaryFacts(
    int Session,
    IReadOnlyList<InputEntry> History,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyList<LessonRecordRow> Lessons,
    PhraseSheet Phrases,
    IReadOnlyList<PhraseRecord> PhraseRecords)
{
    /// <summary>The facts of a session as it stands now.</summary>
    /// <param name="session">The session.</param>
    public static SummaryFacts Of(GuideSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SummaryFacts(session.PlaySession, session.Learner.History, session.Events, session.Lessons.Record, session.Phrases, session.PhraseProgress.Records);
    }
}

/// <summary>
/// The English session summary (X04, R01): the lessons done or begun, the new words met, the phrases said without help, the verb forms used, one or two items to
/// practise next time, and a line on how to replay a lesson. It is built from the learner's record and the session's events, as counts of things done, never a
/// score, a CEFR label or a count of failures (D-game-10); it is presentation only and nothing in the evaluation path reads it. Every line is always present, so the
/// card has the same shape each time.
/// </summary>
public static class SessionSummary
{
    /// <summary>How many items a line lists before it says how many more there are.</summary>
    public const int ListLimit = 6;

    /// <summary>How many items the practise line names.</summary>
    public const int PractiseLimit = 2;

    /// <summary>The summary's lines, in the card's order: lessons, new words, said without help, verb forms, practise next time, and the replay line.</summary>
    /// <param name="facts">What the summary is built from.</param>
    /// <param name="english">The game's English text.</param>
    public static IReadOnlyList<string> Lines(SummaryFacts facts, EnglishText english)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(english);
        return
        [
            LessonsLine(facts, english),
            WordsLine(facts, english),
            UnaidedLine(facts, english),
            FormsLine(facts, english),
            PractiseLine(facts, english),
            english.Ui("summaryReplay"),
        ];
    }

    // The lessons of the record that this run completed or moved on in, in the bundle's order.
    private static string LessonsLine(SummaryFacts facts, EnglishText english)
    {
        var lessonEvents = facts.Events.OfType<LessonEvent>().ToList();
        var items = new List<string>();
        foreach (var lesson in facts.Lessons)
        {
            if (lessonEvents.Any(e => e.Kind == LessonEventKind.LessonCompleted && string.Equals(e.Lesson, lesson.LessonId, StringComparison.Ordinal)))
            {
                items.Add(english.Ui("summaryLessonDone", ("title", lesson.Title)));
                continue;
            }

            var steps = lessonEvents.Count(e => e.Kind == LessonEventKind.StepDone && string.Equals(e.Lesson, lesson.LessonId, StringComparison.Ordinal));
            if (steps > 0)
            {
                items.Add(english.Ui("summaryLessonAdvanced", ("title", lesson.Title), ("steps", steps.ToString(CultureInfo.InvariantCulture))));
            }
        }

        return english.Ui("summaryLessons", ("items", ListOrNone(items, english, ListLimit, "; ")));
    }

    // The words this session's lines used that no earlier session's line used (the learner's own lines: the word record counts only them).
    private static string WordsLine(SummaryFacts facts, EnglishText english)
    {
        var before = facts.History.Where(e => e.Session < facts.Session).SelectMany(static e => e.Words)
            .Where(static w => w.Role == WordRole.Word && w.Lemma is not null).Select(static w => w.Lemma!).ToHashSet(StringComparer.Ordinal);
        var now = facts.History.Where(e => e.Session == facts.Session).SelectMany(static e => e.Words)
            .Where(static w => w.Role == WordRole.Word && w.Lemma is not null && string.Equals(w.Pos, "noun", StringComparison.Ordinal))
            .Select(static w => w.Lemma!).Where(lemma => !before.Contains(lemma)).Distinct(StringComparer.Ordinal).OrderBy(static l => l, StringComparer.Ordinal).ToList();
        return english.Ui("summaryWords", ("items", ListOrNone(now, english, ListLimit)));
    }

    // The phrases said without help this session, correct ones only, in the order they were first said.
    private static string UnaidedLine(SummaryFacts facts, EnglishText english)
    {
        var said = facts.Events.OfType<PhraseUse>()
            .Where(static u => u.Correct && u.Kind is PhraseUseKind.Unaided or PhraseUseKind.Transfer)
            .Select(u => SpanishOf(facts, u.PhraseId)).Distinct(StringComparer.Ordinal).ToList();
        return english.Ui("summaryUnaided", ("items", ListOrNone(said, english, ListLimit)));
    }

    // The verb forms this session's lines used, with their tense and person (quiero (present, 1st singular)).
    private static string FormsLine(SummaryFacts facts, EnglishText english)
    {
        var forms = facts.History.Where(e => e.Session == facts.Session).SelectMany(static e => e.Words)
            .Where(static w => w.Role == WordRole.Word && string.Equals(w.Pos, "verb", StringComparison.Ordinal) && (w.Person is not null || w.Tense is not null))
            .Select(w => FormOf(w, english)).Distinct(StringComparer.Ordinal).OrderBy(static f => f, StringComparer.Ordinal).ToList();
        return english.Ui("summaryForms", ("items", ListOrNone(forms, english, ListLimit)));
    }

    // Up to two phrases to practise next time: first the ones the learner said "no sé" to, then the ones said wrongly, then the ones used this session whose help is still on.
    private static string PractiseLine(SummaryFacts facts, EnglishText english)
    {
        var uses = facts.Events.OfType<PhraseUse>().ToList();
        var items = new List<string>();
        void Add(IEnumerable<string> ids, string reason)
        {
            foreach (var id in ids)
            {
                if (items.Count >= PractiseLimit)
                {
                    return;
                }

                var text = english.Ui(reason, ("phrase", SpanishOf(facts, id)));
                if (!items.Contains(text, StringComparer.Ordinal))
                {
                    items.Add(text);
                }
            }
        }

        Add(uses.Where(static u => u.Kind == PhraseUseKind.DontKnow).SelectMany(static u => u.InPlay ?? Array.Empty<string>()).Where(id => IsLearnerPhrase(facts, id)), "summaryPractiseNoSe");
        Add(uses.Where(static u => !u.Correct && u.Kind is PhraseUseKind.Supported or PhraseUseKind.Unaided or PhraseUseKind.Transfer).Select(static u => u.PhraseId), "summaryPractiseSlip");
        Add(uses.Where(static u => u.Kind is PhraseUseKind.Supported or PhraseUseKind.Unaided or PhraseUseKind.Transfer or PhraseUseKind.Choice)
            .Select(static u => u.PhraseId).Where(id => facts.PhraseRecords.Any(r => string.Equals(r.Id, id, StringComparison.Ordinal) && r.Level != SupportLevel.None)), "summaryPractiseSupport");
        return items.Count == 0
            ? english.Ui("summaryPractiseNone")
            : english.Ui("summaryPractise", ("items", string.Join("; ", items)));
    }

    private static bool IsLearnerPhrase(SummaryFacts facts, string id) => facts.Phrases.IsPractisable(id);

    // The Spanish of a phrase, or its id when the sheet does not have it.
    private static string SpanishOf(SummaryFacts facts, string id) => facts.Phrases.Find(id)?.Spanish.TrimEnd('.') ?? id;

    // "quiero (present, 1st singular)": the text and the reading's tense and person (the number when it is read).
    private static string FormOf(GuideWord word, EnglishText english)
    {
        var features = new List<string>();
        if (word.Tense is { } tense)
        {
            features.Add(tense switch
            {
                "pres" => english.Ui("tensePres"),
                "pret" => english.Ui("tensePret"),
                "impf" => english.Ui("tenseImpf"),
                "fut" => english.Ui("tenseFut"),
                _ => tense,
            });
        }

        if (word.Person is { Length: > 0 } person)
        {
            var number = word.Number is { Length: > 0 } n && char.ToLowerInvariant(n[0]) == 'p' ? english.Ui("plural") : word.Number is { Length: > 0 } ? english.Ui("singular") : null;
            var label = person[0] switch
            {
                '1' => english.Ui("person1"),
                '2' => english.Ui("person2"),
                '3' => english.Ui("person3"),
                _ => person,
            };
            features.Add(number is null ? label : label + " " + number);
        }

        return features.Count == 0 ? word.Text : $"{word.Text} ({string.Join(", ", features)})";
    }

    // The items joined with the separator (commas, or semicolons when an item has commas), the first ListLimit of them, then "and N more"; or "none this session".
    private static string ListOrNone(IReadOnlyList<string> items, EnglishText english, int limit, string separator = ", ")
    {
        if (items.Count == 0)
        {
            return english.Ui("summaryNone");
        }

        var shown = string.Join(separator, items.Take(limit));
        return items.Count > limit ? $"{shown}, {english.Ui("summaryMore", ("n", (items.Count - limit).ToString(CultureInfo.InvariantCulture)))}" : shown;
    }
}
