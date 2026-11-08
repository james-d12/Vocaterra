using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// Where the inventory screen's panel and slots are on a canvas (X08). The slots were 64 x 80 pixels in ten columns, a grid 694 pixels wide, so the panel did not hold it on a
/// canvas narrower than about 730 (a 640 x 480 window, or 150 percent at 800 x 600). On such a canvas the slots are 44 x 56 in the same ten columns: the label is cut to the
/// slot's width with an ellipsis (a stack's full name is on the hotbar's word card and in the book). Plain numbers.
/// </summary>
public readonly record struct InventoryLayout
{
    /// <summary>The number of columns (the hotbar's width).</summary>
    public const int Columns = 10;

    /// <summary>The padding inside the panel.</summary>
    public const int Padding = 16;

    /// <summary>The height of the header (title and rule) above the slots.</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of the footer (the key hints) under the slots.</summary>
    public const int FooterHeight = 34;

    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The height of one row of the needs' list (N05).</summary>
    public const int NeedsRowHeight = 18;

    /// <summary>Computes the layout for a canvas and a number of slots.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="slotCount">The number of slots (a multiple of <see cref="Columns"/>).</param>
    /// <param name="needsRows">How many rows of the needs' list stand under the slots (N05: the five needs); 0 for none. The slots shrink, then the header and footer, until it fits.</param>
    public InventoryLayout(int viewportWidth, int viewportHeight, int slotCount, int needsRows = 0)
    {
        var rows = Math.Max(1, slotCount / Columns);
        var needs = needsRows > 0 ? (needsRows * NeedsRowHeight) + 10 : 0;
        var regularWidth = (Columns * 64) + ((Columns - 1) * 6) + (Padding * 2);
        var regularHeight = (Padding * 2) + 36 + (rows * 80) + ((rows - 1) * 6) + FooterHeight + needs;
        var compactHeight = (Padding * 2) + 36 + (rows * 56) + ((rows - 1) * 4) + FooterHeight + needs;
        Compact = regularWidth > viewportWidth - (2 * Margin) || regularHeight > viewportHeight - (2 * Margin);
        Tight = Compact && compactHeight > viewportHeight - (2 * Margin);
        CellWidth = Compact ? 44 : 64;
        CellHeight = Tight ? 52 : Compact ? 56 : 80;
        Gap = Tight ? 2 : Compact ? 4 : 6;
        var needsRowHeight = Tight ? NeedsRowHeight - 2 : NeedsRowHeight;
        needs = needsRows > 0 ? (needsRows * needsRowHeight) + (Tight ? 6 : 10) : 0;
        var padding = Tight ? 8 : Padding;
        HeaderTop = Tight ? 30 : 44;
        DividerY = Tight ? 26 : 36;
        var footer = Tight ? 26 : FooterHeight;
        HintsOffset = Tight ? 20 : 26;
        var gridWidth = (Columns * CellWidth) + ((Columns - 1) * Gap);
        Rows = rows;
        var width = Math.Min(gridWidth + (Padding * 2), viewportWidth - (2 * Margin));
        var gridHeight = (rows * CellHeight) + ((rows - 1) * Gap);
        var height = padding + HeaderTop + gridHeight + needs + footer + (Tight ? 0 : padding - 8);
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(Margin, (viewportHeight - height) / 2), width, height);
        GridLeft = Panel.X + ((Panel.Width - gridWidth) / 2);
        GridTop = Panel.Y + HeaderTop;
        NeedsTop = GridTop + gridHeight + (Tight ? 3 : 5);
        NeedsRowStep = needsRowHeight;
    }

    /// <summary>Whether the slots are the small ones.</summary>
    public bool Compact { get; }

    /// <summary>Whether even the small slots left no room for the needs' list on this canvas: the slots, the header and the footer are tighter still (N05).</summary>
    public bool Tight { get; }

    /// <summary>The distance from the panel's top to the rule under the title.</summary>
    public int DividerY { get; }

    /// <summary>The distance from the panel's top to the grid.</summary>
    public int HeaderTop { get; }

    /// <summary>The distance from the panel's bottom up to the key hints.</summary>
    public int HintsOffset { get; }

    /// <summary>The top of the needs' list (N05), under the slots.</summary>
    public int NeedsTop { get; }

    /// <summary>The height of one row of the needs' list on this canvas.</summary>
    public int NeedsRowStep { get; }

    /// <summary>The slot's width.</summary>
    public int CellWidth { get; }

    /// <summary>The slot's height.</summary>
    public int CellHeight { get; }

    /// <summary>The gap between slots.</summary>
    public int Gap { get; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The left edge of the grid.</summary>
    public int GridLeft { get; }

    /// <summary>The top edge of the grid.</summary>
    public int GridTop { get; }

    /// <summary>The box of a slot.</summary>
    /// <param name="slot">The slot, from 0.</param>
    public PixelBox Cell(int slot) => new(GridLeft + ((slot % Columns) * (CellWidth + Gap)), GridTop + ((slot / Columns) * (CellHeight + Gap)), CellWidth, CellHeight);
}
