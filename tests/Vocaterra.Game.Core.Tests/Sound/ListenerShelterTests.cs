using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>The listener's shelter (E06): the share of the columns round the feet with cover over the head, read from the grid's highest block like the rain's drops do.</summary>
public sealed class ListenerShelterTests
{
    private static Listener At(double x, double y, double z) => new(x, y, z, 0);

    private static VoxelGrid World()
    {
        var grid = new VoxelGrid(64, 96, 64);
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                grid.FillColumn(x, z, 0, 63, BlockId.Stone);
            }
        }

        for (var z = 45; z <= 55; z++)
        {
            for (var x = 45; x <= 55; x++)
            {
                grid.SetBlock(x, 70, z, BlockId.Stone);
            }
        }

        return grid;
    }

    [Fact]
    public void Given_OpenGround_When_TheShareIsRead_Then_ItIsZero()
    {
        Assert.Equal(0.0, ListenerShelter.CoveredShare(World().HighestBlock, At(10.5, 64, 10.5)));
    }

    [Fact]
    public void Given_ARoofOverTheListener_When_TheShareIsRead_Then_ItIsOne()
    {
        Assert.Equal(1.0, ListenerShelter.CoveredShare(World().HighestBlock, At(50.5, 64, 50.5)));
    }

    [Fact]
    public void Given_TheListenerAtTheEave_When_TheShareIsRead_Then_ItIsPartial()
    {
        // x 45 is the roof's west column: a listener on x 44 has the three columns of x 45 covered and the six of x 43 and 44 open.
        Assert.Equal(3 / 9.0, ListenerShelter.CoveredShare(World().HighestBlock, At(44.5, 64, 50.5)), 9);
    }

    [Fact]
    public void Given_NoQuery_When_TheShareIsRead_Then_TheSkyIsOpen()
    {
        Assert.Equal(0.0, ListenerShelter.CoveredShare(null, At(50.5, 64, 50.5)));
    }

    [Fact]
    public void Given_ARoofRemoved_When_TheShareIsRead_Then_ItFollowsTheBlock()
    {
        var grid = World();
        Assert.Equal(1.0, ListenerShelter.CoveredShare(grid.HighestBlock, At(50.5, 64, 50.5)));

        for (var z = 49; z <= 51; z++)
        {
            for (var x = 49; x <= 51; x++)
            {
                grid.SetBlock(x, 70, z, BlockId.Air);
            }
        }

        Assert.Equal(0.0, ListenerShelter.CoveredShare(grid.HighestBlock, At(50.5, 64, 50.5)));
    }

    [Fact]
    public void Given_TheTopOfACoverAndAFeetHeight_When_Asked_Then_PlantsAtTheFeetAreNotCoverAndARoofIs()
    {
        Assert.False(OverheadCover.Covers(64, 64)); // a flower on the ground: its top is at 65, a block above the feet
        Assert.False(OverheadCover.Covers(63, 64)); // the ground itself
        Assert.True(OverheadCover.Covers(70, 64)); // a roof 7 blocks up
        Assert.True(OverheadCover.Covers(66, 64)); // a roof just over the head
        Assert.Equal(71, OverheadCover.Top(70));
    }
}
