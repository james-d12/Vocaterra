using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Verdicts;

namespace Slg.Evaluation;

/// <summary>Coarse outcome label for a verdict, ordered from worst to best.</summary>
public enum OutcomeBandKind
{
    /// <summary>The utterance was not understood.</summary>
    NotUnderstood,

    /// <summary>The meaning got through but correctness was clearly below the bar.</summary>
    Communicated,

    /// <summary>The meaning got through and correctness sits inside the uncertainty band around the bar, or understanding itself is uncertain.</summary>
    PartiallyCorrect,

    /// <summary>The meaning got through confidently and correctness clears the bar.</summary>
    Correct,
}

/// <summary>An outcome label together with the margin decisions that produced it, for later effect tables.</summary>
/// <param name="Kind">The resulting band.</param>
/// <param name="Communicated">Margin decision for "was the player understood"; Reject when the verdict is not communicated.</param>
/// <param name="Correctness">Margin decision for verdict correctness against the correctness threshold.</param>
/// <param name="Pronunciation">Margin decision for the mean phone score, or null when the verdict has no phone scores; it does not affect <see cref="Kind"/>.</param>
public sealed record OutcomeBand(
    OutcomeBandKind Kind,
    MarginDecision Communicated,
    MarginDecision Correctness,
    MarginDecision? Pronunciation)
{
    /// <summary>Classifies a verdict with the settings of the given difficulty (the verdict itself does not record its difficulty).</summary>
    public static OutcomeBand Classify(Verdict verdict, EvaluationConfig config, Difficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Classify(verdict, config.For(difficulty));
    }

    /// <summary>
    /// Classifies a verdict. Not communicated gives <see cref="OutcomeBandKind.NotUnderstood"/>. Otherwise correctness
    /// Reject gives <see cref="OutcomeBandKind.Communicated"/>, Uncertain gives <see cref="OutcomeBandKind.PartiallyCorrect"/>,
    /// and Accept gives <see cref="OutcomeBandKind.Correct"/> unless understanding itself is only Uncertain, which caps the band at
    /// <see cref="OutcomeBandKind.PartiallyCorrect"/>.
    /// </summary>
    public static OutcomeBand Classify(Verdict verdict, DifficultyEvaluationConfig settings)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(settings);

        var communicated = DecideCommunicated(verdict, settings);
        var correctness = settings.CorrectnessMargin().Evaluate(Sanitize(verdict.Correctness));
        var pronunciation = DecidePronunciation(verdict, settings);

        var kind = (communicated, correctness) switch
        {
            (MarginDecision.Reject, _) => OutcomeBandKind.NotUnderstood,
            (_, MarginDecision.Reject) => OutcomeBandKind.Communicated,
            (MarginDecision.Accept, MarginDecision.Accept) => OutcomeBandKind.Correct,
            _ => OutcomeBandKind.PartiallyCorrect,
        };

        return new OutcomeBand(kind, communicated, correctness, pronunciation);
    }

    // The verdict's bool is authoritative: a communicated verdict is at best Accept and at worst Uncertain (never Reject).
    private static MarginDecision DecideCommunicated(Verdict verdict, DifficultyEvaluationConfig settings)
    {
        if (!verdict.Communicated)
        {
            return MarginDecision.Reject;
        }

        return settings.CommunicatedMargin().Evaluate(Sanitize(verdict.Confidence)) == MarginDecision.Accept
            ? MarginDecision.Accept
            : MarginDecision.Uncertain;
    }

    private static MarginDecision? DecidePronunciation(Verdict verdict, DifficultyEvaluationConfig settings)
    {
        if (verdict.Pronunciation.Count == 0)
        {
            return null;
        }

        var sum = 0.0;
        foreach (var phone in verdict.Pronunciation)
        {
            sum += Sanitize(phone.Score);
        }

        return settings.PronunciationMargin().Evaluate(sum / verdict.Pronunciation.Count);
    }

    private static double Sanitize(double value)
    {
        return double.IsNaN(value) ? 0.0 : value;
    }
}
