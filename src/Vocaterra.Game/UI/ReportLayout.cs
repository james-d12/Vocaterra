using Vocaterra.Game.Core.Dialogue;

namespace Vocaterra.Game.UI;

/// <summary>
/// The report panel's geometry (S21), in the window's pixels: the panel, the Bug and Feature request choices, the text box, the Save and Cancel
/// buttons and the hints. The drawing and the mouse share it, so a click lands where the button is drawn.
/// </summary>
public sealed class ReportLayout
{
    /// <summary>The height of one line of text in the panel (the native font's line).</summary>
    public const int LineHeight = 20;

    /// <summary>The inner margin of the panel.</summary>
    public const int Padding = 16;

    /// <summary>The number of text rows the text box shows before it scrolls.</summary>
    public const int TextRows = 8;

    /// <summary>The widest the panel is, in pixels.</summary>
    public const int MaxWidth = 780;

    private const int ButtonHeight = 30;
    private const int Gap = 12;

    /// <summary>
    /// Creates the layout for a canvas. The hint texts wrap to the panel's width (at most two rows each), and the text box shows fewer rows (at least three) when the panel
    /// would not fit the canvas otherwise: the box scrolls, so nothing of the report is lost.
    /// </summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="measure">The width of a text in pixels, or <see langword="null"/> to take each hint as one row.</param>
    /// <param name="hintsFirst">The first hints line (the notice shows there too), or <see langword="null"/>.</param>
    /// <param name="hintsSecond">The second hints line, or <see langword="null"/>.</param>
    public ReportLayout(int viewportWidth, int viewportHeight, Func<string, float>? measure = null, string? hintsFirst = null, string? hintsSecond = null)
    {
        var width = Math.Min(MaxWidth, viewportWidth - (viewportWidth < 600 ? 24 : 48));
        var left = (viewportWidth - width) / 2;
        var hintWidth = width - (2 * Padding);
        FirstRows = RowsOf(hintsFirst, hintWidth, measure);
        SecondRows = RowsOf(hintsSecond, hintWidth, measure);
        var rows = TextRows;
        int HeightFor(int textRows)
        {
            var textBox = (textRows * LineHeight) + 16;
            var offset = Gap + LineHeight + 8 + ButtonHeight + Gap + textBox + Gap + ButtonHeight + 10;
            return offset + ((FirstRows.Count + SecondRows.Count) * LineHeight) + Gap;
        }

        while (rows > MinTextRows && HeightFor(rows) > viewportHeight - 16)
        {
            rows--;
        }

        VisibleRows = rows;
        var textHeight = (rows * LineHeight) + 16;
        var height = HeightFor(rows);
        var top = Math.Max(8, (viewportHeight - height) / 2);
        Panel = new PixelBox(left, top, width, height);
        TitleY = top + Gap;
        var kindsY = TitleY + LineHeight + 8;
        BugButton = new PixelBox(left + Padding, kindsY, 130, ButtonHeight);
        FeatureButton = new PixelBox(left + Padding + 138, kindsY, 200, ButtonHeight);
        TextBox = new PixelBox(left + Padding, kindsY + ButtonHeight + Gap, width - (2 * Padding), textHeight);
        var buttonsY = TextBox.Y + textHeight + Gap;
        SaveButton = new PixelBox(left + width - Padding - 170, buttonsY, 170, ButtonHeight);
        CancelButton = new PixelBox(SaveButton.X - Gap - 120, buttonsY, 120, ButtonHeight);
        HintsY = buttonsY + ButtonHeight + 10;
        InnerWidth = TextBox.Width - 16;
    }

    // A hint wrapped to at most two rows, the second ending in an ellipsis when text is left out; one empty row when there is no measure.
    private static List<string> RowsOf(string? text, int width, Func<string, float>? measure)
    {
        if (text is null || measure is null)
        {
            return [string.Empty];
        }

        var rows = TextWrap.Wrap(text, width, measure);
        return rows.Count <= 2 ? rows.ToList() : [rows[0], rows[1].TrimEnd() + "…"];
    }

    /// <summary>The fewest rows the text box shows on a short canvas.</summary>
    public const int MinTextRows = 3;

    /// <summary>The rows of the first hints line as wrapped (the notice takes this place, and the same number of rows).</summary>
    public IReadOnlyList<string> FirstRows { get; }

    /// <summary>The rows of the second hints line as wrapped.</summary>
    public IReadOnlyList<string> SecondRows { get; }

    /// <summary>The Y of the second hints line's first row.</summary>
    public int SecondY => HintsY + (FirstRows.Count * LineHeight);

    /// <summary>The panel's box.</summary>
    public PixelBox Panel { get; }

    /// <summary>The Y of the title line.</summary>
    public int TitleY { get; }

    /// <summary>The Bug choice.</summary>
    public PixelBox BugButton { get; }

    /// <summary>The Feature request choice.</summary>
    public PixelBox FeatureButton { get; }

    /// <summary>The text box.</summary>
    public PixelBox TextBox { get; }

    /// <summary>The Save report button.</summary>
    public PixelBox SaveButton { get; }

    /// <summary>The Cancel button.</summary>
    public PixelBox CancelButton { get; }

    /// <summary>The Y of the first hints line (the notice shows there too).</summary>
    public int HintsY { get; }

    /// <summary>The width a line of the text has inside the text box, in pixels.</summary>
    public int InnerWidth { get; }

    /// <summary>How many text rows fit in the text box (<see cref="TextRows"/>, fewer on a short canvas).</summary>
    public int VisibleRows { get; }

    /// <summary>The button under a point, or <see langword="null"/>.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public ReportButton? ButtonAt(int x, int y)
    {
        if (BugButton.Contains(x, y))
        {
            return ReportButton.Bug;
        }

        if (FeatureButton.Contains(x, y))
        {
            return ReportButton.Feature;
        }

        if (SaveButton.Contains(x, y))
        {
            return ReportButton.Save;
        }

        return CancelButton.Contains(x, y) ? ReportButton.Cancel : null;
    }
}
