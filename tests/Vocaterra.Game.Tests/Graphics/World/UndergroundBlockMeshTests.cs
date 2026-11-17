using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B10: the nine underground blocks mesh (their ids are above 63, which the mesher's tables must cover), draw opaque art of their own, and lava is a light kind like the lantern.</summary>
public sealed class UndergroundBlockMeshTests
{
    private static readonly BlockId[] New = [BlockId.Slate, BlockId.Granite, BlockId.Limestone, BlockId.Gravel, BlockId.Lava, BlockId.Bedrock, BlockId.IronOre, BlockId.TinOre, BlockId.CopperOre, BlockId.Basalt];

    public static TheoryData<BlockId> Blocks() => [.. New];

    private static ChunkMeshes Build(MapBlocks blocks, IReadOnlyList<BlockPos>? lights = null) =>
        ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, lights, LightBand.Day));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ABlockAloneHasSixFacesAndBesideAirOrStoneItShowsTheFacesNotTouchingStone(BlockId id)
    {
        Assert.Equal(6, Build(new MapBlocks().Set(5, 5, 5, id)).Opaque.FaceCount);

        var beside = Build(new MapBlocks().Set(5, 5, 5, id).Set(6, 5, 5, BlockId.Stone));
        Assert.Equal(10, beside.Opaque.FaceCount); // five of each: the faces between them are hidden

        var buried = new MapBlocks().Fill(4, 4, 4, 6, 6, 6, BlockId.Stone).Set(5, 5, 5, id);
        Assert.Equal(6 * 9, Build(buried).Opaque.FaceCount); // the surface of the stone cube only
        Assert.True(ChunkMesher.IsOpaqueCube(id));
        Assert.Equal(BlockShape.Cube, ChunkMesher.ShapeOf(id));
        Assert.Equal(ChunkMesher.KindBit(id), Build(new MapBlocks().Set(5, 5, 5, id)).Kinds);
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void TheirFacesTakeTheirOwnPictureNotTheWhiteCell(BlockId id)
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var cell = BlockAtlasLayout.CellFor(id, face);
            Assert.NotEqual(BlockCell.White, cell);
            Assert.True(BlockArtNature.Draws(cell));
        }
    }

    [Fact]
    public void EachBlockHasADifferentOpaquePicture()
    {
        var seen = new HashSet<string>();
        foreach (var id in New)
        {
            var pixels = BlockArt.Pixels(BlockAtlasLayout.CellFor(id, BlockFace.East));
            for (var i = 3; i < pixels.Length; i += 4)
            {
                Assert.Equal(255, pixels[i]);
            }

            Assert.True(seen.Add(Convert.ToHexString(pixels)), $"{id} repeats another picture");
        }

        foreach (var other in new[] { BlockCell.Stone, BlockCell.Cobble, BlockCell.PathTop })
        {
            Assert.DoesNotContain(Convert.ToHexString(BlockArt.Pixels(other)), seen);
        }
    }

    [Fact]
    public void TheArtHasTheCharacterTheBriefSays()
    {
        double Luma(BlockId id)
        {
            var pixels = BlockArt.Pixels(BlockAtlasLayout.CellFor(id, BlockFace.East));
            double sum = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                sum += (0.299 * pixels[i]) + (0.587 * pixels[i + 1]) + (0.114 * pixels[i + 2]);
            }

            return sum / (pixels.Length / 4);
        }

        Assert.True(Luma(BlockId.Bedrock) < 60, "bedrock is near-black");
        Assert.True(Luma(BlockId.Slate) < Luma(BlockId.Stone), "slate is darker than stone");
        Assert.True(Luma(BlockId.Limestone) > Luma(BlockId.Stone), "limestone is paler than stone");
        Assert.True(Luma(BlockId.Lava) > 150, "lava is bright");

        var lava = BlockArt.Pixels(BlockCell.Lava);
        Assert.True(lava[0] > lava[2] + 100, "lava is orange"); // the first pixel's red far above its blue
        var grey = BlockArt.GreyPixels(BlockCell.Lava);
        Assert.Equal(grey[0], grey[1]);
        Assert.Equal(grey[1], grey[2]);
    }

    [Fact]
    public void LavaLightsItsNeighboursLikeALantern()
    {
        MapBlocks Scene(BlockId light) => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Stone).Set(8, 6, 8, light);
        var lights = new[] { new BlockPos(8, 6, 8) };
        var withLava = Build(Scene(BlockId.Lava), lights);
        var withLantern = Build(Scene(BlockId.Lantern), lights);

        Assert.Equal(0, Build(Scene(BlockId.Lava)).GlowMesh.FaceCount); // no light in the list, no glow
        Assert.True(withLava.GlowMesh.FaceCount > 0);

        // The floor around the light glows the same whichever of the two stands there: the glow reads the light's position, not its kind.
        static (float, float, float, uint)[] Floor(ChunkMeshes m) => [.. m.GlowMesh.Vertices.ToArray()
            .Where(static v => v.Y == 6f && (v.X <= 7f || v.X >= 10f || v.Z <= 7f || v.Z >= 10f))
            .Select(static v => (v.X, v.Y, v.Z, (uint)v.Color.GetHashCode()))
            .Order()];
        var lavaFloor = Floor(withLava);
        Assert.NotEmpty(lavaFloor);
        Assert.Equal(Floor(withLantern), lavaFloor);
    }

    [Fact]
    public void TheLightIndexHoldsLavaAsALightAndFollowsItsEdits()
    {
        var blocks = new BlockId[VoxelChunk.Volume];
        blocks[(((4 * VoxelChunk.Size) + 5) * VoxelChunk.Size) + 3] = BlockId.Lava; // x 3, y 4, z 5
        var grid = new VoxelGrid(new WorldLimits(0, 0, 0, 64, 64, 64), new OneChunk(blocks));
        var index = new ChunkLightIndex(grid);
        _ = grid.GetChunk(new VoxelChunkCoord(2, 1, 2));
        Assert.Equal([new BlockPos(35, 20, 37)], index.All);
        Assert.Single(index.TakeChanged());

        ((IVoxelWriter)grid).SetBlock(35, 20, 37, BlockId.Air);
        Assert.Empty(index.All);
        ((IVoxelWriter)grid).SetBlock(36, 20, 37, BlockId.Lava);
        Assert.Equal([new BlockPos(36, 20, 37)], index.All);
    }

    [Fact]
    public void ChunksOfRockAreBuriedLikeStoneAboveIdSixtyThree()
    {
        // A chunk shut in by opaque cubes meshes to nothing whatever the kind: the mesher's flags table reaches the new ids.
        foreach (var id in New.Where(static i => i != BlockId.Lava))
        {
            Assert.True(ChunkMesher.IsOpaqueCube(id), $"{id}");
        }

        Assert.False(ChunkMesher.IsOpaqueCube((BlockId)100)); // an id with no block is no cube
    }

    private sealed class OneChunk(BlockId[] blocks) : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord) => new(blocks);
    }
}
