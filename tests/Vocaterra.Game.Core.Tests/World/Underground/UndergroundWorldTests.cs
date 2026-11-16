using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The underground as the pipeline builds it (B11): the rules that keep it clear of water and of the ground, the entrances, the lava, the floor, and that a chunk is the same alone, among its neighbours
/// and from any thread. The sampled areas are real places of the procedural world for the three seeds the water checks run over.
/// </summary>
public sealed class UndergroundWorldTests
{
    public static TheoryData<ulong, Feature> WaterPlaces => new()
    {
        { 42, Feature.River }, { 42, Feature.Lake }, { 42, Feature.Coast },
        { 7, Feature.River }, { 7, Feature.Coast },
        { 2026, Feature.River }, { 2026, Feature.Lake }, { 2026, Feature.Coast },
    };

    private static UndergroundData Data => TestGame.Data.Underground!;

    private static SampledArea Area(ulong seed, Feature feature, int span = 5)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var source = ProceduralTestWorld.Source(seed);
        return new SampledArea(source, source.GenerateColumn, cx - (span / 2), cz - (span / 2), span, span);
    }

    [Fact]
    public void Given_TheShippedPipeline_When_ItsPassesAreListed_Then_TheUndergroundRunsBetweenTheStoneAndTheTrees()
    {
        var names = ProceduralTestWorld.Source(42).Pipeline.PassNames;

        Assert.Equal(["gen/relief", "gen/banks", "gen/waterside", "gen/surface", "gen/water", "gen/stone", "gen/strata", "gen/floor", "gen/caves", "gen/lava", "gen/ores", "gen/trees", "gen/undergrowth"], names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Given_ASourceWithNoUnderground_When_ItsPassesAreListed_Then_TheGroundIsPlainStoneAsBefore()
    {
        var source = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, 42);

        Assert.Equal(["gen/relief", "gen/banks", "gen/waterside", "gen/surface", "gen/water", "gen/stone", "gen/trees", "gen/undergrowth"], source.Pipeline.PassNames);
        Assert.True(source.Generate(new VoxelChunkCoord(0, -4, 0)).Blocks.ToArray().All(static b => b == BlockId.Stone));
    }

    [Theory]
    [MemberData(nameof(WaterPlaces))]
    public void Given_APlaceWithWater_When_ItsUndergroundIsGenerated_Then_NoCaveCellIsWithinTheMarginOfWaterOrUnderABedWithinTheClearance(ulong seed, Feature feature)
    {
        var area = Area(seed, feature);
        var margin = Data.Caves.WaterMargin;
        var inner = (MinX: area.MinX + margin, MaxX: area.MaxX - margin, MinZ: area.MinZ + margin, MaxZ: area.MaxZ - margin);
        var water = new Dictionary<(int X, int Z), (int Low, int High)>();
        for (var z = area.MinZ; z < area.MaxZ; z++)
        {
            for (var x = area.MinX; x < area.MaxX; x++)
            {
                var range = area.Water(x, z);
                if (range.Low <= range.High)
                {
                    water[(x, z)] = range;
                }
            }
        }

        Assert.NotEmpty(water);
        var (cells, near, under) = (0, 0, 0);
        for (var z = inner.MinZ; z < inner.MaxZ; z++)
        {
            for (var x = inner.MinX; x < inner.MaxX; x++)
            {
                for (var y = -64; y <= area.Height(x, z); y++)
                {
                    if (!area.IsCave(x, y, z))
                    {
                        continue;
                    }

                    cells++;
                    if (water.TryGetValue((x, z), out var own) && y > own.Low - 1 - Data.Caves.BedClearance)
                    {
                        under++;
                    }

                    for (var dz = -margin; dz <= margin; dz++)
                    {
                        for (var dx = -margin; dx <= margin; dx++)
                        {
                            if (water.TryGetValue((x + dx, z + dz), out var range) && y >= range.Low - margin && y <= range.High + margin)
                            {
                                near++;
                            }
                        }
                    }
                }
            }
        }

        Assert.True(cells > 200, $"{seed} {feature}: the sample holds {cells} cave cells");
        Assert.Equal(0, near);
        Assert.Equal(0, under);
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheCavesOfAPlace_When_TheAirIsCounted_Then_ThereAreCavesDeepDownAndNoneIsLostToTheSurface(ulong seed)
    {
        var area = Area(seed, Feature.Plain);
        long deepCells = 0;
        long deepAir = 0;
        for (var z = area.MinZ; z < area.MaxZ; z++)
        {
            for (var x = area.MinX; x < area.MaxX; x++)
            {
                var height = area.Height(x, z);
                for (var y = -60; y < -16; y++)
                {
                    deepCells++;
                    deepAir += area.Block(x, y, z) is BlockId.Air or BlockId.Lava ? 1 : 0;
                }

                // Under the cover, the ground is whole except at an entrance: the layers just under the top are never air.
                if (area.Block(x, height, z) != BlockId.Air)
                {
                    for (var y = height - Data.Caves.SurfaceCover + 1; y <= height; y++)
                    {
                        Assert.NotEqual(BlockId.Air, area.Block(x, y, z));
                    }
                }
            }
        }

        Assert.InRange(deepAir * 100.0 / deepCells, 1.0, 20.0);
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheEntrancesOfAPlace_When_TheyAreFound_Then_EachIsInAZoneOnDryLandWithACaveUnderIt(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var underground = source.World.Underground!;
        var area = Area(seed, Feature.Plain, 9);
        var entrances = new List<(int X, int Z)>();
        for (var z = area.MinZ + 4; z < area.MaxZ - 4; z++)
        {
            for (var x = area.MinX + 4; x < area.MaxX - 4; x++)
            {
                var height = area.Height(x, z);
                if (area.Block(x, height, z) == BlockId.Air)
                {
                    entrances.Add((x, z));
                }
            }
        }

        Assert.NotEmpty(entrances);
        foreach (var (x, z) in entrances)
        {
            var height = area.Height(x, z);
            Assert.True(underground.Entrances.InZone(x, z), $"{x},{z} is open but not in a zone");
            Assert.True(height >= source.World.SeaLevel + Data.Entrances.MinHeightAboveSea, $"{x},{z} is open at height {height}");
            for (var dz = -Data.Entrances.WaterReach; dz <= Data.Entrances.WaterReach; dz++)
            {
                for (var dx = -Data.Entrances.WaterReach; dx <= Data.Entrances.WaterReach; dx++)
                {
                    Assert.True(area.Height(x + dx, z + dz) >= source.World.SeaLevel, $"{x},{z} has water within reach");
                }
            }

            // The shaft is open through the cover from the ground down, and meets cave air within the probe depth under the cover.
            for (var y = height - Data.Caves.SurfaceCover + 1; y <= height; y++)
            {
                Assert.Equal(BlockId.Air, area.Block(x, y, z));
            }

            var meets = Enumerable.Range(height - Data.Caves.SurfaceCover - Data.Entrances.ProbeDepth, Data.Entrances.ProbeDepth + 1).Any(y => area.Block(x, y, z) == BlockId.Air);
            Assert.True(meets, $"{x},{z}: no cave air under the shaft (ground {height})");
        }
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheLava_When_ADeepAreaIsRead_Then_ItFillsCaveAirAtOrUnderItsLevelAndNowhereElse(ulong seed)
    {
        var area = Area(seed, Feature.Plain, 7);
        var level = Data.Lava.Level;
        var (lava, airUnder) = (0, 0);
        for (var z = area.MinZ; z < area.MaxZ; z++)
        {
            for (var x = area.MinX; x < area.MaxX; x++)
            {
                for (var y = -64; y <= area.Height(x, z); y++)
                {
                    var block = area.Block(x, y, z);
                    if (block == BlockId.Lava)
                    {
                        lava++;
                        Assert.True(y <= level, $"lava at y {y}");
                    }

                    if (block == BlockId.Air && y <= level)
                    {
                        airUnder++;
                    }
                }
            }
        }

        Assert.True(lava > 100, $"{lava} lava blocks");
        Assert.Equal(0, airUnder);
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(2026UL)]
    public void Given_TheFloor_When_ColumnsAreRead_Then_EveryColumnHasBedrockFromTheBottomAndNoneAboveItsThickness(ulong seed)
    {
        var area = Area(seed, Feature.Plain, 3);
        var floor = new UndergroundWorld(Data, seed).Floor;
        var thicknesses = new HashSet<int>();
        for (var z = area.MinZ; z < area.MaxZ; z++)
        {
            for (var x = area.MinX; x < area.MaxX; x++)
            {
                var thickness = floor.ThicknessAt(x, z);
                thicknesses.Add(thickness);
                for (var y = -64; y <= -64 + 8; y++)
                {
                    Assert.Equal(y < -64 + thickness, area.Block(x, y, z) == BlockId.Bedrock);
                }
            }
        }

        Assert.Equal([1, 2, 3, 4], thicknesses.Order());
    }

    [Fact]
    public void Given_ManyColumns_When_TheFloorIsMeasured_Then_ItIsOneToFourBlocksAndRagged()
    {
        var floor = new UndergroundWorld(Data, 42).Floor;
        var counts = new int[5];
        for (var z = -50; z < 50; z++)
        {
            for (var x = -50; x < 50; x++)
            {
                var t = floor.ThicknessAt(x, z);
                Assert.InRange(t, 1, 4);
                Assert.Equal(-64 + t - 1, floor.TopAt(x, z));
                counts[t]++;
            }
        }

        Assert.All(counts.Skip(1), static c => Assert.InRange(c, 1800, 3200)); // 2 500 each in a fair draw
        Assert.Equal(-61, floor.HighestPossibleY);
    }

    [Theory]
    [InlineData(42UL, Feature.Forest)]
    [InlineData(7UL, Feature.Mountain)]
    [InlineData(2026UL, Feature.Plain)]
    public void Given_ACaveRichPlace_When_ItsChunksAreGeneratedInAnyOrderAndOnManyThreads_Then_EveryChunkEqualsTheSameChunkAlone(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var coords = new List<VoxelChunkCoord>();
        for (var z = cz - 1; z <= cz + 1; z++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                for (var y = -4; y <= (feature == Feature.Mountain ? 12 : 3); y++)
                {
                    coords.Add(new VoxelChunkCoord(x, y, z));
                }
            }
        }

        var rng = new Vocaterra.Game.Core.Mathematics.WorldRng(seed);
        var shuffled = new List<VoxelChunkCoord>(coords);
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var among = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data);
        var amongDigests = shuffled.ToDictionary(static c => c, c => among.Generate(c).ComputeDigestHex());
        var threaded = coords.AsParallel().WithDegreeOfParallelism(8).Select(c => (c, ProceduralTestWorld.Source(seed).Generate(c).ComputeDigestHex())).ToDictionary(static p => p.c, static p => p.Item2);
        foreach (var coord in coords)
        {
            var alone = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).Generate(coord).ComputeDigestHex();
            Assert.Equal(alone, amongDigests[coord]);
            Assert.Equal(alone, threaded[coord]);
        }
    }

    [Fact]
    public void Given_ASourceWithAWiderWindow_When_TheUndergroundIsGenerated_Then_ItIsUnchangedSoTheMarginHoldsTheWaterRule()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Coast);
        var normal = ProceduralTestWorld.Source(42);
        var wide = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, 42, null, 5, Data);

        Assert.True(normal.Margin >= Data.Caves.WaterMargin + 1);
        for (var z = cz - 1; z <= cz + 1; z++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                for (var y = -4; y <= 3; y++)
                {
                    var coord = new VoxelChunkCoord(x, y, z);
                    Assert.Equal(normal.Generate(coord).ComputeDigestHex(), wide.Generate(coord).ComputeDigestHex());
                }
            }
        }
    }

    [Fact]
    public void Given_AColumn_When_ItIsGeneratedWholeAndChunkByChunk_Then_TheUndergroundLayersEqualTheSingleChunk()
    {
        var (cx, cz) = ProceduralTestWorld.Find(2026, Feature.Plain);
        var source = ProceduralTestWorld.Source(2026);

        var column = source.GenerateColumn(cx, cz);

        for (var i = 0; i < 8; i++)
        {
            var coord = new VoxelChunkCoord(cx, source.MinChunkY + i, cz);
            Assert.Equal(source.Generate(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
            Assert.Equal(source.GenerateWithoutShortcuts(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
        }
    }

    [Fact]
    public void Given_TheRocks_When_AColumnOfChunksIsRead_Then_SlateLiesBelowStoneAndTheFloorIsBedrock()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Plain);
        var column = ProceduralTestWorld.Source(42).GenerateColumn(cx, cz);
        var below = column[0].Blocks.ToArray().Concat(column[1].Blocks.ToArray()).Concat(column[2].Blocks.ToArray()).Where(static b => b is not (BlockId.Air or BlockId.Lava)).ToList();

        Assert.Contains(BlockId.Bedrock, below);
        Assert.Contains(BlockId.Slate, below);
        Assert.DoesNotContain(BlockId.Stone, below);
        Assert.DoesNotContain(BlockId.Water, below);
    }
}
