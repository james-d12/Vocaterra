namespace Vocaterra.Game.Core.Language.Phrases;

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
