using System.Collections.Immutable;
using Slg.Core.Prompts;
using Slg.Core.Taxonomy;

namespace Slg.Evaluation;

/// <summary>Evaluation settings for every difficulty tier; serialize with <c>CanonicalJson</c> and load from content.</summary>
public sealed record EvaluationConfig(
    DifficultyEvaluationConfig Beginner,
    DifficultyEvaluationConfig Intermediate,
    DifficultyEvaluationConfig Advanced)
{
    /// <summary>The built-in settings: grammar penalties grow and thresholds tighten with difficulty.</summary>
    public static EvaluationConfig Default { get; } = new(
        new DifficultyEvaluationConfig(
            Weights(
                (ErrorTags.VocabWrong, 0.6),
                (ErrorTags.VocabUnknown, 0.4),
                (ErrorTags.ConjPerson, 0.25),
                (ErrorTags.ConjTense, 0.25),
                (ErrorTags.ConjIrregular, 0.2),
                (ErrorTags.GenderAgree, 0.15),
                (ErrorTags.NounNumber, 0.15),
                (ErrorTags.SerEstar, 0.2),
                (ErrorTags.WordOrder, 0.1)),
            CommunicatedConfidenceThreshold: 0.6,
            CorrectnessThreshold: 0.7,
            PronunciationThreshold: 0.5,
            Epsilon: 0.02,
            UnknownTagWeight: 0.3),
        new DifficultyEvaluationConfig(
            Weights(
                (ErrorTags.VocabWrong, 0.6),
                (ErrorTags.VocabUnknown, 0.4),
                (ErrorTags.ConjPerson, 0.3),
                (ErrorTags.ConjTense, 0.3),
                (ErrorTags.ConjIrregular, 0.25),
                (ErrorTags.GenderAgree, 0.2),
                (ErrorTags.NounNumber, 0.2),
                (ErrorTags.SerEstar, 0.25),
                (ErrorTags.WordOrder, 0.15)),
            CommunicatedConfidenceThreshold: 0.6,
            CorrectnessThreshold: 0.8,
            PronunciationThreshold: 0.6,
            Epsilon: 0.02,
            UnknownTagWeight: 0.3),
        new DifficultyEvaluationConfig(
            Weights(
                (ErrorTags.VocabWrong, 0.6),
                (ErrorTags.VocabUnknown, 0.4),
                (ErrorTags.ConjPerson, 0.35),
                (ErrorTags.ConjTense, 0.35),
                (ErrorTags.ConjIrregular, 0.3),
                (ErrorTags.GenderAgree, 0.25),
                (ErrorTags.NounNumber, 0.25),
                (ErrorTags.SerEstar, 0.3),
                (ErrorTags.WordOrder, 0.2)),
            CommunicatedConfidenceThreshold: 0.6,
            CorrectnessThreshold: 0.9,
            PronunciationThreshold: 0.7,
            Epsilon: 0.02,
            UnknownTagWeight: 0.3));

    /// <summary>Returns the settings of a difficulty tier.</summary>
    public DifficultyEvaluationConfig For(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Beginner => Beginner,
            Difficulty.Intermediate => Intermediate,
            Difficulty.Advanced => Advanced,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty."),
        };
    }

    // Pronunciation tags are listed with weight 0.0 so the data states explicitly that they never lower correctness.
    private static ImmutableSortedDictionary<string, double> Weights(params (string Tag, double Weight)[] grammarWeights)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, double>(StringComparer.Ordinal);
        foreach (var (tag, weight) in grammarWeights)
        {
            builder.Add(tag, weight);
        }

        builder.Add(ErrorTags.PronVowel, 0.0);
        builder.Add(ErrorTags.PronR, 0.0);
        builder.Add(ErrorTags.PronSilentH, 0.0);
        builder.Add(ErrorTags.PronStress, 0.0);
        return builder.ToImmutable();
    }
}
