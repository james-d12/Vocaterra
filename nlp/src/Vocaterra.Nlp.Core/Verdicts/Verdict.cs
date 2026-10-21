using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>The complete, replayable outcome of evaluating one player utterance.</summary>
public sealed record Verdict(
    IntentId Intent,
    SlotMap Slots,
    bool Communicated,
    double Correctness,
    IReadOnlyList<ErrorTag> Errors,
    IReadOnlyList<PhoneScore> Pronunciation,
    double Confidence,
    ResolvedBy ResolvedBy,
    VersionStamp Versions)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(Verdict? other)
    {
        return other is not null
            && Intent.Equals(other.Intent)
            && EqualityComparer<SlotMap>.Default.Equals(Slots, other.Slots)
            && Communicated == other.Communicated
            && Correctness.Equals(other.Correctness)
            && ValueEquality.ListEquals(Errors, other.Errors)
            && ValueEquality.ListEquals(Pronunciation, other.Pronunciation)
            && Confidence.Equals(other.Confidence)
            && ResolvedBy == other.ResolvedBy
            && EqualityComparer<VersionStamp>.Default.Equals(Versions, other.Versions);
    }

    /// <summary>Hash consistent with <see cref="Equals(Verdict)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Intent);
        hash.Add(Slots);
        hash.Add(Communicated);
        hash.Add(Correctness);
        ValueEquality.AddList(ref hash, Errors);
        ValueEquality.AddList(ref hash, Pronunciation);
        hash.Add(Confidence);
        hash.Add(ResolvedBy);
        hash.Add(Versions);
        return hash.ToHashCode();
    }
}
