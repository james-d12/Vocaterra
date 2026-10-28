namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The book's scroll position in lines: moved by the wheel and the keys, kept inside the page's lines.</summary>
public static class BookScroll
{
    /// <summary>The offset kept inside <c>[0, total - visible]</c> (zero when everything fits).</summary>
    /// <param name="offset">The offset to keep.</param>
    /// <param name="total">The page's lines.</param>
    /// <param name="visible">The lines on screen.</param>
    public static int Clamp(int offset, int total, int visible)
    {
        var last = Math.Max(0, total - visible);
        return Math.Clamp(offset, 0, last);
    }
}
