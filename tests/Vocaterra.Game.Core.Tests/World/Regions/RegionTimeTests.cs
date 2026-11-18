using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// What generating the land beside the valley costs (B06): a 3 x 3 patch of chunk columns, the way a streamed world's loader asks for them. The two patches that are asserted are CPU-time budgets
/// (B29d; they were 3 000 ms wall-clock bounds); the numbers go to the output for the report.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class RegionTimeTests(ITestOutputHelper output)
{
    private const int Attempts = 3;

    // B29d: the Release CPU cost of a 3 x 3 patch (the fastest attempt) and the budget, twice the cost, in local milliseconds.
    // Measured (timing runs 1 and 2, the higher of the two): beside 34.3 ms, budget 70; at the corner 49.6 ms (run 1), budget 100. Was 3 000 ms wall each.
    private static readonly double BesideBudget = CpuTime.Budget(70);
    private static readonly double CornerBudget = CpuTime.Budget(100);

    [Fact]
    public void Given_A3By3PatchOfColumnsBesideTheValley_When_TheyAreGenerated_Then_TheTimeIsReportedAndWithinBudget()
    {
        var source = RegionTestWorld.Composition().Source;
        _ = source.GenerateColumn(40, 40); // warm up the JIT

        void Patch(int x0, int z0)
        {
            for (var cz = z0; cz < z0 + 3; cz++)
            {
                for (var cx = x0; cx < x0 + 3; cx++)
                {
                    _ = source.GenerateColumn(cx, cz);
                }
            }
        }

        var beside = CpuTime.Fastest(Attempts, () => Patch(24, 10));
        var corner = CpuTime.Fastest(2, () => Patch(-3, -3));
        var far = CpuTime.Fastest(2, () => Patch(200, 200));
        const int Chunks = 200; // one surface chunk is below the process's CPU clock (ticks of about 10 ms): time two hundred and take the mean
        var single = CpuTime.Fastest(Attempts, () =>
        {
            for (var i = 0; i < Chunks; i++)
            {
                _ = source.Generate(new VoxelChunkCoord(25, 3, 11 + (i % 20)));
            }
        }) / Chunks;

        output.WriteLine($"CPU time: 3 x 3 columns beside the valley {beside:F1} ms ({beside / 9:F2} ms a column; budget {BesideBudget:F0}), at its corner {corner:F1} ms (budget {CornerBudget:F0}), far from it {far:F1} ms; one surface chunk {single:F2} ms (one thread)");
        Assert.True(beside < BesideBudget, $"{beside:F1} ms of CPU time (budget {BesideBudget:F0})");
        Assert.True(corner < CornerBudget, $"{corner:F1} ms of CPU time (budget {CornerBudget:F0})");
    }
}
