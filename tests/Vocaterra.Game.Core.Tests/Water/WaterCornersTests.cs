using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The height of a corner of a water surface: the average of the four cells that meet there (water-plan 4.8).</summary>
public sealed class WaterCornersTests
{
    private static CornerCell Source => CornerCell.Water(WaterLevel.Source);

    private static CornerCell Flowing(int distance) => CornerCell.Water(WaterLevel.Flowing(distance));

    [Fact]
    public void Given_FourSources_When_TheCornerIsMeasured_Then_ItIsFourteenSixteenths()
    {
        Assert.Equal(14 * 16, WaterCorners.Height(Source, Source, Source, Source));
    }

    [Fact]
    public void Given_ALoneSourceInOpenAir_When_TheCornerIsMeasured_Then_ItSlopesDownToTheOpenEdge()
    {
        // One source (weight 10, 14/16) and three open cells (weight 1 each, nothing): 140 / 13 sixteenths.
        Assert.Equal(140 * 16 / 13, WaterCorners.Height(Source, CornerCell.Open, CornerCell.Open, CornerCell.Open));
    }

    [Fact]
    public void Given_ASourceBesideSolidBlocks_When_TheCornerIsMeasured_Then_TheSolidBlocksDoNotCount()
    {
        Assert.Equal(14 * 16, WaterCorners.Height(Source, CornerCell.Solid, CornerCell.Solid, CornerCell.Solid));
    }

    [Fact]
    public void Given_ASourceBesideAThinFlowingCell_When_TheCornerIsMeasured_Then_TheSourceWeighsTenTimesMore()
    {
        // Distance 7 is 2/16: (14 * 10 + 2 * 1) / 11 sixteenths, not the plain mean of 8.
        Assert.Equal(((14 * 10) + 2) * 16 / 11, WaterCorners.Height(Source, Flowing(7), CornerCell.Solid, CornerCell.Solid));
    }

    [Fact]
    public void Given_TwoFlowingCellsBetweenWalls_When_TheCornerIsMeasured_Then_ItIsTheirMean()
    {
        // Distance 1 is 12/16 and distance 3 is 8/16, both light: (12 + 8) / 2 = 10 sixteenths.
        Assert.Equal(10 * 16, WaterCorners.Height(Flowing(1), Flowing(3), CornerCell.Solid, CornerCell.Solid));
    }

    [Fact]
    public void Given_ASlope_When_CornersAreMeasuredDownIt_Then_TheyNeverRise()
    {
        var last = int.MaxValue;
        var cells = new[] { Source }.Concat(Enumerable.Range(1, WaterLevel.MaxDistance).Select(Flowing)).ToList();
        for (var i = 0; i + 1 < cells.Count; i++)
        {
            var corner = WaterCorners.Height(cells[i], cells[i + 1], CornerCell.Solid, CornerCell.Solid);
            Assert.True(corner < last, $"corner {i} is {corner}, not below {last}");
            last = corner;
        }
    }

    [Fact]
    public void Given_AFallingCell_When_TheCornerIsMeasured_Then_ItCountsAsFull()
    {
        var falling = CornerCell.Water(WaterLevel.Flowing(2, falling: true));

        Assert.Equal(16 * 16, WaterCorners.Height(falling, CornerCell.Solid, CornerCell.Solid, CornerCell.Solid));
    }

    [Fact]
    public void Given_WaterAboveOneOfTheCells_When_TheCornerIsMeasured_Then_ItIsFull()
    {
        var covered = CornerCell.Water(WaterLevel.Source, waterAbove: true);

        Assert.Equal(WaterCorners.PerBlock, WaterCorners.Height(Source, covered, CornerCell.Open, CornerCell.Open));
        Assert.Equal(WaterCorners.PerBlock, WaterCorners.Height(Source, Source, CornerCell.Solid with { WaterAbove = true }, Source));
    }
}
