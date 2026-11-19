using Vocaterra.Game.Core.Items;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>The inventory screen's hit-test (I02): a point is on a bag slot, the sort button, the trash cell or nothing, at every UI scale and for three to five rows, with and without the needs' list.</summary>
public sealed class InventoryLayoutHitTests
{
    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in new[] { (1280, 720), (800, 600) })
            {
                foreach (var scale in new[] { 100, 125, 150 })
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyScale_When_ThePointIsInTheMiddleOfATarget_Then_TheHitNamesIt(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        foreach (var slots in new[] { 30, 40, 50 })
        {
            foreach (var needs in new[] { 0, 5 })
            {
                var layout = new InventoryLayout(width, height, slots, needs);
                for (var slot = 0; slot < slots; slot++)
                {
                    var cell = layout.Cell(slot);
                    Assert.Equal(new InventoryHit(InventoryHitKind.Cell, slot), layout.Hit(cell.X + (cell.Width / 2), cell.Y + (cell.Height / 2), slots));
                }

                Assert.Equal(InventoryHitKind.Sort, layout.Hit(layout.SortButton.X + (layout.SortButton.Width / 2), layout.SortButton.Y + (layout.SortButton.Height / 2), slots).Kind);
                Assert.Equal(InventoryHitKind.Trash, layout.Hit(layout.TrashCell.X + (layout.TrashCell.Width / 2), layout.TrashCell.Y + (layout.TrashCell.Height / 2), slots).Kind);
                Assert.Equal(InventoryHitKind.None, layout.Hit(-5, -5, slots).Kind);
                Assert.Equal(InventoryHitKind.None, layout.Hit(layout.Panel.X + 1, layout.Panel.Bottom - 1, slots).Kind);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyScale_When_TheLayoutIsMade_Then_TheSortButtonAndTheTrashCellAreUnderTheBagInsideThePanelAndClearOfEachOther(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var canvas = new PixelBox(0, 0, width, height);
        foreach (var slots in new[] { 30, 40, 50 })
        {
            foreach (var needs in new[] { 0, 5 })
            {
                var layout = new InventoryLayout(width, height, slots, needs);
                foreach (var box in new[] { layout.SortButton, layout.TrashCell })
                {
                    Assert.True(box.X >= layout.Panel.X && box.Right <= layout.Panel.Right && box.Y >= layout.Panel.Y && box.Bottom <= layout.Panel.Bottom, $"{box} is outside {layout.Panel}");
                    Assert.True(box.X >= canvas.X && box.Right <= canvas.Right && box.Y >= canvas.Y);
                    Assert.True(box.Width >= 16 && box.Height >= 16, $"{box} is too small to click");
                    Assert.True(box.Y >= layout.Cell(slots - 1).Bottom, $"{box} reaches the last row of slots");
                    Assert.True(box.Bottom <= layout.NeedsTop, $"{box} reaches the needs' list");
                }

                Assert.True(layout.SortButton.Right <= layout.TrashCell.X, "the sort button overlaps the trash cell");
            }
        }
    }

    [Fact]
    public void Given_AFewerSlotsThanTheLayoutHolds_When_ThePointIsOnAnInactiveCell_Then_NothingIsHit()
    {
        var layout = new InventoryLayout(1280, 720, 40);
        var cell = layout.Cell(35);

        Assert.Equal(InventoryHitKind.None, layout.Hit(cell.X + 2, cell.Y + 2, 30).Kind);
    }
}
