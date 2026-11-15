using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// Turns low-scoring phones into learner error tags by checking them against the predicted-error variants.
/// A variant applies to a reference when it is exactly one edit away from it (one substituted phone, or one inserted phone).
/// It yields a tag only when the phone actually heard equals the variant's phone at that site and the site scores
/// <see cref="MarginDecision.Reject"/>: <see cref="MarginDecision.Uncertain"/> sites are never tagged. An inserted phone has no reference
/// counterpart, so its site is scored 0 (the spurious phone itself failed), which a sane threshold rejects.
/// <c>pron.stress</c> is never emitted in v1: phone posteriors carry no stress signal, so such variants are skipped.
/// </summary>
internal static class PredictedErrorMatcher
{
    /// <summary>Returns the tags for one scored reference, ordered by span start, span length, taxonomy order, expected and actual text.</summary>
    public static IReadOnlyList<ErrorTag> Match(
        VariantEvidence evidence,
        IReadOnlyList<PredictedErrorVariant> predicted,
        Difficulty difficulty,
        PronunciationThresholds thresholds,
        ErrorTagSet errorTags)
    {
        var reference = evidence.Variant.Phones;
        var tags = new List<ErrorTag>();

        foreach (var variant in predicted)
        {
            if (string.Equals(variant.ErrorTag, ErrorTags.PronStress, StringComparison.Ordinal))
            {
                continue;
            }

            var alignment = PhoneAligner.Align(variant.Phones, reference);
            if (alignment.Distance != 1)
            {
                continue;
            }

            var tag = TryTag(variant, alignment, evidence, difficulty, thresholds);
            if (tag is not null)
            {
                tags.Add(tag);
            }
        }

        return tags
            .Distinct()
            .OrderBy(static tag => tag.Span.Start)
            .ThenBy(static tag => tag.Span.Length)
            .ThenBy(tag => errorTags.Rank(tag.Tag))
            .ThenBy(static tag => tag.Expected, StringComparer.Ordinal)
            .ThenBy(static tag => tag.Actual, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static ErrorTag? TryTag(
        PredictedErrorVariant variant,
        PhoneAlignment alignment,
        VariantEvidence evidence,
        Difficulty difficulty,
        PronunciationThresholds thresholds)
    {
        foreach (var step in alignment.Steps)
        {
            switch (step.Op)
            {
                case AlignmentOp.Substitute:
                    {
                        var observation = evidence.Phones[step.RefIndex];
                        var wrong = variant.Phones[step.HypIndex];
                        if (!string.Equals(observation.Actual, wrong, StringComparison.Ordinal)
                            || thresholds.Evaluate(observation.Expected, difficulty, ScoreMath.Quantize(observation.Score)) != MarginDecision.Reject)
                        {
                            return null;
                        }

                        return new ErrorTag(variant.ErrorTag, variant.GraphemeSpan, observation.Expected, observation.Actual);
                    }

                case AlignmentOp.Insert:
                    {
                        var extra = variant.Phones[step.HypIndex];
                        var before = CountReferencePhonesBefore(alignment, step);
                        if (!HasEquivalentInsertion(evidence, extra, before)
                            || thresholds.Evaluate(extra, difficulty, 0.0) != MarginDecision.Reject)
                        {
                            return null;
                        }

                        return new ErrorTag(variant.ErrorTag, variant.GraphemeSpan, string.Empty, extra);
                    }

                case AlignmentOp.Match:
                case AlignmentOp.Delete:
                default:
                    break;
            }
        }

        return null;
    }

    private static int CountReferencePhonesBefore(PhoneAlignment alignment, AlignmentStep target)
    {
        var count = 0;
        foreach (var step in alignment.Steps)
        {
            if (step.Equals(target))
            {
                break;
            }

            if (step.RefIndex >= 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// True when the learner inserted the same phone at the same site. Inserting a phone next to copies of itself yields the same
    /// sequence at several positions, so every position within a run of that phone counts as the same site.
    /// </summary>
    private static bool HasEquivalentInsertion(VariantEvidence evidence, string phone, int predictedBefore)
    {
        var reference = evidence.Variant.Phones;
        foreach (var inserted in evidence.Insertions)
        {
            if (!string.Equals(inserted.Phone, phone, StringComparison.Ordinal))
            {
                continue;
            }

            var low = Math.Min(inserted.BeforeRefIndex, predictedBefore);
            var high = Math.Max(inserted.BeforeRefIndex, predictedBefore);
            var equivalent = true;
            for (var k = low; k < high; k++)
            {
                if (!string.Equals(reference[k], phone, StringComparison.Ordinal))
                {
                    equivalent = false;
                    break;
                }
            }

            if (equivalent)
            {
                return true;
            }
        }

        return false;
    }
}
