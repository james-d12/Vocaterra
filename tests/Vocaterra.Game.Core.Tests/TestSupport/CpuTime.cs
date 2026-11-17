using System.Diagnostics;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// How the timing tests measure (B24, review T2): the CPU time this process spends while an action runs, and the fastest of several attempts. Another process's load does not count against it, so a busy
/// machine does not make code look slower; a real slowdown adds CPU time in every attempt, so the fastest attempt shows it. It is only meaningful in the timing collection, which runs alone in the assembly (the
/// process counts every thread, and the operating system reports it in ticks of about 10 ms, so time whole samples of work, not one call).
/// </summary>
internal static class CpuTime
{
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
