using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Overlays;

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
    /// <param name="sleepRows">The number of rows of the line offering the sleep (V12c; 22 px each, over the keys line), or 0 when the card does not offer it.</param>
    public EndingLayout(int viewportWidth, int viewportHeight, int spanishRows, int englishRows, int summaryRows, int sleepRows = 0)
    {
        var width = Math.Min(760, viewportWidth - 48);
        int HeightFor(int summary) => Top + (spanishRows * 40) + 8 + (englishRows * 22) + (summary == 0 ? 0 : 14 + 22 + (summary * 20)) + (sleepRows == 0 ? 0 : 8 + (sleepRows * 22)) + 30;
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
