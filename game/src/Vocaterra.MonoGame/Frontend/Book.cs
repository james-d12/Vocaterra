using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The book's five pages (S20, D-game-16), in tab order.</summary>
public enum BookPage
{
    /// <summary>The catalogue nouns (seen or <c>¿?</c>, icon, pips) and the other nouns the learner used, with their forms and last sentence.</summary>
    Nouns,

    /// <summary>The verbs the learner used: the infinitive with its English, each form with its label, counts and last sentence.</summary>
    Verbs,

    /// <summary>The phrases of the phrase sheet with their help and counts (S8).</summary>
    Phrases,

    /// <summary>Every lesson in the bundle with its state, units, phrases, help and (once done) its English summary.</summary>
    Lessons,

    /// <summary>The newest lines the learner gave, newest first, with the core's feedback in English.</summary>
    Said,
}

/// <summary>The labels of the book's pages, in tab order. English only (D-game-13).</summary>
public static class BookTabs
{
    /// <summary>The tab labels, indexed by <see cref="BookPage"/>.</summary>
    public static IReadOnlyList<string> Labels { get; } = ["Nouns", "Verbs", "Phrases", "Lessons", "What I said"];

    /// <summary>The title over the pages.</summary>
    public const string Title = "Your book";

    /// <summary>The key hints under the pages.</summary>
    public const string KeyHints = "J or Esc: close the book   Tab, Left, Right or click: turn the page   wheel, Page Up, Page Down: scroll";

    /// <summary>The label of a page.</summary>
    /// <param name="page">The page.</param>
    public static string LabelOf(BookPage page) => Labels[(int)page];

    /// <summary>The page after <paramref name="page"/> (Tab, Right), or before it (Left, Shift+Tab); the pages wrap round.</summary>
    /// <param name="page">The page.</param>
    /// <param name="forward">Whether to go forward.</param>
    public static BookPage Turn(BookPage page, bool forward)
    {
        var count = Labels.Count;
        var next = ((int)page + (forward ? 1 : count - 1)) % count;
        return (BookPage)next;
    }
}

/// <summary>A rectangle in pixels (the engine's own type is not used in Frontend/, D-game-1).</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct BookRect(int X, int Y, int Width, int Height)
{
    /// <summary>The right edge.</summary>
    public int Right => X + Width;

    /// <summary>The bottom edge.</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether a point is inside.</summary>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>
/// The book's screen geometry at a viewport size: the panel, its title and tab row, the content area and how many lines of content it holds.
/// The layer and the mouse read the same numbers, so a click lands on the tab that is drawn there. Plain values.
/// </summary>
public sealed record BookGeometry(int ViewportWidth, int ViewportHeight)
{
    /// <summary>The widest the panel is.</summary>
    public const int MaxPanelWidth = 760;

    /// <summary>The gap from the window's edge to the panel, and inside the panel to its content.</summary>
    public const int Margin = 16;

    /// <summary>The padding inside the panel.</summary>
    public const int Padding = 16;

    /// <summary>The height of one line of content, in pixels.</summary>
    public const int LineHeight = 19;

    /// <summary>The width of one tab's click area, in pixels.</summary>
    public const int TabWidth = 140;

    /// <summary>The panel.</summary>
    public BookRect Panel
    {
        get
        {
            var width = Math.Min(MaxPanelWidth, ViewportWidth - (Margin * 2));
            return new BookRect((ViewportWidth - width) / 2, Margin, width, ViewportHeight - (Margin * 2));
        }
    }

    /// <summary>The title's line: the top of the panel plus the padding.</summary>
    public int TitleTop => Panel.Y + 12;

    /// <summary>The tab row's top, under the title.</summary>
    public int TabsTop => Panel.Y + 40;

    /// <summary>The first tab's left edge.</summary>
    public int TabsLeft => Panel.X + Padding;

    /// <summary>The rule under the tabs.</summary>
    public int RuleTop => Panel.Y + 66;

    /// <summary>The top of the content.</summary>
    public int ContentTop => Panel.Y + 76;

    /// <summary>The bottom of the content (the key hints sit under it).</summary>
    public int ContentBottom => Panel.Bottom - 46;

    /// <summary>How many whole lines of content fit (at least one).</summary>
    public int LinesVisible => Math.Max(1, (ContentBottom - ContentTop) / LineHeight);

    /// <summary>The click area of a tab, by page.</summary>
    /// <param name="page">The page.</param>
    public BookRect TabRect(BookPage page) => new(TabsLeft + ((int)page * TabWidth), TabsTop - 4, TabWidth, 24);

    /// <summary>The page whose tab is at a point, or <see langword="null"/> when the point is on no tab.</summary>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    public BookPage? TabAt(int x, int y)
    {
        foreach (var page in Enum.GetValues<BookPage>())
        {
            if (TabRect(page).Contains(x, y))
            {
                return page;
            }
        }

        return null;
    }
}

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

/// <summary>
/// The book (J): which page is on screen, whether it is open, and the scroll. It takes the keyboard while it is open (the player does not walk).
/// It is opened by J and by using the book item (both call <see cref="Open"/>), so the two open the same book. Plain values; no clock.
/// </summary>
public sealed class BookModel
{
    private WordProgress? _words;
    private bool _read;

    /// <summary>Whether the book is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The page on screen (the nouns page opens first).</summary>
    public BookPage Page { get; private set; } = BookPage.Nouns;

    /// <summary>The first line on screen of the page (lines, not pixels).</summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Opens the book on its nouns page. Each seen noun's label counts as read once per opening, when the nouns page is shown (the research's "shown
    /// for a second" rule, with the opening as the unit of time, so no clock is needed).
    /// </summary>
    /// <param name="words">The learner's word progress.</param>
    public void Open(WordProgress words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _words = words;
        _read = false;
        Page = BookPage.Nouns;
        Offset = 0;
        MarkRead();
    }

    /// <summary>Shows a page. The nouns page counts the seen labels as read, once per opening; the scroll goes back to the top.</summary>
    /// <param name="page">The page.</param>
    public void Show(BookPage page)
    {
        if (!IsOpen)
        {
            return;
        }

        if (page != Page)
        {
            Offset = 0;
        }

        Page = page;
        MarkRead();
    }

    /// <summary>Turns to the next page (Tab, Right) or the previous one (Left, Shift+Tab).</summary>
    /// <param name="forward">Whether to go forward.</param>
    public void Turn(bool forward) => Show(BookTabs.Turn(Page, forward));

    /// <summary>Moves the scroll by some lines (the wheel up is negative in the layer's terms: pass the lines to go down).</summary>
    /// <param name="lines">The lines to go down (negative: up).</param>
    public void Scroll(int lines)
    {
        if (IsOpen)
        {
            Offset = Math.Max(0, Offset + lines);
        }
    }

    /// <summary>Keeps the scroll inside a page's lines, once the layer knows how many there are and how many fit.</summary>
    /// <param name="total">The page's lines.</param>
    /// <param name="visible">The lines on screen.</param>
    public void ClampTo(int total, int visible) => Offset = BookScroll.Clamp(Offset, total, visible);

    /// <summary>Closes the book.</summary>
    public void Close() => IsOpen = false;

    private void MarkRead()
    {
        if (Page != BookPage.Nouns || _read || _words is null)
        {
            return;
        }

        _read = true;
        foreach (var row in JournalRows.Build(_words))
        {
            if (row.Seen)
            {
                _words.MarkRead(row.Lemma);
            }
        }
    }
}
