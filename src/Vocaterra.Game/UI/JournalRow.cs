using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.UI;

/// <summary>One line of the journal: a catalogue noun, what the learner has done with it, and how far it is known.</summary>
/// <param name="Lemma">The noun (<c>árbol</c>), or the lemma of an unseen noun.</param>
/// <param name="Label">The noun with its article (<c>el árbol</c>) once it has been seen, or <c>¿?</c> before.</param>
/// <param name="Seen">Whether the learner has heard, read or said it.</param>
/// <param name="Record">The counts.</param>
/// <param name="Pips">How many of the three uses are in (0 to <see cref="WordProgress.UsesToKnow"/>), for the three progress pips.</param>
/// <param name="Saturation">The colour of the noun's objects, 0 to 1.</param>
/// <param name="DueText">When the next credited use is possible (<c>otra vez en 8 min</c>, <c>lista para repasar</c>), or <see langword="null"/> for a word with no credited use or a known one.</param>
/// <param name="IsDue">Whether the word is due now (a correct use earns colour).</param>
public sealed record JournalRow(string Lemma, string Label, bool Seen, WordRecord Record, int Pips, double Saturation, string? DueText = null, bool IsDue = false)
{
    /// <summary>Whether the noun is known (three correct uses in three turns).</summary>
    public bool Known => Record.Known;
}
