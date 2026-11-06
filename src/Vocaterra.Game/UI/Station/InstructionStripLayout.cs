using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Station;

/// <summary>
/// Where the pieces of the instruction strip are inside a box (W07): the text above, then a row with the step dots on the left and the three buttons (Repetir, Más despacio,
/// Ayuda) on the right. The box is the caller's, so that the station overlay places the strip at the top of its panel and a page of a carried recipe book (W30) can place the same
/// strip on the page. Plain numbers.
/// </summary>
public readonly record struct InstructionStripLayout
{
    /// <summary>The height of a line of text, in pixels.</summary>
    public const int LineHeight = 20;

    /// <summary>The space between pieces.</summary>
    public const int Gap = 6;

    /// <summary>The height of the strip on a roomy canvas: padding, three lines of text, and the row of the buttons.</summary>
    public const int RegularHeight = 116;

    /// <summary>The height of the strip on a small canvas.</summary>
    public const int CompactHeight = 100;

    /// <summary>Places the strip's pieces in <paramref name="box"/>.</summary>
    /// <param name="box">The strip's box.</param>
    /// <param name="compact">Whether the canvas is small (smaller buttons and padding).</param>
    public InstructionStripLayout(PixelBox box, bool compact)
    {
        Box = box;
        var pad = compact ? 6 : 8;
        var buttonHeight = compact ? 22 : 26;
        var widths = compact ? new[] { 92, 128, 70 } : new[] { 104, 144, 82 };
        var rowTop = box.Bottom - pad - buttonHeight;
        Text = new PixelBox(box.X + pad, box.Y + pad, box.Width - (2 * pad), Math.Max(LineHeight, rowTop - Gap - (box.Y + pad)));
        var right = box.Right - pad;
        var buttons = new PixelBox[3];
        for (var i = 2; i >= 0; i--)
        {
            buttons[i] = new PixelBox(right - widths[i], rowTop, widths[i], buttonHeight);
            right -= widths[i] + Gap;
        }

        Repeat = buttons[0];
        Slower = buttons[1];
        Help = buttons[2];
        Dots = new PixelBox(box.X + pad, rowTop, Math.Max(0, right - (box.X + pad)), buttonHeight);
        MaxTextLines = Math.Max(1, Text.Height / LineHeight);
    }

    /// <summary>The strip's whole box.</summary>
    public PixelBox Box { get; }

    /// <summary>Where the Spanish and the English go.</summary>
    public PixelBox Text { get; }

    /// <summary>How many lines of text the text box holds.</summary>
    public int MaxTextLines { get; }

    /// <summary>Where the row of step dots goes.</summary>
    public PixelBox Dots { get; }

    /// <summary>The Repetir button.</summary>
    public PixelBox Repeat { get; }

    /// <summary>The Más despacio button.</summary>
    public PixelBox Slower { get; }

    /// <summary>The Ayuda button.</summary>
    public PixelBox Help { get; }

    /// <summary>The distance between the centres of two dots for <paramref name="count"/> steps, so that they all fit the dots' box.</summary>
    /// <param name="count">The number of steps.</param>
    public int DotStep(int count) => count <= 1 ? 18 : Math.Clamp((Dots.Width - 14) / count, 6, 18);

    /// <summary>
    /// The lines of the strip's text for a box of <paramref name="width"/> pixels and <paramref name="maxLines"/> lines: the Spanish first, then the English, each wrapped at
    /// its spaces; the English keeps at least one line when there is any, and a text cut short ends in an ellipsis. Nothing is wider than <paramref name="width"/>.
    /// </summary>
    /// <param name="strip">The strip.</param>
    /// <param name="width">The text box's width.</param>
    /// <param name="maxLines">How many lines it holds.</param>
    /// <param name="measure">The width of a text in pixels (the font).</param>
    public static (IReadOnlyList<string> Spanish, IReadOnlyList<string> English) TextLines(InstructionStrip strip, int width, int maxLines, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(strip);
        ArgumentNullException.ThrowIfNull(measure);
        var spanish = strip.Spanish is null ? [] : TextWrap.Wrap(strip.Spanish, width, measure);
        var english = strip.English is null ? [] : TextWrap.Wrap(strip.English, width, measure);
        var spanishLines = english.Count == 0 ? Math.Min(spanish.Count, maxLines) : Math.Min(spanish.Count, Math.Max(0, maxLines - 1));
        var englishLines = Math.Min(english.Count, maxLines - spanishLines);
        return (Cut(spanish, spanishLines, width, measure), Cut(english, englishLines, width, measure));
    }

    private static IReadOnlyList<string> Cut(IReadOnlyList<string> lines, int count, int width, Func<string, float> measure)
    {
        if (count >= lines.Count)
        {
            return lines;
        }

        var kept = lines.Take(count).ToList();
        if (kept.Count > 0)
        {
            kept[^1] = TextWrap.Ellipsize(kept[^1] + "…", width, measure);
        }

        return kept;
    }
}
