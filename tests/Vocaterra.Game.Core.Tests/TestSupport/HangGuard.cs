namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The only wall-clock limit a test outside the timing collections may hold (B29b): a hang guard. A test that checks an outcome must not fail because the machine is slow, so every wait for a worker, a pool or a
/// settle gets a generous limit, multiplied by 4 when CI=true (a GitHub runner is about four times slower than the development machine) or by VOCATERRA_TIMING_FACTOR, and its expiry is reported as a hang ("hung after N s"),
/// never as a wrong result. A test that checks speed holds a CPU-time budget in the timing collection instead (<c>CpuTime</c>); <c>WallClockRulesTests</c> keeps clock reads out of every other test. One source file, linked into
/// the game's test projects.
/// </summary>
internal static class HangGuard
{
    /// <summary>The base limit, in milliseconds, of a wait for a settle (a streamed world or a far view settles in about a minute in Debug on the development machine; the guard is ten minutes).</summary>
    public const long SettleBaseMilliseconds = 600_000;

    /// <summary>The factor every limit is multiplied by: 1 locally, 4 when CI=true or GITHUB_ACTIONS=true, or the value of VOCATERRA_TIMING_FACTOR.</summary>
    public static double Factor { get; } = ReadFactor();

    /// <summary>The limit in milliseconds, scaled by <see cref="Factor"/>.</summary>
    public static long Scale(long milliseconds) => (long)(milliseconds * Factor);

    /// <summary>The limit as a time span, scaled by <see cref="Factor"/>.</summary>
    public static TimeSpan Scale(TimeSpan limit) => limit * Factor;

    /// <summary>The tick count at which a wait that starts now has hung: the limit scaled, added to the clock.</summary>
    public static long DeadlineAfter(long milliseconds) => Environment.TickCount64 + Scale(milliseconds);

    /// <summary>Whether a deadline from <see cref="DeadlineAfter"/> has passed.</summary>
    public static bool Passed(long deadline) => Environment.TickCount64 >= deadline;

    /// <summary>The message of a hang: what was awaited and how long the guard was.</summary>
    public static string Hung(string what, long baseMilliseconds) => $"{what}: hung after {Scale(baseMilliseconds) / 1000} s (a hang guard, not a speed check)";

    /// <summary>The message of a hang for a wait of the given length (<see cref="Scale(long)"/> applied).</summary>
    public static string Hung(string what, TimeSpan baseLimit) => Hung(what, (long)baseLimit.TotalMilliseconds);

    /// <summary>Settles with the scaled limit and fails, as a hang, when it does not settle within it.</summary>
    /// <param name="settle">Settles for at most the given milliseconds and says whether it settled.</param>
    /// <param name="what">What settles, for the message.</param>
    /// <param name="baseMilliseconds">The limit before scaling.</param>
    public static void Settled(Func<double, bool> settle, string what = "the world", long baseMilliseconds = SettleBaseMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(settle);
        var limit = Scale(baseMilliseconds);
        Assert.True(settle(limit), Hung(what + " did not settle", baseMilliseconds));
    }

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
}
