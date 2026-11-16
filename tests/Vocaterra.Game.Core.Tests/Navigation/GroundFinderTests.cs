using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Navigation;

/// <summary>Where a standing player's feet go in a column: the rule of <c>--teleport</c> and of the menu's teleport.</summary>
public sealed class GroundFinderTests
{
    [Fact]
    public void Given_AColumnOfStone_When_Asked_Then_TheFeetStandOnTheTopBlock()
    {
        var grid = new VoxelGrid(16, 32, 16);
        grid.FillColumn(4, 5, 0, 9, BlockId.Stone);
        Assert.Equal(new BlockPos(4, 10, 5), GroundFinder.StandingAt(grid, 4, 5));
    }

    [Fact]
    public void Given_WaterOnTop_When_Asked_Then_TheFeetAreOverTheSurfaceNotOnTheBed()
    {
        var grid = new VoxelGrid(16, 32, 16);
        grid.FillColumn(2, 2, 0, 9, BlockId.Stone);
        grid.FillColumn(2, 2, 10, 12, BlockId.Water);
        Assert.Equal(new BlockPos(2, 13, 2), GroundFinder.StandingAt(grid, 2, 2)); // a pool: over the water, not on the bed at y 10
        grid.FillColumn(9, 9, 0, 3, BlockId.Stone);
        grid.FillColumn(9, 9, 4, 28, BlockId.Water);
        Assert.Equal(new BlockPos(9, 29, 9), GroundFinder.StandingAt(grid, 9, 9)); // a deep lake: still its surface
        Assert.Null(GroundFinder.StandingAt(grid, 8, 8)); // an empty column has no ground
    }

    [Fact]
    public void Given_ALeafCanopyOverTheGround_When_Asked_Then_TheGroundUnderItIsUsed()
    {
        var grid = new VoxelGrid(16, 32, 16);
        grid.FillColumn(6, 6, 0, 9, BlockId.Stone);
        grid.FillColumn(6, 6, 12, 13, BlockId.Leaves);
        Assert.Equal(new BlockPos(6, 10, 6), GroundFinder.StandingAt(grid, 6, 6));
    }

    [Fact]
    public void Given_WaterUnderARoof_When_Asked_Then_NoPlaceIsFound()
    {
        var grid = new VoxelGrid(16, 32, 16);
        grid.FillColumn(4, 4, 0, 9, BlockId.Stone);
        grid.FillColumn(4, 4, 10, 11, BlockId.Water);
        grid.FillColumn(4, 4, 12, 31, BlockId.Stone);
        Assert.Null(GroundFinder.StandingAt(grid, 4, 4)); // a cave lake under solid rock
    }

    [Fact]
    public void Given_ARoofOverTheGround_When_Asked_Then_NoPlaceIsFoundAndOutsideTheWorldIsNone()
    {
        var grid = new VoxelGrid(16, 32, 16);
        grid.FillColumn(3, 3, 0, 9, BlockId.Stone);
        grid.FillColumn(3, 3, 11, 11, BlockId.Stone); // a ledge over a gap: the highest block that has room over it is the ground
        Assert.Equal(new BlockPos(3, 12, 3), GroundFinder.StandingAt(grid, 3, 3)); // the highest solid block that is not a tree wins
        grid.FillColumn(5, 5, 0, 9, BlockId.Stone);
        grid.FillColumn(5, 5, 10, 31, BlockId.Stone);
        Assert.Null(GroundFinder.StandingAt(grid, 5, 5)); // a solid column to the ceiling
        Assert.Null(GroundFinder.StandingAt(grid, -1, 0));
        Assert.Null(GroundFinder.StandingAt(grid, 16, 0));
    }
}
