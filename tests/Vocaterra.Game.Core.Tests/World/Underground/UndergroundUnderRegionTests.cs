using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The underground round and under the valley (B11): below its chunks (under y 0) the composite source serves the procedural underground, with no cave reaching up into the valley's bottom layer
/// or near its water, and no entrance inside the region or its blend margin, where the land is the region's own or eased to its edge.
/// </summary>
public sealed class UndergroundUnderRegionTests
{
    private static readonly UndergroundData Data = TestGame.Data.Underground!;

    private static Vocaterra.Game.Core.World.WorldComposition Valley => RegionTestWorld.Composition();

    [Fact]
    public void Given_TheColumnsUnderTheValley_When_TheirCavesAreRead_Then_NoCaveReachesAboveTheCeilingUnderARegion()
    {
        var bounds = RegionTestWorld.Bounds;
        var air = 0;
        foreach (var (cx, cz) in new[] { (0, 0), (3, 17), (12, 12), (23, 23), (22, 13), (6, 9), (18, 2) })
        {
            var plan = Valley.Procedural.ShapedColumn(cx, cz).CavePlan;
            Assert.All(plan.Ceiling, static c => Assert.True(c <= Data.Caves.UnderRegionCeiling, $"ceiling {c}"));
            Assert.All(plan.ShaftFrom, static s => Assert.Equal(int.MaxValue, s));
            foreach (var chunk in Valley.Source.GenerateColumn(cx, cz).Take(4))
            {
                for (var i = 0; i < ChunkData.Volume; i++)
                {
                    var y = (chunk.Coord.Y << 4) + (i >> 8);
                    if (chunk.Blocks[i] is BlockId.Air or BlockId.Lava)
                    {
                        air++;
                        Assert.True(y <= Data.Caves.UnderRegionCeiling, $"cave air at y {y} under the valley (chunk column {cx},{cz})");
                    }
                }
            }

            Assert.True(bounds.CoversColumn(cx, cz));
        }

        Assert.True(air > 1000, $"{air} cave blocks under the valley");
    }

    [Fact]
    public void Given_TheValleysChunksAndWhatLiesUnderThem_When_TheyAreRead_Then_TheLayerJustUnderTheValleyIsWholeAndFarFromItsWater()
    {
        var region = RegionTestWorld.Valley();
        var lowestWater = int.MaxValue;
        for (var cz = 0; cz < 24; cz += 3)
        {
            for (var cx = 0; cx < 24; cx += 3)
            {
                for (var cy = 0; cy < 8; cy++)
                {
                    var chunk = region.Copy(new VoxelChunkCoord(cx, cy, cz));
                    for (var i = 0; i < ChunkData.Volume && !chunk.IsEmpty; i++)
                    {
                        if (chunk.Blocks[i] == BlockId.Water)
                        {
                            lowestWater = Math.Min(lowestWater, (cy << 4) + (i >> 8));
                        }
                    }
                }

                var top = Valley.Source.Generate(new VoxelChunkCoord(cx, -1, cz));
                for (var i = 0; i < ChunkData.Volume; i++)
                {
                    if (i >> 8 > 7)
                    {
                        Assert.NotEqual(BlockId.Air, top.Blocks[i]); // y -8 to -1: no cave touches the valley's bottom layer
                    }
                }
            }
        }

        Assert.True(lowestWater < int.MaxValue, "the valley has water");
        Assert.True(lowestWater - Data.Caves.UnderRegionCeiling > Data.Caves.BedClearance, $"the valley's water lies as low as y {lowestWater}");
        Assert.True(lowestWater - Data.Caves.UnderRegionCeiling > Data.Caves.WaterMargin);
    }

    [Fact]
    public void Given_TheLandRoundTheValley_When_TheEntranceZonesAreRead_Then_NoneOpensInTheRegionOrItsMarginAndSomeOpenBeyond()
    {
        var margin = TestGame.Data.WorldGen.Blend.Margin;
        var regions = Valley.Procedural.World.Regions!.Regions;
        var (opened, closedInMargin) = (0, 0);
        for (var cz = -10; cz < 34; cz++)
        {
            for (var cx = -10; cx < 34; cx++)
            {
                var ctx = Valley.Procedural.ShapedColumn(cx, cz);
                var plan = ctx.CavePlan;
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                        var distance = regions.Nearest(x, z).Distance;
                        var open = plan.ShaftFrom[(lz * 16) + lx] != int.MaxValue;
                        if (open)
                        {
                            opened++;
                            Assert.True(distance >= margin, $"an entrance at {x},{z}, {distance} blocks from the valley");
                        }
                        else if (distance < margin && Valley.Procedural.World.Underground!.Entrances.InZone(x, z))
                        {
                            closedInMargin++;
                        }
                    }
                }
            }
        }

        Assert.True(opened > 20, $"{opened} entrances round the valley");
        Assert.True(closedInMargin > 20, $"{closedInMargin} zone columns in the margin stay closed");
    }

    [Fact]
    public void Given_ACompositeColumnUnderTheValley_When_ItIsGeneratedWholeAndOneChunkAtATime_Then_TheLayersAgree()
    {
        foreach (var (cx, cz) in new[] { (5, 5), (22, 9) })
        {
            var column = Valley.Source.GenerateColumn(cx, cz);
            for (var i = 0; i < 4; i++)
            {
                var coord = new VoxelChunkCoord(cx, -4 + i, cz);
                Assert.Equal(Valley.Source.Generate(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
            }
        }
    }

    [Fact]
    public void Given_ATownOnProceduralGround_When_ItsColumnsAreRead_Then_TheTownIsItsOwnAndTheProceduralUndergroundLiesBelowItsLayersLikeUnderTheValley()
    {
        var town = SampleTown.Town();
        var world = Vocaterra.Game.Core.World.WorldFactory.Compose(SampleTown.Data, SampleTown.Seed, new Vocaterra.Game.Core.World.Regions.RegionCatalog([RegionTestWorld.Valley(), town]));
        var b = town.Bounds;
        var (cx, cz) = (b.MinChunkX + 2, b.MinChunkZ + 3);

        // The town's chunks are the town's own.
        Assert.Same(town.Copy(new VoxelChunkCoord(cx, b.MinChunkY, cz)), world.Source.Generate(new VoxelChunkCoord(cx, b.MinChunkY, cz)));
        var plan = world.Procedural.ShapedColumn(cx, cz).CavePlan;
        Assert.All(plan.Ceiling, static c => Assert.True(c <= Data.Caves.UnderRegionCeiling, $"ceiling {c}"));
        Assert.All(plan.ShaftFrom, static s => Assert.Equal(int.MaxValue, s));

        // Below its layers the procedural underground: slate, bedrock and caves, none above the ceiling, and equal to the procedural source's own chunk.
        var seen = new HashSet<BlockId>();
        for (var cy = world.Source.MinChunkY; cy < Math.Min(0, b.MinChunkY); cy++)
        {
            var chunk = world.Source.Generate(new VoxelChunkCoord(cx, cy, cz));
            Assert.Equal(world.Procedural.Generate(new VoxelChunkCoord(cx, cy, cz)).ComputeDigestHex(), chunk.ComputeDigestHex());
            seen.UnionWith(chunk.Blocks.ToArray());
        }

        Assert.Contains(BlockId.Slate, seen);
        Assert.Contains(BlockId.Bedrock, seen);
        Assert.DoesNotContain(BlockId.Stone, seen);

        // No entrance in the town's footprint or its margin.
        var margin = TestGame.Data.WorldGen.Blend.Margin;
        var regions = world.Procedural.World.Regions!.Regions;
        for (var z = b.MinChunkZ - 5; z <= b.MinChunkZ + b.ChunksZ + 4; z++)
        {
            for (var x = b.MinChunkX - 5; x <= b.MinChunkX + b.ChunksX + 4; x++)
            {
                var shafts = world.Procedural.ShapedColumn(x, z).CavePlan.ShaftFrom;
                for (var i = 0; i < 256; i++)
                {
                    if (shafts[i] != int.MaxValue)
                    {
                        Assert.True(regions.Nearest((x << 4) + (i & 15), (z << 4) + (i >> 4)).Distance >= margin, "an entrance in the town's margin");
                    }
                }
            }
        }
    }
}
