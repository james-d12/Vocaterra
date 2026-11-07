using Vocaterra.Game.Core.Tests.Water.Flow;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using static Vocaterra.Game.Core.Tests.Water.Flow.FlowWorld;

namespace Vocaterra.Game.Core.Tests.Water.Currents;

/// <summary>The flow vector (water-plan 4.6): none for a source, downhill for flowing water, down for falling water.</summary>
public sealed class FlowVectorTests
{
    private static void Flowing(FlowWorld world, int x, int y, int z, int distance, bool falling = false)
    {
        world.Grid.SetBlock(x, y, z, BlockId.Water);
        world.Grid.SetWater(x, y, z, WaterLevel.Flowing(distance, falling));
    }

    [Fact]
    public void Given_ASource_When_TheVectorIsRead_Then_ItIsZero()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        Flowing(world, 11, Ground, 10, 1);

        Assert.Equal(FlowVector.Zero, FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_ACellThatIsNotWater_When_TheVectorIsRead_Then_ItIsZero()
    {
        var world = new FlowWorld();

        Assert.Equal(FlowVector.Zero, FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_FlatWater_When_TheVectorIsRead_Then_ItIsZero()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 3);
        Flowing(world, 11, Ground, 10, 3);
        Flowing(world, 9, Ground, 10, 3);
        Flowing(world, 10, Ground, 9, 3);
        Flowing(world, 10, Ground, 11, 3);

        Assert.Equal(FlowVector.Zero, FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_AThinnerCellToTheEast_When_TheVectorIsRead_Then_ItPointsEast()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 2);
        Flowing(world, 11, Ground, 10, 3);

        Assert.Equal(new FlowVector(FlowVector.One, 0, 0), FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_ASlopeToTheNorth_When_TheVectorIsRead_Then_ItPointsNorth()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 2);
        Flowing(world, 10, Ground, 9, 5);
        Flowing(world, 10, Ground, 11, 1);

        Assert.Equal(new FlowVector(0, 0, -FlowVector.One), FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_ASlopeInTwoDirections_When_TheVectorIsRead_Then_ItIsTheirSumMadeAUnit()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 2);
        Flowing(world, 11, Ground, 10, 4);
        Flowing(world, 10, Ground, 11, 4);

        var vector = FlowVector.At(world.Grid, 10, Ground, 10);

        Assert.Equal(vector.X, vector.Z);
        Assert.InRange(vector.X, 179, 182); // 256 / sqrt 2
        Assert.Equal(0, vector.Y);
    }

    [Fact]
    public void Given_ASolidSide_When_TheVectorIsRead_Then_ItCountsForNothing()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 2);
        world.Put(11, Ground, 10, BlockId.Stone);
        Flowing(world, 9, Ground, 10, 4);

        Assert.Equal(new FlowVector(-FlowVector.One, 0, 0), FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_AnEdgeWithWaterBelow_When_TheVectorIsRead_Then_ItPointsOverTheEdge()
    {
        var world = new FlowWorld();
        world.Grid.SetBlock(12, Floor, 10, BlockId.Air); // a step down east of the cell at 11
        world.Source(12, Floor, 10);
        Flowing(world, 11, Ground, 10, 1);

        Assert.Equal(new FlowVector(FlowVector.One, 0, 0), FlowVector.At(world.Grid, 11, Ground, 10));
    }

    [Fact]
    public void Given_FallingWater_When_TheVectorIsRead_Then_ItPointsStraightDown()
    {
        var world = new FlowWorld();
        Flowing(world, 10, Ground, 10, 1, falling: true);

        Assert.Equal(new FlowVector(0, -FlowVector.One, 0), FlowVector.At(world.Grid, 10, Ground, 10));
    }

    [Fact]
    public void Given_AFlowingPlateau_When_TheSameCellIsReadTwice_Then_TheAnswerIsTheSame()
    {
        var world = new FlowWorld();
        world.Source(10, Ground, 10);
        world.Settle();

        Assert.Equal(FlowVector.At(world.Grid, 12, Ground, 10), FlowVector.At(world.Grid, 12, Ground, 10));
        Assert.Equal(new FlowVector(FlowVector.One, 0, 0), FlowVector.At(world.Grid, 12, Ground, 10) with { Z = 0 });
    }
}
