using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The height of the water's top inside a cell.</summary>
public sealed class WaterSurfaceTests
{
    [Fact]
    public void Given_ASourceWithAirAbove_When_Measured_Then_ItIsFourteenSixteenths()
    {
        Assert.Equal(14, WaterSurface.Sixteenths(WaterLevel.Source, waterAbove: false));
        Assert.Equal(14 * 256, WaterSurface.Units(WaterLevel.Source, waterAbove: false));
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(2, 10)]
    [InlineData(3, 8)]
    [InlineData(4, 7)]
    [InlineData(5, 5)]
    [InlineData(6, 3)]
    [InlineData(7, 2)]
    public void Given_FlowingWater_When_Measured_Then_ItFallsLinearlyFromTwelveToTwoSixteenths(int distance, int expected)
    {
        Assert.Equal(expected, WaterSurface.Sixteenths(WaterLevel.Flowing(distance), waterAbove: false));
    }

    [Fact]
    public void Given_Flowing_When_Measured_Then_ItDecreasesWithDistance()
    {
        var last = WaterSurface.Sixteenths(WaterLevel.Source, false);
        for (var d = 1; d <= WaterLevel.MaxDistance; d++)
        {
            var now = WaterSurface.Sixteenths(WaterLevel.Flowing(d), false);
            Assert.True(now < last);
            last = now;
        }
    }

    [Fact]
    public void Given_FallingWaterOrWaterAbove_When_Measured_Then_ItIsFull()
    {
        Assert.Equal(16, WaterSurface.Sixteenths(WaterLevel.Flowing(5, falling: true), false));
        Assert.Equal(16, WaterSurface.Sixteenths(WaterLevel.Flowing(7), true));
        Assert.Equal(16, WaterSurface.Sixteenths(WaterLevel.Source, true));
        Assert.Equal(4096, WaterSurface.Units(WaterLevel.Source, true));
    }
}
