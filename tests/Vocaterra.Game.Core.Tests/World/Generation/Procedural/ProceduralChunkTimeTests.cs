using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// What a chunk costs (B03): the median and the worst time over a sample of surface chunks, of chunks wholly underground and of whole columns, and how much of a column is solid, in the manner of
/// <see cref="SceneBuildTimeTests"/>. It runs in the timing collection, one test at a time after the rest of the assembly, and the bounds are wide: the CI runner is about four times slower than
/// the machine the milestone was measured on, and the figures go to the output for the report and for the streaming budget of B06 and B07.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralChunkTimeTests(ITestOutputHelper output)
{
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
    public void Given_ASampleOfChunks_When_ThePlacesAreGenerated_Then_TheTimePerChunkIsReportedAndWithinBudget()
    {
        var surface = new List<double>();
        var stone = new List<double>();
        var air = new List<double>();
        var columns = new List<double>();
        var perFeature = new Dictionary<Feature, (int NonEmpty, int Columns)>();
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

        // Wide on purpose: the numbers above are the measurement; these only catch a pass that has become a hundred times slower.
        Assert.True(s.Median < 400, $"a surface chunk takes {s.Median:F1} ms at the median");
        Assert.True(s.Worst < 2000, $"the worst surface chunk takes {s.Worst:F1} ms");
        Assert.True(u.Median < 400, $"an all-stone chunk takes {u.Median:F1} ms at the median");
        Assert.True(c24.Median < 3000, $"a column of 24 chunks takes {c24.Median:F1} ms at the median");
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
