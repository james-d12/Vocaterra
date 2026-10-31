namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// The book's "Practise again" buttons (S26): a completed lesson's heading on the Lessons page has one at its right edge. The layer draws the button where
/// <see cref="ButtonRect"/> says and the mouse hits the same rectangle. The Phrases page's "Review now" (X06) is the same button with <see cref="ReviewNow"/> in place
/// of a lesson id. Plain values.
/// </summary>
public static class BookPractise
{
    /// <summary>The width of the button, in pixels.</summary>
    public const int ButtonWidth = 132;

    /// <summary>The target of the Phrases page's "Review now" button (X06, R06): the review deck's warm-up, not a lesson (no lesson may have this id).</summary>
    public const string ReviewNow = "review-now";

    /// <summary>The label on the button.</summary>
    /// <param name="ui">The support language (<c>ui.bookPractise</c> and <c>ui.bookReviewNow</c>, C04).</param>
    /// <param name="target">What the button asks for: a lesson's id, or <see cref="ReviewNow"/>.</param>
    public static string Label(Content.LocaleText ui, string? target = null) =>
        (ui ?? throw new ArgumentNullException(nameof(ui))).Ui(string.Equals(target, ReviewNow, StringComparison.Ordinal) ? "bookReviewNow" : "bookPractise");

    /// <summary>The button of a heading whose text starts at <paramref name="rowTop"/>: at the content's right edge.</summary>
    /// <param name="geometry">The book's geometry.</param>
    /// <param name="rowTop">The top of the row, in pixels.</param>
    public static BookRect ButtonRect(BookGeometry geometry, int rowTop)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return new BookRect(geometry.ContentRight - ButtonWidth, rowTop + 1, ButtonWidth, BookGeometry.LineHeight - 3);
    }

    /// <summary>The lesson whose "Practise again" button is under a point, or <see langword="null"/> when none is. Only the lines on screen (from <paramref name="offset"/>) count.</summary>
    /// <param name="geometry">The book's geometry.</param>
    /// <param name="lines">The page's lines.</param>
    /// <param name="offset">The first line on screen.</param>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    public static string? At(BookGeometry geometry, IReadOnlyList<BookLine> lines, int offset, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(lines);
        var last = Math.Min(lines.Count, offset + geometry.LinesVisible);
        for (var i = Math.Max(0, offset); i < last; i++)
        {
            if (lines[i].Practise is { } lesson && ButtonRect(geometry, geometry.ContentTop + ((i - offset) * BookGeometry.LineHeight)).Contains(x, y))
            {
                return lesson;
            }
        }

        return null;
    }

    /// <summary>The first lesson with a button among the lines on screen (the P key), or <see langword="null"/> when there is none.</summary>
    /// <param name="geometry">The book's geometry.</param>
    /// <param name="lines">The page's lines.</param>
    /// <param name="offset">The first line on screen.</param>
    public static string? FirstOnScreen(BookGeometry geometry, IReadOnlyList<BookLine> lines, int offset)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(lines);
        var last = Math.Min(lines.Count, offset + geometry.LinesVisible);
        for (var i = Math.Max(0, offset); i < last; i++)
        {
            if (lines[i].Practise is { } lesson)
            {
                return lesson;
            }
        }

        return null;
    }
}
