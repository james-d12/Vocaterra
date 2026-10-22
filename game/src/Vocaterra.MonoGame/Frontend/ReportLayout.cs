namespace Vocaterra.MonoGame.Frontend;

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

    /// <summary>Creates the layout for a window.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public ReportLayout(int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(MaxWidth, viewportWidth - 48);
        var left = (viewportWidth - width) / 2;
        var textHeight = (TextRows * LineHeight) + 16;
        var hintsOffset = Gap + LineHeight + 8 + ButtonHeight + Gap + textHeight + Gap + ButtonHeight + 10;
        var height = hintsOffset + (2 * LineHeight) + Gap;
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

    /// <summary>How many text rows fit in the text box.</summary>
    public int VisibleRows => TextRows;

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
