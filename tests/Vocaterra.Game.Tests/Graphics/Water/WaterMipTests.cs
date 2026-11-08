using Vocaterra.Game.Graphics.Water;

namespace Vocaterra.Game.Tests.Graphics.Water;

/// <summary>The water strip's mip levels.</summary>
public sealed class WaterMipTests
{
    [Fact]
    public void Given_ATwoByTwoPicture_When_HalvedAndAveraged_Then_ItIsOnePixelOfTheMean()
    {
        byte[] picture = [10, 20, 30, 40, 30, 40, 50, 60, 50, 60, 70, 80, 70, 80, 90, 100];

        Assert.Equal(new byte[] { 40, 50, 60, 70 }, WaterArt.HalfSize(picture, 2, 2));
    }

    [Fact]
    public void Given_TheWholeStrip_When_HalvedToTheLastUsableLevel_Then_EachTileIsOneTexelOfItsOwnColour()
    {
        var pixels = WaterArt.Frame(4);
        int width = WaterTileLayout.Width, height = WaterTileLayout.Height;
        for (var level = 0; level < WaterTileLayout.MaxMipLevel; level++)
        {
            pixels = WaterArt.HalfSize(pixels, width, height);
            (width, height) = (width / 2, height / 2);
        }

        Assert.Equal((WaterTileLayout.Columns, WaterTileLayout.Rows), (width, height));
        // The falling tile is lighter than the deep one at every level: their colours have not run into each other.
        int Red(WaterTile tile) => pixels[(int)tile * 4];
        Assert.True(Red(WaterTile.Falling) > Red(WaterTile.DeepFlow) + 40);
        Assert.True(Red(WaterTile.ShallowFlow) > Red(WaterTile.DeepFlow) + 20);
    }
}
