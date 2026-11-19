namespace Vocaterra.Game.Core.Stats;

/// <summary>Collects the sums and factors of the contributors for one <see cref="StatSheet"/> (I03a). Order does not matter for the sums of whole numbers, and the pipeline calls the contributors in a fixed order anyway.</summary>
public sealed class StatBuilder
{
    private readonly SortedDictionary<string, double> _sums = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, double> _factors = new(StringComparer.Ordinal);

    /// <summary>Adds <paramref name="amount"/> to the sum of a stat.</summary>
    /// <param name="key">The stat's key (<see cref="StatKeys.IsWellFormed"/>).</param>
    /// <param name="amount">The amount; negative takes away.</param>
    /// <exception cref="ArgumentException">The key is not well formed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is not a finite number.</exception>
    public void Add(string key, double amount)
    {
        Check(key);
        if (!double.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount must be a finite number.");
        }

        _sums[key] = _sums.GetValueOrDefault(key) + amount;
    }

    /// <summary>Multiplies the factor of a stat (1 when nothing has).</summary>
    /// <param name="key">The stat's key.</param>
    /// <param name="factor">The factor; 1 leaves the stat alone.</param>
    /// <exception cref="ArgumentException">The key is not well formed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The factor is not a finite number.</exception>
    public void Multiply(string key, double factor)
    {
        Check(key);
        if (!double.IsFinite(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "A factor must be a finite number.");
        }

        _factors[key] = _factors.GetValueOrDefault(key, 1.0) * factor;
    }

    /// <summary>The sheet of everything added so far.</summary>
    public StatSheet Build() => new(_sums, _factors);

    private static void Check(string key)
    {
        if (!StatKeys.IsWellFormed(key))
        {
            throw new ArgumentException($"'{key}' is not a stat key (a lowercase letter, then letters and digits).", nameof(key));
        }
    }
}
