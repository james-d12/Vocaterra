using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>What generating the land beside the valley costs (B06): a 3 x 3 patch of chunk columns, the way a streamed world's loader asks for them. The bounds are loose; the numbers go to the output for the report.</summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class RegionTimeTests(ITestOutputHelper output)
{
    [Fact]
    public void Given_A3By3PatchOfColumnsBesideTheValley_When_TheyAreGenerated_Then_TheTimeIsReportedAndWithinBudget()
    {
        var source = RegionTestWorld.Composition().Source;
        _ = source.GenerateColumn(40, 40); // warm up the JIT

        double Patch(int x0, int z0)
        {
            var watch = Stopwatch.StartNew();
            for (var cz = z0; cz < z0 + 3; cz++)
            {
                for (var cx = x0; cx < x0 + 3; cx++)
                {
                    _ = source.GenerateColumn(cx, cz);
                }
            }

            return watch.Elapsed.TotalMilliseconds;
        }

        var beside = new[] { Patch(24, 10), Patch(24, 10), Patch(24, 10) }.Min();
        var corner = new[] { Patch(-3, -3), Patch(-3, -3) }.Min();
        var far = new[] { Patch(200, 200), Patch(200, 200) }.Min();
        var single = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            _ = source.Generate(new Vocaterra.Game.Core.Voxel.VoxelChunkCoord(25, 3, 11 + i));
        }

        output.WriteLine($"3 x 3 columns beside the valley {beside:F1} ms ({beside / 9:F2} ms a column), at its corner {corner:F1} ms, far from it {far:F1} ms; one surface chunk {single.Elapsed.TotalMilliseconds / 20:F2} ms (Debug, one thread)");
        Assert.True(beside < 3000, $"{beside} ms");
        Assert.True(corner < 3000, $"{corner} ms");
    }
}
