using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of TerrainTuningTests (B28), so they run beside the others: TheRiverLinesAreRead.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningRiverLinesTests(ITestOutputHelper output) : TerrainTuningTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRiverLinesAreRead_Then_NearlyAllHoldWaterAtManyLevelsAndLakesAreCommon(ulong seed)
    {
        // Round 3 (D-game-34 item 24): rivers hold water all the way down. The river's line is where the carve is nearly full (0.9 or more); below the height rivers fade out from (190: above it the river begins) nearly every column of it is under water,
        // and the surface stands at many different levels, stepping down. Round 2: 7% (the water was the sea's).
        var s = WorldSample.Of(seed, 8192, 8);
        var (line, wet) = (0, 0);
        var levels = new HashSet<int>();
        for (var k = 0; k < s.Ground.Length; k++)
        {
            if (s.Carve[k] < 230 || s.Natural[k] <= 33 || s.Natural[k] > 190)
            {
                continue;
            }

            line++;
            if (s.Water[k])
            {
                wet++;
                levels.Add(s.Level[k]);
            }
        }

        var lakes = s.Biome.Count(b => b == s.Row("lake-basin")) * 100.0 / s.Ground.Length;
        Output.WriteLine($"seed {seed}: {line} river-line columns below 190, {wet * 100.0 / Math.Max(1, line):0.0}% under water, {levels.Count} different water levels; lake-basin {lakes:0.00}% of the columns");

        Assert.True(line > 3000, $"{line} river-line columns");
        Assert.True(wet * 100.0 / line >= 95, $"{wet * 100.0 / line:0.0}% of the river line holds water");
        Assert.True(levels.Count >= 8, $"{levels.Count} water levels");
        Assert.True(lakes >= 0.5, $"lake-basin is {lakes:0.00}% of the columns"); // round 2: 0.03 to 0.6%
    }
}
