using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// What sampling the procedural world costs (B01): climate, height and biome for a 256 x 256 area and for one 16 x 16 chunk column, the way B03 will ask for them (the climate filled
/// once for the area, then the height and the biome of each column). The bounds are loose; the numbers go to the output for the report and for B03's budget.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralTimingTests(ITestOutputHelper output)
{
    private static double Time(Action work, int repeats)
    {
        work(); // warm up the JIT
        var best = double.MaxValue;
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            work();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        return best;
    }

    [Fact]
    public void Given_AnAreaAndAChunk_When_ClimateHeightAndBiomeAreSampled_Then_TheTimeIsReportedAndWithinBudget()
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, 42);
        var model = new HeightModel(data.WorldGen, 42);
        var table = new BiomeTable(data.Biomes, data.WorldGen, 42);

        void Region(int x0, int z0, int size)
        {
            var samples = new ClimateSample[size * size];
            climate.Fill(x0, z0, size, size, samples);
            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    var c = samples[(j * size) + i];
                    var shape = model.Shape(x0 + i, z0 + j, c);
                    _ = table.Select(c, shape.Height, x0 + i, z0 + j);
                }
            }
        }

        var area = Time(() => Region(-128, 5000, 256), 3);
        var chunk = Time(() => Region(16, -16, 16), 20);
        var climateOnly = Time(() => climate.Fill(-128, 5000, 256, 256, new ClimateSample[256 * 256]), 3);
        var perColumn = Time(() =>
        {
            for (var i = 0; i < 4096; i++)
            {
                _ = climate.At(i, -i);
            }
        }, 3);
        output.WriteLine($"256 x 256 columns: climate fill {climateOnly:F1} ms; climate, height and biome {area:F1} ms ({area * 1000 / 65536:F2} us a column)");
        output.WriteLine($"one 16 x 16 chunk column: climate, height and biome {chunk:F2} ms");
        output.WriteLine($"4096 single climate At calls: {perColumn:F1} ms ({perColumn * 1000 / 4096:F2} us each)");

        Assert.True(area < 3000, $"a 256 x 256 area takes {area:F0} ms");
        Assert.True(chunk < 100, $"one chunk column takes {chunk:F1} ms");
    }
}
