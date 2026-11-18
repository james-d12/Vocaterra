using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// A region of another size and place, built only in test data (B06), blends and validates with no code change: the shape the data-declared regions of B16 will need. The test region is the front-end tests'
/// small layout (96 x 64 x 96 blocks, ground at 40) placed at chunk (40, -30), with no exit corridor.
/// </summary>
public sealed class RegionOfAnotherSizeTests(ITestOutputHelper output)
{
    private static readonly GameData Data = RegionTestWorld.WithoutCorridors();

    private static readonly Lazy<WorldComposition> Composed = new(() => WorldFactory.Compose(Data, 42, new RegionCatalog([RegionTestWorld.Small()])));

    private static WorldComposition World => Composed.Value;

    [Fact]
    public void Given_TheSmallRegion_When_ItIsFrozen_Then_ItsBoundsAreItsLayoutsSizeAtItsPlace()
    {
        var region = RegionTestWorld.Small();

        Assert.Equal(new RegionBounds(40, 0, -30, 6, 4, 6), region.Bounds);
        Assert.Equal((640, 735, -480, -385, 0, 63), (region.Bounds.MinX, region.Bounds.MaxX, region.Bounds.MinZ, region.Bounds.MaxZ, region.Bounds.MinY, region.Bounds.MaxY));
        Assert.True(region.Covers(new VoxelChunkCoord(45, 3, -25)));
        Assert.False(region.Covers(new VoxelChunkCoord(46, 3, -25)));
        Assert.Equal(40, region.EdgeHeight(700, -400));
        Assert.Equal(region.EdgeHeight(735, -385), region.EdgeHeight(900, 100));
    }

    [Fact]
    public void Given_TheCompositeSourceOverTheSmallRegion_When_ChunksAreGenerated_Then_InsideIsTheRegionsAboveIsAirUnderIsStoneAndOutsideIsProcedural()
    {
        var region = RegionTestWorld.Small();
        var source = World.Source;

        Assert.Same(region.Copy(new VoxelChunkCoord(44, 2, -28)), source.Generate(new VoxelChunkCoord(44, 2, -28)));
        Assert.True(source.Generate(new VoxelChunkCoord(44, 4, -28)).IsEmpty);
        Assert.Equal(ChunkData.Volume, source.Generate(new VoxelChunkCoord(44, -1, -28)).NonAirCount);
        Assert.Equal(World.Procedural.Generate(new VoxelChunkCoord(46, 2, -28)).ComputeDigestHex(), source.Generate(new VoxelChunkCoord(46, 2, -28)).ComputeDigestHex());
        Assert.Equal(region.Id, World.Places.RegionAt(700, -400)!.Id);
        Assert.Null(World.Places.RegionAt(200, 200)); // the valley is not part of this world
    }

    [Fact]
    public void Given_TheSmallRegionsBorderAndMargin_When_TheGroundIsSampled_Then_NoStepPassesTheBoundAndNoSeaOpensWithinTheBiasRadius()
    {
        var b = RegionTestWorld.Small().Bounds;
        var blend = Data.WorldGen.Blend;
        var maxStep = 0;
        for (var d = 0; d <= blend.Margin + 8; d++)
        {
            for (var t = 0; t <= b.MaxX - b.MinX; t += 2)
            {
                foreach (var (x, z, nz) in new[] { (b.MinX + t, b.MinZ - d, -1), (b.MinX + t, b.MaxZ + d, 1) })
                {
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(World, x, z).Height - RegionTestWorld.Shape(World, x, z + nz).Height));
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(World, x, z).Height - RegionTestWorld.Shape(World, x + 1, z).Height));
                }
            }

            for (var t = 0; t <= b.MaxZ - b.MinZ; t += 2)
            {
                foreach (var (x, z, nx) in new[] { (b.MinX - d, b.MinZ + t, -1), (b.MaxX + d, b.MinZ + t, 1) })
                {
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(World, x, z).Height - RegionTestWorld.Shape(World, x + nx, z).Height));
                }
            }
        }

        var sea = 0;
        for (var z = b.MinZ - blend.LandBias.Radius; z <= b.MaxZ + blend.LandBias.Radius; z += 8)
        {
            for (var x = b.MinX - blend.LandBias.Radius; x <= b.MaxX + blend.LandBias.Radius; x += 8)
            {
                var shape = RegionTestWorld.Shape(World, x, z);
                sea += b.DistanceTo(x, z) is > 0 && b.DistanceTo(x, z) <= blend.LandBias.Radius && shape.Water && shape.RiverWeight == 0 && shape.LakeWeight == 0 ? 1 : 0;
            }
        }

        output.WriteLine($"the small region: the largest step in its margin is {maxStep}, sea columns within {blend.LandBias.Radius} blocks {sea}");
        Assert.True(maxStep <= 9, $"a step of {maxStep}");
        Assert.Equal(0, sea);
    }

    [Fact]
    public void Given_TheSmallRegionsSurroundings_When_TheirTrunksAreFound_Then_NoneIsWithinTheClearanceAndTheRegionsOwnChunksHoldNoCrownFromOutside()
    {
        var b = RegionTestWorld.Small().Bounds;
        var near = new List<string>();
        var inside = 0;
        for (var cz = b.MinChunkZ - 2; cz < b.MinChunkZ + b.ChunksZ + 2; cz++)
        {
            for (var cx = b.MinChunkX - 2; cx < b.MinChunkX + b.ChunksX + 2; cx++)
            {
                foreach (var (chunk, index) in World.Source.GenerateColumn(cx, cz).Select(static (c, i) => (c, i)))
                {
                    if (chunk.IsEmpty || b.CoversColumn(cx, cz))
                    {
                        continue;
                    }

                    for (var ly = 0; ly < 16; ly++)
                    {
                        for (var lz = 0; lz < 16; lz++)
                        {
                            for (var lx = 0; lx < 16; lx++)
                            {
                                var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                                if (BlockDefinition.IsTrunk(chunk[lx, ly, lz]) && b.DistanceTo(x, z) <= Data.WorldGen.Blend.TreeClearance)
                                {
                                    near.Add($"{x},{(index - 4) * 16 + ly},{z}");
                                }
                            }
                        }
                    }
                }
            }
        }

        for (var cy = 0; cy < 4; cy++)
        {
            foreach (var (cx, cz) in new[] { (40, -30), (45, -30), (40, -25), (45, -25), (43, -27) })
            {
                inside += World.Procedural.Generate(new VoxelChunkCoord(cx, cy, cz)).Blocks.ToArray().Count(static id => BlockDefinition.IsTrunk(id) || BlockDefinition.IsLeaves(id));
            }
        }

        Assert.Empty(near);
        Assert.Equal(0, inside);
    }

    [Fact]
    public void Given_TheValleyAndTheSmallRegionTogether_When_TheCatalogIsValidated_Then_ItIsSoundAndTheNearestRegionDecidesTheBlend()
    {
        var valley = RegionTestWorld.Valley();
        var small = RegionTestWorld.Small();
        var catalog = new RegionCatalog([valley, small]);
        var limits = WorldLimits.Symmetric(WorldLimits.DefaultHalfExtent, -64, 320);

        Assert.Empty(catalog.Validate(limits, Data.WorldGen.Blend.Margin));
        Assert.Equal(valley, catalog.Covering(new VoxelChunkCoord(3, 3, 3)));
        Assert.Equal(small, catalog.Covering(new VoxelChunkCoord(41, 1, -29)));
        Assert.Null(catalog.Covering(new VoxelChunkCoord(30, 1, -29)));
        Assert.Equal((valley, 17), catalog.Nearest(400, 100));
        Assert.Equal((small, 4), catalog.Nearest(636, -400));
        Assert.Equal(valley, catalog.RegionAt(10, 10));
        Assert.Equal(small, catalog.RegionAt(700, -400));
        Assert.Equal((null, 0), RegionCatalog.Empty.Nearest(5, 5));
        Assert.Equal(small, catalog.ColumnOwner(41, -29));

        var world = WorldFactory.Compose(Data, 42, catalog);
        Assert.Equal(valley.EdgeHeight(383, 100), world.Places.NaturalSurfaceAt(383, 100));
        Assert.InRange(RegionTestWorld.Shape(world, 639, -400).Height - small.EdgeHeight(639, -400), -1, 1); // the first column outside the small region: its edge's height
    }

    [Fact]
    public void Given_RegionsThatOverlapOrCrowdOrLeaveTheWorld_When_TheCatalogIsValidated_Then_EachProblemIsListed()
    {
        var limits = WorldLimits.Symmetric(WorldLimits.DefaultHalfExtent, -64, 320);
        var valley = RegionTestWorld.Valley();
        var scene = RouteScene.Clearing();
        var overlapping = AuthoredReliefRegion.Freeze("overlapping", scene, 20, 20, keepChunks: false);
        var crowding = AuthoredReliefRegion.Freeze("crowding", scene, 30, 0, keepChunks: false); // 96 blocks from the valley's edge: closer than two margins
        var outside = AuthoredReliefRegion.Freeze("outside", scene, 1900, 0, keepChunks: false);
        var twin = AuthoredReliefRegion.Freeze(valley.Id, scene, 100, 100, keepChunks: false);

        Assert.Contains(new RegionCatalog([valley, overlapping]).Validate(limits, 64), static p => p.Contains("overlap", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([valley, crowding]).Validate(limits, 64), static p => p.Contains("closer", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([outside]).Validate(limits, 64), static p => p.Contains("outside", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([valley, twin]).Validate(limits, 64), static p => p.Contains("called", StringComparison.Ordinal));
    }
}
