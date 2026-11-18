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
        var claimed = new bool[evidence.Insertions.Count];

        foreach (var variant in predicted.OrderBy(static v => v.GraphemeSpan.Start))
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

            var tag = TryTag(variant, alignment, evidence, difficulty, thresholds, claimed);
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
        PronunciationThresholds thresholds,
        bool[] claimed)
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
                        var insertion = FindEquivalentInsertion(evidence, extra, before, claimed);
                        if (insertion < 0 || thresholds.Evaluate(extra, difficulty, 0.0) != MarginDecision.Reject)
                        {
                            return null;
                        }

                        claimed[insertion] = true;

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
    /// The index of a heard insertion of the same phone at the same site that no earlier variant has claimed, or -1. Inserting a phone
    /// next to copies of itself yields the same sequence at several positions, so every position within a run of that phone counts as
    /// the same site. One heard phone is one error: two variants can predict the very same phone sequence (a silent h at the end of a word and
    /// another at the start of the next gives the same inserted [h] at the same place), and each heard insertion is claimed once, by the
    /// first variant in text order (<see cref="PredictedErrorVariants.Generate"/> sorts by span), so one extra [h] is one tag.
    /// </summary>
    private static int FindEquivalentInsertion(VariantEvidence evidence, string phone, int predictedBefore, bool[] claimed)
    {
        var reference = evidence.Variant.Phones;
        for (var n = 0; n < evidence.Insertions.Count; n++)
        {
            var inserted = evidence.Insertions[n];
            if (claimed[n])
            {
                continue;
            }

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
                return n;
            }
        }

        return -1;
    }
}
