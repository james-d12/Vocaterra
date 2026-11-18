using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>
/// The seed-42 golden, built for real and timed (S13). It is in the timing collection, so it runs after the rest of the assembly: a run's first
/// seconds build the route for every seed the tests use, many at once, and the budget is for the build, not for those.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class SceneBuildTimeTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    // Measured (B24, Debug, CPU time, fastest of three attempts, with the drainage network): build 3,540 ms and digest 72 ms after the merge of B02c (4,121 and 119 in an earlier, busier run). The budget is twice the quieter, rounded up.
    private static readonly double Budget = CpuTime.Budget(7300);

    [Fact]
    public void Seed42SceneMatchesTheGoldenAndBuildsWithinItsCpuBudget()
    {
        var layout = VoxelTestSupport.RouteDraft; // parsed once, outside the timing

        // B24 (review T2): CPU time of the process (every thread, so the underground's two included), the fastest of three attempts, against twice the measured cost. The wall-clock version failed at 3,026 ms
        // against 3,000 under a load of 15 (it passed alone): another process's load does not count against CPU time, while a build that costs twice as much fails every attempt.
        const int Attempts = 3;
        Scene scene = null!;
        var digest = "";
        var (built, digested) = (double.MaxValue, double.MaxValue);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var buildTime = CpuTime.Milliseconds(() => scene = ValleyBuild.Build(layout, Seed));
            var digestTime = CpuTime.Milliseconds(() => digest = scene.Grid.ComputeDigestHex());
            output.WriteLine($"attempt {attempt + 1}: build CPU time {buildTime:F1} ms (layout already parsed), first world digest {digestTime:F1} ms");
            if (buildTime + digestTime < built + digested)
            {
                (built, digested) = (buildTime, digestTime);
            }
        }

        output.WriteLine($"scene: {scene.Name}, seed {scene.Seed}, {scene.Grid.SizeX} x {scene.Grid.SizeY} x {scene.Grid.SizeZ} blocks, {scene.Grid.Chunks.Count} chunks ({scene.Grid.Chunks.Count(c => c.IsEmpty)} empty), {scene.TreeCount} trees");
        output.WriteLine($"fastest: build CPU time {built:F1} ms, first world digest {digested:F1} ms");
        output.WriteLine($"golden digest: {digest}");
        output.WriteLine($"spawn {scene.Spawn.Feet} yaw {scene.Spawn.Yaw}");
        foreach (var npc in scene.Npcs)
        {
            output.WriteLine($"npc {npc.Name} at {npc.Feet} yaw {npc.Yaw}");
        }

        foreach (var (name, point) in scene.Points)
        {
            output.WriteLine($"point {name} = {point}");
        }

        Assert.Equal(SceneTests.GoldenDigest, digest);
        Assert.True(built + digested < Budget, $"the world takes {built + digested:F0} ms of CPU time to build and digest at the fastest of {Attempts} attempts (the budget is {Budget:F0} ms)");
    }
}
