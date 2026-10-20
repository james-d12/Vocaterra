using Slg.Core.Audio;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Phonology;

namespace Slg.Pronunciation;

/// <summary>Which pronunciation scorer to run.</summary>
public enum ScoringMode
{
    /// <summary>The v1 scorer: greedy CTC decode and phone edit distance (<see cref="GreedyPhoneScorer"/>).</summary>
    Greedy,

    /// <summary>The v2 scorer: CTC forced alignment and goodness of pronunciation (<see cref="GopScorer"/>).</summary>
    Gop,
}

/// <summary>Facade over the pronunciation scorers: one entry point that picks the scorer by <see cref="ScoringMode"/>.</summary>
public sealed class PronunciationScorer
{
    private readonly GreedyPhoneScorer greedy;
    private readonly GopScorer gop;

    /// <summary>Creates a scorer; thresholds and calibration default to their shipped values.</summary>
    public PronunciationScorer(PronunciationThresholds? thresholds = null, GopCalibration? calibration = null)
    {
        var resolved = thresholds ?? PronunciationThresholds.Default;
        greedy = new GreedyPhoneScorer(resolved);
        gop = new GopScorer(resolved, calibration);
    }

    /// <summary>
    /// Scores posteriors against the accepted variants. <paramref name="predictedErrors"/> is normally
    /// <see cref="PredictedErrorsFor"/> of the accepted variants; <paramref name="modelHash"/> is the recognizer's model hash.
    /// </summary>
    public PronunciationResult Score(
        PhonePosteriors posteriors,
        IReadOnlyList<G2pResult> acceptedVariants,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        Difficulty difficulty,
        ScoringMode mode,
        string modelHash)
    {
        return mode switch
        {
            ScoringMode.Greedy => greedy.Score(posteriors, acceptedVariants, predictedErrors, difficulty, modelHash),
            ScoringMode.Gop => gop.Score(posteriors, acceptedVariants, predictedErrors, difficulty, modelHash),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown scoring mode."),
        };
    }

    /// <summary>Runs the recognizer on the clip and scores its posteriors with the recognizer's model hash.</summary>
    public PronunciationResult Score(
        PcmClip clip,
        IPhonemeRecognizer recognizer,
        IReadOnlyList<G2pResult> acceptedVariants,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        Difficulty difficulty,
        ScoringMode mode)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(recognizer);
        return Score(recognizer.Recognize(clip), acceptedVariants, predictedErrors, difficulty, mode, recognizer.ModelHash);
    }

    /// <summary>
    /// The predicted errors of every accepted variant, concatenated in variant order without duplicates. A predicted variant applies
    /// to a scored reference only when it is exactly one edit away from it, so the union covers every dialect the learner may have used.
    /// </summary>
    public static IReadOnlyList<PredictedErrorVariant> PredictedErrorsFor(IReadOnlyList<G2pResult> acceptedVariants)
    {
        ArgumentNullException.ThrowIfNull(acceptedVariants);
        var all = new List<PredictedErrorVariant>();
        foreach (var variant in acceptedVariants)
        {
            foreach (var predicted in PredictedErrorVariants.Generate(variant))
            {
                if (!all.Contains(predicted))
                {
                    all.Add(predicted);
                }
            }
        }

        return all.AsReadOnly();
    }
}
