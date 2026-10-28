using System.Globalization;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Language.Phrases;

/// <summary>
/// The rows of the journal's phrases page, built from the learner's phrase progress in the phrase sheet's order. Plain values; the only time is the
/// stored time the caller gives.
/// </summary>
public static class PhraseJournalRows
{
    /// <summary>The text of a phrase an NPC says in place of a support level.</summary>
    public const string HearsText = "heard";

    /// <summary>The label of <see cref="SupportLevel.EnglishAndCard"/> (S16; English in S17b).</summary>
    public const string EnglishAndCardText = "help: English, card and audio";

    /// <summary>The label of <see cref="SupportLevel.CardAndAudio"/>.</summary>
    public const string CardAndAudioText = "help: card and audio";

    /// <summary>The label of <see cref="SupportLevel.AudioOnly"/>.</summary>
    public const string AudioOnlyText = "help: audio only";

    /// <summary>The label of <see cref="SupportLevel.None"/>.</summary>
    public const string NoneText = "help: none";

    /// <summary>The words for a support level.</summary>
    /// <param name="level">The level.</param>
    public static string LevelLabel(SupportLevel level) => level switch
    {
        SupportLevel.EnglishAndCard => EnglishAndCardText,
        SupportLevel.CardAndAudio => CardAndAudioText,
        SupportLevel.AudioOnly => AudioOnlyText,
        _ => NoneText,
    };

    /// <summary>Builds the rows.</summary>
    /// <param name="progress">The phrase progress.</param>
    /// <param name="now">The stored time in Unix seconds.</param>
    /// <param name="rules">The credit rules (the gaps).</param>
    public static IReadOnlyList<PhraseJournalRow> Build(PhraseProgress progress, long now, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(rules);
        var rows = new List<PhraseJournalRow>();
        foreach (var sheetRow in progress.Sheet.Rows)
        {
            var record = progress.RecordOf(sheetRow.Id);
            var hears = !sheetRow.IsLearnerPhrase;
            var scaffold = progress.HasScaffold(sheetRow.Id);
            var wait = scaffold && record.Level != SupportLevel.None ? progress.SecondsUntilNextCredit(sheetRow.Id, now, rules) : null;
            rows.Add(new PhraseJournalRow(
                sheetRow.Id,
                sheetRow.Spanish,
                sheetRow.English,
                hears,
                record.Level,
                scaffold,
                record,
                hears ? HearsText : LevelLabel(record.Level),
                CountsOf(record, hears),
                wait is { } seconds ? JournalRows.DueText(seconds) : null,
                wait == 0));
        }

        // The phrases the learner has met come first, each group in the sheet's order (S17): with twenty-one phrases the page cannot show them all, and the lesson's
        // recap ends here, so what was just learnt is what shows. A new game has met none, and the page is the sheet's.
        return [.. rows.Where(static r => r.Record.Seen), .. rows.Where(static r => !r.Record.Seen)];
    }

    /// <summary>The counts of a phrase in words: <c>con ayuda</c> (supported), <c>sin ayuda</c> (unaided), <c>opciones</c> (choice-only), <c>no sé</c> and <c>otra situación</c> (transfer); for a phrase an NPC says <c>oída</c> and <c>respondida</c>.</summary>
    /// <param name="record">The phrase's counts.</param>
    /// <param name="hears">Whether it is a phrase an NPC says.</param>
    public static string CountsOf(PhraseRecord record, bool hears)
    {
        ArgumentNullException.ThrowIfNull(record);
        return hears
            ? string.Create(CultureInfo.InvariantCulture, $"heard {record.Heard}   answered {record.Answered}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"with help {PhraseProgress.Uses(record.Supported)}   without help {PhraseProgress.Uses(record.Unaided)}   choices {record.ChoiceOnly}   don't know {record.DontKnow}   other situation {PhraseProgress.Uses(record.Transfer)}");
    }
}
