using System.Diagnostics;
using System.Globalization;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// The bundle lint's CPU-time budget (B29b). It was a wall-clock assert (5 s, a raw <c>Stopwatch</c>) inside <see cref="LintTests"/>; here it is the CPU time of the process, the fastest of three runs, alone in the
/// timing collection, against twice the Release cost. Loading the bundle has its own test and budget (<c>GeneratedLexiconLoadTimeTests</c>), so it is read before the clock starts.
/// </summary>
[Collection(Vocaterra.Nlp.Tests.TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
public class LintTimeTests(ITestOutputHelper output)
{
    // B29b: Release, CPU time, fastest of three: 1 103 ms (measured at load 15; 1 103 to 2 883 over three runs); the budget is twice that, rounded up (the wall-clock bound was 5 000 ms).
    private static readonly double LintBudgetMilliseconds = Vocaterra.Nlp.Tests.TimingFactor.Scale(2300);

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheBundleLintIsWithinItsCpuTimeBudget()
    {
        var bundle = TestSupport.Bundle;
        var options = new LintOptions { Level = "a2" };
        var attempts = new List<double>();
        for (var run = 0; run < 3; run++)
        {
            attempts.Add(CpuMilliseconds(() => ContentLinter.Lint(bundle, options)));
        }

        var fastest = attempts.Min();
        output.WriteLine($"ContentLinter.Lint at level a2, CPU time: {string.Join(", ", attempts.Select(ms => ms.ToString("F0", CultureInfo.InvariantCulture)))} ms (fastest {fastest:F0} ms, budget {LintBudgetMilliseconds:F0} ms)");

        Assert.True(fastest < LintBudgetMilliseconds, $"The lint takes {fastest:F0} ms of CPU time; the budget is {LintBudgetMilliseconds:F0} ms.");
    }

    private static double CpuMilliseconds(Action action)
    {
        using var process = Process.GetCurrentProcess();
        var before = process.TotalProcessorTime;
        action();
        process.Refresh();
        return (process.TotalProcessorTime - before).TotalMilliseconds;
    }
}
