using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>What the scorer observed for one reference phone: the phone heard, and its score in [0, 1].</summary>
/// <param name="Expected">The reference phone.</param>
/// <param name="Actual">The phone heard in its place; empty when the phone was not produced.</param>
/// <param name="Score">The phone score before quantization.</param>
internal sealed record PhoneObservation(string Expected, string Actual, double Score);

/// <summary>A hypothesis phone with no reference counterpart, placed before a reference phone index (equal to the phone count at the end).</summary>
/// <param name="BeforeRefIndex">The reference index the phone precedes.</param>
/// <param name="Phone">The extra phone.</param>
internal sealed record InsertedPhone(int BeforeRefIndex, string Phone);

/// <summary>Everything a scorer knows about one reference variant: per-phone observations and extra phones.</summary>
/// <param name="Variant">The accepted variant that was scored against.</param>
/// <param name="Phones">One observation per phone of <paramref name="Variant"/>.</param>
/// <param name="Insertions">Extra hypothesis phones in reading order.</param>
/// <param name="MeanScore">Mean of the phone scores (0 when the variant has no phones).</param>
internal sealed record VariantEvidence(
    G2pResult Variant,
    IReadOnlyList<PhoneObservation> Phones,
    IReadOnlyList<InsertedPhone> Insertions,
    double MeanScore);

/// <summary>Numeric helpers shared by both scorers; every sum runs in index order.</summary>
internal static class ScoreMath
{
    private const int Decimals = 6;

    /// <summary>Rounds to six decimals so last-bit differences in the platform math library cannot reach a result.</summary>
    public static double Quantize(double value)
    {
        return Math.Round(value, Decimals, MidpointRounding.ToEven) + 0.0;
    }

    /// <summary>Mean of the values in index order; 0 for an empty list.</summary>
    public static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / values.Count;
    }

    /// <summary>Mean over frames of the largest posterior probability (exp of the largest log-probability, clipped to [0, 1]); 0 without frames.</summary>
    public static double FrameConfidence(PhonePosteriors posteriors)
    {
        if (posteriors.FrameCount == 0 || posteriors.PhoneCount == 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        for (var frame = 0; frame < posteriors.FrameCount; frame++)
        {
            var row = posteriors.Frame(frame);
            sum += Math.Clamp(Math.Exp(row[CtcDecoder.ArgMax(row)]), 0.0, 1.0);
        }

        return Math.Clamp(sum / posteriors.FrameCount, 0.0, 1.0);
    }

    /// <summary>The grapheme span of a phone: its own span when the result carries phone spans, otherwise the span of its syllable.</summary>
    public static TextSpan SpanOfPhone(G2pResult variant, int phoneIndex)
    {
        if (variant.PhoneSpans.Count == variant.Phones.Count)
        {
            return variant.PhoneSpans[phoneIndex];
        }

        foreach (var syllable in variant.Syllables)
        {
            if (phoneIndex >= syllable.PhoneStart && phoneIndex < syllable.PhoneStart + syllable.PhoneCount)
            {
                return syllable.GraphemeSpan;
            }
        }

        return default;
    }

    /// <summary>Checks the arguments shared by both scorers.</summary>
    public static void ValidateInputs(
        PhonePosteriors posteriors,
        IReadOnlyList<G2pResult> acceptedVariants,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        string modelHash)
    {
        ArgumentNullException.ThrowIfNull(posteriors);
        ArgumentNullException.ThrowIfNull(acceptedVariants);
        ArgumentNullException.ThrowIfNull(predictedErrors);
        ArgumentNullException.ThrowIfNull(modelHash);
        if (acceptedVariants.Count == 0)
        {
            throw new ArgumentException("At least one accepted variant is required.", nameof(acceptedVariants));
        }

        _ = CtcDecoder.BlankIndex(posteriors);
    }
}
