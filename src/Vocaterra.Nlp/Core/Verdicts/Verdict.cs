using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>The complete, replayable outcome of evaluating one player utterance.</summary>
/// <param name="Intent">The intent among the prompt's allowed intents, or <see cref="IntentId.Unknown"/>.</param>
/// <param name="Slots">The slots of the analysis overlaid by those of the intent.</param>
/// <param name="Communicated">Whether the learner was understood at this prompt.</param>
/// <param name="Correctness">How well they spoke, from 0 to 1.</param>
/// <param name="Errors">The errors, sorted by span start, tag rank and expected text.</param>
/// <param name="Pronunciation">The phone scores, empty without pronunciation scoring.</param>
/// <param name="Confidence">The minimum of the stage confidences.</param>
/// <param name="ResolvedBy">What resolved the line: an answer match, an intent rung, or nothing.</param>
/// <param name="Versions">The content and model hashes.</param>
/// <param name="OffPromptIntent">
/// M72: the intent of a line this prompt did not take (no allowed intent, no answer matched) that the catalog understands, by a slot grammar or a keyword rule
/// (<c>me gusta el río</c> at a prompt without <c>state_like</c>). It is never an intent the prompt allows, it is set only when <see cref="Intent"/> is unknown
/// and the line is not communicated, and it changes neither: a host may answer the line, the core does not. Null otherwise, and left out of the canonical JSON
/// while null, so a verdict without it serializes as before M72.
/// </param>
public sealed record Verdict(
    IntentId Intent,
    SlotMap Slots,
    bool Communicated,
    double Correctness,
    IReadOnlyList<ErrorTag> Errors,
    IReadOnlyList<PhoneScore> Pronunciation,
    double Confidence,
    ResolvedBy ResolvedBy,
    VersionStamp Versions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IntentId? OffPromptIntent = null)
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
            && EqualityComparer<VersionStamp>.Default.Equals(Versions, other.Versions)
            && Nullable.Equals(OffPromptIntent, other.OffPromptIntent);
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
        hash.Add(OffPromptIntent);
        return hash.ToHashCode();
    }
}
