namespace Slg.Decision.Scheduling;

/// <summary>FSRS-4.5 parameters: the 17 weights, the desired retention and the longest allowed interval in days.</summary>
public sealed record FsrsParameters
{
    /// <summary>Number of weights FSRS-4.5 defines.</summary>
    public const int WeightCount = 17;

    /// <summary>
    /// The FSRS-4.5 default weights as published in the open-spaced-repetition reference implementation (ts-fsrs 3.5.7, <c>default_w</c>),
    /// with desired retention 0.9 and a maximum interval of 36500 days.
    /// </summary>
    public static FsrsParameters Default { get; } = new(
        [0.5701, 1.4436, 4.1386, 10.9355, 5.1443, 1.2006, 0.8627, 0.0362, 1.629, 0.1342, 1.0166, 2.1174, 0.0839, 0.3204, 1.4676, 0.219, 2.8237],
        0.9,
        36500);

    private readonly double[] _weights;

    /// <summary>Creates parameters; exactly 17 finite weights are required and the retention must lie in (0, 1).</summary>
    public FsrsParameters(IReadOnlyList<double> weights, double desiredRetention, int maximumIntervalDays)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count != WeightCount)
        {
            throw new ArgumentException($"FSRS-4.5 needs exactly {WeightCount} weights.", nameof(weights));
        }

        foreach (var weight in weights)
        {
            if (!double.IsFinite(weight))
            {
                throw new ArgumentException("Weights must be finite.", nameof(weights));
            }
        }

        if (!(desiredRetention > 0.0 && desiredRetention < 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(desiredRetention), "Desired retention must lie in (0, 1).");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maximumIntervalDays, 1);
        _weights = [.. weights];
        DesiredRetention = desiredRetention;
        MaximumIntervalDays = maximumIntervalDays;
    }

    /// <summary>The 17 weights.</summary>
    public IReadOnlyList<double> Weights => _weights;

    /// <summary>The probability of recall the scheduler aims for at the due date.</summary>
    public double DesiredRetention { get; }

    /// <summary>The longest interval the scheduler will produce, in days.</summary>
    public int MaximumIntervalDays { get; }

    /// <summary>Structural equality including the weights.</summary>
    public bool Equals(FsrsParameters? other)
    {
        return other is not null
            && DesiredRetention.Equals(other.DesiredRetention)
            && MaximumIntervalDays == other.MaximumIntervalDays
            && _weights.AsSpan().SequenceEqual(other._weights);
    }

    /// <summary>Hash consistent with <see cref="Equals(FsrsParameters)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(DesiredRetention);
        hash.Add(MaximumIntervalDays);
        foreach (var weight in _weights)
        {
            hash.Add(weight);
        }

        return hash.ToHashCode();
    }
}
