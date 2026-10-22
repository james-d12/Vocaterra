namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Where the beacon's distance label stands (S22). It used to hang centred under or over the edge arrow, so at the right or left edge half of it was cut off by the window
/// (<c>campamento 111 bloc…</c>). The label is now clamped inside the window, and while a talk is open it keeps clear of the panel, the cards and the bubble (the
/// rectangles the conversation layout gives). Plain numbers, so a test places it with no window.
/// </summary>
public static class BeaconLabel
{
    /// <summary>The label's height.</summary>
    public const int Height = 22;

    /// <summary>The distance kept from the window's edge.</summary>
    public const int Margin = 8;

    /// <summary>The padding either side of the text.</summary>
    public const int Padding = 6;

    /// <summary>Places the label of an off-screen target next to its edge arrow.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="arrowX">The arrow's x on the edge.</param>
    /// <param name="arrowY">The arrow's y on the edge.</param>
    /// <param name="textWidth">The width of the label's text.</param>
    /// <param name="avoid">Rectangles to keep clear of (the panel, the cards, the bubble).</param>
    public static PixelBox Place(int viewportWidth, int viewportHeight, double arrowX, double arrowY, double textWidth, IReadOnlyList<PixelBox> avoid)
    {
        ArgumentNullException.ThrowIfNull(avoid);
        var width = Math.Min((int)Math.Ceiling(textWidth) + (2 * Padding), viewportWidth - (2 * Margin));
        var x = Math.Clamp((int)Math.Round(arrowX - (width / 2.0)), Margin, viewportWidth - Margin - width);
        var inner = arrowY > viewportHeight / 2.0 ? -40 : 28; // towards the middle of the window
        var wantedY = Math.Clamp((int)Math.Round(arrowY + inner) - (Height / 2), Margin, viewportHeight - Margin - Height);
        var preferred = new PixelBox(x, wantedY, width, Height);
        if (!avoid.Any(a => ConversationLayout.Overlaps(preferred, a)))
        {
            return preferred;
        }

        // Something stands there (the panel, a card, the speaker): the free spot nearest to the preferred one, searched on a coarse grid.
        PixelBox? best = null;
        var bestDistance = double.MaxValue;
        for (var gy = Margin; gy <= viewportHeight - Margin - Height; gy += 4)
        {
            for (var gx = Margin; gx <= viewportWidth - Margin - width; gx += 8)
            {
                var box = new PixelBox(gx, gy, width, Height);
                var distance = Math.Pow(gx - x, 2) + Math.Pow(gy - wantedY, 2);
                if (distance < bestDistance && !avoid.Any(a => ConversationLayout.Overlaps(box, a)))
                {
                    best = box;
                    bestDistance = distance;
                }
            }
        }

        return best ?? preferred;
    }
}
