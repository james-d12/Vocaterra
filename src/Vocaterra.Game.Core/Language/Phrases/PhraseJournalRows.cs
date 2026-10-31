using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Language.Phrases;

/// <summary>
/// The rows of the journal's phrases page, built from the learner's phrase progress in the phrase sheet's order. Plain values; the only time is the
/// stored time the caller gives.
/// </summary>
public static class PhraseJournalRows
{
    /// <summary>The words for a support level (<c>ui.phraseHelp*</c>, C04; the label of a phrase an NPC says is <c>ui.phraseHeard</c>).</summary>
    /// <param name="ui">The support language.</param>
    /// <param name="level">The level.</param>
    public static string LevelLabel(LocaleText ui, SupportLevel level) => (ui ?? throw new ArgumentNullException(nameof(ui))).Ui(level switch
    {
        SupportLevel.EnglishAndCard => "phraseHelpEnglishAndCard",
        SupportLevel.CardAndAudio => "phraseHelpCardAndAudio",
        SupportLevel.AudioOnly => "phraseHelpAudioOnly",
        _ => "phraseHelpNone",
    });

    /// <summary>Builds the rows.</summary>
    /// <param name="progress">The phrase progress.</param>
    /// <param name="now">The stored time in Unix seconds.</param>
    /// <param name="rules">The credit rules (the gaps).</param>
    /// <param name="ui">The support language (the rows' words).</param>
    public static IReadOnlyList<PhraseJournalRow> Build(PhraseProgress progress, long now, CreditRules rules, LocaleText ui)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ui);
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
                hears ? ui.Ui("phraseHeard") : LevelLabel(ui, record.Level),
                CountsOf(ui, record, hears),
                wait is { } seconds ? JournalRows.DueText(ui, seconds) : null,
                wait == 0));
        }

        // The phrases the learner has met come first, each group in the sheet's order (S17): with twenty-one phrases the page cannot show them all, and the lesson's
        // recap ends here, so what was just learnt is what shows. A new game has met none, and the page is the sheet's.
        return [.. rows.Where(static r => r.Record.Seen), .. rows.Where(static r => !r.Record.Seen)];
    }

    /// <summary>The counts of a phrase in words: <c>con ayuda</c> (supported), <c>sin ayuda</c> (unaided), <c>opciones</c> (choice-only), <c>no sé</c> and <c>otra situación</c> (transfer); for a phrase an NPC says <c>oída</c> and <c>respondida</c>.</summary>
    /// <param name="ui">The support language.</param>
    /// <param name="record">The phrase's counts.</param>
    /// <param name="hears">Whether it is a phrase an NPC says.</param>
    public static string CountsOf(LocaleText ui, PhraseRecord record, bool hears)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(record);
        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        return hears
            ? ui.Ui("phraseHeardCounts", ("heard", N(record.Heard)), ("answered", N(record.Answered)))
            : ui.Ui(
                "phraseCounts",
                ("supported", PhraseProgress.Uses(record.Supported)),
                ("unaided", PhraseProgress.Uses(record.Unaided)),
                ("choices", N(record.ChoiceOnly)),
                ("dontKnow", N(record.DontKnow)),
                ("transfer", PhraseProgress.Uses(record.Transfer)));
    }
}
