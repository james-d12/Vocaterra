using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Phonology;

namespace Slg.Pronunciation;

/// <summary>
/// The v2 pronunciation scorer. Every accepted variant is force-aligned to the posteriors with CTC Viterbi and the variant with the
/// highest path log-likelihood is kept (the earliest on a tie). For each phone of that variant the goodness of pronunciation (GOP) is
/// the mean, over the frames of its segment, of <c>logP(target) - max over all symbols of logP</c>, which is at most 0. The maximum
/// includes the blank so that a skipped phone, whose frames are blank, scores low. The phone score is <c>exp(gop / temperature)</c>
/// with the temperature of the phone's class (see <see cref="GopCalibration"/>). The phone heard (<c>Actual</c>) is the symbol with
/// the highest mean log-probability over the segment; the blank counts as a missing phone. Extra phones come from the greedy decode.
/// </summary>
/// <remarks>
/// When no variant can be aligned (too few frames, or a target phone missing from the vocabulary) the result is the v1 greedy result
/// with <c>Confidence</c> lowered to the minimum of the greedy confidence and the alignment feasibility: the share of the needed frames
/// that exist (0 when a phone is missing from the vocabulary), taken for the most feasible variant.
/// </remarks>
public sealed class GopScorer
{
    private readonly PronunciationThresholds thresholds;
    private readonly GopCalibration calibration;
    private readonly GreedyPhoneScorer fallback;

    /// <summary>Creates a scorer; thresholds and calibration default to their shipped values.</summary>
    public GopScorer(PronunciationThresholds? thresholds = null, GopCalibration? calibration = null)
    {
        this.thresholds = thresholds ?? PronunciationThresholds.Default;
        this.calibration = calibration ?? GopCalibration.Default;
        fallback = new GreedyPhoneScorer(this.thresholds);
    }

    /// <summary>Scores the posteriors against the accepted variants with forced alignment and GOP.</summary>
    public PronunciationResult Score(
        PhonePosteriors posteriors,
        IReadOnlyList<G2pResult> acceptedVariants,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        Difficulty difficulty,
        string modelHash)
    {
        ScoreMath.ValidateInputs(posteriors, acceptedVariants, predictedErrors, modelHash);

        G2pResult? chosen = null;
        CtcAlignment? chosenAlignment = null;
        foreach (var variant in acceptedVariants)
        {
            var alignment = CtcForcedAligner.Align(posteriors, variant.Phones);
            if (alignment is not null && (chosenAlignment is null || alignment.LogLikelihood > chosenAlignment.LogLikelihood))
            {
                chosen = variant;
                chosenAlignment = alignment;
            }
        }

        if (chosen is null || chosenAlignment is null)
        {
            var greedy = fallback.Score(posteriors, acceptedVariants, predictedErrors, difficulty, modelHash);
            var feasibility = Feasibility(posteriors, acceptedVariants);
            return greedy with { Confidence = ScoreMath.Quantize(Math.Min(greedy.Confidence, feasibility)) };
        }

        var evidence = BuildEvidence(posteriors, chosen, chosenAlignment);
        return ResultAssembler.Assemble(evidence, predictedErrors, difficulty, thresholds, ScoreMath.FrameConfidence(posteriors), modelHash);
    }

    private VariantEvidence BuildEvidence(PhonePosteriors posteriors, G2pResult variant, CtcAlignment alignment)
    {
        var blank = CtcDecoder.BlankIndex(posteriors);
        var observations = new PhoneObservation[variant.Phones.Count];
        var scores = new double[observations.Length];
        foreach (var segment in alignment.Segments)
        {
            var target = CtcForcedAligner.IndexOf(posteriors.Vocabulary, segment.Phone);
            var gop = SegmentGop(posteriors, target, segment);
            var score = calibration.ScoreOf(segment.Phone, gop);
            var heard = HeardColumn(posteriors, segment);
            var actual = heard == blank ? string.Empty : posteriors.Vocabulary[heard];
            observations[segment.PhoneIndex] = new PhoneObservation(segment.Phone, actual, score);
            scores[segment.PhoneIndex] = score;
        }

        var hypothesis = CtcDecoder.Greedy(posteriors);
        var insertions = GreedyPhoneScorer.CollectInsertions(PhoneAligner.Align(hypothesis, variant.Phones), hypothesis);
        return new VariantEvidence(variant, Array.AsReadOnly(observations), insertions, ScoreMath.Mean(scores));
    }

    /// <summary>Mean over the segment's frames of the target log-probability minus the frame's largest log-probability (at most 0).</summary>
    internal static double SegmentGop(PhonePosteriors posteriors, int target, PhoneSegment segment)
    {
        var sum = 0.0;
        for (var t = segment.StartFrame; t < segment.EndFrame; t++)
        {
            var row = posteriors.Frame(t);
            sum += (double)row[target] - row[CtcDecoder.ArgMax(row)];
        }

        return sum / segment.FrameCount;
    }

    private static int HeardColumn(PhonePosteriors posteriors, PhoneSegment segment)
    {
        var best = 0;
        var bestSum = double.NegativeInfinity;
        for (var column = 0; column < posteriors.PhoneCount; column++)
        {
            var sum = 0.0;
            for (var t = segment.StartFrame; t < segment.EndFrame; t++)
            {
                sum += posteriors[t, column];
            }

            if (sum > bestSum)
            {
                bestSum = sum;
                best = column;
            }
        }

        return best;
    }

    private static double Feasibility(PhonePosteriors posteriors, IReadOnlyList<G2pResult> variants)
    {
        var blank = CtcDecoder.BlankIndex(posteriors);
        var best = 0.0;
        foreach (var variant in variants)
        {
            var inVocabulary = true;
            foreach (var phone in variant.Phones)
            {
                var column = CtcForcedAligner.IndexOf(posteriors.Vocabulary, phone);
                if (column < 0 || column == blank)
                {
                    inVocabulary = false;
                    break;
                }
            }

            if (!inVocabulary)
            {
                continue;
            }

            var needed = CtcForcedAligner.MinimumFrames(variant.Phones);
            var share = needed == 0 ? 1.0 : Math.Min(1.0, (double)posteriors.FrameCount / needed);
            best = Math.Max(best, share);
        }

        return best;
    }
}
