using System.Text.Json.Serialization;
using Slg.Core.Determinism;
using Slg.Core.Verdicts;

namespace Slg.Decision.Scheduling;

/// <summary>The correctness cut-offs and uncertainty half-width used to turn a verdict into an FSRS rating.</summary>
public sealed record RatingThresholds(double HardThreshold, double EasyThreshold, double Epsilon)
{
    /// <summary>Below 0.5 is Hard, below 0.9 is Good, otherwise Easy, with a band of 0.02 around each cut-off.</summary>
    public static RatingThresholds Default { get; } = new(0.5, 0.9, 0.02);

    /// <summary>The margin separating Hard from Good; correctness inside the band rounds down to Hard.</summary>
    [JsonIgnore]
    public Margin HardMargin => new(HardThreshold, Epsilon);

    /// <summary>The margin separating Good from Easy; correctness inside the band rounds down to Good.</summary>
    [JsonIgnore]
    public Margin EasyMargin => new(EasyThreshold, Epsilon);
}

/// <summary>Maps a verdict to the FSRS rating of the practised item.</summary>
public static class RatingMapper
{
    /// <summary>Maps a verdict with <see cref="RatingThresholds.Default"/>.</summary>
    public static Rating FromVerdict(Verdict verdict)
    {
        return FromVerdict(verdict, RatingThresholds.Default);
    }

    /// <summary>
    /// Not communicated gives Again. Otherwise correctness is compared with the Hard margin and then the Easy margin;
    /// only Accept moves up, so an Uncertain score rounds down to the lower rating.
    /// </summary>
    public static Rating FromVerdict(Verdict verdict, RatingThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (!verdict.Communicated)
        {
            return Rating.Again;
        }

        if (thresholds.HardMargin.Evaluate(verdict.Correctness) != MarginDecision.Accept)
        {
            return Rating.Hard;
        }

        return thresholds.EasyMargin.Evaluate(verdict.Correctness) == MarginDecision.Accept ? Rating.Easy : Rating.Good;
    }
}
