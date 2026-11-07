using Vocaterra.Game.Graphics.Water;

namespace Vocaterra.Game.Tests.Graphics.Water;

/// <summary>Where the water's pictures lie in a frame and how a block's surface maps onto one, turned to the current.</summary>
public sealed class WaterTileLayoutTests
{
    private const float Epsilon = 1e-5f;

    [Fact]
    public void Given_EveryTile_When_ItsCentreIsMapped_Then_TheInverseFindsTheTileAndRow()
    {
        foreach (var tile in Enum.GetValues<WaterTile>())
        {
            foreach (var grey in new[] { false, true })
            {
                var (u, v) = WaterTileLayout.Uv(tile, grey, 0.5f, 0.5f);

                Assert.Equal((tile, grey), WaterTileLayout.TileAt(u, v));
            }
        }
    }

    [Fact]
    public void Given_TheWholeOfATile_When_ItsEdgesAreMapped_Then_TheyStayInsideItsGutter()
    {
        var (u0, v0) = WaterTileLayout.Uv(WaterTile.DeepFlow, false, 0f, 0f);
        var (u1, v1) = WaterTileLayout.Uv(WaterTile.DeepFlow, false, 1f, 1f);

        Assert.Equal(((1 * WaterTileLayout.Pitch) + 1f) / WaterTileLayout.Width, u0, Epsilon);
        Assert.Equal(((1 * WaterTileLayout.Pitch) + 1f + WaterTileLayout.Cell) / WaterTileLayout.Width, u1, Epsilon);
        Assert.Equal(1f / WaterTileLayout.Height, v0, Epsilon);
        Assert.Equal((1f + WaterTileLayout.Cell) / WaterTileLayout.Height, v1, Epsilon);
    }

    [Fact]
    public void Given_DepthAndCurrent_When_TheTileIsPicked_Then_ItIsTheMatchingOne()
    {
        Assert.Equal(WaterTile.DeepStill, WaterTileLayout.For(true, false));
        Assert.Equal(WaterTile.DeepFlow, WaterTileLayout.For(true, true));
        Assert.Equal(WaterTile.ShallowStill, WaterTileLayout.For(false, false));
        Assert.Equal(WaterTile.ShallowFlow, WaterTileLayout.For(false, true));
    }

    [Fact]
    public void Given_NoCurrent_When_ACornerIsMapped_Then_ThePictureIsUnturned()
    {
        Assert.Equal((0f, 0f), WaterTileLayout.Along(-0.5f, -0.5f, 0, 0));
        Assert.Equal((1f, 0f), WaterTileLayout.Along(0.5f, -0.5f, 0, 0));
        Assert.Equal((0f, 1f), WaterTileLayout.Along(-0.5f, 0.5f, 0, 0));
    }

    [Fact]
    public void Given_AnEastCurrent_When_CornersAreMapped_Then_TheHorizontalRunsDownstreamAndFillsTheTile()
    {
        var west = WaterTileLayout.Along(-0.5f, 0f, 256, 0);
        var east = WaterTileLayout.Along(0.5f, 0f, 256, 0);

        Assert.Equal(0f, west.P, Epsilon);
        Assert.Equal(1f, east.P, Epsilon); // downstream is towards the right of the picture
        Assert.Equal(west.Q, east.Q, Epsilon); // nothing across
    }

    [Fact]
    public void Given_ASouthCurrent_When_CornersAreMapped_Then_TheHorizontalRunsSouth()
    {
        var north = WaterTileLayout.Along(0f, -0.5f, 0, 256);
        var south = WaterTileLayout.Along(0f, 0.5f, 0, 256);

        Assert.Equal(0f, north.P, Epsilon);
        Assert.Equal(1f, south.P, Epsilon);
        Assert.Equal(north.Q, south.Q, Epsilon);
    }

    [Fact]
    public void Given_AWestCurrent_When_CornersAreMapped_Then_TheHorizontalRunsWest()
    {
        Assert.True(WaterTileLayout.Along(-0.5f, 0f, -256, 0).P > WaterTileLayout.Along(0.5f, 0f, -256, 0).P);
    }

    [Theory]
    [InlineData(256, 256)]
    [InlineData(256, -256)]
    [InlineData(100, 230)]
    [InlineData(-30, 250)]
    [InlineData(-200, -120)]
    public void Given_ASlantedCurrent_When_TheCornersAreMapped_Then_TheyLieInsideTheTileAndStillRunDownstream(int flowX, int flowZ)
    {
        var length = MathF.Sqrt(((float)flowX * flowX) + ((float)flowZ * flowZ));
        float dx = flowX / length, dz = flowZ / length;
        var upstream = WaterTileLayout.Along(-0.5f * dx, -0.5f * dz, flowX, flowZ);
        var downstream = WaterTileLayout.Along(0.5f * dx, 0.5f * dz, flowX, flowZ);

        Assert.True(downstream.P > upstream.P);
        foreach (var (cx, cz) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (0.5f, 0.5f), (-0.5f, 0.5f) })
        {
            var (p, q) = WaterTileLayout.Along(cx, cz, flowX, flowZ);

            Assert.InRange(p, 0f, 1f);
            Assert.InRange(q, 0f, 1f);
        }
    }
}
