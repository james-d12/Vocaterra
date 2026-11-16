using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Inventory;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>The equipment panel's geometry (I04): the doll, the bag, the buttons, the stats line and the needs' list at every window size and scale, for three to five rows, in all three layouts; the hit-test; and a group added to the data.</summary>
public sealed class InventoryEquipmentLayoutTests(ITestOutputHelper output)
{
    private static readonly (int Width, int Height)[] Windows = [(1920, 1080), (1280, 720), (800, 600), (640, 480)];
    private static readonly int[] Scales = [100, 125, 150];
    private static readonly int[] Slots = [30, 40, 50];

    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in Windows)
            {
                foreach (var scale in Scales)
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    private static EquipmentShape Shipped => TestGame.Data.Equipment.Shape;

    private static bool Inside(PixelBox box, PixelBox outer) => box.X >= outer.X && box.Y >= outer.Y && box.Right <= outer.Right && box.Bottom <= outer.Bottom;

    private static bool Apart(PixelBox a, PixelBox b) => a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y;

    // The one combination no layout can hold: five rows and the five needs on the smallest canvas (it was so before the doll too; the panel is then cut at the canvas's foot).
    private static bool Holdable(InventoryLayout layout, int slots, int needs) => !(layout.Tight && slots == 50 && needs > 0);

    private static IEnumerable<PixelBox> Boxes(InventoryLayout layout, int slots) =>
        Enumerable.Range(0, slots).Select(layout.Cell)
            .Concat(layout.Shape.Cells.Select(layout.EquipmentCellBox))
            .Concat([layout.SortButton, layout.TrashCell]);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyWindowAndScale_When_TheLayoutIsMade_Then_EveryPartIsInsideThePanelAndTheCanvasAndNothingOverlaps(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var canvas = new PixelBox(0, 0, width, height);
        foreach (var slots in Slots)
        {
            foreach (var needs in new[] { 0, 5 })
            {
                var layout = new InventoryLayout(width, height, slots, needs, Shipped);
                output.WriteLine($"{width}x{height} slots {slots} needs {needs}: compact {layout.Compact} tight {layout.Tight}, panel {layout.Panel}, bag {layout.CellWidth}x{layout.CellHeight}");
                if (!Holdable(layout, slots, needs))
                {
                    continue;
                }

                Assert.True(Inside(layout.Panel, canvas), $"the panel {layout.Panel} is outside the canvas {width}x{height}");
                var boxes = Boxes(layout, slots).ToList();
                Assert.All(boxes, box => Assert.True(Inside(box, layout.Panel), $"{box} is outside the panel {layout.Panel}"));
                for (var i = 0; i < boxes.Count; i++)
                {
                    Assert.True(boxes[i].Y >= layout.Panel.Y + layout.DividerY, $"{boxes[i]} reaches the title's rule");
                    for (var j = i + 1; j < boxes.Count; j++)
                    {
                        Assert.True(Apart(boxes[i], boxes[j]), $"{boxes[i]} and {boxes[j]} overlap");
                    }
                }

                Assert.True(Inside(layout.StatsArea, layout.Panel));
                Assert.All(layout.Shape.Cells, cell => Assert.True(layout.EquipmentCellBox(cell).Bottom <= layout.StatsArea.Y, "a doll cell reaches the stats line"));
                Assert.True(boxes.Max(static b => b.Bottom) <= layout.NeedsTop, "the needs' list starts under every cell and button");
                if (needs > 0)
                {
                    Assert.True(layout.NeedsTop + (needs * layout.NeedsRowStep) <= layout.Panel.Bottom - layout.HintsOffset, "the needs' list ends above the key hints");
                }

                Assert.True(layout.Panel.Bottom - layout.HintsOffset + 14 <= layout.Panel.Bottom, "the key hints stay inside the panel");
                Assert.True(layout.SortButton.Width >= 20 && layout.TrashCell.Width >= 20, "the buttons are too small to click");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyWindowAndScale_When_TheMiddleOfACellIsHit_Then_TheHitNamesItWithTheFlatIndexForTheDoll(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        foreach (var slots in Slots)
        {
            var layout = new InventoryLayout(width, height, slots, 0, Shipped);
            foreach (var cell in layout.Shape.Cells)
            {
                var box = layout.EquipmentCellBox(cell);
                Assert.Equal(new InventoryHit(InventoryHitKind.Equipment, cell.Flat), layout.Hit(box.X + (box.Width / 2), box.Y + (box.Height / 2), slots));
            }

            for (var slot = 0; slot < slots; slot++)
            {
                var box = layout.Cell(slot);
                Assert.Equal(new InventoryHit(InventoryHitKind.Cell, slot), layout.Hit(box.X + (box.Width / 2), box.Y + (box.Height / 2), slots));
            }

            Assert.Equal(InventoryHitKind.Sort, layout.Hit(layout.SortButton.X + 2, layout.SortButton.Y + 2, slots).Kind);
            Assert.Equal(InventoryHitKind.Trash, layout.Hit(layout.TrashCell.X + 2, layout.TrashCell.Y + 2, slots).Kind);
            Assert.Equal(InventoryHitKind.None, layout.Hit(layout.StatsArea.X + 1, layout.StatsArea.Y + 1, slots).Kind);
            Assert.Equal(InventoryHitKind.None, layout.Hit(-1, -1, slots).Kind);
        }
    }

    [Theory]
    [InlineData(1920, 1080, 40, 5, false, false)]
    [InlineData(1280, 720, 30, 5, false, false)]
    [InlineData(1280, 720, 40, 5, false, false)]
    [InlineData(1024, 576, 50, 5, true, false)]
    [InlineData(800, 600, 50, 5, true, false)]
    [InlineData(640, 480, 40, 5, true, true)]
    [InlineData(533, 400, 30, 5, true, true)]
    [InlineData(533, 400, 40, 5, true, true)]
    public void Given_ACanvas_When_TheLayoutIsMade_Then_TheSizeIsTheFirstOfRegularCompactAndTightThatFits(int width, int height, int slots, int needs, bool compact, bool tight)
    {
        var layout = new InventoryLayout(width, height, slots, needs, Shipped);

        Assert.Equal((compact, tight), (layout.Compact, layout.Tight));
        Assert.Equal(!layout.Compact, layout.HasLabelColumn); // the body rows' nouns stand beside the doll in the regular size only
    }

    [Fact]
    public void Given_TheSmallestCanvasAndFortySlots_When_TheLayoutIsMade_Then_TheWholePanelIsInsideIt()
    {
        var layout = new InventoryLayout(UiScale.MinWidth, UiScale.MinHeight, 40, 5, Shipped);

        Assert.True(Inside(layout.Panel, new PixelBox(0, 0, UiScale.MinWidth, UiScale.MinHeight)));
        Assert.True(layout.Panel.Bottom <= UiScale.MinHeight - InventoryLayout.Margin);
    }

    [Fact]
    public void Given_ABagOfSeveralRows_When_TheLayoutIsMade_Then_TheHotbarRowIsSetApartFromThePackRows()
    {
        var layout = new InventoryLayout(1280, 800, 40, 0, Shipped);

        Assert.True(layout.HotbarGap > 0);
        Assert.Equal(layout.CellHeight + layout.Gap + layout.HotbarGap, layout.Cell(10).Y - layout.Cell(0).Y);
        Assert.Equal(layout.CellHeight + layout.Gap, layout.Cell(20).Y - layout.Cell(10).Y);
        Assert.Equal(0, new InventoryLayout(1280, 800, 10).HotbarGap);
    }

    [Fact]
    public void Given_TheDollAndTheBag_When_TheLayoutIsMade_Then_TheDollIsOnTheLeftAndTheButtonsAreUnderTheBagAtItsRightEdge()
    {
        var layout = new InventoryLayout(1280, 800, 40, 0, Shipped);

        Assert.True(layout.Shape.Cells.Select(layout.EquipmentCellBox).All(b => b.Right <= layout.GridLeft));
        Assert.True(layout.SortButton.Y >= layout.Cell(39).Bottom && layout.TrashCell.Y >= layout.Cell(39).Bottom);
        Assert.Equal(layout.Cell(9).Right, layout.TrashCell.Right);
        Assert.True(layout.SortButton.Right <= layout.TrashCell.X);
    }

    [Fact]
    public void Given_NoEquipmentShape_When_TheLayoutIsMade_Then_ThePanelHasNoDollAndTheBagStandsAlone()
    {
        var layout = new InventoryLayout(1280, 800, 40);

        Assert.Empty(layout.Shape.Cells);
        Assert.Equal(0, layout.StatsArea.Width);
        Assert.False(layout.HasLabelColumn);
        Assert.True(layout.Cell(0).X - layout.Panel.X <= (layout.Panel.Width - ((InventoryLayout.Columns * layout.CellWidth) + ((InventoryLayout.Columns - 1) * layout.Gap))) / 2 + 1);
    }

    [Fact]
    public void Given_TheShippedShape_When_TheRowLabelsAreAsked_Then_TheRegularSizeHasABoxForEachBodyRowBesideTheCells()
    {
        var layout = new InventoryLayout(1920, 1080, 40, 0, Shipped);

        for (var row = 0; row < Shipped.BodyRows; row++)
        {
            var box = layout.RowLabelBox(row);
            var firstCell = layout.EquipmentCellBox(Shipped.Cells.First(c => c.Row == row));
            Assert.True(box.Width > 0 && box.Right <= firstCell.X);
            Assert.Equal(firstCell.Y, box.Y);
        }

        Assert.Equal(0, new InventoryLayout(640, 480, 40, 5, Shipped).RowLabelBox(0).Width);
    }

    // ---- A group added to equipment.json gets its cells with no change to the layout.

    private static EquipmentData WithSeventhGroups()
    {
        var shipped = TestGame.Data.Equipment;
        var extra = EquipmentData.Parse("""
            {"schemaVersion":1,"groups":[
              {"id":"charms","layer":4,"slots":["head","feet"],"accepts":["charm"],"noDuplicates":false,"statsApply":true},
              {"id":"pockets","count":2,"wear":"pocket","accepts":["pocket"],"noDuplicates":false,"statsApply":true}]}
            """);
        return new EquipmentData([.. shipped.Groups, .. extra.Groups], shipped.BagSlots);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_ExtraGroupsInTheData_When_TheLayoutIsMade_Then_EachNewCellHasABoxInsideThePanelAndAHitThatNamesItsFlatIndex(int windowWidth, int windowHeight, int scale)
    {
        var data = WithSeventhGroups();
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var canvas = new PixelBox(0, 0, width, height);
        foreach (var slots in new[] { 30, 40 })
        {
            var layout = new InventoryLayout(width, height, slots, 5, data.Shape);
            var boxes = Boxes(layout, slots).ToList();

            Assert.Equal(data.SlotCount, layout.Shape.Cells.Count);
            Assert.True(Inside(layout.Panel, canvas), $"{layout.Panel} is outside {canvas}");
            Assert.All(boxes, box => Assert.True(Inside(box, layout.Panel)));
            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.True(Apart(boxes[i], boxes[j]), $"{boxes[i]} and {boxes[j]} overlap");
                }
            }

            foreach (var flat in Enumerable.Range(Shipped.Cells.Count, data.SlotCount - Shipped.Cells.Count))
            {
                var box = layout.EquipmentCellBox(layout.Shape.Cells.Single(c => c.Flat == flat));
                Assert.Equal(new InventoryHit(InventoryHitKind.Equipment, flat), layout.Hit(box.X + 1, box.Y + 1, slots));
            }

            Assert.True(boxes.Max(static b => b.Bottom) <= layout.NeedsTop);
        }
    }
}
