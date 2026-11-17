using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The far view's sampler (B07, D-game-34 item 21): the land read from the height model and the biome table with no chunk generated; pure, deterministic and the same land the generator makes.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarTerrainSamplerTests(ITestOutputHelper output)
{
    private static FarTerrainSampler Sampler => RegionTestWorld.Composition().World.Far;

    // Columns outside the valley on a coarse grid, west to east and north to south, over coast, plain and range alike.
    private static IEnumerable<(int X, int Z)> OutsideColumns(int step, int from = -1400, int to = 1400)
    {
        for (var z = from; z <= to; z += step)
        {
            for (var x = from; x <= to; x += step)
            {
                if (x is < -100 or > 480 || z is < -100 or > 480)
                {
                    yield return (x + 5, z + 3);
                }
            }
        }
    }

    [Fact]
    public void Given_ARangeOfMountains_When_TheFarViewReadsItsColumns_Then_TheCliffBandsAndTheScreeShowInTheirTopBlocks()
    {
        // B11b: the far view reads the column's top block, so the cliffs' banded rock and the steep ground's gravel show from afar as they do in the generated chunks.
        var rocks = new HashSet<BlockId> { BlockId.Granite, BlockId.Limestone, BlockId.Slate, BlockId.Gravel };
        var counts = new Dictionary<BlockId, int>();
        var total = 0;
        foreach (var (x, z) in OutsideColumns(11))
        {
            var sample = Sampler.At(x, z);
            total++;
            if (!sample.IsWater && rocks.Contains(sample.Top))
            {
                counts[sample.Top] = counts.GetValueOrDefault(sample.Top) + 1;
            }
        }

        output.WriteLine($"{total} columns: " + string.Join(", ", counts.Select(static c => $"{c.Key} {c.Value}")));
        Assert.True(counts.Values.Sum() >= 20, $"{counts.Values.Sum()} of {total} far columns show cliff rock or scree");
        Assert.True(counts.Count >= 2, "more than one kind of rock shows");
    }

    [Fact]
    public void Given_TheSameColumnsAskedTwiceAndFromManyThreads_When_TheyAreSampled_Then_TheAnswersAreIdentical()
    {
        var columns = new List<(int X, int Z)>(OutsideColumns(97).Take(300)) { (200, 200), (390, 208), (-5, 100) };
        var first = columns.Select(c => Sampler.At(c.X, c.Z)).ToList();

        Assert.Equal(first, columns.Select(c => Sampler.At(c.X, c.Z)).ToList());
        var results = new FarSample[6][];
        Parallel.For(0, 6, new ParallelOptions { MaxDegreeOfParallelism = 3 }, t => results[t] = [.. columns.Select(c => Sampler.At(c.X, c.Z))]);
        Assert.All(results, r => Assert.Equal(first, r));
    }

    [Fact]
    public void Given_AGridOfSamples_When_ItIsFilled_Then_EachEntryIsTheColumnItNames()
    {
        var into = new FarSample[5 * 4];

        Sampler.Fill(-640, 800, 64, 5, 4, into);

        for (var j = 0; j < 4; j++)
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(Sampler.At(-640 + (i * 64), 800 + (j * 64)), into[(j * 5) + i]);
            }
        }
    }

    [Fact]
    public void Given_ColumnsOutsideTheValley_When_TheyAreSampledAndTheChunksGenerated_Then_TheFarLandAgreesWithTheGeneratedSurface()
    {
        var world = RegionTestWorld.Composition().World;
        var columns = OutsideColumns(211).ToList();
        var heightWithin = 0;
        var topEqual = 0;
        var waterEqual = 0;
        var total = 0;
        var worst = 0;
        foreach (var (x, z) in columns.Take(120))
        {
            var sample = world.Far.At(x, z);
            var surface = GeneratedSurface(world, x, z);
            total++;
            var gap = Math.Abs(sample.Height - surface.Ground);
            worst = Math.Max(worst, gap);
            heightWithin += gap <= 3 ? 1 : 0;
            waterEqual += sample.IsWater == surface.Water ? 1 : 0;
            topEqual += sample.Top == surface.Top ? 1 : 0;
        }

        output.WriteLine($"{total} columns: height within 3 blocks {heightWithin}, same water {waterEqual}, same top block {topEqual}, worst height gap {worst}");
        Assert.True(heightWithin >= total * 0.9, $"{heightWithin} of {total}");
        Assert.True(waterEqual >= total * 0.9, $"{waterEqual} of {total}");
        Assert.True(topEqual >= total * 0.8, $"{topEqual} of {total}");
    }

    [Fact]
    public void Given_ColumnsInsideTheValley_When_TheyAreSampled_Then_TheGroundIsTheRegionsOwnWithItsRealTopBlock()
    {
        var region = RegionTestWorld.Valley();
        var equal = 0;
        var total = 0;
        for (var z = 8; z < 384; z += 24)
        {
            for (var x = 8; x < 384; x += 24)
            {
                var sample = Sampler.At(x, z);
                total++;
                equal += sample.Height >= region.EdgeHeight(x, z) && sample.Height <= region.EdgeHeight(x, z) + 14 ? 1 : 0; // a roof or a prop may stand over the ground, nothing deeper than it
                Assert.True(sample.Height >= region.EdgeHeight(x, z) - 1, $"({x}, {z})");
            }
        }

        Assert.True(equal >= total * 0.95, $"{equal} of {total}");
    }

    [Fact]
    public void Given_ColumnsOverTheSeaAndTheMountains_When_TheyAreSampled_Then_WaterAndSnowAreReportedWhereTheyBelong()
    {
        var sea = 0;
        var snow = 0;
        var forest = 0;
        foreach (var (x, z) in OutsideColumns(53, -2600, 2600))
        {
            var sample = Sampler.At(x, z);
            if (sample.IsWater)
            {
                sea++;
                Assert.True(sample.Height < sample.WaterLevel, "the bed is under the water's top");
                Assert.True(sample.WaterLevel >= Sampler.SeaLevel, "a river or a lake stands at its own level, never under the sea's");
                Assert.Equal(0, sample.Forest);
            }
            if (sample.Snow)
            {
                snow++;
                Assert.True(sample.Height > Sampler.SeaLevel + 40, "snow lies on the high land only");
            }

            forest += sample.Forest > 0 ? 1 : 0;
            Assert.InRange(sample.Forest, 0, 100);
            Assert.InRange(sample.Height, Sampler.MinY, Sampler.MaxY);
        }

        output.WriteLine($"sea {sea}, snow {snow}, forest {forest}");
        Assert.True(sea > 0 || snow > 0 || forest > 0);
        Assert.True(forest > 0);
    }

    [Fact]
    public void Given_RiverAndLakeColumnsAboveTheSea_When_TheyAreSampled_Then_TheWaterIsAtItsOwnLevelAsTheGeneratorLeavesIt()
    {
        var world = RegionTestWorld.Composition().World;
        var found = new List<(int X, int Z, FarSample Sample)>();
        for (var z = -3000; z <= 3000 && found.Count < 60; z += 29)
        {
            for (var x = -3000; x <= 3000 && found.Count < 60; x += 31)
            {
                if ((x is < -100 or > 480 || z is < -100 or > 480) && world.Far.At(x, z) is { IsWater: true } sample && sample.WaterLevel > world.Far.SeaLevel)
                {
                    found.Add((x, z, sample));
                }
            }
        }

        Assert.True(found.Count >= 20, $"{found.Count} columns of water above the sea were found: the world has rivers and lakes of their own level");
        var same = 0;
        var worst = 0;
        foreach (var (x, z, sample) in found)
        {
            var top = GeneratedWaterTop(world, x, z);
            var gap = Math.Abs(top - sample.WaterLevel);
            worst = Math.Max(worst, gap);
            same += gap == 0 ? 1 : 0;
        }

        output.WriteLine($"{found.Count} columns of river and lake water above the sea: the sampled level equals the generated water top in {same}, the widest gap is {worst} blocks");
        Assert.True(same >= found.Count * 0.8, $"{same} of {found.Count}");
        Assert.True(worst <= 6, $"the widest gap is {worst} blocks");
    }

    // The y of the top water block of a column in the generated chunks (-1 where there is none).
    private static int GeneratedWaterTop(Vocaterra.Game.Core.World.Streaming.StreamedWorld world, int x, int z)
    {
        var column = world.Source.GenerateColumn(x >> 4, z >> 4);
        for (var i = column.Count - 1; i >= 0; i--)
        {
            for (var ly = 15; ly >= 0; ly--)
            {
                if (!column[i].IsEmpty && column[i][x & 15, ly, z & 15] == BlockId.Water)
                {
                    return ((world.Source.MinChunkY + i) << 4) + ly;
                }
            }
        }

        return -1;
    }

    [Fact]
    public void Given_ManyColumns_When_TheyAreSampled_Then_TheTimePerSampleIsReported()
    {
        _ = Sampler.At(1000, 1000);
        var watch = Stopwatch.StartNew();
        var count = 0;
        foreach (var (x, z) in OutsideColumns(64, -1000, 1000))
        {
            _ = Sampler.At(x, z);
            count++;
        }

        output.WriteLine($"{count} samples, {watch.Elapsed.TotalMilliseconds / count * 1000:0.0} us each (Debug, one thread)");
        Assert.True(watch.Elapsed.TotalSeconds < 20);
    }

    // The ground of a column as the generated chunks have it: from the top, past the air, plants and crowns, the water's top noted, down to the first solid block.
    private static (int Ground, bool Water, BlockId Top) GeneratedSurface(Vocaterra.Game.Core.World.Streaming.StreamedWorld world, int x, int z)
    {
        var column = world.Source.GenerateColumn(x >> 4, z >> 4);
        var water = false;
        for (var i = column.Count - 1; i >= 0; i--)
        {
            if (column[i].IsEmpty)
            {
                continue;
            }

            for (var ly = 15; ly >= 0; ly--)
            {
                var block = column[i][x & 15, ly, z & 15];
                if (block == BlockId.Water)
                {
                    water = true;
                }
                else if (BlockDefinition.IsSolid(block) && !BlockDefinition.IsLeaves(block) && !BlockDefinition.IsTrunk(block))
                {
                    return (((world.Source.MinChunkY + i) << 4) + ly, water, block);
                }
            }
        }

        return (world.Source.MinChunkY << 4, water, BlockId.Stone);
    }
}
