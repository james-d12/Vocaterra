using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of TerrainTuningTests (B28), so they run beside the others: RowsCrossTheCoastAndTheLowlandIsSearchedForPools.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningCoastPoolsTests(ITestOutputHelper output) : TerrainTuningTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_RowsCrossTheCoastAndTheLowlandIsSearchedForPools_Then_TheShoreIsOneCleanStripAndNoPuddleLiesOnTheLand(ulong seed)
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var model = new HeightModel(data.WorldGen, seed);
        var table = new BiomeTable(data.Biomes, data.WorldGen, seed);
        var beach = table.Rows.First(r => r.Id == "beach").Index;
        var (crossings, worstWater, worstBeach, beachFlips) = (0, 0, 0, 0);
        var (waterChanges, beachChanges) = (new List<int>(), new List<int>());
        for (var z = -7000; z <= 7000; z += 700)
        {
            var transitions = new List<int>();
            var water = new bool[16385];
            var isBeach = new bool[16385];
            for (var i = 0; i <= 16384; i++)
            {
                var (x, c) = (-8192 + i, climate.At(-8192 + i, z));
                var shape = model.Shape(x, z, c);
                water[i] = shape.Water;
                isBeach[i] = table.Classify(c, shape.Height, x, z, shape.Water, shape.RiverWeight).Row.Index == beach;
                if (i > 0 && water[i] != water[i - 1] && c.Continentalness * 1000 / 4096 <= 20)
                {
                    transitions.Add(i);
                }
            }

            for (var t = 0; t < transitions.Count;)
            {
                var group = transitions.Skip(t).TakeWhile(i => i - transitions[t] <= 300).ToList();
                t += group.Count;
                var (from, to) = (Math.Max(1, group[0] - 300), Math.Min(16384, group[^1] + 300));
                var flips = Enumerable.Range(from, to - from).Count(i => isBeach[i] != isBeach[i - 1]);
                crossings++;
                waterChanges.Add(group.Count);
                beachChanges.Add(flips);
                worstWater = Math.Max(worstWater, group.Count);
                worstBeach = Math.Max(worstBeach, flips);
                beachFlips += flips;
            }
        }

        var s = WorldSample.Of(seed, 4096, 4);
        var pools = s.Bodies(k => s.Water[k] && s.Biome[k] != s.Row("river-channel") && s.Carve[k] == 0, eight: true).Where(b => !b.AtEdge).ToArray();
        var (puddles, ponds) = (pools.Count(b => b.Count <= 3), pools.Count(b => b.Count <= 8));
        var wetShores = waterChanges.Count(c => c > 2) * 100.0 / Math.Max(1, crossings);
        var speckled = beachChanges.Count(c => c > 4) * 100.0 / Math.Max(1, crossings);
        Output.WriteLine($"seed {seed}: {crossings} shores crossed; land/water changes at one shore: most {worstWater}, {wetShores:0.0}% of shores more than 2; beach/not-beach changes within 300 blocks of a shore: most {worstBeach}, mean {beachFlips / (double)Math.Max(1, crossings):0.0}, {speckled:0.0}% of shores more than 4; in 8 192 blocks {pools.Length} bodies of water, {puddles} of 3 samples (48 columns) or fewer, {ponds} of 8 or fewer");

        Assert.True(crossings >= 5, $"{crossings} shores crossed");
        Assert.True(wetShores <= 12, $"{wetShores:0.0}% of the shores change between land and water more than twice"); // one shore, one change; a bay or a river's mouth may add
        Assert.True(speckled <= 12, $"{speckled:0.0}% of the shores flicker between beach and not beach more than 4 times"); // the strip begins and ends
        Assert.True(puddles <= 8, $"{puddles} puddles"); // round 1 left dozens of small ponds and flats on the lowland; a wandering coast pinches off a few inlets of a few columns (round 2: none)
    }
}
