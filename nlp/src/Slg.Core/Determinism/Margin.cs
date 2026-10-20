namespace Slg.Core.Determinism;

/// <summary>Three-way outcome of comparing a score against a threshold with an uncertainty band.</summary>
public enum MarginDecision
{
    /// <summary>The score is at least threshold plus epsilon.</summary>
    Accept,

    /// <summary>The score is at most threshold minus epsilon.</summary>
    Reject,

    /// <summary>The score lies strictly inside the band around the threshold.</summary>
    Uncertain,
}

/// <summary>A threshold with a symmetric epsilon band so no decision sits on a knife edge.</summary>
public readonly record struct Margin(double Threshold, double Epsilon)
{
    /// <summary>The decision threshold; must not be NaN.</summary>
    public double Threshold { get; init; } = double.IsNaN(Threshold)
        ? throw new ArgumentOutOfRangeException(nameof(Threshold), "Threshold must not be NaN.")
        : Threshold;

    /// <summary>Half-width of the uncertainty band; must be non-negative and not NaN.</summary>
    public double Epsilon { get; init; } = double.IsNaN(Epsilon) || Epsilon < 0
        ? throw new ArgumentOutOfRangeException(nameof(Epsilon), "Epsilon must be non-negative and not NaN.")
        : Epsilon;

    /// <summary>Accepts at or above threshold + epsilon, rejects at or below threshold - epsilon, otherwise uncertain.</summary>
    public MarginDecision Evaluate(double score)
    {
        if (double.IsNaN(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must not be NaN.");
        }

        if (score >= Threshold + Epsilon)
        {
            return MarginDecision.Accept;
        }

        if (score <= Threshold - Epsilon)
        {
            return MarginDecision.Reject;
        }

        return MarginDecision.Uncertain;
    }
}
