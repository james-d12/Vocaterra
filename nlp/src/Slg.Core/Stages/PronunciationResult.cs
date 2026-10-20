using Slg.Core.Determinism;
using Slg.Core.Verdicts;

namespace Slg.Core.Stages;

/// <summary>Output of pronunciation scoring: per-phone scores, pronunciation errors, an overall word score and confidence.</summary>
public sealed record PronunciationResult(
    IReadOnlyList<PhoneScore> Phones,
    IReadOnlyList<ErrorTag> Errors,
    double WordScore,
    double Confidence,
    string ModelHash)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(PronunciationResult? other)
    {
        return other is not null
            && ValueEquality.ListEquals(Phones, other.Phones)
            && ValueEquality.ListEquals(Errors, other.Errors)
            && WordScore.Equals(other.WordScore)
            && Confidence.Equals(other.Confidence)
            && string.Equals(ModelHash, other.ModelHash, StringComparison.Ordinal);
    }

    /// <summary>Hash consistent with <see cref="Equals(PronunciationResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ValueEquality.AddList(ref hash, Phones);
        ValueEquality.AddList(ref hash, Errors);
        hash.Add(WordScore);
        hash.Add(Confidence);
        hash.Add(ModelHash);
        return hash.ToHashCode();
    }
}
