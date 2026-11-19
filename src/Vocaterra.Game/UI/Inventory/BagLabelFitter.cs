namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// How a bag slot's name is cut when the slot is too narrow for it (I10b, QA-2026-11-19-ui-4): a cut label keeps at least as many letters as tell it apart from every other name shown in the bag (never fewer than
/// <see cref="MinLetters"/>), so madera and manzana never both read <c>ma...</c>. When the room does not hold those letters at the normal font size, the label is drawn in a smaller font (the steps of
/// <see cref="Steps"/>); only if even the smallest cannot show them is the label cut to what fits. Plain numbers and a measuring function, so a test can check it without a font. Hover keeps the full name.
/// </summary>
public static class BagLabelFitter
{
    /// <summary>The fewest letters a cut label keeps.</summary>
    public const int MinLetters = 3;

    /// <summary>How many font steps below the normal size a label may use: 0 is the normal size.</summary>
    public const int Steps = 3;

    /// <summary>The label to draw for <paramref name="label"/> in <paramref name="maxWidth"/> pixels, and the font step to draw it at.</summary>
    /// <param name="label">The slot's full name.</param>
    /// <param name="shown">Every slot's full name shown in the bag now (this label included, repeats allowed).</param>
    /// <param name="maxWidth">The room.</param>
    /// <param name="measure">The width of a text at a font step (0 normal, larger smaller).</param>
    public static (string Text, int Step) Fit(string label, IEnumerable<string> shown, int maxWidth, Func<string, int, float> measure)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(measure);
        if (measure(label, 0) <= maxWidth)
        {
            return (label, 0);
        }

        var need = Math.Min(DistinguishingLength(label, shown), label.Length);
        var best = (Text: string.Empty, Step: Steps);
        for (var step = 0; step <= Steps; step++)
        {
            if (measure(label, step) <= maxWidth)
            {
                return (label, step); // a smaller font holds the whole name
            }

            var cut = label.Length;
            while (cut > 0 && measure(label[..cut] + "...", step) > maxWidth)
            {
                cut--;
            }

            if (cut >= need)
            {
                return (label[..cut] + "...", step);
            }

            best = (cut > 0 ? label[..cut] + "..." : string.Empty, step);
        }

        return best; // the smallest font could not show the letters that tell it apart: the most that fits
    }

    /// <summary>The shortest prefix length (at least <see cref="MinLetters"/>) at which <paramref name="label"/> reads differently from every other name in <paramref name="shown"/> that is cut there too.</summary>
    /// <param name="label">The name.</param>
    /// <param name="shown">The names shown, this one included.</param>
    public static int DistinguishingLength(string label, IEnumerable<string> shown)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(shown);
        var others = shown.Where(other => !string.Equals(other, label, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        for (var length = MinLetters; length < label.Length; length++)
        {
            var prefix = label[..length];
            if (!others.Any(other => other.Length >= length && other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return length;
            }
        }

        return label.Length;
    }
}
