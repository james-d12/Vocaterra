using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Language.Learner;

/// <summary>
/// The console summary of the learner's record at exit (S19's demo, beside the phrases' summary): the nouns and verbs with their counts and forms, and the
/// lessons with their states. Plain English and Spanish text, in the order the record gives (sorted), so the same record always prints the same lines.
/// </summary>
public static class LearnerSummary
{
    /// <summary>The summary's lines.</summary>
    /// <param name="learner">The learner's record.</param>
    /// <param name="lessons">The lesson record (<see cref="LessonRunner.Record"/>).</param>
    /// <param name="session">The play session the summary is at.</param>
    /// <param name="storedTime">The stored game time the summary is at, in Unix seconds.</param>
    public static IReadOnlyList<string> Lines(LearnerRecord learner, IReadOnlyList<LessonRecordRow> lessons, int session, long storedTime)
    {
        ArgumentNullException.ThrowIfNull(learner);
        ArgumentNullException.ThrowIfNull(lessons);
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"book: summary at exit (session {session}, stored time {storedTime}, {learner.History.Count} lines in the history)"),
        };

        var nouns = learner.Nouns;
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"  nouns ({nouns.Count}):"));
        foreach (var noun in nouns)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    {noun.Lemma}{(noun.Gender is null ? string.Empty : $" ({noun.Gender})")}: used {noun.Times}, correct {noun.TimesCorrect}; forms {FormsOf(noun)}; last \"{noun.LastSentence}\""));
        }

        var verbs = learner.Verbs;
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"  verbs ({verbs.Count}):"));
        foreach (var verb in verbs)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    {verb.Lemma}: used {verb.Times}, correct {verb.TimesCorrect}; forms {FormsOf(verb)}; last \"{verb.LastSentence}\""));
        }

        var others = learner.Rows.Where(static row => row.Pos is not ("noun" or "verb")).ToList();
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"  other words: {others.Count} rows ({string.Join(", ", others.Select(static row => row.Pos).Distinct(StringComparer.Ordinal).OrderBy(static p => p, StringComparer.Ordinal))})"));

        lines.Add(string.Create(CultureInfo.InvariantCulture, $"  lessons ({lessons.Count}):"));
        foreach (var lesson in lessons)
        {
            var status = lesson.Status switch
            {
                LessonStatus.Completed => "completed" + (lesson.CompletedSession is { } done ? $" in session {done}" : string.Empty),
                LessonStatus.InProgress => "in progress",
                _ => "not started",
            };
            var units = string.Join(", ", lesson.Units.Select(static u => $"{u.StepId} {OutcomeName(u.Outcome)}"));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    {lesson.LessonId}: {status}; units {lesson.Units.Count}/{lesson.StepCount}{(units.Length == 0 ? string.Empty : $" ({units})")}; turns {lesson.Turns}; help {lesson.HelpUses}; no sé {lesson.DontKnowUses}; repeats {lesson.RepeatUses}"));
        }

        return lines;
    }

    // "agua 2, aguas 1" or "quiero (1 singular present) 1, quieres (2 singular present) 1".
    private static string FormsOf(WordRow row) => string.Join(", ", row.Forms.Select(static f =>
    {
        var features = string.Join(" ", new[] { f.Person, f.Number, f.Tense, f.Mood }.Where(static v => v is not null));
        return features.Length == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{f.Form} {f.Times}")
            : string.Create(CultureInfo.InvariantCulture, $"{f.Form} ({features}) {f.Times}");
    }));

    private static string OutcomeName(StepOutcome outcome) => outcome switch
    {
        StepOutcome.Done => "done",
        StepOutcome.Skipped => "skipped",
        _ => "moved on",
    };
}
