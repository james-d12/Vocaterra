namespace Vocaterra.Nlp.Decision.Learning;

/// <summary>Fixed Bayesian Knowledge Tracing parameters: prior mastery, learning rate, slip and guess probabilities.</summary>
public sealed record BktParameters(double PriorKnown, double Learn, double Slip, double Guess)
{
    /// <summary>The default parameters: prior 0.3, learn 0.1, slip 0.1, guess 0.2.</summary>
    public static BktParameters Default { get; } = new(0.3, 0.1, 0.1, 0.2);

    /// <summary>Probability the skill is known before any evidence, in [0, 1].</summary>
    public double PriorKnown { get; init; } = Probability(PriorKnown, nameof(PriorKnown));

    /// <summary>Probability of learning the skill after one attempt, in [0, 1].</summary>
    public double Learn { get; init; } = Probability(Learn, nameof(Learn));

    /// <summary>Probability of answering wrongly although the skill is known, in [0, 1].</summary>
    public double Slip { get; init; } = Probability(Slip, nameof(Slip));

    /// <summary>Probability of answering correctly although the skill is unknown, in [0, 1].</summary>
    public double Guess { get; init; } = Probability(Guess, nameof(Guess));

    /// <summary>
    /// One BKT step: the posterior probability of mastery given the observation, followed by the learning transition.
    /// A degenerate evidence denominator (zero) leaves the prior unchanged before the learning transition.
    /// </summary>
    public double Step(double pKnown, bool correct)
    {
        double numerator;
        double denominator;
        if (correct)
        {
            numerator = pKnown * (1.0 - Slip);
            denominator = numerator + ((1.0 - pKnown) * Guess);
        }
        else
        {
            numerator = pKnown * Slip;
            denominator = numerator + ((1.0 - pKnown) * (1.0 - Guess));
        }

        var posterior = denominator > 0.0 ? numerator / denominator : pKnown;
        var next = posterior + ((1.0 - posterior) * Learn);
        return Math.Clamp(next, 0.0, 1.0);
    }

    private static double Probability(double value, string name)
    {
        return double.IsNaN(value) || value < 0.0 || value > 1.0
            ? throw new ArgumentOutOfRangeException(name, "Must be a probability in [0, 1].")
            : value;
    }
}
