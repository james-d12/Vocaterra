namespace Vocaterra.Game.UI;

/// <summary>
/// The controls help's panel on a canvas (X08). It lists every control with its keys, and with 20 of them it did not fit a canvas under about 620 pixels tall (800 x 600
/// already clipped it). The panel is now at most the canvas tall; when the controls do not all fit, it shows the ones that do from a first one the player scrolls to (Up and
/// Down, Page Up and Page Down, the wheel), and says so in its footer. Plain numbers: each control's height is given (a control may wrap to a few rows).
/// </summary>
public readonly record struct ControlsLayout
{
    /// <summary>The space above the first control (the title).</summary>
    public const int HeaderHeight = 40;

    /// <summary>The space under the last control shown (the close hint).</summary>
    public const int FooterHeight = 30;

    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="rowHeights">The height of each control (its rows of text and the gap under it).</param>
    /// <param name="scroll">The first control the player scrolled to (clamped).</param>
    public ControlsLayout(int viewportWidth, int viewportHeight, IReadOnlyList<int> rowHeights, int scroll)
    {
        ArgumentNullException.ThrowIfNull(rowHeights);
        var width = Math.Min(820, viewportWidth - 32);
        var total = rowHeights.Sum();
        var limit = viewportHeight - (2 * Margin);
        var height = Math.Min(HeaderHeight + total + FooterHeight, limit);
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(Margin, (viewportHeight - height) / 2), width, height);
        var room = height - HeaderHeight - FooterHeight;
        var max = MaxScrollFor(rowHeights, room);
        First = Math.Clamp(scroll, 0, max);
        var used = 0;
        var count = 0;
        for (var i = First; i < rowHeights.Count && used + rowHeights[i] <= room; i++)
        {
            used += rowHeights[i];
            count++;
        }

        Count = count;
        MaxScroll = max;
    }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The first control shown.</summary>
    public int First { get; }

    /// <summary>How many controls are shown from <see cref="First"/>.</summary>
    public int Count { get; }

    /// <summary>The last first control: scrolling goes no further than this (0 when everything fits).</summary>
    public int MaxScroll { get; }

    /// <summary>Whether there are controls above or below the ones shown.</summary>
    public bool Scrolls => MaxScroll > 0;

    /// <summary>The largest first control for which the rest of the controls still fit a room of this height.</summary>
    /// <param name="rowHeights">The height of each control.</param>
    /// <param name="room">The height available to the controls.</param>
    public static int MaxScrollFor(IReadOnlyList<int> rowHeights, int room)
    {
        ArgumentNullException.ThrowIfNull(rowHeights);
        var used = 0;
        for (var i = rowHeights.Count - 1; i >= 0; i--)
        {
            used += rowHeights[i];
            if (used > room)
            {
                return Math.Min(i + 1, Math.Max(0, rowHeights.Count - 1));
            }
        }

        return 0;
    }
}

/// <summary>
/// The closing card of the session on a canvas (X08): the title, the Spanish line, the English and the session summary. On a short canvas the summary gives up its last rows
/// (with a closing ellipsis row) until the card fits, so the card is never taller than the canvas; the Spanish and the English always stay.
/// </summary>
public readonly record struct EndingLayout
{
    /// <summary>The card's inner padding on the left.</summary>
    public const int Side = 20;

    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The height of the title and the Spanish rows' gaps above the English: the card's top padding, the title and the gap.</summary>
    public const int Top = 18 + 40;

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="spanishRows">The number of rows of the Spanish line in the large font (40 px each).</param>
    /// <param name="englishRows">The number of rows of the English line (22 px each).</param>
    /// <param name="summaryRows">The number of rows of the summary (20 px each, with a 36 px heading).</param>
    public EndingLayout(int viewportWidth, int viewportHeight, int spanishRows, int englishRows, int summaryRows)
    {
        var width = Math.Min(760, viewportWidth - 48);
        int HeightFor(int summary) => Top + (spanishRows * 40) + 8 + (englishRows * 22) + (summary == 0 ? 0 : 14 + 22 + (summary * 20)) + 30;
        var shown = summaryRows;
        while (shown > 0 && HeightFor(shown) > viewportHeight - (2 * Margin))
        {
            shown--;
        }

        var height = HeightFor(shown);
        var preferred = Math.Max(40, (viewportHeight / 5) - (height / 2));
        var top = Math.Max(Margin, Math.Min(preferred, viewportHeight - Margin - height));
        Box = new PixelBox((viewportWidth - width) / 2, top, width, height);
        SummaryShown = shown;
        SummaryCut = shown < summaryRows;
    }

    /// <summary>The card's box.</summary>
    public PixelBox Box { get; }

    /// <summary>How many summary rows are shown (the last one is the ellipsis row when <see cref="SummaryCut"/>).</summary>
    public int SummaryShown { get; }

    /// <summary>Whether summary rows were left out.</summary>
    public bool SummaryCut { get; }
}
