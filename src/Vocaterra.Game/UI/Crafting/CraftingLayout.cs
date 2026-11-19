namespace Vocaterra.Game.UI.Crafting;

/// <summary>
/// Where the crafting panel's pieces are for a viewport, in pixels, so that drawing and mouse hits use the same numbers (and a test can click
/// a row without a window). The panel is centred; rows are stacked under a header. When the rows do not fit at the smallest row height (I10b,
/// QA-2026-11-19-ui-1) the panel shows a window of them, <see cref="VisibleRows"/> from <see cref="Scroll"/>, so the header and the footer stay on
/// the canvas at every size; the wheel and the keys move the window.
/// </summary>
public readonly record struct CraftingLayout
{
    /// <summary>The height of the header (title and rule) in pixels.</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of the footer (key hints) in pixels.</summary>
    public const int FooterHeight = 32;

    /// <summary>The widest panel, in pixels.</summary>
    public const int MaxWidth = 800;

    /// <summary>The tallest row, in pixels.</summary>
    public const int MaxRowHeight = 46;

    /// <summary>The space between rows, in pixels.</summary>
    public const int RowGap = 4;

    /// <summary>The padding inside the panel, in pixels.</summary>
    public const int Padding = 16;

    /// <summary>The smallest row, in pixels: below it the panel scrolls instead.</summary>
    public const int MinRowHeight = 24;

    /// <summary>Computes the layout of a panel with <paramref name="rowCount"/> rows in a viewport.</summary>
    /// <param name="viewportWidth">The canvas width.</param>
    /// <param name="viewportHeight">The canvas height.</param>
    /// <param name="rowCount">How many rows the panel has.</param>
    /// <param name="scroll">The first row shown when the rows do not all fit; kept between 0 and <see cref="MaxScroll"/>.</param>
    public CraftingLayout(int viewportWidth, int viewportHeight, int rowCount, int scroll = 0)
    {
        RowCount = Math.Max(rowCount, 0);
        Width = Math.Min(MaxWidth, Math.Max(viewportWidth - 32, 200));
        var room = Math.Max(viewportHeight - 16 - HeaderHeight - FooterHeight - (Padding * 2), MinRowHeight + RowGap);
        var perRow = RowCount == 0 ? MaxRowHeight : (room / RowCount) - RowGap;
        RowHeight = Math.Clamp(perRow, MinRowHeight, MaxRowHeight);
        VisibleRows = perRow >= MinRowHeight || RowCount == 0 ? RowCount : Math.Max(1, room / (RowHeight + RowGap));
        Scroll = Math.Clamp(scroll, 0, MaxScroll);
        Height = (Padding * 2) + HeaderHeight + FooterHeight + (VisibleRows * (RowHeight + RowGap));
        X = (viewportWidth - Width) / 2;
        Y = Math.Max(8, (viewportHeight - Height) / 2);
    }

    /// <summary>
    /// Where the "Uses mesa" column of a row starts, from the row's left edge, or <see langword="null"/> when the row has no room for it (X08: on a narrow canvas the column is left
    /// out; the status on the right still says <c>Needs mesa</c>). It goes at <see cref="StationColumn"/> when it fits there, else as far left as it can, never over the ingredients
    /// or the status.
    /// </summary>
    /// <param name="rowWidth">The row's width.</param>
    /// <param name="ingredientsEnd">Where the ingredients end, from the row's left edge.</param>
    /// <param name="stationWidth">The width of the column's text.</param>
    /// <param name="statusWidth">The width of the status text on the right.</param>
    public static int? StationX(int rowWidth, int ingredientsEnd, int stationWidth, int statusWidth)
    {
        var limit = rowWidth - 10 - statusWidth - 12 - stationWidth;
        var x = Math.Max(ingredientsEnd + 12, Math.Min(StationColumn, limit));
        return x <= limit ? x : null;
    }

    /// <summary>The column the station text prefers, from the row's left edge.</summary>
    public const int StationColumn = 470;

    /// <summary>The number of rows.</summary>
    public int RowCount { get; }

    /// <summary>How many rows are on the panel at once: all of them unless the canvas is too small.</summary>
    public int VisibleRows { get; }

    /// <summary>The first row on the panel.</summary>
    public int Scroll { get; }

    /// <summary>The largest <see cref="Scroll"/>: the one that puts the last row at the bottom.</summary>
    public int MaxScroll => Math.Max(RowCount - VisibleRows, 0);

    /// <summary>Whether some rows are off the panel (a scroll mark is drawn).</summary>
    public bool Scrolls => VisibleRows < RowCount;

    /// <summary>The panel's left edge.</summary>
    public int X { get; }

    /// <summary>The panel's top edge.</summary>
    public int Y { get; }

    /// <summary>The panel's width.</summary>
    public int Width { get; }

    /// <summary>The panel's height.</summary>
    public int Height { get; }

    /// <summary>The height of one row.</summary>
    public int RowHeight { get; }

    /// <summary>The left edge of the rows.</summary>
    public int RowX => X + Padding;

    /// <summary>The width of the rows.</summary>
    public int RowWidth => Width - (Padding * 2);

    /// <summary>Whether the row at <paramref name="index"/> is on the panel (inside the window of <see cref="VisibleRows"/> from <see cref="Scroll"/>).</summary>
    public bool IsShown(int index) => index >= Scroll && index < Scroll + VisibleRows;

    /// <summary>The top edge of the row at <paramref name="index"/> (a row off the window is outside the panel).</summary>
    public int RowY(int index) => Y + Padding + HeaderHeight + ((index - Scroll) * (RowHeight + RowGap));

    /// <summary>The scroll mark's track: a strip in the panel's right padding beside the rows, or an empty box when the rows all fit.</summary>
    public (int X, int Y, int Width, int Height) Track => Scrolls ? (X + Width - 10, RowY(Scroll), 4, (VisibleRows * (RowHeight + RowGap)) - RowGap) : (0, 0, 0, 0);

    /// <summary>The scroll mark's thumb inside the <see cref="Track"/>: its length is the shown share of the rows, its place the scroll.</summary>
    public (int X, int Y, int Width, int Height) Thumb
    {
        get
        {
            var track = Track;
            if (!Scrolls)
            {
                return track;
            }

            var length = Math.Max(8, track.Height * VisibleRows / RowCount);
            var travel = track.Height - length;
            return (track.X, track.Y + (MaxScroll == 0 ? 0 : travel * Scroll / MaxScroll), track.Width, length);
        }
    }

    /// <summary>The row under a point, or <see langword="null"/> when the point is on no row.</summary>
    public int? RowAt(int x, int y)
    {
        if (x < RowX || x >= RowX + RowWidth)
        {
            return null;
        }

        for (var i = Scroll; i < Math.Min(RowCount, Scroll + VisibleRows); i++)
        {
            var top = RowY(i);
            if (y >= top && y < top + RowHeight)
            {
                return i;
            }
        }

        return null;
    }
}
