using Vocaterra.Nlp.Language.Matching;

namespace Vocaterra.Nlp.Language.Intent;

/// <summary>
/// A lemma keyword rule: it fires when every lemma occurs in the utterance and every required slot can be filled.
/// <c>SlotFromNoun</c> takes the lemma of the first noun, <c>SlotFromNumber</c> the lemma (a digit string) of the first numeral.
/// </summary>
/// <param name="Lemmas">Lemmas that must all be present (compared accent-insensitively against lemmas and surface forms).</param>
/// <param name="RequiresSlots">Slot names that must be filled for the rule to fire; ordinal-sorted.</param>
/// <param name="SlotFromNoun">Name of the slot filled from the first noun, or null.</param>
/// <param name="SlotFromNumber">Name of the slot filled from the first numeral, or null.</param>
public sealed record KeywordRule(
    IReadOnlyList<string> Lemmas,
    IReadOnlyList<string> RequiresSlots,
    string? SlotFromNoun,
    string? SlotFromNumber)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(KeywordRule? other)
    {
        return other is not null
            && ListEquality.Equal(Lemmas, other.Lemmas)
            && ListEquality.Equal(RequiresSlots, other.RequiresSlots)
            && string.Equals(SlotFromNoun, other.SlotFromNoun, StringComparison.Ordinal)
            && string.Equals(SlotFromNumber, other.SlotFromNumber, StringComparison.Ordinal);
    }

    /// <summary>Hash consistent with <see cref="Equals(KeywordRule)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Lemmas);
        ListEquality.Add(ref hash, RequiresSlots);
        hash.Add(SlotFromNoun);
        hash.Add(SlotFromNumber);
        return hash.ToHashCode();
    }
}
