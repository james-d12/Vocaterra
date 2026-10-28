namespace Vocaterra.Game.UI.Book;

/// <summary>
/// The book's speaker buttons (S20b): which rows have one, and the geometry of the button at a row. A row with no recording (<see cref="BookLine.Speak"/>
/// is <see langword="null"/>) has no button. The layer draws the button where <see cref="ButtonRect"/> says and the mouse hits the same rectangle.
/// </summary>
public static class BookSpeakers
{
    /// <summary>The side of the button, in pixels.</summary>
    public const int ButtonSize = 16;

    /// <summary>The gap between the button and the text it sits beside.</summary>
    public const int Gap = 10;

    /// <summary>The indexes (into <paramref name="lines"/>) of the lines that have a button, in order.</summary>
    /// <param name="lines">The page's lines.</param>
    public static IReadOnlyList<int> Speakable(IReadOnlyList<BookLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var result = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Speak is not null)
            {
                result.Add(i);
            }
        }

        return result;
    }

    /// <summary>
    /// The rows the selection moves over (S28): the rows with a speaker button, and on the verbs page the verbs themselves (choosing one shows its table).
    /// Their indexes are lines' indexes; on every other page this is <see cref="Speakable"/>.
    /// </summary>
    /// <param name="lines">The page's list.</param>
    public static IReadOnlyList<int> Selectable(IReadOnlyList<BookLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var result = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Speak is not null || lines[i].Verb is not null)
            {
                result.Add(i);
            }
        }

        return result;
    }

    /// <summary>The button of a row whose text starts at <paramref name="rowTop"/>: at the content's right edge.</summary>
    /// <param name="geometry">The book's geometry.</param>
    /// <param name="rowTop">The top of the row, in pixels.</param>
    public static BookRect ButtonRect(BookGeometry geometry, int rowTop) => new(geometry.ContentRight - ButtonSize, rowTop + 2, ButtonSize, ButtonSize);

    /// <summary>
    /// The speakable row (its index among the speakable lines, not among all lines) whose button is under a point, or <see langword="null"/> when no
    /// button is there. Only the lines on screen (from <paramref name="offset"/>) have buttons that can be hit.
    /// </summary>
    /// <param name="geometry">The book's geometry.</param>
    /// <param name="lines">The page's lines.</param>
    /// <param name="offset">The first line on screen.</param>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    public static int? At(BookGeometry geometry, IReadOnlyList<BookLine> lines, int offset, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(lines);
        var visible = geometry.LinesVisible;
        var speakable = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Speak is null)
            {
                continue;
            }

            if (i >= offset && i < offset + visible)
            {
                var top = geometry.ContentTop + ((i - offset) * BookGeometry.LineHeight);
                if (ButtonRect(geometry, top).Contains(x, y))
                {
                    return speakable;
                }
            }

            speakable++;
        }

        return null;
    }
}
