using System.Globalization;
using Vocaterra.Game.Core.Content;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>
/// The journal's rows, built from the learner's counts: every catalogue noun, the known ones first, then the ones learned in part, the ones met
/// and the ones not met yet, each group in catalogue (ordinal) order. Plain values; no clock.
/// </summary>
public static class JournalRows
{
    /// <summary>
    /// The text for the wait before a word can earn colour again: <c>again in 8 min</c> (minutes rounded up), <c>again in 40 s</c> under a minute, <c>again in 1 h</c>
    /// from an hour; <c>due for review</c> for zero (<c>ui.due*</c>, C04).
    /// </summary>
    /// <param name="ui">The support language.</param>
    /// <param name="seconds">The wait.</param>
    public static string DueText(LocaleText ui, long seconds)
    {
        ArgumentNullException.ThrowIfNull(ui);
        return seconds switch
        {
            <= 0 => ui.Ui("dueNow"),
            < 60 => ui.Ui("dueSeconds", ("count", seconds.ToString(CultureInfo.InvariantCulture))),
            < 3600 => ui.Ui("dueMinutes", ("count", ((seconds + 59) / 60).ToString(CultureInfo.InvariantCulture))),
            _ => ui.Ui("dueHours", ("count", ((seconds + 3599) / 3600).ToString(CultureInfo.InvariantCulture))),
        };
    }

    /// <summary>Builds the rows for the words' catalogue, without the time of the next review.</summary>
    /// <param name="words">The learner's words.</param>
    /// <param name="unseen">The label of a noun the learner has not met (<c>¿?</c>, <c>game/words.json</c>).</param>
    public static IReadOnlyList<JournalRow> Build(WordProgress words, string unseen) => Build(words, null, unseen, null);

    /// <summary>
    /// Builds the rows for the words' catalogue. With a stored time (Unix seconds), a word that has a credited use and is not known shows when
    /// its next credited use is possible.
    /// </summary>
    /// <param name="words">The learner's words.</param>
    /// <param name="now">The stored time, in Unix seconds.</param>
    /// <param name="unseen">The label of a noun the learner has not met (<c>¿?</c>, <c>game/words.json</c>).</param>
    /// <param name="ui">The support language (the wait's text).</param>
    public static IReadOnlyList<JournalRow> Build(WordProgress words, long now, string unseen, LocaleText ui) =>
        Build(words, (long?)now, unseen, ui ?? throw new ArgumentNullException(nameof(ui)));

    private static IReadOnlyList<JournalRow> Build(WordProgress words, long? now, string unseen, LocaleText? ui)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(unseen);
        var rows = new List<JournalRow>();
        foreach (var noun in words.Catalogue.Nouns)
        {
            var record = words.RecordOf(noun.Lemma);
            var wait = now is { } at ? words.SecondsUntilNextCredit(noun.Lemma, at, CreditRules.Default) : null;
            rows.Add(new JournalRow(
                noun.Lemma,
                record.Seen ? noun.WithDefinite : unseen,
                record.Seen,
                record,
                record.Pips,
                record.Saturation,
                wait is { } seconds && ui is not null ? DueText(ui, seconds) : null,
                wait == 0));
        }

        return rows
            .OrderBy(static r => StageRank(r.Record.Stage))
            .ThenBy(static r => r.Lemma, StringComparer.Ordinal)
            .ToList();
    }

    private static int StageRank(WordStage stage) => stage switch
    {
        WordStage.Known => 0,
        WordStage.Learning => 1,
        WordStage.Encountered => 2,
        _ => 3,
    };
}
