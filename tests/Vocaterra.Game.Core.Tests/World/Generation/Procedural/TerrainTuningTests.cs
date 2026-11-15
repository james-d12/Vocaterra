using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The owner's map review of B02 carried out (B02b, D-game-34 item 16), as numbers over a 16 384 x 16 384 block square of each shipped seed, one sample every 32 blocks (the rivers over 8 192 blocks, one sample every 8): the
/// seas are big and connected and inland water is a lake; the ranges are higher and more common than before and run in chains; the shore is a gentle shelf and the sea floor deepens offshore; the snow lies only high and
/// cold lowland stays taiga; the ground has no cliffs; and the rivers run to a lake or the sea. The bounds hold the shape of the tuned world with room for the data to be tuned again, never its exact numbers.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningTests(ITestOutputHelper output)
{
    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheSeasAreMapped_Then_TheyAreBigAndOneOceanAndInlandWaterIsALake(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var (ocean, lake) = (s.Row("ocean"), s.Row("lake-basin"));
        var seas = s.Bodies(k => s.Biome[k] == ocean);
        var total = seas.Sum(b => b.Count);
        var share = total * 100.0 / s.Ground.Length;
        var largest = seas[0].Count * 100.0 / total;
        var small = seas.Where(b => b.Count * 20 < total).Sum(b => b.Count) * 100.0 / total;
        var lakes = s.Biome.Count(b => b == lake) * 100.0 / s.Ground.Length;
        output.WriteLine($"seed {seed}: ocean {share:0.0}% of the columns in {seas.Count} bodies, the largest {largest:0.0}% of them, bodies under a twentieth {small:0.0}%; lake-basin {lakes:0.00}%");

        Assert.InRange(share, 50, 75); // B02 had 14%
        Assert.True(largest >= 60, $"the largest sea is {largest:0.0}% of the ocean"); // B02: 6%
        Assert.True(small <= 12, $"{small:0.0}% of the ocean lies in small bodies");
        Assert.InRange(lakes, 0.3, 3);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRangesAreMapped_Then_TheLandAbove150IsSeveralTimesB02sAndRunsInBigChainsNotSpots(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var land = s.Ground.Where((_, k) => !s.Water[k]).ToArray();
        double Above(int y) => land.Count(h => h > y) * 100.0 / land.Length;
        var ranges = s.Bodies(k => !s.Water[k] && s.Ground[k] > 150, eight: true);
        var inRange = ranges.Sum(b => b.Count);
        var big = ranges.Where(b => b.Count >= 100).Sum(b => b.Count) * 100.0 / inRange;
        var longest = ranges.Max(b => Math.Max(b.WidthBlocks, b.DepthBlocks));
        output.WriteLine($"seed {seed}: land above 150/200/250: {Above(150):0.0}% {Above(200):0.0}% {Above(250):0.0}%; {ranges.Count} ranges above 150, {big:0.0}% of the columns in ranges of 100 samples or more, the longest {longest} blocks across");

        Assert.True(Above(150) >= 10, $"{Above(150):0.0}% of the land is above 150"); // B02: 5%
        Assert.True(Above(200) >= 4, $"{Above(200):0.0}% above 200"); // B02: 2%
        Assert.True(Above(250) >= 1, $"{Above(250):0.0}% above 250"); // B02: 0.3%
        Assert.True(big >= 80, $"{big:0.0}% of the high ground is in ranges of some size");
        Assert.True(longest >= 2500, $"the longest range spans {longest} blocks");
        Assert.InRange(land.Max(), 250, 310);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheGroundIsWalkedAlongRows_Then_NoStepPassesTheBoundAndSteepWallsAreRare(ulong seed)
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var model = new HeightModel(data.WorldGen, seed);
        var (pairs, steep, walls, tests, biggest) = (0, 0, 0, 0, 0);
        for (var z = -7000; z <= 7000; z += 1400)
        {
            var ground = new int[16385];
            var water = new bool[16385];
            for (var i = 0; i <= 16384; i++)
            {
                var shape = model.Shape(-8192 + i, z, climate.At(-8192 + i, z));
                (ground[i], water[i]) = (shape.Height, shape.Water);
            }

            for (var i = 1; i <= 16384; i++)
            {
                if (water[i] || water[i - 1])
                {
                    continue;
                }

                pairs++;
                var step = Math.Abs(ground[i] - ground[i - 1]);
                steep += step >= 5 ? 1 : 0;
                biggest = Math.Max(biggest, step);
            }

            for (var i = 40; i <= 16384; i += 8)
            {
                if (!water[i] && !water[i - 40])
                {
                    tests++;
                    walls += Math.Abs(ground[i] - ground[i - 40]) >= 40 ? 1 : 0; // a rise of 40 blocks in 40 columns: a wall, a plateau's rim
                }
            }
        }

        output.WriteLine($"seed {seed}: the largest step {biggest}; {steep * 100.0 / pairs:0.000}% of {pairs} land pairs step 5 or more; {walls * 100.0 / tests:0.00}% of {tests} stretches rise 40 in 40 columns");
        Assert.True(biggest <= 9, $"a step of {biggest}"); // B01's bound
        Assert.True(steep * 100.0 / pairs < 0.05, $"{steep} of {pairs} pairs step 5 or more"); // B02: 0.07%
        Assert.True(walls * 100.0 / tests < 1.5, $"{walls} of {tests} stretches are walls"); // B02: 5%
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheCoastIsProfiled_Then_TheLandFallsToTheSeaOverTensOfBlocksAndTheSeaFloorDeepensOffshore(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var n = s.Size;
        var distance = new int[n * n];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        for (var k = 0; k < n * n; k++)
        {
            var (x, z) = (k % n, k / n);
            if ((x > 0 && s.Water[k - 1] != s.Water[k]) || (x < n - 1 && s.Water[k + 1] != s.Water[k]) || (z > 0 && s.Water[k - n] != s.Water[k]) || (z < n - 1 && s.Water[k + n] != s.Water[k]))
            {
                distance[k] = 0;
                queue.Enqueue(k);
            }
        }

        while (queue.Count > 0)
        {
            var k = queue.Dequeue();
            foreach (var m in new[] { k % n > 0 ? k - 1 : -1, k % n < n - 1 ? k + 1 : -1, k / n > 0 ? k - n : -1, k / n < n - 1 ? k + n : -1 })
            {
                if (m >= 0 && distance[m] == int.MaxValue)
                {
                    distance[m] = distance[k] + 1;
                    queue.Enqueue(m);
                }
            }
        }

        double Mean(bool sea, int fromBlocks, int toBlocks)
        {
            var cells = Enumerable.Range(0, n * n).Where(k => s.Water[k] == sea && distance[k] * s.Step >= fromBlocks && distance[k] * s.Step < toBlocks).ToArray();
            return cells.Length == 0 ? double.NaN : cells.Average(k => (double)s.Ground[k]);
        }

        var (landNear, landFar, seaNear, seaMid, seaFar) = (Mean(false, 0, 128), Mean(false, 128, 512), Mean(true, 0, 64), Mean(true, 128, 384), Mean(true, 1024, int.MaxValue));
        var deep = Enumerable.Range(0, n * n).Where(k => s.Water[k] && s.Biome[k] == s.Row("ocean")).Count(k => s.Ground[k] <= 0) * 100.0 / Enumerable.Range(0, n * n).Count(k => s.Biome[k] == s.Row("ocean"));
        output.WriteLine($"seed {seed}: mean ground on land within 128 blocks of the shore {landNear:0.0}, 128 to 512 {landFar:0.0}; at sea within 64 blocks {seaNear:0.0}, 128 to 384 {seaMid:0.0}, over 1024 {seaFar:0.0}; {deep:0.0}% of the sea is at y 0 or below");

        Assert.InRange(landNear, 32, 46); // B02: 37 to 61 within the first 128 blocks (it rose to 61)
        Assert.InRange(seaNear, 24, 32); // the floor is under the sea level but barely
        Assert.True(seaMid < seaNear - 3, $"the floor deepens: {seaNear:0.0} at the shore, {seaMid:0.0} 128 to 384 blocks out");
        Assert.True(seaFar <= 0, $"far offshore the floor is at y {seaFar:0.0}"); // B02: no floor under y 10
        Assert.True(deep >= 20, $"{deep:0.0}% of the sea is at y 0 or below");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheSnowIsMapped_Then_ItLiesOnlyHighAndColdLowlandIsGreenTaiga(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var (taiga, ocean, lake, river, beach) = (s.Row("taiga"), s.Row("ocean"), s.Row("lake-basin"), s.Row("river-channel"), s.Row("beach"));
        var snowLow = Enumerable.Range(0, s.Ground.Length).Where(k => s.Snow[k]).Select(k => (int)s.Ground[k]).DefaultIfEmpty(int.MaxValue).Min();
        var coldLowland = Enumerable.Range(0, s.Ground.Length)
            .Where(k => !s.Water[k] && s.Ground[k] < 90 && s.Temperature[k] <= -300 && s.Biome[k] != beach && s.Biome[k] != river && s.Biome[k] != lake && s.Biome[k] != ocean)
            .ToArray();
        var green = coldLowland.Count(k => s.Biome[k] == taiga) * 100.0 / coldLowland.Length;
        output.WriteLine($"seed {seed}: the lowest snow-covered ground {snowLow}; {coldLowland.Length} cold lowland samples, {green:0.0}% taiga, {coldLowland.Count(k => s.Snow[k])} of them under snow");

        Assert.True(snowLow >= 120, $"snow lies at {snowLow}"); // B02: 32, on the cold lowland by the sea
        Assert.True(coldLowland.Length > 500, $"{coldLowland.Length} cold lowland samples");
        Assert.True(green >= 85, $"{green:0.0}% of the cold lowland is taiga");
        Assert.DoesNotContain(coldLowland, k => s.Snow[k]);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRiversAreMapped_Then_NearlyAllOfThemRunToTheSeaOrALake(ulong seed)
    {
        var s = WorldSample.Of(seed, 4096, 8);
        var (ocean, lake, river) = (s.Row("ocean"), s.Row("lake-basin"), s.Row("river-channel"));
        var (labels, touching) = s.Label(k => s.Biome[k] == river, k => s.Biome[k] == ocean || s.Biome[k] == lake, 2);
        var (total, reaching) = (0, 0);
        for (var k = 0; k < labels.Length; k++)
        {
            if (labels[k] != 0)
            {
                total++;
                reaching += touching[labels[k]] ? 1 : 0;
            }
        }

        output.WriteLine($"seed {seed}: {total} river columns in {touching.Length - 1} bodies; {reaching * 100.0 / total:0.0}% of the columns are in bodies that reach the sea or a lake");
        Assert.True(total > 2000, $"{total} river columns");
        Assert.True(reaching * 100.0 / total >= 80, $"{reaching * 100.0 / total:0.0}% reach water"); // B02: 65 to 73%
    }
}
