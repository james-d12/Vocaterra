namespace Vocaterra.Game.UI;

/// <summary>Breaks text into lines that fit a width, measured by a function the engine layer supplies (the font), so the logic is testable.</summary>
public static class TextWrap
{
    /// <summary>
    /// Wraps <paramref name="text"/> at spaces so that no line measures wider than <paramref name="maxWidth"/>; a single word wider than the
    /// width is cut where it no longer fits. An empty text gives one empty line.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            var rest = word;
            while (measure(rest) > maxWidth && rest.Length > 1)
            {
                var cut = rest.Length - 1;
                while (cut > 1 && measure(rest[..cut]) > maxWidth)
                {
                    cut--;
                }

                lines.Add(rest[..cut]);
                rest = rest[cut..];
            }

            current = rest;
        }

        lines.Add(current);
        return lines;
    }

    /// <summary>
    /// The part of a one-line text that fits <paramref name="maxWidth"/> and keeps the caret in view, with the caret's place in it: the
    /// text is cut at the start so that everything before the caret fits, then at the end so that the rest fits.
    /// </summary>
    public static (string Visible, int CaretOffset) Window(string text, int caret, float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        caret = Math.Clamp(caret, 0, text.Length);
        var start = 0;
        while (start < caret && measure(text[start..caret]) > maxWidth)
        {
            start++;
        }

        var end = text.Length;
        while (end > caret && measure(text[start..end]) > maxWidth)
        {
            end--;
        }

        return (text[start..end], caret - start);
    }
}
