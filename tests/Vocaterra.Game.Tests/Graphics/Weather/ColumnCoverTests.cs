using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Weather;

namespace Vocaterra.Game.Tests.Graphics.Weather;

/// <summary>The rain's cover heights (E06): read from the grid's highest block, cached by chunk column, and made again when a block of the column changes.</summary>
public sealed class ColumnCoverTests
{
    [Fact]
    public void Given_OpenGround_When_TheTopIsRead_Then_ItIsTheGroundsTopFace()
    {
        var cover = new ColumnCover(WeatherTestWorld.Flat());

        Assert.Equal(WeatherTestWorld.GroundBlock + 1, cover.TopAt(40, 50));
    }

    [Fact]
    public void Given_ARoofPlacedAfterTheFirstRead_When_TheCoverIsRefreshed_Then_TheColumnsTopIsTheRoofs()
    {
        var grid = WeatherTestWorld.Flat();
        var cover = new ColumnCover(grid);
        cover.Refresh(40, 50, 20);
        Assert.Equal(64, cover.TopAt(40, 50));

        grid.SetBlock(40, WeatherTestWorld.RoofBlock, 50, BlockId.Stone);
        cover.Refresh(40, 50, 20);

        Assert.Equal(WeatherTestWorld.RoofBlock + 1, cover.TopAt(40, 50));
        Assert.Equal(64, cover.TopAt(41, 50)); // the next column is open
    }

    [Fact]
    public void Given_ARoofRemoved_When_TheCoverIsRefreshed_Then_TheColumnIsOpenAgain()
    {
        var grid = WeatherTestWorld.Flat();
        WeatherTestWorld.Roof(grid, 38, 48, 42, 52);
        var cover = new ColumnCover(grid);
        cover.Refresh(40, 50, 20);
        Assert.Equal(71, cover.TopAt(40, 50));

        grid.SetBlock(40, WeatherTestWorld.RoofBlock, 50, BlockId.Air);
        cover.Refresh(40, 50, 20);

        Assert.Equal(64, cover.TopAt(40, 50));
        Assert.Equal(71, cover.TopAt(41, 50));
    }

    [Fact]
    public void Given_AQuietWorld_When_RefreshedAgain_Then_NoTileIsReadAgain()
    {
        var cover = new ColumnCover(WeatherTestWorld.Flat());
        cover.Refresh(60, 60, 20);
        var rebuilds = cover.Rebuilds;

        for (var i = 0; i < 10; i++)
        {
            cover.Refresh(60, 60, 20);
        }

        Assert.True(rebuilds > 0);
        Assert.Equal(rebuilds, cover.Rebuilds);
    }

    [Fact]
    public void Given_ABlockPlacedInOneChunkColumn_When_Refreshed_Then_OnlyThatTileIsReadAgain()
    {
        var grid = WeatherTestWorld.Flat();
        var cover = new ColumnCover(grid);
        cover.Refresh(60, 60, 20);
        var rebuilds = cover.Rebuilds;

        grid.SetBlock(60, WeatherTestWorld.RoofBlock, 60, BlockId.Stone);
        cover.Refresh(60, 60, 20);

        Assert.Equal(rebuilds + 1, cover.Rebuilds);
    }

    [Fact]
    public void Given_AColumnOutsideTheGrid_When_TheTopIsRead_Then_ItIsZero()
    {
        var cover = new ColumnCover(WeatherTestWorld.Flat());

        Assert.Equal(0, cover.TopAt(-1, 5));
        Assert.Equal(0, cover.TopAt(5, 128));
    }

    [Fact]
    public void Given_ALeafAboveTheGround_When_TheTopIsRead_Then_ItIsCover()
    {
        var grid = WeatherTestWorld.Flat();
        grid.SetBlock(10, 64, 10, BlockId.Leaves); // a tree, a plant, a fence: whatever is not air is cover
        var cover = new ColumnCover(grid);

        Assert.Equal(65, cover.TopAt(10, 10));
    }
}
