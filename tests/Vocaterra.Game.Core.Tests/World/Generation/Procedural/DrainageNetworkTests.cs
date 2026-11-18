using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The rest of DrainageNetworkTests's tests (B28 split the slow ones into classes of their own, sharing DrainageNetworkTestsBase).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class DrainageNetworkTests(ITestOutputHelper output) : DrainageNetworkTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASeed_When_ItsReachesAreRead_Then_TheWaterNeverRisesAlongOneOrAcrossTwo(ulong seed)
    {
        var model = new HeightModel(World, seed);

        var reaches = model.Reaches(-Box, -Box, Box, Box);

        Output.WriteLine($"seed {seed}: {reaches.Count} reaches in {2 * Box} x {2 * Box} blocks");
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

        Assert.True(watch.ElapsedMilliseconds < 100 * Vocaterra.Game.Core.Tests.TestSupport.CpuTime.Factor, $"a dry column took {watch.ElapsedMilliseconds} ms");
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

    [Fact]
    public void Given_AWarmModel_When_IsWarmAndADryScopeAreAskedEveryFrame_Then_NothingIsAllocated()
    {
        // The game loop asks IsWarm and takes a dry scope for every column it looks at, every frame: the look-up of the nodes round a column built an array of offsets per call (160 bytes), and that was 64 000 bytes in the residency test.
        var model = new HeightModel(World, 7);
        var climate = new ClimateSampler(World, 7);
        model.Warm(3200, 7600);
        _ = model.Shape(3200, 7600, climate.At(3200, 7600));
        var climateAt = climate.At(3300, 7650);
        _ = model.Shape(3300, 7650, climateAt); // everything the loop below reads is built
        var warm = false;
        var before = 0L;
        for (var round = 0; round < 2; round++) // the first round makes what the second reads (a thread's static, a tile of nodes); the second is the steady state of a frame
        {
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                warm = model.IsWarm(3200, 7600);
                using (DrainageNetwork.WithoutReading())
                {
                    warm &= model.IsWarm(3200 + (i & 7), 7600);
                }
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(warm);
        Assert.Equal(0, allocated);
    }
}
