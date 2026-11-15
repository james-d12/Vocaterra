using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The valley as a region (B06): its box from its layout, its chunks frozen, its edge profile, its trunks inside the edge and its template digest.</summary>
public sealed class AuthoredReliefRegionTests
{
    [Fact]
    public void Given_TheShippedValley_When_ItIsFrozen_Then_ItsBoxIsTheLayoutsSizeInChunks()
    {
        var layout = RouteScene.Layout;
        var region = RegionTestWorld.Valley();

        Assert.Equal(new RegionBounds(0, 0, 0, layout.SizeX / 16, layout.SizeY / 16, layout.SizeZ / 16), region.Bounds);
        Assert.Equal((24, 8, 24), (region.Bounds.ChunksX, region.Bounds.ChunksY, region.Bounds.ChunksZ));
        Assert.Equal((0, 383, 0, 383, 0, 127), (region.Bounds.MinX, region.Bounds.MaxX, region.Bounds.MinZ, region.Bounds.MaxZ, region.Bounds.MinY, region.Bounds.MaxY));
        Assert.Equal(Vocaterra.Game.Core.World.WorldFactory.ValleyRegionId, region.Id);
    }

    [Fact]
    public void Given_TheFrozenValley_When_EveryChunkIsCopied_Then_ItEqualsTheBuiltGridBlockForBlock()
    {
        var built = RouteScene.Build().Grid;
        var region = RegionTestWorld.Valley();
        var checkedChunks = 0;
        foreach (var coord in Coords(region.Bounds))
        {
            var copy = region.Copy(coord);
            Assert.True(region.Covers(coord));
            Assert.Equal(coord, copy.Coord);
            var expected = built.TryGetChunk(coord, out var chunk) ? chunk.ComputeDigestHex() : AirHex;
            Assert.Equal(expected, copy.ComputeDigestHex());
            checkedChunks++;
        }

        Assert.Equal(24 * 8 * 24, checkedChunks);
    }

    [Fact]
    public void Given_AFrozenRegion_When_TheSceneItCameFromIsEditedAfterwards_Then_TheRegionsChunksAreUntouchedAndEveryReadGivesTheSameChunk()
    {
        var scene = RouteScene.Build();
        var region = AuthoredReliefRegion.Freeze("copy", scene);
        var coord = new VoxelChunkCoord(10, 3, 10);
        var before = region.Copy(coord).ComputeDigestHex();

        scene.Grid.SetBlock(165, 50, 165, scene.Grid.GetBlock(165, 50, 165) == BlockId.Planks ? BlockId.Stone : BlockId.Planks);
        scene.Grid.SetBlock(160, 49, 160, BlockId.Planks);

        Assert.Equal(before, region.Copy(coord).ComputeDigestHex());
        Assert.Same(region.Copy(coord), region.Copy(coord)); // frozen: the same immutable chunk every time, nothing copied on the way out
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Copy(new VoxelChunkCoord(24, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Copy(new VoxelChunkCoord(0, 8, 0)));
    }

    [Fact]
    public void Given_TheEdgeOfTheValley_When_ItsHeightIsAskedFromInsideAndOutside_Then_ItIsTheGroundOfTheNearestEdgeColumn()
    {
        var region = RegionTestWorld.Valley();
        var grid = RouteScene.Build().Grid;

        // Inside: the column's own ground (a trunk or a crown does not count; the highest block that is not part of a tree is the ground, and the edge columns hold no trunk).
        foreach (var (x, z) in new[] { (0, 0), (0, 200), (383, 208), (200, 383), (100, 100), (383, 383) })
        {
            Assert.True(GroundOf(grid, x, z) == region.EdgeHeight(x, z), $"column {x},{z}: ground {GroundOf(grid, x, z)}, edge height {region.EdgeHeight(x, z)}, top block {grid.GetBlock(x, GroundOf(grid, x, z), z)}");
        }

        // Outside: the nearest column of the footprint.
        Assert.Equal(region.EdgeHeight(383, 208), region.EdgeHeight(400, 208));
        Assert.Equal(region.EdgeHeight(383, 208), region.EdgeHeight(383 + 5000, 208));
        Assert.Equal(region.EdgeHeight(0, 0), region.EdgeHeight(-30, -90));
        Assert.Equal(region.EdgeHeight(383, 383), region.EdgeHeight(500, 700));
        Assert.Equal(58, region.EdgeHeight(383, 208)); // the pass's road ends at the east edge at y 58 (B08a)
    }

    [Fact]
    public void Given_TheValleysEdgeColumns_When_TheirBlocksAreRead_Then_NoTrunkStandsWithinTwoBlocksOfTheBorderAndNoCrownCrossesIt()
    {
        var region = RegionTestWorld.Valley();
        var bounds = region.Bounds;
        var trunkNearBorder = new List<string>();
        foreach (var coord in Coords(bounds))
        {
            var onBorder = coord.X == bounds.MinChunkX || coord.X == bounds.MinChunkX + bounds.ChunksX - 1 || coord.Z == bounds.MinChunkZ || coord.Z == bounds.MinChunkZ + bounds.ChunksZ - 1;
            if (!onBorder)
            {
                continue;
            }

            var chunk = region.Copy(coord);
            if (chunk.IsEmpty)
            {
                continue;
            }

            for (var ly = 0; ly < 16; ly++)
            {
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var (x, z) = ((coord.X << 4) + lx, (coord.Z << 4) + lz);
                        var distance = Math.Min(Math.Min(x - bounds.MinX, bounds.MaxX - x), Math.Min(z - bounds.MinZ, bounds.MaxZ - z));
                        if (distance < 2 && BlockDefinition.IsTrunk(chunk[lx, ly, lz]))
                        {
                            trunkNearBorder.Add($"{x},{(coord.Y << 4) + ly},{z}");
                        }
                    }
                }
            }
        }

        Assert.Empty(trunkNearBorder);
    }

    [Fact]
    public void Given_TheTemplate_When_ItsDigestIsTaken_Then_ItIsThirtyTwoBytesStableAndMovedByAnEditOrAnotherPlace()
    {
        var region = RegionTestWorld.Valley();
        var again = AuthoredReliefRegion.Freeze("again", RouteScene.Build());
        var moved = AuthoredReliefRegion.Freeze("moved", RouteScene.Build(), 4, -2, keepChunks: false);
        var edited = RouteScene.Build();
        var stone = edited.Grid.GetBlock(100, 20, 100);
        edited.Grid.SetBlock(100, 20, 100, stone == BlockId.Planks ? BlockId.Stone : BlockId.Planks);

        Assert.Equal(32, region.TemplateDigest.Length);
        Assert.Equal(region.TemplateDigest, again.TemplateDigest);
        Assert.NotEqual(region.TemplateDigest, moved.TemplateDigest);
        Assert.NotEqual(region.TemplateDigest, AuthoredReliefRegion.Freeze("edited", edited, keepChunks: false).TemplateDigest);
        Assert.Equal(4, moved.Bounds.MinChunkX);
        Assert.Equal(-2, moved.Bounds.MinChunkZ);
    }

    [Fact]
    public void Given_ARegionMadeWithoutItsChunks_When_ItIsAsked_Then_ItKeepsItsEdgeDigestAndBlocksAndRefusesACopy()
    {
        var light = AuthoredReliefRegion.Freeze("light", RouteScene.Build(), keepChunks: false);

        Assert.False(light.HasChunks);
        Assert.Equal(RegionTestWorld.Valley().TemplateDigest, light.TemplateDigest);
        Assert.Equal(RegionTestWorld.Valley().EdgeHeight(383, 208), light.EdgeHeight(383, 208));
        Assert.NotEmpty(light.Blocks.Of(BlockId.FireRing));
        Assert.Throws<InvalidOperationException>(() => light.Copy(new VoxelChunkCoord(0, 0, 0)));
    }

    [Fact]
    public void Given_TheTemplateBlockIndex_When_AKindIsAsked_Then_ItsPositionsAreTheBuiltGridsInTheDenseOrderAndACommonKindIsRefused()
    {
        var scene = RouteScene.Build();
        var index = scene.Template!;

        Assert.Equal(scene.Grid.BlocksOfKind(BlockId.FireRing), index.Of(BlockId.FireRing));
        Assert.Equal(scene.Grid.BlocksOfKind(BlockId.Path), index.Of(BlockId.Path));
        Assert.Equal(scene.Grid.BlocksOfKind(BlockId.Bridge), index.Of(BlockId.Bridge));
        Assert.Throws<ArgumentException>(() => index.Of(BlockId.Stone));
        Assert.Throws<ArgumentException>(() => index.Of(BlockId.Air));
        Assert.All(RegionTestWorld.Valley().Blocks.Of(BlockId.FireRing), cell => Assert.Equal(BlockId.FireRing, scene.Grid.GetBlock(cell.X, cell.Y, cell.Z)));
        var shifted = AuthoredReliefRegion.Freeze("shifted", scene, 3, 5, keepChunks: false).Blocks.Of(BlockId.FireRing);
        Assert.Equal(index.Of(BlockId.FireRing).Select(static b => b with { X = b.X + 48, Z = b.Z + 80 }), shifted);
    }

    private static readonly string AirHex = Convert.ToHexString(VoxelChunk.AirChunkDigest).ToLowerInvariant();

    private static IEnumerable<VoxelChunkCoord> Coords(RegionBounds b)
    {
        for (var y = b.MinChunkY; y < b.MinChunkY + b.ChunksY; y++)
        {
            for (var z = b.MinChunkZ; z < b.MinChunkZ + b.ChunksZ; z++)
            {
                for (var x = b.MinChunkX; x < b.MinChunkX + b.ChunksX; x++)
                {
                    yield return new VoxelChunkCoord(x, y, z);
                }
            }
        }
    }

    // The ground of a column: the highest solid block that is not a part of a tree (undergrowth and water are not solid).
    private static int GroundOf(VoxelGrid grid, int x, int z)
    {
        for (var y = grid.SizeY - 1; y >= 0; y--)
        {
            var block = grid.GetBlock(x, y, z);
            if (BlockDefinition.IsSolid(block) && !BlockDefinition.IsTrunk(block) && !BlockDefinition.IsLeaves(block))
            {
                return y;
            }
        }

        return -1;
    }
}
