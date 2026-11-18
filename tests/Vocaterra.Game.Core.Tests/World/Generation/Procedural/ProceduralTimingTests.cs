using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// What sampling the procedural world costs (B01): climate, height and biome for a 256 x 256 area and for one 16 x 16 chunk column, the way B03 will ask for them (the climate filled
/// once for the area, then the height and the biome of each column). The two asserted costs are CPU-time budgets (B29d; they were wall-clock bounds of 3 000 and 100 ms); the numbers go to the
/// output for the report and for B03's budget.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralTimingTests(ITestOutputHelper output)
{
    private const int Attempts = 3;

    // B29d: the Release CPU cost of each asserted sample (fastest attempt) and the budget, twice the cost, in local milliseconds.
    // Measured (timing runs 1 and 2, the higher of the two): the 256 x 256 area 24.9 ms, budget 50 (was 3 000 ms wall); one 16 x 16 chunk column 0.19 ms (the mean of 1 000 repeats,
    // which the clock resolves), budget 0.4 (was 100 ms wall).
    private static readonly double AreaBudget = CpuTime.Budget(50);
    private static readonly double ChunkBudget = CpuTime.Budget(0.4);

    /// <summary>The CPU time of one call of <paramref name="work"/>, the fastest of <paramref name="repeats"/> attempts after a warm-up.</summary>
    private static double Time(Action work, int repeats)
    {
        work(); // warm up the JIT
        return CpuTime.Fastest(repeats, work);
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

        // The small samples are batched: one call of each is below the process's CPU clock (ticks of about 10 ms), so each figure is the mean over the batch.
        const int ChunkRepeats = 1000; // one column is 0.14 ms: a batch of a thousand is about 140 ms, well above the CPU clock's tick
        const int ClimateRepeats = 10;
        const int AtRepeats = 100;
        var area = Time(() => Region(-128, 5000, 256), Attempts);
        var chunk = Time(() =>
        {
            for (var r = 0; r < ChunkRepeats; r++)
            {
                Region(16, -16, 16);
            }
        }, Attempts) / ChunkRepeats;
        var climateOnly = Time(() =>
        {
            for (var r = 0; r < ClimateRepeats; r++)
            {
                climate.Fill(-128, 5000, 256, 256, new ClimateSample[256 * 256]);
            }
        }, Attempts) / ClimateRepeats;
        var perColumn = Time(() =>
        {
            for (var r = 0; r < AtRepeats; r++)
            {
                for (var i = 0; i < 4096; i++)
                {
                    _ = climate.At(i, -i);
                }
            }
        }, Attempts) / AtRepeats;
        output.WriteLine($"CPU time, fastest of {Attempts}: 256 x 256 columns: climate fill {climateOnly:F1} ms; climate, height and biome {area:F1} ms (budget {AreaBudget:F0}; {area * 1000 / 65536:F2} us a column)");
        output.WriteLine($"one 16 x 16 chunk column: climate, height and biome {chunk:F2} ms (budget {ChunkBudget:F2}, the mean of {ChunkRepeats})");
        output.WriteLine($"4096 single climate At calls: {perColumn:F1} ms ({perColumn * 1000 / 4096:F2} us each)");

        Assert.True(area < AreaBudget, $"a 256 x 256 area takes {area:F0} ms of CPU time (budget {AreaBudget:F0})");
        Assert.True(chunk < ChunkBudget, $"one chunk column takes {chunk:F2} ms of CPU time (budget {ChunkBudget:F2})");
    }
}
