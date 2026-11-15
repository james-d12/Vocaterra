using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>
/// What streaming the tall world costs per chunk (B07), beside <c>SceneBuildTimeTests</c> and <c>RegionTimeTests</c>: the surface band of a column, a column's band layers made from one window, the install, the plan, the far land's sample.
/// The bounds are loose; the numbers go to the output for the report.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldTimeTests(ITestOutputHelper output)
{
    private static double Time(Action action, int repeats)
    {
        action();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < repeats; i++)
        {
            action();
        }

        return watch.Elapsed.TotalMilliseconds / repeats;
    }

    [Fact]
    public void Given_ColumnsOutsideTheValley_When_TheirBandsAndLayersAreMade_Then_TheTimePerChunkIsReportedAndWithinBudget()
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
        var plan = Time(() => _ = streamer.Plan(new VoxelChunkCoord(100, 4, 0), false, Cached), 5);

        output.WriteLine($"a column's band {band:F2} ms (about {layers:F1} layers); its band layers and a layer each side {made:F2} ms ({made / (layers + 2):F2} ms a chunk), a whole column of 24 layers {whole:F2} ms; one chunk installed and dropped {install * 1000:F0} us; a plan of 28 columns' radius (cached bands) {plan:F1} ms (Debug, one thread)");
        Assert.True(band < 200, $"{band} ms");
        Assert.True(made < 400, $"{made} ms");
        Assert.True(plan < 500, $"{plan} ms");
    }
}
