using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Verdicts;
using Slg.Phonology;

namespace Slg.Pronunciation;

/// <summary>Builds the <see cref="PronunciationResult"/> from the evidence of the chosen variant; shared by the greedy and GOP scorers.</summary>
internal static class ResultAssembler
{
    public static PronunciationResult Assemble(
        VariantEvidence evidence,
        IReadOnlyList<PredictedErrorVariant> predictedErrors,
        Difficulty difficulty,
        PronunciationThresholds thresholds,
        double confidence,
        string modelHash)
    {
        var phones = new PhoneScore[evidence.Phones.Count];
        var scores = new double[phones.Length];
        for (var i = 0; i < phones.Length; i++)
        {
            var observation = evidence.Phones[i];
            var score = ScoreMath.Quantize(Math.Clamp(observation.Score, 0.0, 1.0));
            scores[i] = score;
            phones[i] = new PhoneScore(ScoreMath.SpanOfPhone(evidence.Variant, i), observation.Expected, observation.Actual, score);
        }

        var errors = PredictedErrorMatcher.Match(evidence, predictedErrors, difficulty, thresholds);
        return new PronunciationResult(
            Array.AsReadOnly(phones),
            errors,
            ScoreMath.Quantize(ScoreMath.Mean(scores)),
            ScoreMath.Quantize(Math.Clamp(confidence, 0.0, 1.0)),
            modelHash);
    }
}
