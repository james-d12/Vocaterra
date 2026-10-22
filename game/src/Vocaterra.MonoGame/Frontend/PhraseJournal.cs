using System.Globalization;
using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>One line of the journal's phrases page (S8): a phrase of the phrase sheet, how much help the learner has with it and what they have done with it.</summary>
/// <param name="Id">The phrase sheet's id.</param>
/// <param name="Spanish">The phrase as taught.</param>
/// <param name="English">Its meaning.</param>
/// <param name="Hears">Whether it is a phrase an NPC says (the learner understands it) rather than one the learner says.</param>
/// <param name="Level">The support level now (<see cref="SupportLevel.None"/> for a phrase with no scaffold).</param>
/// <param name="HasScaffold">Whether the phrase has a card and audio to fade.</param>
/// <param name="Record">The counts.</param>
/// <param name="LevelText">The support level in words, for example <c>ayuda: tarjeta y audio</c>, or <c>se oye</c> for a phrase an NPC says.</param>
/// <param name="CountsText">The counts in words, for example <c>con ayuda 1   sin ayuda 0   opciones 0   no sé 0   otra situación 0</c>.</param>
/// <param name="DueText">When the next credited use is possible (<c>otra vez en 8 min</c>, <c>lista para repasar</c>), or <see langword="null"/> when the phrase has nothing left to fade or no credited use.</param>
/// <param name="IsDue">Whether the guide would ask it now.</param>
public sealed record PhraseJournalRow(
    string Id,
    string Spanish,
    string English,
    bool Hears,
    SupportLevel Level,
    bool HasScaffold,
    PhraseRecord Record,
    string LevelText,
    string CountsText,
    string? DueText,
    bool IsDue);

/// <summary>
/// The rows of the journal's phrases page, built from the learner's phrase progress in the phrase sheet's order. Plain values; the only time is the
/// stored time the caller gives.
/// </summary>
public static class PhraseJournalRows
{
    /// <summary>The text of a phrase an NPC says in place of a support level.</summary>
    public const string HearsText = "se oye";

    /// <summary>The label of <see cref="SupportLevel.EnglishAndCard"/> (S16).</summary>
    public const string EnglishAndCardText = "ayuda: inglés, tarjeta y audio";

    /// <summary>The label of <see cref="SupportLevel.CardAndAudio"/>.</summary>
    public const string CardAndAudioText = "ayuda: tarjeta y audio";

    /// <summary>The label of <see cref="SupportLevel.AudioOnly"/>.</summary>
    public const string AudioOnlyText = "ayuda: solo audio";

    /// <summary>The label of <see cref="SupportLevel.None"/>.</summary>
    public const string NoneText = "ayuda: ninguna";

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

        return rows;
    }

    /// <summary>The counts of a phrase in words: <c>con ayuda</c> (supported), <c>sin ayuda</c> (unaided), <c>opciones</c> (choice-only), <c>no sé</c> and <c>otra situación</c> (transfer); for a phrase an NPC says <c>oída</c> and <c>respondida</c>.</summary>
    /// <param name="record">The phrase's counts.</param>
    /// <param name="hears">Whether it is a phrase an NPC says.</param>
    public static string CountsOf(PhraseRecord record, bool hears)
    {
        ArgumentNullException.ThrowIfNull(record);
        return hears
            ? string.Create(CultureInfo.InvariantCulture, $"oída {record.Heard}   respondida {record.Answered}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"con ayuda {PhraseProgress.Uses(record.Supported)}   sin ayuda {PhraseProgress.Uses(record.Unaided)}   opciones {record.ChoiceOnly}   no sé {record.DontKnow}   otra situación {PhraseProgress.Uses(record.Transfer)}");
    }
}
