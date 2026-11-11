using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>The village's pictures and shapes (S15): the new atlas cells, the signs' words, the lantern's glow in the mesh and the small shapes of the lantern and the stall's goods.</summary>
public sealed class VillageArtTests
{
    private static int Alpha(BlockCell cell, int x, int y) => BlockArt.Pixels(cell)[(((y * BlockAtlasLayout.CellSize) + x) * 4) + 3];

    private static bool Ink(BlockCell cell, int x, int y)
    {
        var p = BlockArt.Pixels(cell);
        var i = ((y * BlockAtlasLayout.CellSize) + x) * 4;
        return p[i + 3] > 0 && p[i] < 70 && p[i + 1] < 50 && p[i + 2] < 30;
    }

    [Fact]
    public void EveryNewBlockKindHasAPictureAndTheAtlasHasRoomForAll()
    {
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows);
        foreach (var id in Enum.GetValues<BlockId>().Where(static b => b >= BlockId.Timber))
        {
            foreach (var face in Enum.GetValues<BlockFace>())
            {
                Assert.NotEqual(BlockCell.White, BlockAtlasLayout.CellFor(id, face));
            }
        }

        foreach (var cell in new[] { BlockCell.TimberSide, BlockCell.Lantern, BlockCell.Canvas, BlockCell.ChestSide, BlockCell.Loaves, BlockCell.Apples, BlockCell.Bottles, BlockCell.SignTiendaA, BlockCell.FaceGuide, BlockCell.Cloth, BlockCell.Backpack, BlockCell.IconTorch, BlockCell.FaceFisher, BlockCell.FaceBaker, BlockCell.FaceSmith, BlockCell.FaceFarmer, BlockCell.IconCoin, BlockCell.IconApple, BlockCell.IconAxe })
        {
            var pixels = BlockArt.Pixels(cell);
            Assert.Equal(16 * 16 * 4, pixels.Length);
            Assert.Contains(Enumerable.Range(0, 256), i => pixels[(i * 4) + 3] > 0); // not empty
            Assert.Equal(pixels, BlockArt.Pixels(cell)); // the same every time
        }
    }

    [Fact]
    public void TheVillagesFourFacesAreDrawnAndDifferFromEachOtherAndFromTheFirstFour()
    {
        // village-plan V05: the fisher, the baker (the one woman of the four, with hair down beside her cheeks), the smith and the farmer.
        var faces = new[] { BlockCell.FaceGuide, BlockCell.FaceShopkeeper, BlockCell.FaceVillager, BlockCell.FacePlayer, BlockCell.FaceFisher, BlockCell.FaceBaker, BlockCell.FaceSmith, BlockCell.FaceFarmer };
        var pictures = faces.Select(static f => Convert.ToHexString(BlockArt.Pixels(f))).ToList();
        Assert.Equal(faces.Length, pictures.Distinct().Count());
        foreach (var face in faces[4..])
        {
            Assert.True(BlockArtVillage.Draws(face));
            Assert.All(Enumerable.Range(0, 256), i => Assert.Equal(255, BlockArt.Pixels(face)[(i * 4) + 3])); // a whole face, no hole
        }

        static bool Hair(BlockCell face, int x, int y)
        {
            var p = BlockArt.Pixels(face);
            var i = ((y * 16) + x) * 4;
            var skin = p[i] > 200 && p[i + 1] > 150; // the skin is light and warm; the hair here is darker
            return !skin;
        }

        Assert.True(Hair(BlockCell.FaceBaker, 0, 10) && Hair(BlockCell.FaceBaker, 15, 10), "the baker's hair falls beside her cheeks");
        Assert.False(Hair(BlockCell.FaceSmith, 0, 10) || Hair(BlockCell.FaceFarmer, 0, 10), "the men's hair is short");
        Assert.True(Hair(BlockCell.FaceFisher, 7, 13), "the fisher has a beard");
        Assert.True(Hair(BlockCell.FaceSmith, 5, 10) && !Hair(BlockCell.FaceSmith, 7, 13), "the smith has a moustache and no beard");
    }

    [Fact]
    public void TheFiguresClothSkinAndHairAreNearlyWhiteSoTheirColourComesFromTheVertex()
    {
        foreach (var cell in new[] { BlockCell.Cloth, BlockCell.Skin, BlockCell.Hair })
        {
            var pixels = BlockArt.Pixels(cell);
            for (var i = 0; i < 256; i++)
            {
                Assert.Equal(255, pixels[(i * 4) + 3]);
                Assert.True(pixels[i * 4] > 180, $"{cell} pixel {i} is light enough to be tinted");
            }
        }
    }

    [Theory]
    [InlineData(BlockCell.SignTiendaA)]
    [InlineData(BlockCell.SignTiendaB)]
    [InlineData(BlockCell.SignRio)]
    [InlineData(BlockCell.SignMontanaA)]
    [InlineData(BlockCell.SignMontanaB)]
    public void ASignBoardIsAPlankWithDarkLettersAndClearRowsAboveAndBelowSoStackedBoardsShowTheirGap(BlockCell cell)
    {
        for (var x = 0; x < 16; x++)
        {
            Assert.Equal(0, Alpha(cell, x, 0)); // the gap
            Assert.Equal(0, Alpha(cell, x, 15));
            Assert.Equal(255, Alpha(cell, x, 8)); // the plank is solid across the whole cell
        }

        var ink = Enumerable.Range(0, 16 * 16).Count(i => Ink(cell, i % 16, i / 16));
        Assert.InRange(ink, 12, 90); // letters and an arrow, in dark ink
    }

    [Fact]
    public void TheWordsAreOnTheFacesThatReadLeftToRightAndTheOtherFacesAreBlankPlank()
    {
        foreach (var id in new[] { BlockId.SignTiendaA, BlockId.SignTiendaB, BlockId.SignRio, BlockId.SignMontanaA, BlockId.SignMontanaB })
        {
            var word = BlockAtlasLayout.CellFor(id, BlockFace.West);
            Assert.NotEqual(BlockCell.SignBlank, word);
            Assert.Equal(word, BlockAtlasLayout.CellFor(id, BlockFace.South));
            foreach (var other in new[] { BlockFace.North, BlockFace.East, BlockFace.Up, BlockFace.Down })
            {
                Assert.Equal(BlockCell.SignBlank, BlockAtlasLayout.CellFor(id, other));
            }
        }

        Assert.Equal(0, Enumerable.Range(0, 256).Count(i => Ink(BlockCell.SignBlank, i % 16, i / 16))); // no letter on the blank
        Assert.Equal(255, Alpha(BlockCell.SignBlank, 3, 8));
        Assert.Equal(0, Alpha(BlockCell.SignBlank, 3, 0));
    }

    [Fact]
    public void TheSignsWordsAreInThePixelFontIncludingTheAccentsOfRioAndMontana()
    {
        // The font: three letters wide, five high, and the glyphs the three names need.
        foreach (var c in "TIENDARMO<^")
        {
            var glyph = BlockArtVillage.Glyph(c);
            Assert.NotNull(glyph);
            Assert.Equal(5, glyph.Length);
            Assert.All(glyph, static row => Assert.Equal(3, row.Length));
            Assert.Contains(glyph, static row => row.Contains('#', StringComparison.Ordinal));
        }

        Assert.Null(BlockArtVillage.Glyph('?'));
        // RÍO: the acute accent is above the I (glyph 2 of "<RIO", x 8 to 10, the rows over the letters), TAÑA: the tilde is above the N (glyph 2, x 8 to 10).
        Assert.True(Ink(BlockCell.SignRio, 9, 5) || Ink(BlockCell.SignRio, 10, 4), "an accent over the I of Río");
        Assert.False(Ink(BlockCell.SignTiendaB, 9, 5) || Ink(BlockCell.SignTiendaB, 10, 4), "NDA has no accent");
        Assert.True(Ink(BlockCell.SignMontanaB, 8, 5) || Ink(BlockCell.SignMontanaB, 9, 4), "a tilde over the N of Montaña");
        // The arrow of the shop's board points left, the mountain's up: their first glyphs differ.
        var left = Enumerable.Range(0, 3).SelectMany(x => Enumerable.Range(6, 5).Select(y => Ink(BlockCell.SignTiendaA, x, y))).ToArray();
        var up = Enumerable.Range(0, 3).SelectMany(x => Enumerable.Range(6, 5).Select(y => Ink(BlockCell.SignMontanaA, x, y))).ToArray();
        Assert.NotEqual(left, up);
    }

    [Fact]
    public void TheAwningsStripesAreRedAndWhiteAndTheStallsGoodsAreWarmAndBlue()
    {
        static (double R, double G, double B) Mean(BlockCell cell)
        {
            var p = BlockArt.Pixels(cell);
            double r = 0, g = 0, b = 0;
            var n = 0;
            for (var i = 0; i < 256; i++)
            {
                if (p[(i * 4) + 3] == 0)
                {
                    continue;
                }

                r += p[i * 4];
                g += p[(i * 4) + 1];
                b += p[(i * 4) + 2];
                n++;
            }

            return (r / n, g / n, b / n);
        }

        var red = Mean(BlockCell.AwningRed);
        var white = Mean(BlockCell.AwningWhite);
        Assert.True(red.R > red.G * 3 && red.R > red.B * 3);
        Assert.True(white.R > 220 && white.G > 220 && white.B > 200);
        var loaves = Mean(BlockCell.Loaves);
        Assert.True(loaves.R > loaves.B + 40, "bread is warm");
        var bottles = Mean(BlockCell.Bottles);
        Assert.True(bottles.B > bottles.R, "the bottles are blue");
        var apples = Mean(BlockCell.Apples);
        Assert.True(apples.R > apples.G + 30, "apples are red");
    }

    [Fact]
    public void ALanternIsASmallGlowingBoxAndAPileIsALowBoxWithItsTopAndSidesDrawn()
    {
        // The glass is its own light: it is in the self-lit mesh, which the renderer draws with no light tint (E02), not in the opaque one.
        var lanternMeshes = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Lantern), 0, 0, 0);
        Assert.Equal(0, lanternMeshes.Opaque.FaceCount);
        var lantern = lanternMeshes.LitMesh;
        Assert.Equal(6, lantern.FaceCount);
        Assert.All(lantern.Vertices.ToArray(), static v =>
        {
            Assert.InRange(v.X, 5.3f, 5.7f);
            Assert.InRange(v.Y, 5.18f, 5.82f);
            Assert.Equal(255u, v.Color & 0xFF); // unshaded: it is the light
        });
        var pile = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Loaves), 0, 0, 0).Opaque;
        Assert.Equal(6, pile.FaceCount);
        Assert.InRange(pile.Vertices.ToArray().Max(static v => v.Y), 5.4f, 5.45f);
        var wheat = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Wheat), 0, 0, 0);
        Assert.Equal(0, wheat.Opaque.FaceCount);
        Assert.Equal(2, wheat.PlantMesh.FaceCount); // crossed quads, with the plants
        var torch = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.PostTorch), 0, 0, 0);
        Assert.Equal(12, torch.Opaque.FaceCount + torch.LitMesh.FaceCount); // a stick (opaque) and a flame (self-lit), like the torch the player places
    }

    [Fact]
    public void ALanternLightsTheFacesNearItInTheGlowMeshAndNotTheFarOnes()
    {
        // A floor of stone and a lantern one block over it at (8, 6, 8); the floor's top face at (8, 5) is near it, at (8 + 12, 5) is not. The glow is its own mesh (E02a): the opaque
        // mesh never changes with a lantern, and it is the glow mesh that has the quads of the faces the lantern reaches, brightest next to it.
        var floor = new MapBlocks().Fill(0, 4, 0, 31, 5, 15, BlockId.Stone);
        var lights = new[] { new BlockPos(8, 6, 8) };
        float Glow(MeshOptions options, int x)
        {
            var vertices = ChunkMesher.Build(floor, x / 16, 0, 0, 0, options).GlowMesh.Vertices.ToArray();
            var tops = vertices.Where(v => v.Y == 6f && (int)MathF.Floor(v.X) == x && v.Z is >= 7f and <= 10f).ToArray();
            return tops.Length == 0 ? 0f : tops.Average(static v => (float)(v.Color & 0xFF));
        }

        var plain = ChunkMesher.Build(floor, 0, 0, 0, 0, MeshOptions.Full);
        var lit = ChunkMesher.Build(floor, 0, 0, 0, 0, new MeshOptions(true, 100, lights));
        Assert.Equal(0, plain.GlowMesh.FaceCount);
        Assert.Equal(plain.Opaque.Vertices.ToArray(), lit.Opaque.Vertices.ToArray());
        var near = Glow(new MeshOptions(true, 100, lights), 8);
        Assert.True(near > 100, "the lantern lights the floor beside it brightly, " + near);
        Assert.True(Glow(new MeshOptions(true, 100, lights), 12) is > 0f && Glow(new MeshOptions(true, 100, lights), 12) < near, "less further out");
        Assert.Equal(0f, Glow(new MeshOptions(true, 100, lights), 23)); // nothing reaches a block 15 blocks away
    }

    [Fact]
    public void TheGlowIsADeterministicFunctionOfTheBlocksAndTheLights()
    {
        var floor = new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Stone);
        var options = new MeshOptions(true, 100, [new BlockPos(3, 6, 3), new BlockPos(10, 6, 9)], LightBand.Dusk);
        var a = ChunkMesher.Build(floor, 0, 0, 0, 0, options);
        var b = ChunkMesher.Build(floor, 0, 0, 0, 0, options);
        Assert.Equal(a.Opaque.Vertices.ToArray(), b.Opaque.Vertices.ToArray());
        Assert.NotEqual(0, a.GlowMesh.FaceCount);
        Assert.Equal(a.GlowMesh.Vertices.ToArray(), b.GlowMesh.Vertices.ToArray());
    }

    [Fact]
    public void TheFiguresAndTheArmAreLitWithTheSameSunTableAsTheBlocks()
    {
        Assert.Equal(ChunkMesher.SunTintFor(0, 1, 0), ChunkMesher.SunTintFor(0, 1, 0));
        var up = ChunkMesher.SunTintFor(0, 1, 0);
        var down = ChunkMesher.SunTintFor(0, -1, 0);
        var away = ChunkMesher.SunTintFor(0.5f, 0, 0.5f);
        Assert.True(up.R > away.R && away.R > down.R);
        Assert.Equal(ChunkMesher.FaceShade(BlockFace.Up), up.R, 4);
        Assert.Equal(ChunkMesher.FaceShade(BlockFace.Down), down.R, 4);
    }
}
