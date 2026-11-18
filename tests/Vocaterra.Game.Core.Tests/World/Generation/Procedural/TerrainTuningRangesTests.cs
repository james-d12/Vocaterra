using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of TerrainTuningTests (B28), so they run beside the others: TheyAreChainsOfPeaksAndSaddles.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningRangesTests(ITestOutputHelper output) : TerrainTuningTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRangesAreMapped_Then_TheyAreChainsOfPeaksAndSaddlesNotPlateaus(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var land = s.Ground.Where((_, k) => !s.Water[k]).ToArray();
        double Above(int y) => land.Count(h => h > y) * 100.0 / land.Length;
        var ranges = s.Bodies(k => !s.Water[k] && s.Ground[k] > 150, eight: true);
        var inRange = ranges.Sum(b => b.Count);
        var big = ranges.Where(b => b.Count >= 100).Sum(b => b.Count) * 100.0 / inRange;
        var longest = ranges.Max(b => Math.Max(b.WidthBlocks, b.DepthBlocks));

        // Flatness of the high land: over the columns above 220, the spread of height within 64 blocks, and the 64 x 64 block tiles that lie wholly above 220 and are level.
        var f = WorldSample.Of(seed, 6144, 8);
        var n = f.Size;
        int At(int x, int z) => f.Ground[(z * n) + x];
        var (high, spreadSum, tiles, levelTiles) = (0, 0L, 0, 0);
        for (var z = 8; z < n - 8; z += 2)
        {
            for (var x = 8; x < n - 8; x += 2)
            {
                if (At(x, z) <= 220 || f.Water[(z * n) + x])
                {
                    continue;
                }

                int lo = int.MaxValue, hi = int.MinValue;
                for (var dz = -4; dz <= 4; dz++)
                {
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        (lo, hi) = (Math.Min(lo, At(x + dx, z + dz)), Math.Max(hi, At(x + dx, z + dz)));
                    }
                }

                high++;
                spreadSum += hi - lo;
            }
        }

        for (var z = 0; z + 8 <= n; z += 8)
        {
            for (var x = 0; x + 8 <= n; x += 8)
            {
                int lo = int.MaxValue, hi = int.MinValue;
                for (var dz = 0; dz < 8; dz++)
                {
                    for (var dx = 0; dx < 8; dx++)
                    {
                        (lo, hi) = (Math.Min(lo, At(x + dx, z + dz)), Math.Max(hi, At(x + dx, z + dz)));
                    }
                }

                if (lo > 220)
                {
                    tiles++;
                    levelTiles += hi - lo < 6 ? 1 : 0;
                }
            }
        }

        var spread = high == 0 ? 0 : spreadSum / (double)high;
        Output.WriteLine($"seed {seed}: land above 150/200/250: {Above(150):0.0}% {Above(200):0.0}% {Above(250):0.0}%; {ranges.Count} ranges above 150, {big:0.0}% of the columns in ranges of 100 samples or more, the longest {longest} blocks across; {high} sampled columns above 220 vary by {spread:0.0} blocks within 64 blocks, {tiles} tiles of 64 x 64 blocks lie wholly above 220 and {levelTiles} of them are level within 6 blocks");

        Assert.True(Above(150) >= 4, $"{Above(150):0.0}% of the land is above 150"); // B02: 5%, round 2: 10 to 19% in broad massifs; chains are thinner
        Assert.True(Above(200) >= 2, $"{Above(200):0.0}% above 200"); // B02: 2%
        Assert.True(Above(250) >= 0.8, $"{Above(250):0.0}% above 250"); // B02: 0.3%
        Assert.True(big >= 80, $"{big:0.0}% of the high ground is in ranges of some size");
        Assert.True(longest >= 2500, $"the longest range spans {longest} blocks");
        Assert.InRange(land.Max(), 250, 310);
        Assert.True(tiles >= 100, $"{tiles} tiles above 220");
        Assert.True(spread >= 20, $"the high land varies by {spread:0.0} blocks within 64 blocks"); // a plateau would be near 0
        Assert.True(levelTiles * 100.0 / tiles <= 3, $"{levelTiles} of {tiles} tiles above 220 are level"); // round 1 had flat snow tablelands: a chain's rounded crest may hold a few level tiles, not a plateau
    }
}
