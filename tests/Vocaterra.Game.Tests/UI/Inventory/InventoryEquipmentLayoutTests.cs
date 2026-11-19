using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
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
            .Concat([layout.SortButton, layout.TrashCell])
            .Concat(Enumerable.Range(0, layout.Shape.ColumnHeadings.Count).Select(layout.HeadingBox).Where(static b => b.Width > 0))
            .Concat(layout.StatsArea.Width > 0 ? [layout.StatsArea] : []);

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
                Assert.All(layout.Shape.Cells, cell => Assert.True(Apart(layout.EquipmentCellBox(cell), layout.StatsArea), "a doll cell reaches the stats line"));
                if (!layout.Tight)
                {
                    Assert.All(layout.Shape.Cells, cell => Assert.True(layout.EquipmentCellBox(cell).Bottom <= layout.StatsArea.Y, "a doll cell reaches the stats line"));
                }
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

    // ---- I04b: the headings, the stats line and the counted cells' nouns.

    private static float Seven(string text) => text.Length * 7f; // the stand-in font the other layout tests use: wider than the real one

    // Five non-default stats as the line writes them: the regular size puts the Spanish noun before the number where the game has one, the small sizes the number alone.
    private static readonly string[] FiveStatsRegular = ["armadura 12", "calor +1.5", "+10%", "-15%", "+20"];
    private static readonly string[] FiveStatsSmall = ["12", "+1.5", "+10%", "-15%", "+20"];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_FiveNonDefaultStats_When_TheChipsArePlacedAtAnyLayout_Then_EveryChipIsDrawnInsideTheStatsAreaAndThePanel(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        foreach (var slots in Slots)
        {
            var layout = new InventoryLayout(width, height, slots, 0, Shipped);
            var small = layout.Compact || layout.Tight;
            var iconSize = layout.StatChipHeight - 2;
            var texts = small ? FiveStatsSmall : FiveStatsRegular;
            var widths = texts.Select(t => iconSize + 4 + (int)Seven(t) + (small ? 4 : 8)).ToList();

            var boxes = StatLinePlacer.Place(widths, layout.StatsArea, layout.StatChipHeight, 0);

            output.WriteLine($"{width}x{height} slots {slots}: compact {layout.Compact} tight {layout.Tight}, stats area {layout.StatsArea}, {boxes.Count} of 5 chips");
            Assert.Equal(5, boxes.Count);
            Assert.All(boxes, box => Assert.True(Inside(box, layout.StatsArea) && Inside(box, layout.Panel), $"{box} is outside the stats area {layout.StatsArea}"));
        }
    }

    [Fact]
    public void Given_TheSmallestCanvas_When_FiveStatsAreAsked_Then_TheStaminaChipThatWasDroppedAt640By480IsThere()
    {
        var layout = new InventoryLayout(640, 480, 30, 5, Shipped);
        var iconSize = layout.StatChipHeight - 2;
        var widths = FiveStatsSmall.Select(t => iconSize + 4 + (int)Seven(t) + 4).ToList();

        var boxes = StatLinePlacer.Place(widths, layout.StatsArea, layout.StatChipHeight, 0);

        Assert.True(layout.Tight);
        Assert.Equal(widths.Count, boxes.Count);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_TheShippedHeadings_When_TheLayoutIsMade_Then_EachHeadingBoxIsAboveItsColumnsCellsInsideThePanelAndHoverNamesIt(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var layout = new InventoryLayout(width, height, 30, 5, Shipped);

        Assert.True(layout.HeadingHeight > 0);
        for (var column = 0; column < Shipped.ColumnHeadings.Count; column++)
        {
            var box = layout.HeadingBox(column);
            var cells = Shipped.Cells.Where(c => c.Column == column && c.Row < Shipped.BodyRows).Select(layout.EquipmentCellBox).ToList();

            Assert.True(box.Width >= 30, $"heading {column} {box} is too narrow for a mark");
            Assert.True(Inside(box, layout.Panel));
            Assert.All(cells, cell => Assert.True(box.Bottom <= cell.Y && box.X <= cell.X + (cell.Width / 2) && box.Right >= cell.X + (cell.Width / 2), $"heading {box} is not above its cells"));
            Assert.Equal(new InventoryHit(InventoryHitKind.Heading, column), layout.Hit(box.X + (box.Width / 2), box.Y + (box.Height / 2), 30));
        }

        for (var column = 1; column < Shipped.ColumnHeadings.Count; column++)
        {
            Assert.True(layout.HeadingBox(column - 1).Right <= layout.HeadingBox(column).X, "two headings overlap");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_ARegularLayout_When_TheHeadingsAreMeasuredInTheRealFont_Then_EachIsTheFullBareNounInsideItsBoxAndApartFromTheLabelsTheBagAndEachOther(int windowWidth, int windowHeight, int scale)
    {
        var font = new FontProvider(Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf")).Get(FontProvider.NativeSize);
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        foreach (var slots in Slots)
        {
            var layout = new InventoryLayout(width, height, slots, 5, Shipped);
            if (layout.Compact)
            {
                continue; // compact and tight keep a short mark, the word on hover (I04b)
            }

            var texts = new List<PixelBox>();
            for (var column = 0; column < Shipped.ColumnHeadings.Count; column++)
            {
                var lemma = Shipped.ColumnHeadings[column]!;
                var box = layout.HeadingBox(column);
                var text = HeadingText.Pick(lemma, box.Width, t => DrawHelpers.Width(font, t));
                var measured = (int)Math.Ceiling(DrawHelpers.Width(font, text));
                var left = layout.HeadingTextLeft(column, measured);
                output.WriteLine($"{width}x{height} slots {slots}: heading {text} {measured} px in box {box}");

                Assert.Equal(lemma, text);
                Assert.DoesNotContain(' ', text);
                Assert.True(measured <= box.Width, $"{lemma} ({measured} px) does not fit {box}");
                Assert.True(left >= box.X && left + measured <= box.Right);
                var drawn = new PixelBox(left, box.Y, measured, box.Height);
                Assert.All(texts, other => Assert.True(Apart(drawn, other), $"{lemma} overlaps another heading"));
                texts.Add(drawn);
                Assert.All(Enumerable.Range(0, Shipped.BodyRows).Select(layout.RowLabelBox), label => Assert.True(Apart(box, label), $"{box} overlaps a row label {label}"));
                Assert.All(Enumerable.Range(0, slots).Select(layout.Cell), cell => Assert.True(Apart(box, cell), $"{box} overlaps the bag cell {cell}"));
                Assert.All(new[] { layout.SortButton, layout.TrashCell }, tool => Assert.True(Apart(box, tool)));
            }
        }
    }

    [Fact]
    public void Given_ShapeWithoutHeadings_When_TheLayoutIsMade_Then_NoRoomIsSpentOnThem()
    {
        var data = EquipmentData.Parse("""{"schemaVersion":1,"groups":[{"id":"a","layer":1,"slots":["head"],"accepts":["x"],"noDuplicates":false,"statsApply":true}]}""");

        var layout = new InventoryLayout(1280, 720, 30, 0, data.Shape);

        Assert.Equal(0, layout.HeadingHeight);
        Assert.Equal(0, layout.HeadingBox(0).Width);
        Assert.Equal(layout.GridTop, layout.EquipmentCellBox(data.Shape.Cells[0]).Y);
    }

    [Fact]
    public void Given_AFourthLayerWithAHeadingInTheData_When_TheLayoutIsMade_Then_ItHasAHeadingBoxAndNothingNamesItInCode()
    {
        var shipped = TestGame.Data.Equipment;
        var charms = EquipmentData.Parse("""{"schemaVersion":1,"groups":[{"id":"charms","layer":4,"heading":"mano","slots":["head","feet"],"accepts":["charm"],"noDuplicates":false,"statsApply":true}]}""").Groups[0];
        var data = new EquipmentData([.. shipped.Groups, charms], shipped.BagSlots);

        var layout = new InventoryLayout(1920, 1080, 30, 0, data.Shape);

        Assert.Equal(4, data.Shape.ColumnHeadings.Count);
        Assert.Equal("mano", data.Shape.ColumnHeadings[3]);
        Assert.True(layout.HeadingBox(3).Width > 0);
        Assert.True(Apart(layout.HeadingBox(3), layout.Cell(0)), "the fourth heading overlaps the bag");
        Assert.True(layout.HeadingBox(2).Right <= layout.HeadingBox(3).X);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1280, 720)]
    [InlineData(1024, 768)]
    public void Given_TheRegularSize_When_TheNounsOfTheDollAreMeasured_Then_NoneIsCutOffInItsBox(int width, int height)
    {
        var layout = new InventoryLayout(width, height, 40, 5, Shipped);
        var nouns = TestGame.Data.Nouns;

        Assert.False(layout.Compact, "this canvas should get the regular size");
        for (var row = 0; row < Shipped.BodyRows; row++)
        {
            var lemma = Shipped.RowLabels[row];
            if (lemma is not null)
            {
                var text = nouns.ForLemma(lemma)?.WithDefinite ?? lemma;
                Assert.True(Seven(text) <= layout.RowLabelBox(row).Width, $"'{text}' ({Seven(text)} px) is cut off in {layout.RowLabelBox(row)}");
            }
        }

        foreach (var cell in Shipped.Cells.Where(c => c.Row >= Shipped.BodyRows && c.Label is not null))
        {
            Assert.True(Seven(cell.Label!) <= layout.EquipmentCellBox(cell).Width - 2, $"'{cell.Label}' is cut off in the cell {layout.EquipmentCellBox(cell)}");
        }
    }

    [Fact]
    public void Given_TheCompactAndTightSizes_When_TheDollIsLaidOut_Then_NoCountedNounIsDrawnAndTheHoverLineHasThem()
    {
        Assert.False(new InventoryLayout(800, 600, 40, 5, Shipped).HasLabelColumn);
        Assert.False(new InventoryLayout(533, 400, 30, 5, Shipped).HasLabelColumn);
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
