namespace Vocaterra.Game.Core.Stats;

/// <summary>What the code knows about one stat key (I05a): how contributions combine, the range one item may give and whether it must be a whole number.</summary>
/// <param name="Key">The stat's key.</param>
/// <param name="Combine">Sum or factor.</param>
/// <param name="Min">The lowest amount one item may give (for a factor, above 0).</param>
/// <param name="Max">The highest amount one item may give.</param>
/// <param name="Whole">Whether an amount must be a whole number.</param>
public sealed record StatRule(string Key, StatCombine Combine, double Min, double Max, bool Whole = false)
{
    /// <summary>The problem with one item's amount, or <see langword="null"/> when it is allowed.</summary>
    /// <param name="amount">What one item gives for this key.</param>
    public string? Problem(double amount)
    {
        if (!double.IsFinite(amount) || amount < Min || amount > Max)
        {
            return $"it must be a number from {Min} to {Max}";
        }

        return Whole && amount != Math.Floor(amount) ? "it must be a whole number" : null;
    }
}
