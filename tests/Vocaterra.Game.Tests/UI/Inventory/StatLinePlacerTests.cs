using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>The stats line's chips (I04): where they go in their room, and the icons they carry.</summary>
public sealed class StatLinePlacerTests
{
    [Fact]
    public void Given_ChipsThatFit_When_Placed_Then_TheyRunLeftToRightInOneRow()
    {
        var boxes = StatLinePlacer.Place([40, 50], new PixelBox(10, 100, 200, 36), 18, 0);

        Assert.Equal([new PixelBox(10, 100, 40, 18), new PixelBox(50, 100, 50, 18)], boxes);
    }

    [Fact]
    public void Given_ChipsThatDoNotFitARow_When_Placed_Then_TheyWrapAndTheOnesWithNoRowAreLeftOut()
    {
        var boxes = StatLinePlacer.Place([60, 60, 60, 60, 60], new PixelBox(0, 0, 130, 36), 18, 0);

        Assert.Equal(4, boxes.Count);
        Assert.Equal([0, 60, 0, 60], boxes.Select(static b => b.X));
        Assert.Equal([0, 0, 18, 18], boxes.Select(static b => b.Y));
    }

    [Fact]
    public void Given_NoRoom_When_Placed_Then_NothingIsPlaced()
    {
        Assert.Empty(StatLinePlacer.Place([20], new PixelBox(0, 0, 0, 0), 18, 0));
        Assert.Empty(StatLinePlacer.Place([], new PixelBox(0, 0, 100, 36), 18, 0));
    }

    [Fact]
    public void Given_TheStatsTheLineShows_When_TheirIconsAreAsked_Then_EachHasItsOwnSevenBySevenPicture()
    {
        foreach (var display in StatDisplay.Defaults)
        {
            var rows = StatIcons.Of(display.Key);

            Assert.NotSame(StatIcons.Fallback, rows);
            Assert.Equal(StatIcons.Size, rows.Count);
            Assert.All(rows, row => Assert.Equal(StatIcons.Size, row.Length));
            Assert.Contains(rows, static row => row.Contains('#'));
        }

        Assert.Same(StatIcons.Fallback, StatIcons.Of("light"));
    }
}
