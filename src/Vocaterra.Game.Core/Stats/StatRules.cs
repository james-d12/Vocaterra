namespace Vocaterra.Game.Core.Stats;

/// <summary>
/// The table of the stat keys the code reads (I05a): each key's <see cref="StatRule"/>. A key with no row is open (sum, no range), as before; a new reader adds a constant to
/// <see cref="StatKeys"/> and a row here, and contributors and readers go through <see cref="Contribute"/> and <see cref="Read"/> so no one repeats the rule.
/// </summary>
public static class StatRules
{
    private static readonly IReadOnlyDictionary<string, StatRule> Rows = new[]
    {
        new StatRule(StatKeys.Warmth, StatCombine.Sum, -2000, 2000),
        new StatRule(StatKeys.WalkSpeed, StatCombine.Factor, 0.5, 1.5),
        new StatRule(StatKeys.HungerDrain, StatCombine.Factor, 0.25, 2),
        new StatRule(StatKeys.ThirstDrain, StatCombine.Factor, 0.25, 2),
        new StatRule(StatKeys.MaxStamina, StatCombine.Sum, 0, 100),
        new StatRule(StatKeys.Armour, StatCombine.Sum, 0, 100, Whole: true),
    }.ToDictionary(static r => r.Key, StringComparer.Ordinal);

    /// <summary>The rule of a key, or <see langword="null"/> for an open key.</summary>
    /// <param name="key">The stat's key.</param>
    public static StatRule? Find(string key) => Rows.GetValueOrDefault(key);

    /// <summary>Adds one contribution to the builder by the key's rule (a factor is multiplied in, anything else is added).</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="key">The stat's key.</param>
    /// <param name="amount">The amount.</param>
    public static void Contribute(StatBuilder builder, string key, double amount)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Find(key) is { Combine: StatCombine.Factor })
        {
            builder.Multiply(key, amount);
        }
        else
        {
            builder.Add(key, amount);
        }
    }

    /// <summary>A stat's value by its rule: the factor for a factor key (1 when untouched), the sum for any other (0 when untouched).</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="key">The stat's key.</param>
    public static double Read(StatSheet sheet, string key)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Find(key) is { Combine: StatCombine.Factor } ? sheet.Factor(key) : sheet.Sum(key);
    }
}
