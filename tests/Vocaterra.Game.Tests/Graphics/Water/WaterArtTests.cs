using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Water;

/// <summary>The water's pictures drawn in code: deterministic, looping, and moving the way the mesh expects.</summary>
public sealed class WaterArtTests
{
    private const int Cell = WaterTileLayout.Cell;

    [Fact]
    public void Given_TheSameFrame_When_ItIsDrawnTwice_Then_ThePixelsAreIdentical()
    {
        Assert.Equal(WaterArt.Frame(3), WaterArt.Frame(3));
    }

    [Fact]
    public void Given_AFrame_When_ItIsDrawn_Then_ItFillsTheStripAndHasWaterAlpha()
    {
        var pixels = WaterArt.Frame(0);

        Assert.Equal(WaterTileLayout.Width * WaterTileLayout.Height * 4, pixels.Length);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            Assert.InRange(pixels[i], 150, 255);
        }
    }

    [Theory]
    [InlineData(WaterTile.DeepFlow)]
    [InlineData(WaterTile.ShallowFlow)]
    public void Given_AFlowingTile_When_TheFrameAdvances_Then_ThePictureSlidesOnePixelRight(WaterTile tile)
    {
        for (var frame = 0; frame < WaterFrames.Count; frame++)
        {
            for (var y = 0; y < Cell; y++)
            {
                for (var x = 0; x < Cell; x++)
                {
                    Assert.Equal(WaterArt.Pixel(tile, x, y, frame), WaterArt.Pixel(tile, (x + 1) % Cell, y, frame + 1));
                }
            }
        }
    }

    [Fact]
    public void Given_TheFallingTile_When_TheFrameAdvances_Then_ThePictureSlidesDown()
    {
        for (var frame = 0; frame < WaterFrames.Count; frame++)
        {
            for (var y = 0; y < Cell; y++)
            {
                for (var x = 0; x < Cell; x++)
                {
                    Assert.Equal(WaterArt.Pixel(WaterTile.Falling, x, y, frame), WaterArt.Pixel(WaterTile.Falling, x, (y + 3) % Cell, frame + 1));
                }
            }
        }
    }

    [Theory]
    [InlineData(WaterTile.DeepStill)]
    [InlineData(WaterTile.ShallowStill)]
    public void Given_AStillTile_When_TheFramesPass_Then_ItShimmers(WaterTile tile)
    {
        var changed = 0;
        for (var y = 0; y < Cell; y++)
        {
            for (var x = 0; x < Cell; x++)
            {
                if (!Equals(WaterArt.Pixel(tile, x, y, 0), WaterArt.Pixel(tile, x, y, 5)))
                {
                    changed++;
                }
            }
        }

        Assert.True(changed >= 20, $"{changed} pixels differ between frame 0 and frame 5");
    }

    [Fact]
    public void Given_TheLoopsLastFrame_When_ItIsFollowedByTheFirst_Then_TheStepIsNoBiggerThanBetweenAnyTwoFrames()
    {
        // The flowing tile at frame 16 is the tile at frame 0 (period 16), so the loop has no jump.
        for (var y = 0; y < Cell; y++)
        {
            for (var x = 0; x < Cell; x++)
            {
                Assert.Equal(WaterArt.Pixel(WaterTile.DeepFlow, x, y, 0), WaterArt.Pixel(WaterTile.DeepFlow, x, y, WaterFrames.Count));
                Assert.Equal(WaterArt.Pixel(WaterTile.DeepStill, x, y, 0), WaterArt.Pixel(WaterTile.DeepStill, x, y, WaterFrames.Count));
                Assert.Equal(WaterArt.Pixel(WaterTile.Falling, x, y, 0), WaterArt.Pixel(WaterTile.Falling, x, y, WaterFrames.Count));
            }
        }
    }

    [Fact]
    public void Given_ATilesGutter_When_ItIsRead_Then_ItHoldsThePixelsFromTheOtherSideOfThePicture()
    {
        var pixels = WaterArt.Frame(2);
        var width = WaterTileLayout.Width;
        (byte R, byte G, byte B, byte A) At(int x, int y) => (pixels[((y * width) + x) * 4], pixels[(((y * width) + x) * 4) + 1], pixels[(((y * width) + x) * 4) + 2], pixels[(((y * width) + x) * 4) + 3]);

        foreach (var tile in Enum.GetValues<WaterTile>())
        {
            var left = (int)tile * WaterTileLayout.Pitch;
            var first = left + WaterTileLayout.Gutter; // the picture's first column
            for (var y = WaterTileLayout.Gutter; y < WaterTileLayout.Gutter + Cell; y++)
            {
                Assert.Equal(At(first + Cell - 1, y), At(first - 1, y)); // the gutter on the left is the picture's last column
                Assert.Equal(At(first, y), At(first + Cell, y)); // the gutter on the right is its first
                Assert.Equal(At(first + Cell - WaterTileLayout.Gutter, y), At(left, y)); // and so on to its outer edge
            }
        }
    }

    [Fact]
    public void Given_TheGreyRow_When_ItIsRead_Then_ItIsTheLumaOfTheColourRowWithTheSameAlpha()
    {
        var pixels = WaterArt.Frame(1);
        var rowBytes = WaterTileLayout.Pitch * WaterTileLayout.Width * 4;
        var colour = pixels.AsSpan(0, rowBytes).ToArray();

        Assert.Equal(Greyscale.ToLuma(colour), pixels.AsSpan(rowBytes, rowBytes).ToArray());
    }
}
