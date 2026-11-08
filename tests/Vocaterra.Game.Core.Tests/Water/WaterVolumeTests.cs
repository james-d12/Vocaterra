using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The water volume on a small hand-built pool in the column (32, 32), ground at y 0 to 9.</summary>
public sealed class WaterVolumeTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Ground = VoxelTestSupport.GroundTop;

    private static VoxelGrid Pool(int layers, WaterLevel? topLevel = null)
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var i = 1; i <= layers; i++)
        {
            grid.SetBlock(32, Ground + i, 32, BlockId.Water);
        }

        if (topLevel is { } level)
        {
            grid.SetWater(32, Ground + layers, 32, level);
        }

        return grid;
    }

    [Fact]
    public void Given_ASourceCell_When_ThePointIsBelowItsSurface_Then_ItIsInTheWater()
    {
        var grid = Pool(1);
        var floor = (Ground + 1) * U;

        Assert.True(WaterVolume.Contains(grid, (32 * U) + 100, floor + 10, (32 * U) + 100));
        Assert.True(WaterVolume.Contains(grid, (32 * U) + 100, floor + (14 * (U / 16)) - 1, (32 * U) + 100));
    }

    [Fact]
    public void Given_ASourceCell_When_ThePointIsAtOrAboveItsSurface_Then_ItIsDry()
    {
        var grid = Pool(1);
        var surface = ((Ground + 1) * U) + (14 * (U / 16));

        Assert.False(WaterVolume.Contains(grid, (32 * U) + 100, surface, (32 * U) + 100));
        Assert.False(WaterVolume.Contains(grid, (32 * U) + 100, ((Ground + 1) * U) + U - 1, (32 * U) + 100));
    }

    [Fact]
    public void Given_WaterAboveACell_When_ThePointIsHighInTheLowerCell_Then_ItIsInTheWater()
    {
        var grid = Pool(2);

        Assert.True(WaterVolume.Contains(grid, (32 * U) + 100, ((Ground + 1) * U) + U - 1, (32 * U) + 100));
    }

    [Fact]
    public void Given_ALowFlowingCell_When_ThePointIsAboveItsSurface_Then_ItIsDry()
    {
        var grid = Pool(1, WaterLevel.Flowing(7)); // 2/16 of a block
        var floor = (Ground + 1) * U;

        Assert.True(WaterVolume.Contains(grid, (32 * U) + 100, floor + (U / 16), (32 * U) + 100));
        Assert.False(WaterVolume.Contains(grid, (32 * U) + 100, floor + (U / 4), (32 * U) + 100));
    }

    [Fact]
    public void Given_AFallingCell_When_ThePointIsAnywhereInIt_Then_ItIsInTheWater()
    {
        var grid = Pool(1, WaterLevel.Flowing(7, falling: true));

        Assert.True(WaterVolume.Contains(grid, (32 * U) + 100, ((Ground + 1) * U) + U - 1, (32 * U) + 100));
    }

    [Fact]
    public void Given_ADryCellOrNegativeUnits_When_Asked_Then_ItIsDryWithoutThrowing()
    {
        var grid = Pool(1);

        Assert.False(WaterVolume.Contains(grid, 10 * U, (Ground + 1) * U, 10 * U));
        Assert.False(WaterVolume.Contains(grid, -1, -1, -1));
        Assert.False(WaterVolume.Contains(grid, 1000 * U, 1000 * U, 1000 * U));
    }
}
