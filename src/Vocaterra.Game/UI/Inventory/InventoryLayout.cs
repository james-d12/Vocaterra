using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// Where the equipment panel's parts are on a canvas (I04, on X08's slots): the paper doll on the left, the bag on the right, the sort button and the trash cell under the bag, the stats line under the doll,
/// and under both the needs' list (N05) and the key hints. Three sizes serve the canvases the game supports (533 x 400 and up), each the first that fits both ways:
/// <list type="bullet">
/// <item><description><b>Regular</b>: 64 x 80 bag slots, 44 px doll cells with the body rows' Spanish nouns in a column beside them. It needs a canvas of about 950 x 700 for 40 slots with the needs' list.</description></item>
/// <item><description><b>Compact</b> (<see cref="Compact"/>): 44 x 56 bag slots, 36 px doll cells, the nouns left to the hover line: about 640 x 520.</description></item>
/// <item><description><b>Tight</b> (<see cref="Tight"/>): 36 x 50 bag slots, 30 px doll cells and a thinner header and footer, for the 533 x 400 canvas of a small window at 150 percent.</description></item>
/// </list>
/// The doll's grid is <see cref="EquipmentShape"/>, read from <c>equipment.json</c>, so a group added there gets its cells with no change here. With no equipment (a shape with no cell) the doll is left out and the bag
/// stands alone. The hotbar row is set apart from the pack rows by <see cref="HotbarGap"/>. Plain numbers.
/// </summary>
public readonly record struct InventoryLayout
{
    /// <summary>The number of columns (the hotbar's width).</summary>
    public const int Columns = 10;

    /// <summary>The padding inside a regular panel.</summary>
    public const int Padding = 16;

    /// <summary>The height of the header (title and rule) above the slots.</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of the footer (the key hints) under the slots.</summary>
    public const int FooterHeight = 34;

    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The height of one row of the needs' list (N05).</summary>
    public const int NeedsRowHeight = 18;

    /// <summary>How many stat chips the stats line reserves room for: every stat the panel can show (<see cref="StatDisplay.Defaults"/>), so the panel keeps its size when gear is put on or taken off (I04b).</summary>
    public static int StatChipsReserved => StatDisplay.Defaults.Count;

    private readonly EquipmentShape _shape;
    private readonly int _dollCell;
    private readonly int _dollPitch;
    private readonly int _rowPitch;
    private readonly int _countedWidth;
    private readonly int _headingHeight;
    private readonly int _dollTop;
    private readonly int _labelColumn;
    private readonly int _sectionGap;
    private readonly int _columnGap;
    private readonly bool _headingsOverhang;

    /// <summary>Computes the layout for a canvas and a number of slots.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="slotCount">The number of slots, the inventory's active count (30 to 50: three to five rows; a count that is not a multiple of <see cref="Columns"/> fills the last row in part).</param>
    /// <param name="needsRows">How many rows of the needs' list stand under the slots (N05: the five needs); 0 for none. The slots shrink, then the header and footer, until it fits.</param>
    /// <param name="shape">The paper doll's grid (<see cref="EquipmentData.Shape"/>), or <see langword="null"/> for a panel with no doll.</param>
    public InventoryLayout(int viewportWidth, int viewportHeight, int slotCount, int needsRows = 0, EquipmentShape? shape = null)
    {
        _shape = shape ?? EquipmentShape.Empty;
        var rows = Math.Max(1, (slotCount + Columns - 1) / Columns);
        var level = Fit(viewportWidth, viewportHeight, rows, needsRows, _shape);
        Compact = level > 0;
        Tight = level > 1;
        var m = Metrics.For(level);
        CellWidth = m.CellWidth;
        CellHeight = m.CellHeight;
        Gap = m.Gap;
        HotbarGap = rows > 1 ? m.HotbarGap : 0;
        _dollCell = m.DollCell;
        _dollPitch = m.DollPitch;
        _rowPitch = m.DollCell + m.DollGap;
        _countedWidth = m.CountedWidth;
        _headingHeight = _shape.HasHeadings ? m.Heading : 0;
        _sectionGap = m.SectionGap;
        _columnGap = m.ColumnGap;
        _headingsOverhang = level == 0;
        _labelColumn = _shape.HasLabels ? m.LabelColumn : 0;
        HeaderTop = m.HeaderTop;
        DividerY = m.DividerY;
        HintsOffset = m.HintsOffset;
        NeedsRowStep = m.NeedsRowHeight;
        StatChipHeight = m.ChipHeight;
        Rows = rows;

        var sizes = Sizes(m, rows, needsRows, _shape);
        var width = Math.Min(sizes.Width, viewportWidth - (2 * Margin));
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(Margin, (viewportHeight - sizes.Height) / 2), width, sizes.Height);
        ContentLeft = Panel.X + m.Pad;
        ContentWidth = Panel.Width - (2 * m.Pad);
        var innerLeft = Panel.X + ((Panel.Width - sizes.Inner) / 2);
        GridTop = Panel.Y + HeaderTop;
        DollLeft = innerLeft;
        _dollTop = GridTop + _headingHeight;
        GridLeft = innerLeft + sizes.Doll + (sizes.Doll > 0 ? m.ColumnGap : 0);
        NeedsTop = GridTop + sizes.Column + (Tight ? 3 : 5);
        var bagBottom = GridTop + sizes.Bag;
        var trashRight = GridLeft + sizes.BagWidth;
        TrashCell = new PixelBox(trashRight - m.Button, bagBottom + m.ToolGap, m.Button, m.Button);
        SortButton = new PixelBox(TrashCell.X - m.ToolGap - m.Button, TrashCell.Y, m.Button, m.Button);
        StatsArea = sizes.Doll == 0
            ? new PixelBox(DollLeft, GridTop, 0, 0)
            : m.StatsBeside
                ? new PixelBox(GridLeft, SortButton.Y, SortButton.X - m.ToolGap - GridLeft, sizes.StatRows * m.ChipHeight) // the small canvas has no room under the doll: the chips stand beside the buttons, under the bag
                : new PixelBox(DollLeft, _dollTop + sizes.DollHeight + _sectionGap, sizes.Doll, sizes.StatRows * m.ChipHeight);
    }

    /// <summary>Whether the slots are the small ones (the compact or the tight size).</summary>
    public bool Compact { get; }

    /// <summary>Whether even the small slots left no room on this canvas: the slots, the doll's cells, the header and the footer are tighter still (N05).</summary>
    public bool Tight { get; }

    /// <summary>The distance from the panel's top to the rule under the title.</summary>
    public int DividerY { get; }

    /// <summary>The distance from the panel's top to the grid.</summary>
    public int HeaderTop { get; }

    /// <summary>The distance from the panel's bottom up to the key hints.</summary>
    public int HintsOffset { get; }

    /// <summary>The top of the needs' list (N05), under the slots and the doll.</summary>
    public int NeedsTop { get; }

    /// <summary>The height of one row of the needs' list on this canvas.</summary>
    public int NeedsRowStep { get; }

    /// <summary>The slot's width.</summary>
    public int CellWidth { get; }

    /// <summary>The slot's height.</summary>
    public int CellHeight { get; }

    /// <summary>The gap between slots.</summary>
    public int Gap { get; }

    /// <summary>The height of the row of column headings above the doll (0 when no column has a heading).</summary>
    public int HeadingHeight => _headingHeight;

    /// <summary>The extra gap under the hotbar row that sets it apart from the pack rows (0 when the bag is one row).</summary>
    public int HotbarGap { get; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The left edge of the panel's text: the title, the key hints and the needs' list start here.</summary>
    public int ContentLeft { get; }

    /// <summary>The width of the panel's text.</summary>
    public int ContentWidth { get; }

    /// <summary>The left edge of the bag grid.</summary>
    public int GridLeft { get; }

    /// <summary>The top edge of the bag grid and of the doll.</summary>
    public int GridTop { get; }

    /// <summary>The left edge of the doll (the labels' column first, in the regular size).</summary>
    public int DollLeft { get; }

    /// <summary>The height of one row of the stats line.</summary>
    public int StatChipHeight { get; }

    /// <summary>The paper doll's grid this layout was made for.</summary>
    public EquipmentShape Shape => _shape ?? EquipmentShape.Empty;

    /// <summary>The room for the stats line, under the doll (empty when there is no doll).</summary>
    public PixelBox StatsArea { get; }

    /// <summary>The sort button, under the bag grid at its right edge, left of the trash cell.</summary>
    public PixelBox SortButton { get; }

    /// <summary>The trash cell, under the bag grid at its right edge.</summary>
    public PixelBox TrashCell { get; }

    /// <summary>Whether the body rows' nouns stand in a column beside the doll (the regular size, when the data has labels).</summary>
    public bool HasLabelColumn => _labelColumn > 0;

    /// <summary>What a point of the canvas is on: an equipment cell, a bag slot (the active ones, <paramref name="slotCount"/> of them), the sort button, the trash cell, or nothing.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <param name="slotCount">The number of slots the layout was made for (the inventory's active count).</param>
    public InventoryHit Hit(int x, int y, int slotCount)
    {
        if (SortButton.Contains(x, y))
        {
            return new InventoryHit(InventoryHitKind.Sort);
        }

        if (TrashCell.Contains(x, y))
        {
            return new InventoryHit(InventoryHitKind.Trash);
        }

        foreach (var cell in Shape.Cells)
        {
            if (EquipmentCellBox(cell).Contains(x, y))
            {
                return new InventoryHit(InventoryHitKind.Equipment, cell.Flat);
            }
        }

        for (var column = 0; column < Shape.ColumnHeadings.Count; column++)
        {
            if (Shape.ColumnHeadings[column] is not null && HeadingBox(column).Contains(x, y))
            {
                return new InventoryHit(InventoryHitKind.Heading, column);
            }
        }

        for (var slot = 0; slot < slotCount; slot++)
        {
            if (Cell(slot).Contains(x, y))
            {
                return new InventoryHit(InventoryHitKind.Cell, slot);
            }
        }

        return InventoryHit.None;
    }

    /// <summary>The box of a bag slot.</summary>
    /// <param name="slot">The slot, from 0.</param>
    public PixelBox Cell(int slot) => new(GridLeft + ((slot % Columns) * (CellWidth + Gap)), GridTop + ((slot / Columns) * (CellHeight + Gap)) + (slot >= Columns ? HotbarGap : 0), CellWidth, CellHeight);

    /// <summary>The box of a doll cell. A cell of a counted group is wider than a body cell in the regular size, so its noun is written in it in full (I04b).</summary>
    /// <param name="cell">The cell of <see cref="Shape"/>.</param>
    public PixelBox EquipmentCellBox(EquipmentCell cell) => new(
        DollLeft + _labelColumn + (cell.Column * _dollPitch),
        _dollTop + RowOffset(cell.Row),
        cell.Row >= _shape.BodyRows ? _countedWidth : _dollCell,
        _dollCell);

    /// <summary>
    /// The box a column's heading is written in, above its cells (empty when the data gives the column no heading). The small sizes give it the column's pitch less a gap; the regular size lets the outer
    /// columns' headings overhang into the free room beside the doll (the first column, over the labels' column, which stands lower) and into the gap before the bag (the last), I04c.
    /// </summary>
    /// <param name="column">The doll column, from 0.</param>
    public PixelBox HeadingBox(int column)
    {
        if (_headingHeight == 0 || column < 0 || column >= _shape.ColumnHeadings.Count || _shape.ColumnHeadings[column] is null)
        {
            return new PixelBox(DollLeft, GridTop, 0, 0);
        }

        var width = Math.Max(_dollCell, _dollPitch - 4);
        var centre = HeadingCentre(column);
        var left = centre - (width / 2);
        var right = left + width;
        if (_headingsOverhang)
        {
            if (column == 0)
            {
                left = Math.Min(left, DollLeft);
            }

            if (column == _shape.ColumnHeadings.Count - 1)
            {
                right = Math.Max(right, DollLeft + _labelColumn + (column * _dollPitch) + _countedWidth + _columnGap - 4);
            }
        }

        return new PixelBox(left, GridTop, right - left, _headingHeight);
    }

    /// <summary>The left edge a heading's text of <paramref name="textWidth"/> is drawn at: centred over its column, moved only as far as keeps it inside <see cref="HeadingBox"/>.</summary>
    /// <param name="column">The doll column, from 0.</param>
    /// <param name="textWidth">The text's measured width.</param>
    public int HeadingTextLeft(int column, int textWidth)
    {
        var box = HeadingBox(column);
        return Math.Clamp(HeadingCentre(column) - (textWidth / 2), box.X, Math.Max(box.X, box.Right - textWidth));
    }

    private int HeadingCentre(int column) => DollLeft + _labelColumn + (column * _dollPitch) + (_dollCell / 2);

    /// <summary>The box a body row's noun is written in (the column beside the doll, regular size only), or an empty box.</summary>
    /// <param name="row">The body row, from 0.</param>
    public PixelBox RowLabelBox(int row) => _labelColumn > 0 ? new PixelBox(DollLeft, _dollTop + RowOffset(row), _labelColumn - 6, _dollCell) : new PixelBox(DollLeft, GridTop, 0, 0);

    private int RowOffset(int row) => (row * _rowPitch) + (row >= _shape.BodyRows && _shape.BodyRows > 0 ? _sectionGap : 0);

    private static int Fit(int viewportWidth, int viewportHeight, int rows, int needsRows, EquipmentShape shape)
    {
        for (var level = 0; level < 2; level++)
        {
            var sizes = Sizes(Metrics.For(level), rows, needsRows, shape);
            if (sizes.Width <= viewportWidth - (2 * Margin) && sizes.Height <= viewportHeight - (2 * Margin))
            {
                return level;
            }
        }

        return 2;
    }

    private static Extent Sizes(Metrics m, int rows, int needsRows, EquipmentShape shape)
    {
        var hasDoll = !shape.IsEmpty;
        var bagWidth = (Columns * m.CellWidth) + ((Columns - 1) * m.Gap);
        var lastCellWidth = shape.Cells.Count > 0 && shape.Cells.Any(c => c.Column == shape.Columns - 1 && c.Row >= shape.BodyRows) ? m.CountedWidth : m.DollCell;
        var dollWidth = hasDoll ? (shape.HasLabels ? m.LabelColumn : 0) + ((shape.Columns - 1) * m.DollPitch) + lastCellWidth : 0;
        var dollHeight = hasDoll ? (shape.Rows * m.DollCell) + ((shape.Rows - 1) * m.DollGap) + (shape.Rows > shape.BodyRows && shape.BodyRows > 0 ? m.SectionGap : 0) : 0;
        var heading = hasDoll && shape.HasHeadings ? m.Heading : 0;
        var statRows = 0;
        var left = 0;
        var besideWidth = bagWidth - (2 * m.Button) - (2 * m.ToolGap);
        if (hasDoll)
        {
            statRows = StatRowsFor(m.StatsBeside ? besideWidth : dollWidth, m);
            left = heading + dollHeight + (m.StatsBeside ? 0 : m.SectionGap + (statRows * m.ChipHeight));
        }

        var bag = (rows * m.CellHeight) + ((rows - 1) * m.Gap) + (rows > 1 ? m.HotbarGap : 0);
        var tools = m.StatsBeside && hasDoll ? Math.Max(m.Button, statRows * m.ChipHeight) : m.Button;
        var right = bag + m.ToolGap + tools + m.ToolGap;
        var column = Math.Max(left, right);
        var needs = needsRows > 0 ? (needsRows * m.NeedsRowHeight) + m.NeedsPad : 0;
        var inner = dollWidth + (hasDoll ? m.ColumnGap : 0) + bagWidth;
        var height = m.Pad + m.HeaderTop + column + needs + m.Footer + m.ExtraBottom;
        return new Extent(inner + (2 * m.Pad), height, inner, dollWidth, dollHeight, bagWidth, bag, column, statRows);
    }

    // The rows the stats line needs for every stat the panel can show, from the width a chip takes at most (so the real widths never need more).
    private static int StatRowsFor(int width, Metrics m) => (StatChipsReserved + Math.Max(1, width / m.ChipWidth) - 1) / Math.Max(1, width / m.ChipWidth);

    private readonly record struct Extent(int Width, int Height, int Inner, int Doll, int DollHeight, int BagWidth, int Bag, int Column, int StatRows);

    // One column of numbers per size: 0 regular, 1 compact, 2 tight.
    private readonly record struct Metrics(
        int CellWidth, int CellHeight, int Gap, int HotbarGap, int DollCell, int DollGap, int DollPitch, int CountedWidth, int LabelColumn, int SectionGap, int ColumnGap, int Pad,
        int HeaderTop, int DividerY, int Footer, int HintsOffset, int NeedsRowHeight, int NeedsPad, int ChipHeight, int ChipWidth, int Heading, bool StatsBeside, int Button, int ToolGap, int ExtraBottom)
    {
        public static Metrics For(int level) => level switch
        {
            0 => new Metrics(64, 80, 6, 8, 44, 4, 66, 56, 70, 8, 16, 16, 44, 36, InventoryLayout.FooterHeight, 26, InventoryLayout.NeedsRowHeight, 10, 18, 110, 18, false, 28, 6, 8),
            1 => new Metrics(44, 56, 4, 6, 36, 3, 39, 36, 0, 6, 10, 16, 44, 36, InventoryLayout.FooterHeight, 26, InventoryLayout.NeedsRowHeight, 10, 16, 52, 16, false, 24, 4, 8),
            _ => new Metrics(36, 50, 2, 2, 30, 2, 32, 30, 0, 4, 8, 8, 30, 26, 26, 20, InventoryLayout.NeedsRowHeight - 2, 6, 14, 46, 14, true, 20, 2, 0),
        };
    }
}
