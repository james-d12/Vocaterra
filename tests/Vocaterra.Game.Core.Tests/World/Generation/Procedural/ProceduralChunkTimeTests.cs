using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// What a chunk costs (B03): the median and the worst time over a sample of surface chunks, of chunks wholly underground and of whole columns, and how much of a column is solid, in the manner of
/// <see cref="SceneBuildTimeTests"/>. It runs in the timing collection, one test at a time after the rest of the assembly. The figures (a median and a worst wall time per chunk) go to the output; what is
/// asserted (B24, review T2) is the CPU time per chunk over the whole sample, the fastest of three passes, against a budget of twice what a pass measured, so a busy machine does not fail it and a pass
/// that has become twice as slow does (CI runners are slower than the development machine: raise a budget there with the measured figure, not past a factor of two).
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralChunkTimeTests(ITestOutputHelper output)
{
    private const int Passes = 3;

    // Measured (B24, Debug as the suite runs, CPU time, fastest of three, after B27's reach cache in the drainage network; B02c had doubled the cost to 13.9, 13.6, 13.1 and 24.2): a surface chunk 6.7 ms, an all-stone chunk 6.4, a deep chunk 6.4, a column of 24 chunks 17.0
    // (4.2 before the underground: a ratio of 4.9). Under load the figures run higher, so the budgets sit at twice the quietest.
    // Each budget is twice the measurement, rounded up: a pass that costs twice as much fails.
    private static readonly double SurfaceBudget = CpuTime.Budget(14);
    private static readonly double StoneBudget = CpuTime.Budget(13);
    private static readonly double ColumnBudget = CpuTime.Budget(41);
    private static readonly double DeepBudget = CpuTime.Budget(13);
    // The ratio of a column with the underground to one without: 4.7 at its quietest (B27, load 10 to 15; 4.7 to 5.3 seen). B27 made the base (the column without the underground) cheaper, so the ratio rose while the
    // underground did not change (2.9 before B02c's network, 6 until B27). The bound is twice the quietest, rounded up.
    private const double RatioBound = 10;

    private static double Milliseconds(Action work)
    {
        var watch = Stopwatch.StartNew();
        work();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static (double Median, double Worst) Summary(List<double> times)
    {
        times.Sort();
        return (times[times.Count / 2], times[^1]);
    }

    [Fact]
    public void Given_ASampleOfChunks_When_ThePlacesAreGenerated_Then_TheCpuTimePerChunkIsReportedAndWithinBudget()
    {
        var surface = new List<double>();
        var stone = new List<double>();
        var air = new List<double>();
        var columns = new List<double>();
        var perFeature = new Dictionary<Feature, (int NonEmpty, int Columns)>();
        var places = new List<(ProceduralChunkSource Source, int Cx, int Cz, List<int> Layers)>();
        _ = ProceduralTestWorld.Source(42).Generate(new VoxelChunkCoord(0, 0, 0)); // warm the JIT outside the timing
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            foreach (var feature in Enum.GetValues<Feature>())
            {
                for (var skip = 0; skip < 2; skip++)
                {
                    var (cx, cz) = ProceduralTestWorld.Find(seed, feature, skip);
                    var source = ProceduralTestWorld.Source(seed);
                    var column = source.GenerateColumn(cx, cz);
                    var surfaceLayers = Enumerable.Range(0, column.Count).Where(i => column[i].Blocks.ToArray().Any(static b => b is not (BlockId.Air or BlockId.Stone))).ToList();
                    places.Add((source, cx, cz, surfaceLayers));
                    foreach (var i in surfaceLayers)
                    {
                        surface.Add(Milliseconds(() => source.Generate(new VoxelChunkCoord(cx, source.MinChunkY + i, cz))));
                    }

                    stone.Add(Milliseconds(() => source.Generate(new VoxelChunkCoord(cx, source.MinChunkY, cz))));
                    air.Add(Milliseconds(() => source.Generate(new VoxelChunkCoord(cx, source.MaxChunkY - 1, cz))));
                    columns.Add(Milliseconds(() => source.GenerateColumn(cx, cz)));
                    var old = perFeature.GetValueOrDefault(feature);
                    perFeature[feature] = (old.NonEmpty + column.Count(static c => !c.IsEmpty), old.Columns + 1);
                }
            }
        }

        var s = Summary(surface);
        var u = Summary(stone);
        var a = Summary(air);
        var c24 = Summary(columns);
        output.WriteLine($"surface chunks ({surface.Count} sampled): median {s.Median:F2} ms, worst {s.Worst:F2} ms");
        output.WriteLine($"all-stone chunks ({stone.Count}): median {u.Median:F2} ms, worst {u.Worst:F2} ms");
        output.WriteLine($"all-air chunks ({air.Count}): median {a.Median:F2} ms, worst {a.Worst:F2} ms");
        output.WriteLine($"whole columns of 24 chunks ({columns.Count}): median {c24.Median:F1} ms, worst {c24.Worst:F1} ms");
        foreach (var (feature, (nonEmpty, count)) in perFeature.OrderBy(static p => p.Key))
        {
            output.WriteLine($"{feature}: {nonEmpty / (double)count:F1} of the 24 chunks of a column are not empty");
        }

        // CPU time over the whole sample (the chunks are generated once above, so the code is warm), the fastest of three passes, per chunk or column.
        var surfaceCount = places.Sum(static p => p.Layers.Count);
        var surfaceCpu = CpuTime.Fastest(Passes, () => places.ForEach(static p => p.Layers.ForEach(i => _ = p.Source.Generate(new VoxelChunkCoord(p.Cx, p.Source.MinChunkY + i, p.Cz))))) / surfaceCount;
        var stoneCpu = CpuTime.Fastest(Passes, () => places.ForEach(static p => _ = p.Source.Generate(new VoxelChunkCoord(p.Cx, p.Source.MinChunkY, p.Cz)))) / places.Count;
        var columnCpu = CpuTime.Fastest(Passes, () => places.ForEach(static p => _ = p.Source.GenerateColumn(p.Cx, p.Cz))) / places.Count;
        output.WriteLine($"CPU time, fastest of {Passes}: a surface chunk {surfaceCpu:F2} ms (budget {SurfaceBudget}), an all-stone chunk {stoneCpu:F2} ms (budget {StoneBudget}), a column of 24 {columnCpu:F1} ms (budget {ColumnBudget})");

        Assert.True(surfaceCpu < SurfaceBudget, $"a surface chunk takes {surfaceCpu:F2} ms of CPU time (budget {SurfaceBudget})");
        Assert.True(stoneCpu < StoneBudget, $"an all-stone chunk takes {stoneCpu:F2} ms of CPU time (budget {StoneBudget})");
        Assert.True(columnCpu < ColumnBudget, $"a column of 24 chunks takes {columnCpu:F1} ms of CPU time (budget {ColumnBudget})");
    }

    [Fact]
    public void Given_ASampleOfChunks_When_TheUndergroundIsAddedToThePipeline_Then_TheCostOfASurfaceChunkAndADeepChunkBeforeAndAfterIsReported()
    {
        // B11: the same chunks of the same places, once from a source with plain stone under the ground (before) and once from the shipped source with the strata, floor, caves, lava and ores (after).
        var before = new List<double>[2] { [], [] };
        var after = new List<double>[2] { [], [] };
        var columnBefore = new List<double>();
        var columnAfter = new List<double>();
        var places = new List<(ProceduralChunkSource Plain, ProceduralChunkSource Shipped, int Cx, int Cz, List<int> Surface)>();
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var plain = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed);
            var shipped = ProceduralTestWorld.Source(seed);
            _ = plain.Generate(new VoxelChunkCoord(0, 0, 0));
            _ = shipped.Generate(new VoxelChunkCoord(0, -2, 0));
            foreach (var feature in new[] { Feature.Plain, Feature.Forest, Feature.Mountain })
            {
                for (var skip = 0; skip < 2; skip++)
                {
                    var (cx, cz) = ProceduralTestWorld.Find(seed, feature, skip);
                    var surface = shipped.GenerateColumn(cx, cz).Select((c, i) => (c, i)).Where(p => p.c.Blocks.ToArray().Any(static b => b is BlockId.Grass or BlockId.Snow or BlockId.Sand)).Select(static p => p.i).ToList();
                    places.Add((plain, shipped, cx, cz, surface));
                    foreach (var i in surface)
                    {
                        before[0].Add(Milliseconds(() => plain.Generate(new VoxelChunkCoord(cx, plain.MinChunkY + i, cz))));
                        after[0].Add(Milliseconds(() => shipped.Generate(new VoxelChunkCoord(cx, shipped.MinChunkY + i, cz))));
                    }

                    foreach (var layer in new[] { -4, -3, -2, -1 })
                    {
                        before[1].Add(Milliseconds(() => plain.Generate(new VoxelChunkCoord(cx, layer, cz))));
                        after[1].Add(Milliseconds(() => shipped.Generate(new VoxelChunkCoord(cx, layer, cz))));
                    }

                    columnBefore.Add(Milliseconds(() => plain.GenerateColumn(cx, cz)));
                    columnAfter.Add(Milliseconds(() => shipped.GenerateColumn(cx, cz)));
                }
            }
        }

        var (sb, sa, db, da, cb, ca) = (Summary(before[0]), Summary(after[0]), Summary(before[1]), Summary(after[1]), Summary(columnBefore), Summary(columnAfter));
        output.WriteLine($"surface chunks ({before[0].Count}): median {sb.Median:F2} ms before, {sa.Median:F2} ms after (worst {sb.Worst:F2} and {sa.Worst:F2})");
        output.WriteLine($"deep chunks, layers -4 to -1 ({before[1].Count}): median {db.Median:F2} ms before, {da.Median:F2} ms after (worst {db.Worst:F2} and {da.Worst:F2})");
        output.WriteLine($"whole columns of 24 chunks ({columnBefore.Count}): median {cb.Median:F1} ms before, {ca.Median:F1} ms after (worst {cb.Worst:F1} and {ca.Worst:F1})");

        // CPU time over the whole sample, the fastest of three passes (the chunks were generated above, so the code is warm), per chunk or column, before and after the underground.
        var surfaceCount = places.Sum(static p => p.Surface.Count);
        double Surface(bool shipped) => CpuTime.Fastest(Passes, () => places.ForEach(p => p.Surface.ForEach(i => _ = (shipped ? p.Shipped : p.Plain).Generate(new VoxelChunkCoord(p.Cx, p.Plain.MinChunkY + i, p.Cz))))) / surfaceCount;
        double Deep(bool shipped) => CpuTime.Fastest(Passes, () => places.ForEach(p => new[] { -4, -3, -2, -1 }.ToList().ForEach(layer => _ = (shipped ? p.Shipped : p.Plain).Generate(new VoxelChunkCoord(p.Cx, layer, p.Cz))))) / (places.Count * 4);
        double Column(bool shipped) => CpuTime.Fastest(Passes, () => places.ForEach(p => _ = (shipped ? p.Shipped : p.Plain).GenerateColumn(p.Cx, p.Cz))) / places.Count;
        var (surfaceAfter, deepAfter, columnAfterCpu, columnBeforeCpu) = (Surface(true), Deep(true), Column(true), Column(false));
        output.WriteLine($"CPU time, fastest of {Passes}: a surface chunk {surfaceAfter:F2} ms (budget {SurfaceBudget}), a deep chunk {deepAfter:F2} ms (budget {DeepBudget}), a column of 24 {columnAfterCpu:F1} ms (budget {ColumnBudget}) against {columnBeforeCpu:F1} ms before the underground (ratio {columnAfterCpu / columnBeforeCpu:F2}, bound {RatioBound})");

        Assert.True(surfaceAfter < SurfaceBudget, $"a surface chunk takes {surfaceAfter:F2} ms of CPU time (budget {SurfaceBudget})");
        Assert.True(deepAfter < DeepBudget, $"a deep chunk takes {deepAfter:F2} ms of CPU time (budget {DeepBudget})");
        Assert.True(columnAfterCpu < ColumnBudget, $"a column of 24 chunks takes {columnAfterCpu:F1} ms of CPU time (budget {ColumnBudget})");
        Assert.True(columnAfterCpu < RatioBound * columnBeforeCpu, $"the underground costs a column {columnAfterCpu / columnBeforeCpu:F2} times what it cost before (bound {RatioBound})");
    }

    [Fact]
    public void Given_AChunkOfTheWorld_When_ItsMemoryIsCounted_Then_ASurfaceChunkHoldsItsBlocksAndAnEmptyOneNothing()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Plain);
        var source = ProceduralTestWorld.Source(42);
        var column = source.GenerateColumn(cx, cz);

        var surfaceChunk = column.First(static c => !c.IsEmpty && c.NonAirCount < 4096);
        var empty = column[^1];

        output.WriteLine($"a surface chunk holds {surfaceChunk.Blocks.Length * sizeof(ushort)} bytes of blocks ({surfaceChunk.NonAirCount} solid blocks), a chunk of air {(empty.IsEmpty ? 0 : empty.Blocks.Length * sizeof(ushort))} bytes");
        Assert.Equal(ChunkData.Volume, surfaceChunk.Blocks.Length);
        Assert.False(surfaceChunk.HasWaterLevels);
        Assert.True(empty.IsEmpty);
    }
}
