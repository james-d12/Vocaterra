using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The rest of TerrainTuningTests's tests (B28 split the slow ones into classes of their own, sharing TerrainTuningTestsBase).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainTuningTests(ITestOutputHelper output) : TerrainTuningTestsBase(output)
{
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
        var wideSeas = wide.Bodies(k => wide.Biome[k] == wide.Row("ocean"));
        var wideLargest = wideSeas[0].Count * 100.0 / wideSeas.Sum(b => b.Count); // the connection of the ocean may lie outside a 16 384 block square: look at 32 768
        var land = wide.Bodies(k => wide.Biome[k] != wide.Row("ocean"));
        var landTotal = land.Sum(b => b.Count);
        var big = land.Where(b => b.Count >= 400).Sum(b => b.Count) * 100.0 / landTotal;
        var sizes = land.Where(b => !b.AtEdge).Select(b => Math.Max(b.WidthBlocks, b.DepthBlocks)).Order().ToArray();
        Output.WriteLine($"seed {seed}: ocean {share:0.0}% of the columns, the largest sea {largest:0.0}% of it, lake-basin {lakes:0.00}%; in 32 768 blocks {land.Count} bodies of land, {big:0.0}% of the land in those of 400 samples or more, whole continents (not cut by the square) {string.Join(", ", sizes.Where(z => z >= 3000))} blocks across");

        Assert.InRange(share, 35, 46); // B02: 14%, round 1: 59 to 65%
        Assert.True(wideLargest >= 95, $"the largest sea is {wideLargest:0.0}% of the ocean in 32 768 blocks (and {largest:0.0}% in 16 384)"); // B02: 6%
        Assert.InRange(lakes, 0.5, 6); // round 2: 0.03 to 0.6%
        Assert.True(big >= 95, $"{big:0.0}% of the land is in continents");
        Assert.True(land.Take(5).All(b => Math.Max(b.WidthBlocks, b.DepthBlocks) >= 5000), "the five biggest bodies of land are continents 5 000 blocks across or more");
        Assert.True(land[0].WidthBlocks >= 8000 || land[0].DepthBlocks >= 8000, "the biggest is 8 000 blocks across or more");
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

        Output.WriteLine($"seed {seed}: the largest step {biggest}; {steep * 100.0 / pairs:0.000}% of {pairs} land pairs step 5 or more; {walls * 100.0 / tests:0.00}% of {tests} stretches rise 40 in 40 columns");
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
        var oceanRow = s.Row("ocean");
        var sea = new bool[n * n]; // the sea itself: a river or a lake inland is water, not shore
        for (var k = 0; k < sea.Length; k++)
        {
            sea[k] = s.Biome[k] == oceanRow;
        }

        var distance = new int[n * n];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        for (var k = 0; k < n * n; k++)
        {
            var (x, z) = (k % n, k / n);
            if ((x > 0 && sea[k - 1] != sea[k]) || (x < n - 1 && sea[k + 1] != sea[k]) || (z > 0 && sea[k - n] != sea[k]) || (z < n - 1 && sea[k + n] != sea[k]))
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

        double Mean(bool offshore, int fromBlocks, int toBlocks)
        {
            var cells = Enumerable.Range(0, n * n).Where(k => sea[k] == offshore && distance[k] * s.Step >= fromBlocks && distance[k] * s.Step < toBlocks).ToArray();
            return cells.Length == 0 ? double.NaN : cells.Average(k => (double)s.Ground[k]);
        }

        var (landNear, landFar, seaNear, seaMid, seaFar) = (Mean(false, 0, 128), Mean(false, 128, 512), Mean(true, 0, 64), Mean(true, 128, 384), Mean(true, 1024, int.MaxValue));
        var deep = Enumerable.Range(0, n * n).Where(k => s.Biome[k] == s.Row("ocean")).Count(k => s.Ground[k] <= 0) * 100.0 / Enumerable.Range(0, n * n).Count(k => s.Biome[k] == s.Row("ocean"));
        Output.WriteLine($"seed {seed}: mean ground on land within 128 blocks of the shore {landNear:0.0}, 128 to 512 {landFar:0.0}; at sea within 64 blocks {seaNear:0.0}, 128 to 384 {seaMid:0.0}, over 1024 {seaFar:0.0}; {deep:0.0}% of the sea is at y 0 or below");

        Assert.InRange(landNear, 32, 44); // B02: 37 to 61 within the first 128 blocks (it rose to 61)
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
        var green = coldLowland.Count(k => s.Biome[k] == taiga) * 100.0 / Math.Max(1, coldLowland.Length);
        Output.WriteLine($"seed {seed}: the lowest snow-covered ground {snowLow}; {coldLowland.Length} cold lowland samples, {green:0.0}% taiga, {coldLowland.Count(k => s.Snow[k])} of them under snow");

        Assert.True(snowLow >= 120, $"snow lies at {snowLow}"); // B02: 32, on the cold lowland by the sea
        Assert.True(coldLowland.Length > 300, $"{coldLowland.Length} cold lowland samples");
        Assert.True(green >= 85, $"{green:0.0}% of the cold lowland is taiga");
        Assert.DoesNotContain(coldLowland, k => s.Snow[k]);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRiversAreMapped_Then_TheirWaterReachesTheSeaOrALake(ulong seed)
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

        Output.WriteLine($"seed {seed}: {total} river-water columns; {reaching * 100.0 / Math.Max(1, total):0.0}% are in rivers that reach the sea or a lake");
        Assert.True(total > 1000, $"{total} river columns");
        Assert.True(reaching * 100.0 / total >= 85, $"{reaching * 100.0 / total:0.0}% reach water"); // B02: 65 to 73%; the rivers now run the whole way from the ranges, so a few end in a closed basin
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheRangesAreMeasured_Then_TheyAreChainsAFewHundredBlocksWideAndThousandsLongAndMassifsAreRare(ulong seed)
    {
        // Round 3 (D-game-34 item 24): chains, not massifs. The bodies of land above 150 (eight-connected, whole inside the square), each with its thickness (2 x area / perimeter: the width of a strip however it forks), its extent (the longer side of its box)
        // and the share of the area in chains: thinner than 500 blocks and at least 4 times as long as thick.
        var s = WorldSample.Of(seed);
        var n = s.Size;
        bool High(int k) => !s.Water[k] && s.Ground[k] > 150;
        var seen = new bool[n * n];
        var bodies = new List<(int Area, double Thickness, int Extent)>();
        var stack = new Stack<int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || !High(start))
            {
                continue;
            }

            seen[start] = true;
            stack.Push(start);
            int area = 0, edges = 0, minX = n, maxX = 0, minZ = n, maxZ = 0;
            var atEdge = false;
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                int x = k % n, z = k / n;
                area++;
                (minX, maxX, minZ, maxZ) = (Math.Min(minX, x), Math.Max(maxX, x), Math.Min(minZ, z), Math.Max(maxZ, z));
                atEdge |= x == 0 || z == 0 || x == n - 1 || z == n - 1;
                foreach (var m in new[] { x > 0 ? k - 1 : -1, x < n - 1 ? k + 1 : -1, z > 0 ? k - n : -1, z < n - 1 ? k + n : -1 })
                {
                    edges += m < 0 || !High(m) ? 1 : 0;
                }

                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if ((uint)nx < (uint)n && (uint)nz < (uint)n && !seen[(nz * n) + nx] && High((nz * n) + nx))
                        {
                            seen[(nz * n) + nx] = true;
                            stack.Push((nz * n) + nx);
                        }
                    }
                }
            }

            if (!atEdge && area >= 12)
            {
                bodies.Add((area, 2.0 * area * s.Step / Math.Max(1, edges), Math.Max(maxX - minX, maxZ - minZ) * s.Step));
            }
        }

        var total = bodies.Sum(b => (long)b.Area);
        var chains = bodies.Where(b => b.Thickness < 500 && b.Extent >= 4 * b.Thickness).Sum(b => (long)b.Area) * 100.0 / total;
        var massifs = bodies.Where(b => b.Thickness >= 600).Sum(b => (long)b.Area) * 100.0 / total;
        long acc = 0;
        var median = 0.0;
        foreach (var b in bodies.OrderBy(b => b.Thickness))
        {
            acc += b.Area;
            if (acc * 2 >= total)
            {
                median = b.Thickness;
                break;
            }
        }

        var longest = bodies.Max(b => b.Extent);
        Output.WriteLine($"seed {seed}: {bodies.Count} bodies of land above 150 inside the square; thickness (area-weighted median) {median:0} blocks; the longest spans {longest} blocks; {chains:0.0}% of the area in chains, {massifs:0.0}% in massifs");

        Assert.InRange(median, 100, 500); // round 2: broad massifs (900 to 2 800 wide)
        Assert.True(chains >= 85, $"{chains:0.0}% of the high land is in chains");
        Assert.True(massifs <= 10, $"{massifs:0.0}% of the high land is in massifs");
        Assert.True(longest >= 2000, $"the longest chain spans {longest} blocks");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_TheCoastIsFollowed_Then_ItWandersAndNoMoreThanAThirdAndAHalfOfItRunsStraightFor300Blocks(ulong seed)
    {
        // Round 3 (D-game-34 item 24): coasts that wander at every scale. From each coast column (land next to the sea), in 32 directions, a line is followed while a coast column lies within one column of it; both ways through the column is how far the coast runs straight
        // there, to within about 8 to 16 blocks either side. Round 2: 51 to 58% of the coast lay on a run of 300 blocks or more and the longest was 1 100 to 1 300.
        var s = WorldSample.Of(seed, 6144, 8);
        var n = s.Size;
        var ocean = s.Row("ocean");
        bool Sea(int k) => s.Biome[k] == ocean;
        var coast = new bool[n * n];
        var points = new List<int>();
        for (var z = 1; z < n - 1; z++)
        {
            for (var x = 1; x < n - 1; x++)
            {
                var k = (z * n) + x;
                if (!Sea(k) && !s.Water[k] && (Sea(k - 1) || Sea(k + 1) || Sea(k - n) || Sea(k + n)))
                {
                    coast[k] = true;
                    points.Add(k);
                }
            }
        }

        bool Near(double fx, double fz)
        {
            int cx = (int)Math.Round(fx), cz = (int)Math.Round(fz);
            for (var oz = -1; oz <= 1; oz++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    int px = cx + ox, pz = cz + oz;
                    if ((uint)px < (uint)n && (uint)pz < (uint)n && coast[(pz * n) + px])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        const int Directions = 32;
        var maxSteps = 1200 / s.Step;
        var run = new double[Directions];
        double longest = 0;
        var straight = 0;
        foreach (var k in points)
        {
            int x = k % n, z = k / n;
            for (var d = 0; d < Directions; d++)
            {
                double dx = Math.Cos(2 * Math.PI * d / Directions), dz = Math.Sin(2 * Math.PI * d / Directions);
                var steps = 0;
                while (steps < maxSteps && Near(x + (dx * (steps + 1)), z + (dz * (steps + 1))))
                {
                    steps++;
                }

                run[d] = steps;
            }

            var best = Enumerable.Range(0, Directions / 2).Max(d => run[d] + run[d + (Directions / 2)]);
            longest = Math.Max(longest, best);
            straight += best * s.Step >= 300 ? 1 : 0;
        }

        var share = straight * 100.0 / points.Count;
        Output.WriteLine($"seed {seed}: {points.Count} coast columns; the longest straight run {longest * s.Step:0} blocks; {share:0.0}% of the coast lies on a straight run of 300 blocks or more");

        Assert.True(points.Count > 3000, $"{points.Count} coast columns");
        Assert.True(share <= 50, $"{share:0.0}% of the coast runs straight for 300 blocks or more");
        Assert.True(longest * s.Step <= 1200, $"the coast runs straight for {longest * s.Step:0} blocks");
    }
}
