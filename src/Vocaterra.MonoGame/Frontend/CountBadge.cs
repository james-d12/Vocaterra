namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// How a stack's count is drawn, in plain values: the text, and the colours of the text and of the plate behind it. The count sits on a
/// dark plate with white text so that it reads on every icon and every slot (P6 drew it in white straight on a pale plank icon, and it was
/// lost); <see cref="Contrast"/> is checked by a test, so a colour change cannot make it unreadable again.
/// </summary>
public static class CountBadge
{
    /// <summary>The text colour (red, green, blue).</summary>
    public static (byte R, byte G, byte B) TextColour { get; } = (255, 255, 255);

    /// <summary>The plate colour (red, green, blue), drawn opaque behind the text.</summary>
    public static (byte R, byte G, byte B) PlateColour { get; } = (10, 10, 20);

    /// <summary>The text of a count, or <see langword="null"/> when nothing is drawn: a count of one is drawn only when <paramref name="showOne"/> is set.</summary>
    /// <param name="count">The number of items in the stack.</param>
    /// <param name="showOne">Whether a stack of one shows its count (the inventory does, the hotbar does not).</param>
    public static string? Text(int count, bool showOne) =>
        count < 1 || (count == 1 && !showOne) ? null : count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The WCAG contrast ratio (1 to 21) of the text colour on the plate colour.</summary>
    public static double Contrast()
    {
        var text = Luminance(TextColour);
        var plate = Luminance(PlateColour);
        var lighter = Math.Max(text, plate);
        var darker = Math.Min(text, plate);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance((byte R, byte G, byte B) colour) =>
        (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));

    private static double Channel(byte value)
    {
        var v = value / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
