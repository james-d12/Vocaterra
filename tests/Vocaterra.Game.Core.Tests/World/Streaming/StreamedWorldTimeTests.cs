using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>
/// What streaming the tall world costs per chunk (B07), beside <c>SceneBuildTimeTests</c> and <c>RegionTimeTests</c>: the surface band of a column, a column's band layers made from one window, the install, the plan, the far land's sample.
/// The numbers go to the output for the report. What is asserted (B24, review T2) is CPU time, the fastest of three passes over the repeats, against a budget of twice what a pass measured on the
/// development machine, so a busy machine does not fail it and a step that has become twice as slow does.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldTimeTests(ITestOutputHelper output)
{
    // Measured (B24, Debug as the suite runs, CPU time, fastest of three): (B27, after the drainage network's reach cache; 15.4 and 20.4 before it) a column's band 6.4 ms, its band layers 11.2 (2.6 a chunk), a plan of 28 columns' radius 1.0. Each budget is twice the measurement, rounded up.
    private const double BandBudget = 13;
    private const double MadeBudget = 23;
    private const double PerChunkBudget = 6;
    private const double PlanBudget = 2;

    // The CPU milliseconds one call costs: after one warm-up call, the fastest of three passes of `repeats` calls.
    private static double Time(Action action, int repeats)
    {
        action();
        return CpuTime.Fastest(3, () =>
        {
            for (var i = 0; i < repeats; i++)
            {
                action();
            }
        }) / repeats;
    }

    [Fact]
    public void Given_ColumnsOutsideTheValley_When_TheirBandsAndLayersAreMade_Then_TheCpuTimePerChunkIsReportedAndWithinBudget()
    {
        var world = RegionTestWorld.Composition().World;
        var columns = Enumerable.Range(0, 24).Select(static i => (X: 60 + (i * 7), Z: -40 + ((i * 13) % 50))).ToList();
        var next = 0;

        var band = Time(() => _ = world.Bands.Of(columns[next++ % columns.Count].X, columns[next % columns.Count].Z), 24);
        var bands = columns.Select(c => world.Bands.Of(c.X, c.Z)).ToList();
        var layers = bands.Average(static b => b.Count);
        next = 0;
        var made = Time(() =>
        {
            var c = columns[next++ % columns.Count];
            var b = bands[(next - 1) % bands.Count];
            _ = world.GenerateLayers(c.X, c.Z, b.MinLayer - 1, b.MaxLayer + 1);
        }, 24);
        var whole = Time(() => _ = world.Source.GenerateColumn(columns[next++ % columns.Count].X, columns[next % columns.Count].Z), 12);

        var scene = RegionTestWorld.StreamedScene();
        var chunks = world.GenerateLayers(columns[0].X, columns[0].Z, 0, 8);
        var install = Time(() =>
        {
            foreach (var chunk in chunks)
            {
                _ = GridResidency.Install(scene.Grid, chunk);
                _ = GridResidency.TryEvict(scene.Grid, chunk.Coord);
            }
        }, 20) / chunks.Count;

        var streamer = new BandStreamer(new BandStreamPolicy(28, 30, 4, 6), scene.Grid.Limits);
        var cache = new Dictionary<(int, int), ColumnBand>();
        ColumnBand Cached(int x, int z) => cache.TryGetValue((x, z), out var b) ? b : cache[(x, z)] = new ColumnBand(3, 5);
        _ = streamer.Plan(new VoxelChunkCoord(100, 4, 0), false, Cached);
        var plan = Time(() => _ = streamer.Plan(new VoxelChunkCoord(100, 4, 0), false, Cached), 20); // the process clock ticks every 10 ms, so enough repeats to span several

        output.WriteLine($"a column's band {band:F2} ms (about {layers:F1} layers); its band layers and a layer each side {made:F2} ms ({made / (layers + 2):F2} ms a chunk), a whole column of 24 layers {whole:F2} ms; one chunk installed and dropped {install * 1000:F0} us; a plan of 28 columns' radius (cached bands) {plan:F1} ms (CPU time, fastest of three)");
        Assert.True(band < BandBudget, $"a column's band takes {band:F2} ms of CPU time (budget {BandBudget})");
        Assert.True(made < MadeBudget, $"a column's band layers take {made:F2} ms of CPU time (budget {MadeBudget})");
        Assert.True(made / (layers + 2) < PerChunkBudget, $"a chunk of a band takes {made / (layers + 2):F2} ms of CPU time (budget {PerChunkBudget})");
        Assert.True(plan < PlanBudget, $"a plan takes {plan:F1} ms of CPU time (budget {PlanBudget})");
    }
}
