namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The book's scroll position in lines: moved by the wheel and the keys, kept inside the page's lines.</summary>
public static class BookScroll
{
    /// <summary>
    /// The offset the lessons page opens at (V21b): the title of the <c>next:</c> lesson at the top of the page, kept inside the page, so in sessions 8 to 10 the lesson
    /// to go to is in view and not 190 lines down. Zero when no line is the next lesson.
    /// </summary>
    /// <param name="lines">The page's lines.</param>
    /// <param name="visible">The lines on screen.</param>
    public static int OpeningOffset(IReadOnlyList<BookLine> lines, int visible) => NextIndex(lines) is { } i ? Clamp(i, lines.Count, visible) : 0;

    /// <summary>The index of the line that is the <c>next:</c> lesson's title, or <see langword="null"/> when no line is.</summary>
    /// <param name="lines">The page's lines.</param>
    public static int? NextIndex(IReadOnlyList<BookLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Next)
            {
                return i;
            }
        }

        return null;
    }

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
