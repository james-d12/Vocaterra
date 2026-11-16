using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The owner's map reviews of B02 carried out (B02b and its redo, D-game-34 items 16 and 20), as numbers over a 16 384 x 16 384 block square of each shipped seed, one sample every 32 blocks (the rivers one every 8, the pools one
/// every 4 over 8 192 blocks): the sea is 35 to 45% of the map, one connected ocean between continents thousands of blocks across, and inland water is a lake; the ranges are higher and more common than before, run in chains and
/// have peaks and saddles, not plateaus; the shore is a clean gentle shelf, the sea floor deepens offshore and no pool or speckle lies on the beach or the lowland; the snow lies only high and cold lowland stays taiga; the ground has
/// no cliffs; and the rivers' valleys run down from the ranges to water that reaches the sea or a lake. The bounds hold the shape of the tuned world with room for the data to be tuned again, never its exact numbers.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningTests(ITestOutputHelper output)
{
    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheSeasAreMapped_Then_TheyAreAboutFortyPercentOneOceanBetweenContinentsOfThousandsOfBlocks(ulong seed)
    {
        var s = WorldSample.Of(seed);
        var (ocean, lake) = (s.Row("ocean"), s.Row("lake-basin"));
        var seas = s.Bodies(k => s.Biome[k] == ocean);
        var total = seas.Sum(b => b.Count);
        var share = total * 100.0 / s.Ground.Length;
        var largest = seas[0].Count * 100.0 / total;
        var lakes = s.Biome.Count(b => b == lake) * 100.0 / s.Ground.Length;

        var wide = WorldSample.Of(seed, 16384, 64);
        var land = wide.Bodies(k => !wide.Water[k]);
        var landTotal = land.Sum(b => b.Count);
        var big = land.Where(b => b.Count >= 400).Sum(b => b.Count) * 100.0 / landTotal;
        var sizes = land.Where(b => !b.AtEdge).Select(b => Math.Max(b.WidthBlocks, b.DepthBlocks)).Order().ToArray();
        output.WriteLine($"seed {seed}: ocean {share:0.0}% of the columns, the largest sea {largest:0.0}% of it, lake-basin {lakes:0.00}%; in 32 768 blocks {land.Count} bodies of land, {big:0.0}% of the land in those of 400 samples or more, whole continents (not cut by the square) {string.Join(", ", sizes.Where(z => z >= 3000))} blocks across");

        Assert.InRange(share, 35, 46); // B02: 14%, round 1: 59 to 65%
        Assert.True(largest >= 90, $"the largest sea is {largest:0.0}% of the ocean"); // B02: 6%
        Assert.InRange(lakes, 0.02, 3);
        Assert.True(big >= 95, $"{big:0.0}% of the land is in continents");
        Assert.True(land.Take(5).All(b => Math.Max(b.WidthBlocks, b.DepthBlocks) >= 5000), "the five biggest bodies of land are continents 5 000 blocks across or more");
        Assert.True(land[0].WidthBlocks >= 8000 || land[0].DepthBlocks >= 8000, "the biggest is 8 000 blocks across or more");
    }

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
        output.WriteLine($"seed {seed}: land above 150/200/250: {Above(150):0.0}% {Above(200):0.0}% {Above(250):0.0}%; {ranges.Count} ranges above 150, {big:0.0}% of the columns in ranges of 100 samples or more, the longest {longest} blocks across; {high} sampled columns above 220 vary by {spread:0.0} blocks within 64 blocks, {tiles} tiles of 64 x 64 blocks lie wholly above 220 and {levelTiles} of them are level within 6 blocks");

        Assert.True(Above(150) >= 8, $"{Above(150):0.0}% of the land is above 150"); // B02: 5%
        Assert.True(Above(200) >= 4, $"{Above(200):0.0}% above 200"); // B02: 2%
        Assert.True(Above(250) >= 1.5, $"{Above(250):0.0}% above 250"); // B02: 0.3%
        Assert.True(big >= 80, $"{big:0.0}% of the high ground is in ranges of some size");
        Assert.True(longest >= 2500, $"the longest range spans {longest} blocks");
        Assert.InRange(land.Max(), 250, 310);
        Assert.True(tiles >= 100, $"{tiles} tiles above 220");
        Assert.True(spread >= 20, $"the high land varies by {spread:0.0} blocks within 64 blocks"); // a plateau would be near 0
        Assert.Equal(0, levelTiles); // round 1 had flat snow tablelands: no 64 x 64 block area above 220 is level within 6 blocks
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

        Assert.InRange(landNear, 32, 44); // B02: 37 to 61 within the first 128 blocks (it rose to 61)
        Assert.InRange(seaNear, 24, 32); // the floor is under the sea level but barely
        Assert.True(seaMid < seaNear - 3, $"the floor deepens: {seaNear:0.0} at the shore, {seaMid:0.0} 128 to 384 blocks out");
        Assert.True(seaFar <= 0, $"far offshore the floor is at y {seaFar:0.0}"); // B02: no floor under y 10
        Assert.True(deep >= 20, $"{deep:0.0}% of the sea is at y 0 or below");
    }

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
                isBeach[i] = table.Classify(c, shape.Height, x, z).Row.Index == beach;
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
        var pools = s.Bodies(k => s.Water[k], eight: true).Where(b => !b.AtEdge).ToArray();
        var (puddles, ponds) = (pools.Count(b => b.Count <= 3), pools.Count(b => b.Count <= 8));
        var wetShores = waterChanges.Count(c => c > 2) * 100.0 / Math.Max(1, crossings);
        var speckled = beachChanges.Count(c => c > 4) * 100.0 / Math.Max(1, crossings);
        output.WriteLine($"seed {seed}: {crossings} shores crossed; land/water changes at one shore: most {worstWater}, {wetShores:0.0}% of shores more than 2; beach/not-beach changes within 300 blocks of a shore: most {worstBeach}, mean {beachFlips / (double)Math.Max(1, crossings):0.0}, {speckled:0.0}% of shores more than 4; in 8 192 blocks {pools.Length} bodies of water, {puddles} of 3 samples (48 columns) or fewer, {ponds} of 8 or fewer");

        Assert.True(crossings >= 5, $"{crossings} shores crossed");
        Assert.True(wetShores <= 10, $"{wetShores:0.0}% of the shores change between land and water more than twice"); // one shore, one change; a bay or a river's mouth may add
        Assert.True(speckled <= 10, $"{speckled:0.0}% of the shores flicker between beach and not beach more than 4 times"); // the strip begins and ends
        Assert.Equal(0, puddles); // round 1 left many small ponds and flats on the lowland: no body of water of a few columns
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
        var green = coldLowland.Count(k => s.Biome[k] == taiga) * 100.0 / Math.Max(1, coldLowland.Length);
        output.WriteLine($"seed {seed}: the lowest snow-covered ground {snowLow}; {coldLowland.Length} cold lowland samples, {green:0.0}% taiga, {coldLowland.Count(k => s.Snow[k])} of them under snow");

        Assert.True(snowLow >= 120, $"snow lies at {snowLow}"); // B02: 32, on the cold lowland by the sea
        Assert.True(coldLowland.Length > 300, $"{coldLowland.Length} cold lowland samples");
        Assert.True(green >= 85, $"{green:0.0}% of the cold lowland is taiga");
        Assert.DoesNotContain(coldLowland, k => s.Snow[k]);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRiversAreMapped_Then_TheirValleysRunDownFromTheRangesAndTheirWaterReachesTheSeaOrALake(ulong seed)
    {
        var s = WorldSample.Of(seed, 8192, 8);
        bool River(int k) => s.Water[k] && s.Carve[k] > 63; // under water and carved by a river (a lake or the sea is water that is not)
        var (labels, touching) = s.Label(River, k => s.Water[k] && !River(k), 2);
        var edge = new bool[touching.Length];
        var n = s.Size;
        for (var k = 0; k < labels.Length; k++)
        {
            if (labels[k] != 0 && (k % n == 0 || k / n == 0 || k % n == n - 1 || k / n == n - 1))
            {
                edge[labels[k]] = true; // a body that runs off the square has an end the square does not show
            }
        }

        var (total, reaching) = (0, 0);
        for (var k = 0; k < labels.Length; k++)
        {
            if (labels[k] != 0 && !edge[labels[k]])
            {
                total++;
                reaching += touching[labels[k]] ? 1 : 0;
            }
        }

        // The valleys: the columns a river carved at all, in bodies of 100 or more, and how many of them stand in a valley that climbs to ground of 150 or more.
        var fine = WorldSample.Of(seed, 4096, 8);
        var (valleys, valleyTouching) = fine.Label(k => fine.Carve[k] > 100, k => fine.Water[k], 1);
        var (top, count) = (new int[valleyTouching.Length], new int[valleyTouching.Length]);
        for (var k = 0; k < valleys.Length; k++)
        {
            if (valleys[k] != 0)
            {
                top[valleys[k]] = Math.Max(top[valleys[k]], fine.Natural[k]);
                count[valleys[k]]++;
            }
        }

        var (columns, fromHigh) = (0, 0);
        for (var b = 1; b < count.Length; b++)
        {
            if (count[b] >= 100)
            {
                columns += count[b];
                fromHigh += top[b] >= 150 ? count[b] : 0;
            }
        }

        output.WriteLine($"seed {seed}: {total} river-water columns; {reaching * 100.0 / Math.Max(1, total):0.0}% are in rivers that reach the sea or a lake; {columns} valley columns, {fromHigh * 100.0 / Math.Max(1, columns):0.0}% of them in valleys that climb to ground of 150 or more");
        Assert.True(total > 1000, $"{total} river columns");
        Assert.True(reaching * 100.0 / total >= 93, $"{reaching * 100.0 / total:0.0}% reach water"); // B02: 65 to 73%
        Assert.True(columns > 5000, $"{columns} valley columns");
        Assert.True(fromHigh * 100.0 / columns >= 40, $"{fromHigh * 100.0 / columns:0.0}% of the valleys climb to high ground"); // the rivers begin in the ranges
    }
}
