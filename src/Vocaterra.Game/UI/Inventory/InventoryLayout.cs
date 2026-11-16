using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
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

    /// <summary>How many rows of stat chips the stats line reserves room for.</summary>
    public const int StatRows = 2;

    private readonly EquipmentShape _shape;
    private readonly int _dollCell;
    private readonly int _dollGap;
    private readonly int _labelColumn;
    private readonly int _sectionGap;

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
        _dollGap = m.DollGap;
        _sectionGap = m.SectionGap;
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
        GridLeft = innerLeft + sizes.Doll + (sizes.Doll > 0 ? m.ColumnGap : 0);
        NeedsTop = GridTop + sizes.Column + (Tight ? 3 : 5);
        var bagBottom = GridTop + sizes.Bag;
        var trashRight = GridLeft + sizes.BagWidth;
        TrashCell = new PixelBox(trashRight - m.Button, bagBottom + m.ToolGap, m.Button, m.Button);
        SortButton = new PixelBox(TrashCell.X - m.ToolGap - m.Button, TrashCell.Y, m.Button, m.Button);
        StatsArea = sizes.Doll > 0 ? new PixelBox(DollLeft, GridTop + sizes.DollHeight + _sectionGap, sizes.Doll, StatRows * m.ChipHeight) : new PixelBox(DollLeft, GridTop, 0, 0);
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

    /// <summary>The box of a doll cell.</summary>
    /// <param name="cell">The cell of <see cref="Shape"/>.</param>
    public PixelBox EquipmentCellBox(EquipmentCell cell) => new(
        DollLeft + _labelColumn + (cell.Column * (_dollCell + _dollGap)),
        GridTop + RowOffset(cell.Row),
        _dollCell,
        _dollCell);

    /// <summary>The box a body row's noun is written in (the column beside the doll, regular size only), or an empty box.</summary>
    /// <param name="row">The body row, from 0.</param>
    public PixelBox RowLabelBox(int row) => _labelColumn > 0 ? new PixelBox(DollLeft, GridTop + RowOffset(row), _labelColumn - 6, _dollCell) : new PixelBox(DollLeft, GridTop, 0, 0);

    private int RowOffset(int row) => (row * (_dollCell + _dollGap)) + (row >= _shape.BodyRows && _shape.BodyRows > 0 ? _sectionGap : 0);

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
        var dollWidth = hasDoll ? (shape.HasLabels ? m.LabelColumn : 0) + (shape.Columns * m.DollCell) + ((shape.Columns - 1) * m.DollGap) : 0;
        var dollHeight = hasDoll ? (shape.Rows * m.DollCell) + ((shape.Rows - 1) * m.DollGap) + (shape.Rows > shape.BodyRows && shape.BodyRows > 0 ? m.SectionGap : 0) : 0;
        var left = hasDoll ? dollHeight + m.SectionGap + (StatRows * m.ChipHeight) : 0;
        var bagWidth = (Columns * m.CellWidth) + ((Columns - 1) * m.Gap);
        var bag = (rows * m.CellHeight) + ((rows - 1) * m.Gap) + (rows > 1 ? m.HotbarGap : 0);
        var right = bag + m.ToolGap + m.Button + m.ToolGap;
        var column = Math.Max(left, right);
        var needs = needsRows > 0 ? (needsRows * m.NeedsRowHeight) + m.NeedsPad : 0;
        var inner = dollWidth + (hasDoll ? m.ColumnGap : 0) + bagWidth;
        var height = m.Pad + m.HeaderTop + column + needs + m.Footer + m.ExtraBottom;
        return new Extent(inner + (2 * m.Pad), height, inner, dollWidth, dollHeight, bagWidth, bag, column);
    }

    private readonly record struct Extent(int Width, int Height, int Inner, int Doll, int DollHeight, int BagWidth, int Bag, int Column);

    // One column of numbers per size: 0 regular, 1 compact, 2 tight.
    private readonly record struct Metrics(
        int CellWidth, int CellHeight, int Gap, int HotbarGap, int DollCell, int DollGap, int LabelColumn, int SectionGap, int ColumnGap, int Pad,
        int HeaderTop, int DividerY, int Footer, int HintsOffset, int NeedsRowHeight, int NeedsPad, int ChipHeight, int Button, int ToolGap, int ExtraBottom)
    {
        public static Metrics For(int level) => level switch
        {
            0 => new Metrics(64, 80, 6, 8, 44, 4, 64, 8, 16, 16, 44, 36, InventoryLayout.FooterHeight, 26, InventoryLayout.NeedsRowHeight, 10, 18, 28, 6, 8),
            1 => new Metrics(44, 56, 4, 6, 36, 3, 0, 6, 10, 16, 44, 36, InventoryLayout.FooterHeight, 26, InventoryLayout.NeedsRowHeight, 10, 16, 24, 4, 8),
            _ => new Metrics(36, 50, 2, 2, 30, 2, 0, 4, 8, 8, 30, 26, 26, 20, InventoryLayout.NeedsRowHeight - 2, 6, 14, 20, 2, 0),
        };
    }
}
