using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>
/// What the guide's snapshot of a streamed world costs (B07; held to a budget by B24, review T2). It used to be a wall-clock bound inside <see cref="PristineChunksTests"/> (20 s) that failed at 21.6 s
/// under a load of 17. Here it is CPU time of the process, the fastest of three passes, in the timing collection (alone in the assembly), against twice what a pass measured: a busy machine does not fail it,
/// and a snapshot that has become twice as slow (a chunk read again and again, a column generated per block) does.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class PristineSnapshotTimeTests(ITestOutputHelper output)
{
    private const int Passes = 3;

    // Measured (B24, Debug as the suite runs, CPU time, fastest of three): 2,930 ms a snapshot on average over the three windows (B27, after the drainage network's reach cache; 5,780 after B02c's drainage network before it, 2,740 before B02c). The budget is twice the measurement, rounded up.
    private static readonly double BudgetMilliseconds = CpuTime.Budget(5_900);

    [Fact]
    public void Given_ThreeWindowsOfTheStreamedWorld_When_TheyAreSnapshotWithoutInstalling_Then_TheCpuTimePerSnapshotIsWithinBudget()
    {
        var nouns = TestGame.Nouns;
        var directions = TestGame.Data.Words.Directions;
        var places = new[] { (200, 200), (600, 260), (-150, 300) };
        var scenes = places.Select(static p => (p, Scene: RegionTestWorld.StreamedScene())).ToList();
        void Snapshot(Scene scene, (int X, int Z) at) => _ = WorldFacts.Snapshot(scene.Grid, nouns, directions, WorldWindow.For(scene, at.X, 70, at.Z), new PristineChunks(scene.Streaming!));

        Snapshot(scenes[0].Scene, scenes[0].p); // warm the code and the shared composition; each timed pass takes new scenes (empty grids), so the snapshots read the pristine source as a first look does
        var best = double.MaxValue;
        for (var pass = 0; pass < Passes; pass++)
        {
            var fresh = places.Select(static p => (p, Scene: RegionTestWorld.StreamedScene())).ToList();
            best = Math.Min(best, CpuTime.Milliseconds(() => fresh.ForEach(f => Snapshot(f.Scene, f.p))) / places.Length);
        }

        output.WriteLine($"a snapshot without installing: {best:F0} ms of CPU time on average over {places.Length} windows, fastest of {Passes} (budget {BudgetMilliseconds:F0} ms)");
        Assert.True(best < BudgetMilliseconds, $"a snapshot takes {best:F0} ms of CPU time (budget {BudgetMilliseconds:F0})");
    }
}
