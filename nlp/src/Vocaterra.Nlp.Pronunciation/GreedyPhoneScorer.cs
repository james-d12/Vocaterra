using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// The v1 pronunciation scorer: greedy CTC decode, phone edit-distance alignment against every accepted variant, and a discrete
/// per-phone score. A matched phone scores 1, a substituted or deleted phone 0, and each extra phone next to a phone takes 0.5 off
/// that phone (the following phone, or the last phone when the extra phone is at the end), never below 0. The variant with the
/// highest mean score wins (the earliest on a tie).
/// </summary>
public sealed class GreedyPhoneScorer
{
    private const double InsertionPenalty = 0.5;

    private readonly PronunciationThresholds thresholds;

    /// <summary>Creates a scorer; thresholds default to <see cref="PronunciationThresholds.Default"/>.</summary>
    public GreedyPhoneScorer(PronunciationThresholds? thresholds = null)
    {
        this.thresholds = thresholds ?? PronunciationThresholds.Default;
    }

    /// <summary>
    /// Scores the posteriors against the accepted variants. <c>Confidence</c> is the mean over frames of the largest posterior probability.
    /// <paramref name="predictedErrors"/> should be the union of <see cref="PredictedErrorVariants.Generate"/> over the accepted variants.
    /// </summary>
    public PronunciationResult Score(
        PhonePosteriors posteriors,
        IReadOnlyList<G2pResult> acceptedVariants,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        Difficulty difficulty,
        string modelHash)
    {
        ScoreMath.ValidateInputs(posteriors, acceptedVariants, predictedErrors, modelHash);
        var hypothesis = CtcDecoder.Greedy(posteriors);

        VariantEvidence? best = null;
        var bestMean = double.NegativeInfinity;
        foreach (var variant in acceptedVariants)
        {
            var evidence = BuildEvidence(variant, hypothesis);
            var mean = ScoreMath.Quantize(evidence.MeanScore);
            if (mean > bestMean)
            {
                best = evidence;
                bestMean = mean;
            }
        }

        return ResultAssembler.Assemble(best!, predictedErrors, difficulty, thresholds, ScoreMath.FrameConfidence(posteriors), modelHash);
    }

    /// <summary>Aligns a hypothesis to one variant and derives the discrete per-phone observations.</summary>
    internal static VariantEvidence BuildEvidence(G2pResult variant, IReadOnlyList<string> hypothesis)
    {
        var reference = variant.Phones;
        var alignment = PhoneAligner.Align(hypothesis, reference);
        var scores = new double[reference.Count];
        var actual = new string[reference.Count];
        Array.Fill(actual, string.Empty);
        var insertions = CollectInsertions(alignment, hypothesis);

        foreach (var step in alignment.Steps)
        {
            switch (step.Op)
            {
                case AlignmentOp.Match:
                    scores[step.RefIndex] = 1.0;
                    actual[step.RefIndex] = hypothesis[step.HypIndex];
                    break;
                case AlignmentOp.Substitute:
                    actual[step.RefIndex] = hypothesis[step.HypIndex];
                    break;
                case AlignmentOp.Delete:
                case AlignmentOp.Insert:
                default:
                    break;
            }
        }

        if (reference.Count > 0)
        {
            foreach (var inserted in insertions)
            {
                var neighbour = Math.Min(inserted.BeforeRefIndex, reference.Count - 1);
                scores[neighbour] = Math.Max(0.0, scores[neighbour] - InsertionPenalty);
            }
        }

        var observations = new PhoneObservation[reference.Count];
        for (var i = 0; i < observations.Length; i++)
        {
            observations[i] = new PhoneObservation(reference[i], actual[i], scores[i]);
        }

        return new VariantEvidence(variant, Array.AsReadOnly(observations), insertions, ScoreMath.Mean(scores));
    }

    /// <summary>Lists the extra hypothesis phones of an alignment, each tagged with the reference index it precedes.</summary>
    internal static IReadOnlyList<InsertedPhone> CollectInsertions(PhoneAlignment alignment, IReadOnlyList<string> hypothesis)
    {
        var insertions = new List<InsertedPhone>();
        var consumed = 0;
        foreach (var step in alignment.Steps)
        {
            if (step.Op == AlignmentOp.Insert)
            {
                insertions.Add(new InsertedPhone(consumed, hypothesis[step.HypIndex]));
            }
            else
            {
                consumed++;
            }
        }

        return insertions.AsReadOnly();
    }
}
