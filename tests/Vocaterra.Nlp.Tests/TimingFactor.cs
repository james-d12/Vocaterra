namespace Vocaterra.Nlp.Tests;

/// <summary>
/// The factor the CPU-time budgets are multiplied by (B30): 1 on the development machine, 4 on CI (a GitHub runner has 4 cores and is about four times slower), or the value of
/// VOCATERRA_TIMING_FACTOR. CI is recognised by CI=true or GITHUB_ACTIONS=true. The game's tests have the same rule in CpuTime.Budget.
/// </summary>
internal static class TimingFactor
{
    public static double Value { get; } = Read();

    /// <summary>The local budget in milliseconds, times the factor.</summary>
    public static double Scale(double localBudget) => localBudget * Value;

    private static double Read()
    {
        var text = Environment.GetEnvironmentVariable("VOCATERRA_TIMING_FACTOR");
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var set) && set > 0)
        {
            return set;
        }

        return IsTrue("CI") || IsTrue("GITHUB_ACTIONS") ? 4 : 1;
    }

    private static bool IsTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);
}
