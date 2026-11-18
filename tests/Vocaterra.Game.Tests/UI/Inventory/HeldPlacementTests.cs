using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>I04b: the held stack is drawn even with no pointer, beside the cell it was picked up from.</summary>
public sealed class HeldPlacementTests
{
    private const int Icon = 32;

    private static InventoryLayout Layout => new(1280, 720, 30, 0, TestGame.Data.Equipment.Shape);

    [Fact]
    public void Given_APointer_When_TheHeldIconIsPlaced_Then_ItIsCentredOnIt()
    {
        var box = HeldPlacement.Icon(new Point(300, 200), new PixelBox(10, 10, 64, 80), Icon, 1280, 720);

        Assert.Equal(new PixelBox(284, 184, Icon, Icon), box);
    }

    [Fact]
    public void Given_NoPointerAndAnOriginCell_When_TheHeldIconIsPlaced_Then_ItIsBesideTheCellAndTheCellsOwnIconPlaceShowsEmpty()
    {
        var layout = Layout;
        var cell = layout.Cell(3);

        var box = HeldPlacement.Icon(null, cell, Icon, 1280, 720);

        var iconPlace = new PixelBox(cell.X + (cell.Width / 2) - (Icon / 2), cell.Y + 4, Icon, Icon); // where the cell draws its own icon
        Assert.True(box.X > cell.X + (cell.Width / 2), "the held icon sits on the cell's right half or beyond");
        Assert.True(box.X + Icon > cell.Right, "it reaches past the cell's edge");
        Assert.True(box.X >= iconPlace.Right, "it does not cover the cell's icon place");
        Assert.True(box.Bottom <= 720 && box.Right <= 1280);
    }

    [Fact]
    public void Given_NoPointerAndNoOrigin_When_TheHeldIconIsPlaced_Then_ItIsInTheMiddleOfTheCanvas()
    {
        Assert.Equal(new PixelBox(624, 344, Icon, Icon), HeldPlacement.Icon(null, null, Icon, 1280, 720));
    }

    [Theory]
    [InlineData(-50, -50)]
    [InlineData(5000, 5000)]
    public void Given_APointerOutsideTheCanvas_When_TheHeldIconIsPlaced_Then_ItIsKeptInsideIt(int x, int y)
    {
        var box = HeldPlacement.Icon(new Point(x, y), null, Icon, 1280, 720);

        Assert.True(box.X >= 0 && box.Y >= 0 && box.Right <= 1280 && box.Bottom <= 720);
    }

    [Fact]
    public void Given_AnOriginOnTheRightEdgeOfTheCanvas_When_TheHeldIconIsPlaced_Then_ItIsKeptInside()
    {
        var box = HeldPlacement.Icon(null, new PixelBox(1230, 600, 48, 48), Icon, 1280, 720);

        Assert.True(box.Right <= 1280);
    }

    [Fact]
    public void Given_EachContainer_When_TheOriginCellIsAsked_Then_ItIsTheBagCellTheDollCellOrTheTrashAndNothingForAnUnknownOne()
    {
        var layout = Layout;
        var cell = TestGame.Data.Equipment.Shape.Cells[5];

        Assert.Equal(layout.Cell(7), HeldPlacement.OriginCell(layout, ContainerIds.Bag, 7, 30));
        Assert.Equal(layout.EquipmentCellBox(cell), HeldPlacement.OriginCell(layout, ContainerIds.Equipment, cell.Flat, 30));
        Assert.Equal(layout.TrashCell, HeldPlacement.OriginCell(layout, ContainerIds.Trash, 0, 30));
        Assert.Null(HeldPlacement.OriginCell(layout, ContainerIds.Bag, 99, 30));
        Assert.Null(HeldPlacement.OriginCell(layout, ContainerIds.Held, 0, 30));
    }
}
