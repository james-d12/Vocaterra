using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Nlp.Evaluation;

/// <summary>Error weights and decision thresholds that evaluation uses for one difficulty tier.</summary>
/// <remarks>
/// Every number must be finite. <see cref="ErrorWeights"/> is copied into an ordinal-sorted immutable dictionary
/// so enumeration and serialization order never depend on the source collection.
/// </remarks>
public sealed record DifficultyEvaluationConfig(
    IReadOnlyDictionary<string, double> ErrorWeights,
    double CommunicatedConfidenceThreshold,
    double CorrectnessThreshold,
    double PronunciationThreshold,
    double Epsilon,
    double UnknownTagWeight)
{
    /// <summary>Correctness penalty per error tag; pronunciation-family tags are 0.0 by default so they never lower correctness.</summary>
    public IReadOnlyDictionary<string, double> ErrorWeights { get; init; } = NormalizeWeights(ErrorWeights);

    /// <summary>Confidence at which an utterance counts as communicated (before the epsilon band).</summary>
    public double CommunicatedConfidenceThreshold { get; init; } = RequireFinite(CommunicatedConfidenceThreshold, nameof(CommunicatedConfidenceThreshold));

    /// <summary>Correctness at which an utterance counts as correct (before the epsilon band).</summary>
    public double CorrectnessThreshold { get; init; } = RequireFinite(CorrectnessThreshold, nameof(CorrectnessThreshold));

    /// <summary>Mean phone score at which pronunciation counts as good (before the epsilon band).</summary>
    public double PronunciationThreshold { get; init; } = RequireFinite(PronunciationThreshold, nameof(PronunciationThreshold));

    /// <summary>Half-width of the uncertainty band applied to every threshold; must be non-negative.</summary>
    public double Epsilon { get; init; } = RequireNonNegative(Epsilon, nameof(Epsilon));

    /// <summary>Correctness penalty for a non-pronunciation tag that has no entry in <see cref="ErrorWeights"/>; must be non-negative.</summary>
    public double UnknownTagWeight { get; init; } = RequireNonNegative(UnknownTagWeight, nameof(UnknownTagWeight));

    /// <summary>Returns the correctness penalty of a tag: its configured weight, 0.0 for an unconfigured pronunciation tag, else <see cref="UnknownTagWeight"/>.</summary>
    public double WeightFor(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (ErrorWeights.TryGetValue(tag, out var weight))
        {
            return weight;
        }

        return ErrorTags.IsPronunciation(tag) ? 0.0 : UnknownTagWeight;
    }

    /// <summary>The margin deciding whether the verdict confidence is high enough to count as communicated.</summary>
    public Margin CommunicatedMargin()
    {
        return new Margin(CommunicatedConfidenceThreshold, Epsilon);
    }

    /// <summary>The margin deciding whether correctness is high enough to count as correct.</summary>
    public Margin CorrectnessMargin()
    {
        return new Margin(CorrectnessThreshold, Epsilon);
    }

    /// <summary>The margin deciding whether the mean phone score is high enough to count as good pronunciation.</summary>
    public Margin PronunciationMargin()
    {
        return new Margin(PronunciationThreshold, Epsilon);
    }

    /// <summary>Structural equality including the weight map contents.</summary>
    public bool Equals(DifficultyEvaluationConfig? other)
    {
        if (other is null
            || !CommunicatedConfidenceThreshold.Equals(other.CommunicatedConfidenceThreshold)
            || !CorrectnessThreshold.Equals(other.CorrectnessThreshold)
            || !PronunciationThreshold.Equals(other.PronunciationThreshold)
            || !Epsilon.Equals(other.Epsilon)
            || !UnknownTagWeight.Equals(other.UnknownTagWeight)
            || ErrorWeights.Count != other.ErrorWeights.Count)
        {
            return false;
        }

        foreach (var pair in ErrorWeights)
        {
            if (!other.ErrorWeights.TryGetValue(pair.Key, out var value) || !pair.Value.Equals(value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(DifficultyEvaluationConfig)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(CommunicatedConfidenceThreshold);
        hash.Add(CorrectnessThreshold);
        hash.Add(PronunciationThreshold);
        hash.Add(Epsilon);
        hash.Add(UnknownTagWeight);
        foreach (var pair in ErrorWeights)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    private static ImmutableSortedDictionary<string, double> NormalizeWeights(IReadOnlyDictionary<string, double> source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(ErrorWeights));
        foreach (var pair in source)
        {
            RequireNonNegative(pair.Value, $"{nameof(ErrorWeights)}[{pair.Key}]");
        }

        if (source is ImmutableSortedDictionary<string, double> sorted && ReferenceEquals(sorted.KeyComparer, StringComparer.Ordinal))
        {
            return sorted;
        }

        return ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, source);
    }

    private static double RequireFinite(double value, string name)
    {
        return double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Value must be finite.");
    }

    private static double RequireNonNegative(double value, string name)
    {
        return double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Value must be finite and non-negative.");
    }
}
