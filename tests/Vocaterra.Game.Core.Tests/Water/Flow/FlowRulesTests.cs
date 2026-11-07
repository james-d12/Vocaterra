using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;
using static Vocaterra.Game.Core.Tests.Water.Flow.FlowWorld;

namespace Vocaterra.Game.Core.Tests.Water.Flow;

/// <summary>The flow rules cell by cell (water-plan 4.5): down first, sideways only when blocked, a level lost per cell up to 7, the nearest drop, draining, and no third source.</summary>
public sealed class FlowRulesTests
{
    private static FlowCell Next(FlowWorld world, int x, int y, int z) => FlowRules.Next(world.Grid, x, y, z);

    private static FlowCell Air => new(BlockId.Air, WaterLevel.Source);

    private static FlowCell Flowing(int distance, bool falling = false) => new(BlockId.Water, WaterLevel.Flowing(distance, falling));

    [Fact]
    public void Given_ASourceOverAir_When_TheCellBelowIsRead_Then_ItFallsFull()
    {
        var world = new FlowWorld();
        world.Source(10, Ground + 3, 10);

        Assert.Equal(Flowing(1, falling: true), Next(world, 10, Ground + 2, 10));
    }

    [Fact]
    public void Given_FlowingWaterOverAir_When_TheCellBelowIsRead_Then_ItFallsToo()
    {
        var world = new FlowWorld();
        world.Source(10, Ground + 3, 10);
        world.Grid.SetBlock(11, Ground + 3, 10, BlockId.Water);
        world.Grid.SetWater(11, Ground + 3, 10, WaterLevel.Flowing(5));

        Assert.Equal(Flowing(1, falling: true), Next(world, 11, Ground + 2, 10));
    }

    [Fact]
    public void Given_ASourceOverAir_When_ASideCellIsRead_Then_ItStaysAir()
    {
        var world = new FlowWorld();
        world.Source(10, Ground + 3, 10);

        Assert.Equal(Air, Next(world, 11, Ground + 3, 10)); // water goes down first, sideways only when it cannot
    }

    [Fact]
    public void Given_ASourceOnStone_When_ASideCellIsRead_Then_ItFlowsAtDistanceOne()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
        Assert.Equal(Flowing(1), Next(world, 9, Ground, 10));
        Assert.Equal(Flowing(1), Next(world, 10, Ground, 9));
        Assert.Equal(Flowing(1), Next(world, 10, Ground, 11));
    }

    [Fact]
    public void Given_ASourceOnSourceWater_When_ASideCellIsRead_Then_ItFlowsAtDistanceOne()
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Source(10, Ground + 1, 10);

        Assert.Equal(Flowing(1), Next(world, 11, Ground + 1, 10));
    }

    [Fact]
    public void Given_ASourceNextToADiagonalCell_When_ItIsRead_Then_NothingFeedsIt()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);

        Assert.Equal(Air, Next(world, 11, Ground, 11));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(6, 7)]
    public void Given_FlowingWater_When_TheNextCellIsRead_Then_ItIsOneLevelThinner(int distance, int expected)
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground, 10, WaterLevel.Flowing(distance));

        Assert.Equal(Flowing(expected), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_WaterAtTheLastLevel_When_TheNextCellIsRead_Then_ItDoesNotSpreadFurther()
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground, 10, WaterLevel.Flowing(WaterLevel.MaxDistance));

        Assert.Equal(Air, Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_FallingWaterThatLandedOnStone_When_TheSideCellIsRead_Then_ItStartsAfreshAtDistanceOne()
    {
        var world = new FlowWorld();
        world.Source(10, Ground + 3, 10);
        world.Grid.SetBlock(10, Ground + 2, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground + 2, 10, WaterLevel.Flowing(1, falling: true));
        world.Grid.SetBlock(10, Ground + 1, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground + 1, 10, WaterLevel.Flowing(1, falling: true));
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground, 10, WaterLevel.Flowing(1, falling: true));

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_TheNearestDropIsEast_When_TheSideCellsAreRead_Then_OnlyEastIsFed()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(13, Floor, 10, BlockId.Air); // a hole 3 cells east of the source

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
        Assert.Equal(Air, Next(world, 9, Ground, 10));
        Assert.Equal(Air, Next(world, 10, Ground, 9));
        Assert.Equal(Air, Next(world, 10, Ground, 11));
    }

    [Fact]
    public void Given_TheNearerOfTwoDrops_When_TheSideCellsAreRead_Then_OnlyItsDirectionIsFed()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(13, Floor, 10, BlockId.Air); // 3 cells east
        world.Put(10, Floor, 7, BlockId.Air); // 3 cells north too
        world.Put(8, Floor, 10, BlockId.Air); // 2 cells west: the nearest

        Assert.Equal(Flowing(1), Next(world, 9, Ground, 10));
        Assert.Equal(Air, Next(world, 11, Ground, 10));
        Assert.Equal(Air, Next(world, 10, Ground, 9));
    }

    [Fact]
    public void Given_ADropWithinFourCells_When_TheSideCellsAreRead_Then_ItIsFound()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(14, Floor, 10, BlockId.Air); // the cell with the drop under it is 4 cells from the source

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
        Assert.Equal(Air, Next(world, 9, Ground, 10));
    }

    [Fact]
    public void Given_ADropFurtherThanFourCells_When_TheSideCellsAreRead_Then_EveryDirectionIsFed()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(15, Floor, 10, BlockId.Air); // 5 cells east: out of sight

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
        Assert.Equal(Flowing(1), Next(world, 9, Ground, 10));
        Assert.Equal(Flowing(1), Next(world, 10, Ground, 9));
        Assert.Equal(Flowing(1), Next(world, 10, Ground, 11));
    }

    [Fact]
    public void Given_ADropBehindAWall_When_TheSideCellsAreRead_Then_ItIsNotSeenThroughTheWall()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        for (var z = 6; z <= 14; z++)
        {
            world.Put(11, Ground, z, BlockId.Stone); // a wall, too long to go round in 4 cells
        }

        world.Put(12, Floor, 10, BlockId.Air);

        Assert.Equal(Flowing(1), Next(world, 9, Ground, 10)); // no drop is reachable, so water goes every open way
        Assert.Equal(Flowing(1), Next(world, 10, Ground, 9));
        Assert.Equal(new FlowCell(BlockId.Stone, WaterLevel.Source), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_FlowingWaterNothingFeeds_When_ItIsRead_Then_ItDrains()
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground, 10, WaterLevel.Flowing(3));

        Assert.Equal(Air, Next(world, 10, Ground, 10));
    }

    [Fact]
    public void Given_FlowingWaterThatIsFedByAThickerNeighbour_When_ItIsRead_Then_ItTakesTheBestLevel()
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(10, Ground, 10, BlockId.Water);
        world.Grid.SetWater(10, Ground, 10, WaterLevel.Flowing(6));
        world.Grid.SetBlock(9, Ground, 10, BlockId.Water);
        world.Grid.SetWater(9, Ground, 10, WaterLevel.Flowing(2));
        world.Grid.SetBlock(11, Ground, 10, BlockId.Water);
        world.Grid.SetWater(11, Ground, 10, WaterLevel.Flowing(4));

        Assert.Equal(Flowing(3), Next(world, 10, Ground, 10));
    }

    [Fact]
    public void Given_TwoSourcesWithAGapBetween_When_TheGapIsRead_Then_ItIsFlowingWaterAndNeverASource()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Source(12, Ground, 10);

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_ASource_When_ItIsRead_Then_ItNeverChanges()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Source(11, Ground, 10);

        Assert.Equal(new FlowCell(BlockId.Water, WaterLevel.Source), Next(world, 10, Ground, 10));
    }

    [Theory]
    [InlineData(BlockId.Stone)]
    [InlineData(BlockId.Planks)]
    [InlineData(BlockId.Trunk)]
    [InlineData(BlockId.Torch)]
    public void Given_ASolidBlockNextToASource_When_ItIsRead_Then_WaterDoesNotEnterIt(BlockId block)
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(11, Ground, 10, block);

        Assert.Equal(new FlowCell(block, WaterLevel.Source), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_UndergrowthNextToASource_When_ItIsRead_Then_WaterTakesItsPlace()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(11, Ground, 10, BlockId.TallGrass);

        Assert.Equal(Flowing(1), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_UndergrowthNothingFeeds_When_ItIsRead_Then_ItStays()
    {
        var world = new FlowWorld();
        world.Put(11, Ground, 10, BlockId.TallGrass);

        Assert.Equal(new FlowCell(BlockId.TallGrass, WaterLevel.Source), Next(world, 11, Ground, 10));
    }

    [Fact]
    public void Given_WaterAtTheEdgeOfTheWorld_When_ItIsRead_Then_NothingFlowsOutOfIt()
    {
        var world = new FlowWorld();
        world.Source(0, Ground, 10);

        Assert.Equal(Flowing(1), Next(world, 1, Ground, 10));
        Assert.Equal(0, FlowRules.PreferredDirections(world.Grid, 0, Ground, 10) & 0b1000); // no way west, out of the grid
        world.Source(5, 0, 10);
        Assert.False(FlowRules.CanGoDown(world.Grid, 5, 0, 10)); // the floor of the world holds water up
    }

    [Fact]
    public void Given_TheSameGrid_When_ReadTwice_Then_TheRulesAnswerTheSame()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Put(13, Floor, 10, BlockId.Air);

        Assert.Equal(Next(world, 11, Ground, 10), Next(world, 11, Ground, 10));
        Assert.Equal(FlowRules.PreferredDirections(world.Grid, 10, Ground, 10), FlowRules.PreferredDirections(world.Grid, 10, Ground, 10));
    }
}
