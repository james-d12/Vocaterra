using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The valley's underground timed (B24a). It is in the timing collection, so it runs after the rest of the assembly and alone in it: the first timing test
/// failed four full runs in a row at a load of 11 to 19 (it compared wall clock with a busy machine), so this one only compares what a quiet run measures.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ValleyUndergroundTimeTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;
    private const int Attempts = 3;
    // Measured (B24a, machine at load 10 to 12): the underground adds 2 500 ms of CPU time (plain 837, with it 3 337). The budget is twice that.
    // B29a: re-set from Release (the suite runs on the Release build): the underground adds 526 ms (plain 156, with it 682); the budget is twice that (was 5000 from Debug).
    private static readonly double Budget = CpuTime.Budget(1060);

    [Fact]
    public void Given_TheValleyBuiltWithAndWithoutTheUnderground_When_TheCpuTimeIsMeasured_Then_TheUndergroundCostsUnderItsBudget()
    {
        var layout = TestGame.Data.Scene;
        _ = ValleyBuild.Bare(layout, Seed); // warm up the code: both builds, so the first timed attempt is not paying for the JIT
        _ = ValleyBuild.Build(layout, Seed);
        var (bare, deep) = (double.MaxValue, double.MaxValue);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            bare = Math.Min(bare, CpuMilliseconds(() => ValleyBuild.Bare(layout, Seed)));
            deep = Math.Min(deep, CpuMilliseconds(() => ValleyBuild.Build(layout, Seed)));
        }

        output.WriteLine($"valley build CPU time, fastest of {Attempts}: plain stone {bare:F0} ms, with the underground {deep:F0} ms (the difference {deep - bare:F0} ms)");
        Assert.True(deep - bare < Budget, $"the underground adds {deep - bare:F0} ms of CPU time to the valley's build (budget {Budget:F0} ms)");
    }

    // The CPU time of this process while the action runs, on any thread. Another process's load does not count against it, so a busy machine does not
    // make the underground look slower; a real slowdown adds CPU time in every attempt.
    private static double CpuMilliseconds(Action action)
    {
        using var process = Process.GetCurrentProcess();
        var before = process.TotalProcessorTime;
        action();
        process.Refresh();
        return (process.TotalProcessorTime - before).TotalMilliseconds;
    }
}
