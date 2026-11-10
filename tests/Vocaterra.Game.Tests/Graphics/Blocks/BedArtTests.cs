using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>Y03: the bed and the bedroll have pictures of their own, and their piles stand at their own heights.</summary>
public sealed class BedArtTests
{
    private static (byte R, byte G, byte B, byte A) Texel(BlockCell cell, int x, int y)
    {
        var pixels = BlockArt.Pixels(cell);
        var i = ((y * BlockAtlasLayout.CellSize) + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    [Fact]
    public void Given_ABedAndABedroll_When_TheirPicturesAreDrawn_Then_TheyDifferFromEachOtherAndFromTheLoaves()
    {
        var bed = BlockArt.Pixels(BlockCell.Bed);
        var roll = BlockArt.Pixels(BlockCell.Bedroll);
        Assert.False(bed.AsSpan().SequenceEqual(roll));
        Assert.False(bed.AsSpan().SequenceEqual(BlockArt.Pixels(BlockCell.Loaves)));
        Assert.False(roll.AsSpan().SequenceEqual(BlockArt.Pixels(BlockCell.Loaves)));
    }

    [Fact]
    public void Given_ABed_When_ItsTopIsDrawn_Then_ThePillowIsAtTheHeadAndTheBlanketCoversTheRest()
    {
        var pillow = Texel(BlockCell.Bed, 8, 3);
        Assert.Equal(255, pillow.A);
        Assert.True(pillow.R > 230 && pillow.G > 220 && pillow.B > 210, "the pillow is cream");
        var blanket = Texel(BlockCell.Bed, 8, 12);
        Assert.Equal(255, blanket.A);
        Assert.True(blanket.R > 140 && blanket.G < 100, "the blanket is red");
    }

    [Fact]
    public void Given_ABedroll_When_ItIsDrawn_Then_ItsEndIsACoilOfClothAndItsBodyIsStriped()
    {
        Assert.Equal(255, Texel(BlockCell.Bedroll, 12, 7).A); // the spiral at the end
        Assert.Equal(0, Texel(BlockCell.Bedroll, 4, 0).A); // above the roll the picture is empty
        Assert.Equal(0, Texel(BlockCell.Bedroll, 4, 15).A);
        Assert.NotEqual(Texel(BlockCell.Bedroll, 4, 5), Texel(BlockCell.Bedroll, 4, 6)); // the stripes
    }

    [Fact]
    public void Given_ABedAndABedroll_When_TheyAreDrawnAsBlocks_Then_EveryFaceShowsItsOwnPicture()
    {
        Assert.Equal(BlockCell.Bed, BlockAtlasLayout.CellFor(BlockId.Bed, BlockFace.Up));
        Assert.Equal(BlockCell.Bed, BlockAtlasLayout.CellFor(BlockId.Bed, BlockFace.North));
        Assert.Equal(BlockCell.Bedroll, BlockAtlasLayout.CellFor(BlockId.Bedroll, BlockFace.Up));
        Assert.Equal(BlockCell.Bedroll, BlockAtlasLayout.CellFor(BlockId.Bedroll, BlockFace.West));
    }
}
