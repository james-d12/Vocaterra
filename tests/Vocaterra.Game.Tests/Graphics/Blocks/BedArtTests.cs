using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>Y03, Y08: the halves of the bed and the bedroll have pictures of their own that go on from one half to the next.</summary>
public sealed class BedArtTests
{
    private static (byte R, byte G, byte B, byte A) Texel(BlockCell cell, int x, int y)
    {
        var pixels = BlockArt.Pixels(cell);
        var i = ((y * BlockAtlasLayout.CellSize) + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    [Fact]
    public void Given_TheHalvesOfABedAndABedroll_When_TheirPicturesAreDrawn_Then_TheyDifferFromEachOtherAndFromTheLoaves()
    {
        BlockCell[] cells = [BlockCell.Bed, BlockCell.BedFoot, BlockCell.BedSide, BlockCell.Bedroll, BlockCell.BedrollFoot, BlockCell.Loaves];
        for (var a = 0; a < cells.Length; a++)
        {
            for (var b = a + 1; b < cells.Length; b++)
            {
                Assert.False(BlockArt.Pixels(cells[a]).AsSpan().SequenceEqual(BlockArt.Pixels(cells[b])), $"{cells[a]} and {cells[b]} are the same picture");
            }
        }
    }

    [Fact]
    public void Given_ABedsHeadTop_When_ItIsDrawn_Then_ThePillowIsAtTheHeadTheSheetIsTurnedDownAndTheBlanketRunsToTheFoot()
    {
        var pillow = Texel(BlockCell.Bed, 8, 5);
        Assert.Equal(255, pillow.A);
        Assert.True(pillow.R > 230 && pillow.G > 220 && pillow.B > 210, "the pillow is cream");
        var sheet = Texel(BlockCell.Bed, 8, 10);
        Assert.True(sheet.R > 220 && sheet.G > 210, "the sheet turned down over the blanket is light");
        var blanket = Texel(BlockCell.Bed, 8, 14);
        Assert.True(blanket.R > 140 && blanket.G < 100, "the blanket is red");
        Assert.InRange(Math.Abs(blanket.R - Texel(BlockCell.BedFoot, 8, 0).R), 0, 12); // the foot half goes on from the same red
        Assert.True(Texel(BlockCell.Bed, 8, 1).R < 140, "the headboard's planks fill the first rows");
    }

    [Fact]
    public void Given_ABedsFootTop_When_ItIsDrawn_Then_TheBlanketHasAFoldWithALitRidgeOverADarkCrease()
    {
        Assert.True(Texel(BlockCell.BedFoot, 8, 9).R < Texel(BlockCell.BedFoot, 8, 8).R - 60);
        Assert.True(Texel(BlockCell.BedFoot, 8, 4).R > 140 && Texel(BlockCell.BedFoot, 8, 4).G < 100);
    }

    [Fact]
    public void Given_ABedrollsHalves_When_TheyAreDrawn_Then_TheMatIsStripedAndOpaqueAndTheHeadHasAPatchForThePillow()
    {
        foreach (var cell in new[] { BlockCell.Bedroll, BlockCell.BedrollFoot })
        {
            for (var y = 0; y < BlockAtlasLayout.CellSize; y++)
            {
                Assert.Equal(255, Texel(cell, 8, y).A);
            }

            Assert.NotEqual(Texel(cell, 4, 10), Texel(cell, 7, 10)); // the stripes
        }

        Assert.NotEqual(Texel(BlockCell.Bedroll, 8, 4), Texel(BlockCell.BedrollFoot, 8, 4));
    }

    [Fact]
    public void Given_TheBlocksOfABedAndABedroll_When_TheyAreDrawn_Then_TheTopShowsTheHalfsPictureAndTheSidesTheBlanketOrTheMat()
    {
        Assert.Equal(BlockCell.Bed, BlockAtlasLayout.CellFor(BlockId.Bed, BlockFace.Up));
        Assert.Equal(BlockCell.BedFoot, BlockAtlasLayout.CellFor(BlockId.BedFoot, BlockFace.Up));
        Assert.Equal(BlockCell.BedSide, BlockAtlasLayout.CellFor(BlockId.Bed, BlockFace.North));
        Assert.Equal(BlockCell.BedSide, BlockAtlasLayout.CellFor(BlockId.BedFoot, BlockFace.West));
        Assert.Equal(BlockCell.Bedroll, BlockAtlasLayout.CellFor(BlockId.Bedroll, BlockFace.Up));
        Assert.Equal(BlockCell.BedrollFoot, BlockAtlasLayout.CellFor(BlockId.BedrollFoot, BlockFace.Up));
        Assert.Equal(BlockCell.BedrollFoot, BlockAtlasLayout.CellFor(BlockId.Bedroll, BlockFace.West));
        Assert.Equal(BlockCell.Planks, BlockAtlasLayout.CellFor(BlockId.Counter, BlockFace.Up));
        Assert.Equal(BlockCell.CrateSide, BlockAtlasLayout.CellFor(BlockId.Counter, BlockFace.South));
    }
}
