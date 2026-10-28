namespace Vocaterra.Game.UI.Book;

/// <summary>
/// The right page's scroll (X03): the detail is drawn in pixels from its top, and it moves in whole lines. The page's writing is <c>content</c> pixels
/// tall and the page shows <c>view</c>; the offset is kept inside the difference (zero when all of it fits). Plain values.
/// </summary>
public static class DetailScroll
{
    /// <summary>The offset kept inside <c>[0, content - view]</c> (zero when the detail fits).</summary>
    /// <param name="offset">The offset to keep, in pixels.</param>
    /// <param name="content">The detail's height, in pixels.</param>
    /// <param name="view">The height the page shows, in pixels.</param>
    public static int Clamp(int offset, int content, int view) => Math.Clamp(offset, 0, Math.Max(0, content - view));

    /// <summary>Whether writing lies under the bottom of the page at this offset (the "more below" marker).</summary>
    /// <param name="offset">The offset, in pixels (already kept inside its bounds).</param>
    /// <param name="content">The detail's height, in pixels.</param>
    /// <param name="view">The height the page shows, in pixels.</param>
    public static bool MoreBelow(int offset, int content, int view) => content - offset > view;
}
