using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The drainage network (B02c, D-game-34 item 33): rivers that only fall, from sources in the hills to the sea or a lake, as a pure function of the seed. The rule the water follows is stated in each test: along every reach and along every chain of
/// reaches the water's level is never higher downstream than upstream (<c>ToLevel &lt;= FromLevel</c>); a river that is not the sea's ends in the sea or in a lake; a lake is flat; and the network is the same whichever column asked for it first.
/// </summary>
public sealed class DrainageNetworkTests(ITestOutputHelper output)
{
    private const int Box = 4096;

    private static WorldGenData World => TestGame.Data.WorldGen;

    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_ItsReachesAreRead_Then_TheWaterNeverRisesAlongOneOrAcrossTwo(ulong seed)
    {
        var model = new HeightModel(World, seed);

        var reaches = model.Reaches(-Box, -Box, Box, Box);

        output.WriteLine($"seed {seed}: {reaches.Count} reaches in {2 * Box} x {2 * Box} blocks");
        Assert.True(reaches.Count > 100, $"{reaches.Count} reaches");
        Assert.All(reaches, r => Assert.True(r.ToLevel <= r.FromLevel, $"the water rises from {r.FromLevel} to {r.ToLevel} between ({r.FromX}, {r.FromZ}) and ({r.ToX}, {r.ToZ})"));
        var from = reaches.ToDictionary(static r => (r.FromX, r.FromZ));
        var joined = 0;
        foreach (var r in reaches)
        {
            if (from.TryGetValue((r.ToX, r.ToZ), out var next))
            {
                joined++;
                Assert.Equal(r.ToLevel, next.FromLevel); // one node, one level, whichever reach reads it
            }
        }

        Assert.True(joined > reaches.Count / 2, "most reaches run on into another");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ARiver_When_ItIsTracedFromItsSource_Then_ItFallsToTheSeaOrALakeAndNeverRises(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var reaches = model.Reaches(-Box, -Box, Box, Box);
        var ends = 0;
        var traced = 0;
        foreach (var reach in reaches.Where(static r => !r.IntoSea && !r.IntoLake).OrderByDescending(static r => r.Area).Take(60))
        {
            var path = model.TraceRiver(reach.FromX, reach.FromZ, 4000);
            Assert.True(path.Count >= 2);
            traced++;
            for (var i = 1; i < path.Count; i++)
            {
                Assert.True(path[i].Level <= path[i - 1].Level, $"the river through ({reach.FromX}, {reach.FromZ}) rises at ({path[i].X}, {path[i].Z})");
                Assert.True(path[i].Area >= path[i - 1].Area || path[i].Lake || path[i].Sea || path[i - 1].Lake, "a river grows downstream");
            }

            if (path[^1].Sea || path[^1].Lake)
            {
                ends++;
            }
        }

        Assert.True(traced >= 20);
        Assert.True(ends >= traced * 98 / 100, $"{ends} of {traced} rivers end in the sea or a lake");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheWholeWorldOfASeed_When_TheRiverWaterIsFollowedToItsEnd_Then_AtLeast98PercentOfItReachesTheSeaOrALake(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var reaches = model.Reaches(-8192, -8192, 8192, 8192);
        var from = reaches.ToDictionary(static r => (r.FromX, r.FromZ));
        double total = 0, reaching = 0, stranded = 0;
        foreach (var reach in reaches)
        {
            var end = reach;
            var steps = 0;
            var outcome = 0; // 1 reaches water, 2 leaves the area, 3 stranded
            while (steps++ < 4000)
            {
                if (end.IntoSea || end.IntoLake)
                {
                    outcome = 1;
                    break;
                }

                if (!from.TryGetValue((end.ToX, end.ToZ), out end))
                {
                    break;
                }
            }

            if (outcome == 0)
            {
                // The chain left the area (the box's edge) or ended on nothing: it counts as stranded only when it ended away from the edge.
                var last = reach;
                var count = 0;
                while (from.TryGetValue((last.ToX, last.ToZ), out var next) && count++ < 4000)
                {
                    last = next;
                }

                var nearEdge = Math.Abs(last.ToX) > 8192 - 160 || Math.Abs(last.ToZ) > 8192 - 160;
                if (nearEdge)
                {
                    continue;
                }

                outcome = 3;
            }

            var length = Math.Sqrt(Math.Pow(reach.ToX - reach.FromX, 2) + Math.Pow(reach.ToZ - reach.FromZ, 2));
            total += length;
            reaching += outcome == 1 ? length : 0;
            stranded += outcome == 3 ? length : 0;
        }

        output.WriteLine($"seed {seed}: {reaches.Count} reaches, {100 * reaching / total:0.00}% of the river reaches the sea or a lake, {100 * stranded / total:0.00}% is stranded");
        Assert.True(reaching >= total * 0.98, $"{100 * reaching / total:0.00}% reaches water");
    }

    [Fact]
    public void Given_TwoModelsOfOneSeed_When_TheirNetworksAreFirstAskedForInDifferentOrders_Then_TheReachesAreTheSame()
    {
        var first = new HeightModel(World, 42);
        var second = new HeightModel(World, 42);
        var climate = new ClimateSampler(World, 42);
        foreach (var (x, z) in new[] { (6000, 5000), (-7000, 2000), (300, -6500), (2500, 2500) }) // other parts of the world first, in an order the box does not follow
        {
            second.Shape(x, z, climate.At(x, z));
        }

        var a = first.Reaches(-2048, -2048, 2048, 2048);
        var b = second.Reaches(-2048, -2048, 2048, 2048);

        Assert.True(a.Count > 20);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Given_ARiverThatCrossesAnAreaBorder_When_ItsReachesAreRead_Then_ItContinuesAcrossIt()
    {
        var model = new HeightModel(World, 7);
        var all = model.Reaches(-Box, -Box, Box, Box);
        var left = model.Reaches(-Box, -Box, 0, Box);
        var right = model.Reaches(1, -Box, Box, Box);
        var across = all.Where(static r => (r.FromX <= 0) != (r.ToX <= 0)).ToList();

        Assert.True(across.Count > 3, $"{across.Count} reaches cross x = 0");
        Assert.True(left.Count + right.Count >= all.Count - across.Count - 8, "the two halves hold the reaches the whole does");
        var from = all.ToDictionary(static r => (r.FromX, r.FromZ));
        foreach (var r in across.Where(static r => !r.IntoSea && !r.IntoLake))
        {
            Assert.True(from.ContainsKey((r.ToX, r.ToZ)) || Math.Abs(r.ToX) > Box - 100 || Math.Abs(r.ToZ) > Box - 100, $"the river stops at ({r.ToX}, {r.ToZ}) after crossing the border");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheColumnsAlongARiver_When_TheirWaterIsRead_Then_ItStandsAtOneLevelAcrossAndNeverRisesDownstream(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var reaches = model.Reaches(-Box, -Box, Box, Box).Where(static r => !r.IntoSea && !r.IntoLake).OrderByDescending(static r => r.Area).Take(8).ToList();
        var rivers = 0;
        foreach (var reach in reaches)
        {
            var path = model.TraceRiver(reach.FromX, reach.FromZ, 4000);
            var levels = new List<int>();
            foreach (var point in path.Where(static p => !p.Sea && !p.Lake))
            {
                var shape = model.Shape(point.X, point.Z, climate.At(point.X, point.Z));
                if (shape.RiverWeight >= 2000 && shape.Water && shape.LakeWeight == 0)
                {
                    levels.Add(shape.WaterLevel);
                }
            }

            if (levels.Count < 5)
            {
                continue;
            }

            rivers++;
            for (var i = 1; i < levels.Count; i++)
            {
                Assert.True(levels[i] <= levels[i - 1], $"the water of the river through ({reach.FromX}, {reach.FromZ}) rises from {levels[i - 1]} to {levels[i]}");
            }
        }

        Assert.True(rivers >= 3, $"{rivers} rivers with water along their line");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ThePlaneOfASeed_When_ItsLakesAreCounted_Then_TheyAreFlatAndTakeAShareOfTheColumns(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        const int Side = 16_384;
        const int Step = 32;
        const int N = Side / Step;
        var lake = new bool[N * N];
        var level = new int[N * N];
        long water = 0;
        long lakeColumns = 0;
        for (var j = 0; j < N; j++)
        {
            for (var i = 0; i < N; i++)
            {
                var (x, z) = (-8192 + (i * Step), -8192 + (j * Step));
                var shape = model.Shape(x, z, climate.At(x, z));
                if (shape.Water)
                {
                    water++;
                }

                if (shape.Water && shape.LakeWeight > shape.RiverWeight && shape.LakeWeight >= 2048)
                {
                    lake[(j * N) + i] = true;
                    level[(j * N) + i] = shape.WaterLevel;
                    lakeColumns++;
                }
            }
        }

        // A lake is flat: of every two neighbouring lake columns (north-south or east-west, 32 blocks apart) all but the few that belong to two lakes a sill narrower than 32 blocks apart (one spills into the next, a step lower) are at the same level.
        long pairs = 0;
        long equal = 0;
        for (var j = 0; j < N; j++)
        {
            for (var i = 0; i < N; i++)
            {
                var k = (j * N) + i;
                if (!lake[k])
                {
                    continue;
                }

                foreach (var other in new[] { i + 1 < N ? k + 1 : -1, j + 1 < N ? k + N : -1 })
                {
                    if (other >= 0 && lake[other])
                    {
                        pairs++;
                        equal += level[k] == level[other] ? 1 : 0;
                    }
                }
            }
        }

        var bodies = lake.Count(static l => l);
        Assert.True(pairs > 100, $"{pairs} neighbouring lake columns");
        Assert.True(equal >= pairs * 94 / 100, $"{equal} of {pairs} neighbouring lake columns are at one level");
        var share = 100.0 * lakeColumns / (N * N);
        output.WriteLine($"seed {seed}: {share:0.00}% of the columns are lake, {equal} of {pairs} neighbouring pairs level");
        Assert.InRange(share, 1.0, 4.0);
        Assert.True(bodies >= 200, $"{bodies} lake columns");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheRiversOfASeed_When_TheirNumberAndReachAreCounted_Then_TheyAreCommonAndNoneBeginsOnACrest(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var reaches = model.Reaches(-8192, -8192, 8192, 8192);
        var hasUpstream = new HashSet<(int, int)>(reaches.Select(static r => (r.ToX, r.ToZ)));
        var sources = reaches.Where(r => !hasUpstream.Contains((r.FromX, r.FromZ))).ToList();

        output.WriteLine($"seed {seed}: {reaches.Count} reaches, {sources.Count} sources");
        Assert.InRange(sources.Count, 100, 900);
        // A source is never on a crest: the ranges' peaks add less than the height at which the river is gone, where it begins.
        foreach (var source in sources)
        {
            var c = climate.At(source.FromX, source.FromZ);
            Assert.True(c.Ridges < 3900 || source.FromLevel < World.Height.Rivers.FadeToHeight, $"a river begins on a crest at ({source.FromX}, {source.FromZ})");
            Assert.True(source.FromLevel < World.Height.Rivers.FadeToHeight, $"a river begins at {source.FromLevel}, above the height they fade out");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_RowsOfColumns_When_TheCarveOfARiverOrALakeIsReadAlongThem_Then_NeighbouringColumnsDifferByLittleSoNoRiverEndsInACliff(ulong seed)
    {
        // A river's carve is looked for among the nodes round the column: if the look were too short a wide river would be cut off at the edge of it, with a wall in the ground. The weight (0 to 4096) of a carve changes by at most a few hundred from column to column.
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var biggest = 0;
        var where = string.Empty;
        for (var z = -7000; z <= 7000; z += 2000)
        {
            var previous = default(ColumnShape);
            for (var i = 0; i <= 16384; i++)
            {
                var x = -8192 + i;
                var shape = model.Shape(x, z, climate.At(x, z));
                if (i > 0)
                {
                    var change = Math.Max(Math.Abs(shape.RiverWeight - previous.RiverWeight), Math.Abs(shape.LakeWeight - previous.LakeWeight));
                    if (change > biggest)
                    {
                        (biggest, where) = (change, $"({x}, {z})");
                    }
                }

                previous = shape;
            }
        }

        output.WriteLine($"seed {seed}: the carve changes by at most {biggest} between two columns, at {where}");
        Assert.True(biggest <= 1300, $"the carve jumps by {biggest} at {where}");
    }

    [Fact]
    public void Given_AFreshModel_When_AColumnOfAnUnreadLandmassIsAskedForDry_Then_NothingIsReadUntilItIsWarmedAndTheWaterThenAppears()
    {
        // The game loop must never wait for a landmass (up to a second): it asks dry, a pool thread warms the landmass, and the next ask has the water.
        var model = new HeightModel(World, 7);
        var climate = new ClimateSampler(World, 7);
        var reach = new HeightModel(World, 7).Reaches(3000, 7400, 3500, 7900).OrderByDescending(static r => r.Area).First(); // a river of seed 7's continent near (3 200, 7 600)
        var (x, z) = (reach.FromX, reach.FromZ);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ColumnShape dry;
        using (DrainageNetwork.WithoutReading())
        {
            dry = model.Shape(x, z, climate.At(x, z));
        }

        Assert.True(watch.ElapsedMilliseconds < 100, $"a dry column took {watch.ElapsedMilliseconds} ms");
        Assert.Equal(0, dry.RiverWeight + dry.LakeWeight);
        Assert.False(model.IsWarm(x, z), "a dry look-up read nothing");
        model.Warm(x, z);
        Assert.True(model.IsWarm(x, z));
        var wet = default(ColumnShape);
        for (var dz = -16; dz <= 16; dz += 2)
        {
            for (var dx = -16; dx <= 16; dx += 2)
            {
                var shape = model.Shape(x + dx, z + dz, climate.At(x + dx, z + dz));
                wet = shape.RiverWeight > wet.RiverWeight ? shape : wet;
            }
        }

        Assert.True(wet.RiverWeight > 0, "the river is there once the landmass is read");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheCaveEntrancesOfASeed_When_TheirColumnsAreCheckedAgainstTheRiversAndLakes_Then_NoPassageCutsIntoACarvedColumn(ulong seed)
    {
        // B11b's entrances keep clear of water; with B02c's rivers and lakes (carved down to a bed of their own, in the foothills as well as the lowland) no passage may run in a river's or a lake's bed or banks either.
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var sites = ProceduralTestWorld.Source(seed).World.Entrances!;
        var checkedNodes = 0;
        for (var sz = -10; sz < 10; sz++)
        {
            for (var sx = -10; sx < 10; sx++)
            {
                if (sites.Site(sx, sz) is not { } ramp)
                {
                    continue;
                }

                for (var i = 0; i <= ramp.Last; i++)
                {
                    var node = ramp.Nodes[i];
                    var shape = model.Shape(node.X, node.Z, climate.At(node.X, node.Z));
                    checkedNodes++;
                    Assert.True(!shape.Water && shape.RiverWeight == 0 && shape.LakeWeight == 0, $"a passage of seed {seed} runs through ({node.X}, {node.Z}), a column of {(shape.Water ? "water" : "a river's or lake's bank")}");
                }
            }
        }

        output.WriteLine($"seed {seed}: {checkedNodes} passage nodes, none in a carved column");
        Assert.True(checkedNodes > 50, $"{checkedNodes} nodes");
    }

    [Fact]
    public void Given_AFreshWorld_When_AColumnOfARiverIsGeneratedThroughTheSource_Then_ItIsWarmAndTheQueryReturnsTheWater()
    {
        // The game loop asks the query dry until a streaming worker has read the landmass by generating a chunk round the column; nothing here starts a thread.
        var data = TestGame.Data;
        var found = new HeightModel(World, 7);
        var climate = new ClimateSampler(World, 7);
        var reach = found.Reaches(3000, 7400, 3500, 7900).OrderByDescending(static r => r.Area).First();
        var (x, z, weight) = (0, 0, 0);
        for (var dz = -16; dz <= 16; dz += 2)
        {
            for (var dx = -16; dx <= 16; dx += 2)
            {
                var shape = found.Shape(reach.FromX + dx, reach.FromZ + dz, climate.At(reach.FromX + dx, reach.FromZ + dz));
                if (shape.RiverWeight > weight && shape.Water)
                {
                    (x, z, weight) = (reach.FromX + dx, reach.FromZ + dz, shape.RiverWeight);
                }
            }
        }

        Assert.True(weight > 3000, "a river column of seed 7");
        var source = new ProceduralChunkSource(data.WorldGen, data.Biomes, 7, null, data.Underground);
        var query = new WorldQuery(source.World, RegionCatalog.Empty);

        Assert.False(source.World.Ground.IsWarm(x, z));
        Assert.NotEqual("river-channel", query.BiomeAt(x, z).Id); // dry: the ground without the river
        Assert.False(source.World.Ground.IsWarm(x, z), "the dry answer read nothing");

        _ = source.GenerateColumn(x >> 4, z >> 4);

        Assert.True(source.World.Ground.IsWarm(x, z));
        Assert.Equal("river-channel", query.BiomeAt(x, z).Id);
    }
}
