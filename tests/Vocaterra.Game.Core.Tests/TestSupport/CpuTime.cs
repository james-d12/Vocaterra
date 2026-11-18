using System.Diagnostics;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// How the timing tests measure (B24, review T2): the CPU time this process spends while an action runs, and the fastest of several attempts. Another process's load does not count against it, so a busy
/// machine does not make code look slower; a real slowdown adds CPU time in every attempt, so the fastest attempt shows it. It is only meaningful in the timing collection, which runs alone in the assembly (the
/// process counts every thread, and the operating system reports it in ticks of about 10 ms, so time whole samples of work, not one call).
/// </summary>
internal static class CpuTime
{
    /// <summary>
    /// The factor every CPU budget is multiplied by (B30): 1 locally, 4 when CI=true or GITHUB_ACTIONS=true (a GitHub runner has 4 cores and is about four times slower), or the value of
    /// VOCATERRA_TIMING_FACTOR. The core's tests have the same rule in Vocaterra.Nlp.Tests.TimingFactor.
    /// </summary>
    public static double Factor { get; } = ReadFactor();

    /// <summary>A budget as measured on the development machine, in milliseconds, scaled by <see cref="Factor"/>.</summary>
    public static double Budget(double localMilliseconds) => localMilliseconds * Factor;

    private static double ReadFactor()
    {
        var text = Environment.GetEnvironmentVariable("VOCATERRA_TIMING_FACTOR");
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var set) && set > 0)
        {
            return set;
        }

        return IsTrue("CI") || IsTrue("GITHUB_ACTIONS") ? 4 : 1;
    }

    private static bool IsTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>The CPU time, in milliseconds, of this process while <paramref name="action"/> runs, on any thread.</summary>
    public static double Milliseconds(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var process = Process.GetCurrentProcess();
        var before = process.TotalProcessorTime;
        action();
        process.Refresh();
        return (process.TotalProcessorTime - before).TotalMilliseconds;
    }

    /// <summary>The smallest <see cref="Milliseconds"/> over <paramref name="attempts"/> runs of the action (it should be warmed up first).</summary>
    public static double Fastest(int attempts, Action action)
    {
        var best = double.MaxValue;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            best = Math.Min(best, Milliseconds(action));
        }

        return best;
    }
}
