namespace Vocaterra.Nlp.Nlp.Matching;

/// <summary>One acceptable answer: a canonical phrase, alias phrases and the dialects the phrases come from.</summary>
/// <param name="AnswerId">Stable identifier; ties between answers are broken by ordinal comparison of this value.</param>
/// <param name="Canonical">The preferred phrase.</param>
/// <param name="Aliases">Other accepted phrases, for example regional words for the same thing.</param>
/// <param name="Dialects">Dialect tags of the phrases (informational: matching accepts every dialect variant).</param>
public sealed record Answer(
    string AnswerId,
    string Canonical,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Dialects)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(Answer? other)
    {
        return other is not null
            && string.Equals(AnswerId, other.AnswerId, StringComparison.Ordinal)
            && string.Equals(Canonical, other.Canonical, StringComparison.Ordinal)
            && ListEquality.Equal(Aliases, other.Aliases)
            && ListEquality.Equal(Dialects, other.Dialects);
    }

    /// <summary>Hash consistent with <see cref="Equals(Answer)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(AnswerId);
        hash.Add(Canonical);
        ListEquality.Add(ref hash, Aliases);
        ListEquality.Add(ref hash, Dialects);
        return hash.ToHashCode();
    }
}
