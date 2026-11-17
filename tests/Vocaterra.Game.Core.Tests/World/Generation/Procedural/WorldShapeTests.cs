using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The shipped numbers make the world the plan asks for (B01): a sea and a coast, lowlands, uplands and great ranges, rivers and lakes, and every shipped biome somewhere. The
/// bounds are wide on purpose: they hold the shape of the world, not a tuning the owner will change after the maps (B02).
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class WorldShapeTests
{
    [Fact]
    public void Given_ThreeSeedsOverAWideArea_When_TheWorldIsSampled_Then_ItHasSeaLandRangesAndEveryShippedBiome()
    {
        var data = TestGame.Data;
        var counts = new Dictionary<string, int>();
        var tallest = 0;
        var samples = 0;
        var water = 0;
        var high = 0;
        var rivers = 0;
        foreach (ulong seed in new ulong[] { 42, 7, 2026 })
        {
            var climate = new ClimateSampler(data.WorldGen, seed);
            var model = new HeightModel(data.WorldGen, seed);
            var table = new BiomeTable(data.Biomes, data.WorldGen, seed);
            for (var z = -12_000; z < 12_000; z += 64)
            {
                for (var x = -12_000; x < 12_000; x += 64)
                {
                    var c = climate.At(x, z);
                    var shape = model.Shape(x, z, c);
                    var id = table.Classify(c, shape.Height, x, z, shape.Water, shape.RiverWeight).Row.Id;
                    counts[id] = counts.GetValueOrDefault(id) + 1;
                    samples++;
                    water += shape.Water ? 1 : 0;
                    high += shape.Height >= 190 ? 1 : 0;
                    rivers += shape.RiverWeight > 0 ? 1 : 0;
                    tallest = Math.Max(tallest, shape.Height);
                }
            }
        }

        var waterShare = water * 100.0 / samples;
        Assert.InRange(waterShare, 35, 48); // the tuned world (B02b redo): big seas between continents, 35 to 45% of the map
        Assert.InRange(high * 100.0 / samples, 0.3, 8);
        Assert.InRange(tallest, 220, data.WorldGen.Height.Clamp);
        Assert.True(rivers > samples / 200, $"{rivers} river valley columns of {samples}");
        foreach (var row in data.Biomes.Rows)
        {
            Assert.True(counts.GetValueOrDefault(row.Id) > 0, $"the biome '{row.Id}' never appears in {samples} columns of three seeds");
        }

        Assert.True(counts.Where(static c => c.Key != "ocean").Max(static c => c.Value) < samples * 0.5, "no land biome covers half the world"); // the sea does: about two thirds of the tuned map (B02b)
    }
}
