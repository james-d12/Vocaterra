using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>B07b: a teleport onto water (<c>--teleport</c>, the menu's teleport) puts the feet over the water's surface, in the real seed-42 world: the waterfall's pool and the lake by the camp.</summary>
public sealed class TeleportOntoWaterTests
{
    [Theory]
    [InlineData(258, 118)] // the waterfall's pool (the QA pass: it stood on the bed, y 38, under water)
    [InlineData(212, 256)] // the lake by the camp's trail
    public void Given_AColumnOfWater_When_TheFeetAreSet_Then_TheyAreOverTheSurfaceAndTheBodyFitsThere(int x, int z)
    {
        var grid = StreamedWorlds.Scene().Grid;

        var feet = GroundFinder.StandingAt(grid, x, z);

        Assert.NotNull(feet);
        var standing = feet!.Value;
        Assert.Equal(BlockId.Air, grid.GetBlock(standing.X, standing.Y, standing.Z));
        var below = grid.GetBlock(standing.X, standing.Y - 1, standing.Z);
        Assert.True(below == BlockId.Water || BlockDefinition.IsSolid(below), $"the feet are over {below}");
        if (below == BlockId.Water)
        {
            // the cell under the feet is the column's highest water: nothing wet over it, so the body floats in the open
            Assert.False(grid.BlocksBody(standing.X, standing.Y, standing.Z));
            Assert.False(grid.BlocksBody(standing.X, standing.Y + 1, standing.Z));
        }
    }

    [Fact]
    public void Given_ThePool_When_TheFeetAreSet_Then_TheyAreNotOnTheBed()
    {
        var grid = StreamedWorlds.Scene().Grid;

        var feet = GroundFinder.StandingAt(grid, 258, 118)!.Value;

        Assert.Equal(BlockId.Water, grid.GetBlock(feet.X, feet.Y - 1, feet.Z)); // the QA pass: y 38 was the bed, the water over it was a body's depth
        Assert.True(feet.Y > 38);
    }
}
