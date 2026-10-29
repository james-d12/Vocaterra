using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

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

    /// <summary>
    /// A box moved (and, if it is wider than the canvas, narrowed) so it lies inside the canvas less <see cref="Margin"/> (X08): the label of an on-screen target hangs at the
    /// target, which may be near an edge, and a larger text scale makes the label wider.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    public static PixelBox Clamp(PixelBox box, int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(box.Width, viewportWidth - (2 * Margin));
        var height = Math.Min(box.Height, viewportHeight - (2 * Margin));
        return new PixelBox(Math.Clamp(box.X, Margin, viewportWidth - Margin - width), Math.Clamp(box.Y, Margin, viewportHeight - Margin - height), width, height);
    }

    /// <summary>
    /// Places the label like <see cref="Place"/>, but never over <paramref name="hard"/> (the panel and the lesson strip), and returns <see langword="null"/> when there is no free place
    /// for it there (a short canvas under a tall lesson strip has none): the arrow stays and the label is left out. It keeps clear of <paramref name="soft"/> (the speaker's head and the
    /// bubble) when it can, and takes a place over them when that is the only one.
    /// </summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="arrowX">The arrow's x on the edge.</param>
    /// <param name="arrowY">The arrow's y on the edge.</param>
    /// <param name="textWidth">The width of the label's text.</param>
    /// <param name="hard">Rectangles the label never overlaps.</param>
    /// <param name="soft">Rectangles the label keeps clear of when there is room.</param>
    public static PixelBox? TryPlace(int viewportWidth, int viewportHeight, double arrowX, double arrowY, double textWidth, IReadOnlyList<PixelBox> hard, IReadOnlyList<PixelBox> soft)
    {
        ArgumentNullException.ThrowIfNull(hard);
        ArgumentNullException.ThrowIfNull(soft);
        var all = hard.Concat(soft).ToList();
        var placed = Place(viewportWidth, viewportHeight, arrowX, arrowY, textWidth, all);
        if (!all.Any(a => ConversationLayout.Overlaps(placed, a)))
        {
            return placed;
        }

        var onlyHard = Place(viewportWidth, viewportHeight, arrowX, arrowY, textWidth, hard);
        return hard.Any(a => ConversationLayout.Overlaps(onlyHard, a)) ? null : onlyHard;
    }

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
