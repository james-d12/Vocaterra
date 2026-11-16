namespace Vocaterra.Game.Core.Stats;

/// <summary>
/// The player's stats for one tick (I03a): a read-only value for each open key, built from the contributors by a <see cref="StatBuilder"/>. A stat has a sum (what was added) and a factor (what
/// was multiplied); <see cref="Get"/> is the two together. A key nobody touched reads as no sum (0) and no factor (1), so a reader never needs to know whether any source exists.
/// </summary>
public sealed class StatSheet
{
    private readonly SortedDictionary<string, double> _sums;
    private readonly SortedDictionary<string, double> _factors;

    internal StatSheet(IDictionary<string, double> sums, IDictionary<string, double> factors)
    {
        _sums = new SortedDictionary<string, double>(sums, StringComparer.Ordinal);
        _factors = new SortedDictionary<string, double>(factors, StringComparer.Ordinal);
    }

    /// <summary>A sheet with nothing in it.</summary>
    public static StatSheet Empty { get; } = new(new Dictionary<string, double>(), new Dictionary<string, double>());

    /// <summary>The keys that have a sum or a factor, in key order.</summary>
    public IEnumerable<string> Keys => _sums.Keys.Union(_factors.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);

    /// <summary>The sum of what was added to <paramref name="key"/> (0 when nothing was).</summary>
    /// <param name="key">The stat's key.</param>
    public double Sum(string key) => _sums.GetValueOrDefault(key);

    /// <summary>The product of what <paramref name="key"/> was multiplied by (1 when nothing was).</summary>
    /// <param name="key">The stat's key.</param>
    public double Factor(string key) => _factors.GetValueOrDefault(key, 1.0);

    /// <summary>The stat's value: its sum times its factor (0 when nothing was added).</summary>
    /// <param name="key">The stat's key.</param>
    public double Get(string key) => Sum(key) * Factor(key);

    /// <summary>The value rounded to the nearest whole number, halves away from zero (for the stats that are whole numbers, like <see cref="StatKeys.Warmth"/>'s tenths of a degree).</summary>
    /// <param name="key">The stat's key.</param>
    public int GetWhole(string key) => (int)Math.Round(Get(key), MidpointRounding.AwayFromZero);
}
