namespace Vocaterra.Game.UI;

/// <summary>
/// Where the crafting panel's pieces are for a viewport, in pixels, so that drawing and mouse hits use the same numbers (and a test can click
/// a row without a window). The panel is centred; rows are stacked under a header.
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

    /// <summary>Computes the layout of a panel with <paramref name="rowCount"/> rows in a viewport.</summary>
    public CraftingLayout(int viewportWidth, int viewportHeight, int rowCount)
    {
        RowCount = Math.Max(rowCount, 0);
        Width = Math.Min(MaxWidth, Math.Max(viewportWidth - 32, 200));
        var room = Math.Max(viewportHeight - 16 - HeaderHeight - FooterHeight - (Padding * 2), 40);
        var perRow = RowCount == 0 ? MaxRowHeight : (room / RowCount) - RowGap;
        RowHeight = Math.Clamp(perRow, 24, MaxRowHeight);
        Height = (Padding * 2) + HeaderHeight + FooterHeight + (RowCount * (RowHeight + RowGap));
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

    /// <summary>The top edge of the row at <paramref name="index"/>.</summary>
    public int RowY(int index) => Y + Padding + HeaderHeight + (index * (RowHeight + RowGap));

    /// <summary>The row under a point, or <see langword="null"/> when the point is on no row.</summary>
    public int? RowAt(int x, int y)
    {
        if (x < RowX || x >= RowX + RowWidth)
        {
            return null;
        }

        for (var i = 0; i < RowCount; i++)
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
