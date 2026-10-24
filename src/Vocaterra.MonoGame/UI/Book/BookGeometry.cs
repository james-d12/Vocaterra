namespace Vocaterra.MonoGame.UI.Book;

/// <summary>
/// The book's screen geometry at a viewport size (S28): the cover, the paper of the open book as two pages with the spine between them, the ribbon
/// bookmarks along the top edge and the areas the pages are written in. The left page lists (<see cref="ContentLeft"/> to <see cref="ContentRight"/>, the lines
/// the scroll moves over); the right page shows the selected item's detail (<see cref="DetailLeft"/> to <see cref="DetailRight"/>). The layer and the mouse
/// read the same numbers, so a click lands on the ribbon that is drawn there. Plain values.
/// </summary>
public sealed record BookGeometry(int ViewportWidth, int ViewportHeight)
{
    /// <summary>The widest the cover is.</summary>
    public const int MaxPanelWidth = 1180;

    /// <summary>The gap from the window's edge to the cover.</summary>
    public const int Margin = 10;

    /// <summary>The padding inside a page, between its edge and its writing.</summary>
    public const int Padding = 16;

    /// <summary>The height of one line of content, in pixels.</summary>
    public const int LineHeight = 19;

    /// <summary>The widest a ribbon's click area is, in pixels (it narrows when the paper is too narrow for the ribbons side by side, X08).</summary>
    public const int TabWidth = 124;

    /// <summary>The narrowest a ribbon is: the longest label (Phrases, Lessons) is seven letters, about 45 px in the font, with 8 px either side.</summary>
    public const int MinTabWidth = 72;

    /// <summary>The gap between two ribbons.</summary>
    public const int TabGap = 6;

    /// <summary>The height of a ribbon (it hangs from above the paper's top edge into the page).</summary>
    public const int TabHeight = 44;

    /// <summary>The width of the cover showing at the left, the right and the top of the paper.</summary>
    public const int CoverBorder = 9;

    /// <summary>The height of the cover above the paper (the ribbons rise out of it).</summary>
    public const int CoverTop = 18;

    /// <summary>The height of the cover under the paper (the key hints are written on it).</summary>
    public const int CoverBottom = 30;

    /// <summary>The width of the spine between the two pages.</summary>
    public const int SpineWidth = 14;

    /// <summary>The cover.</summary>
    public BookRect Panel
    {
        get
        {
            var width = Math.Min(MaxPanelWidth, ViewportWidth - (Margin * 2));
            return new BookRect((ViewportWidth - width) / 2, Margin, width, ViewportHeight - (Margin * 2));
        }
    }

    /// <summary>The paper: the cover less its border, under the ribbons' rise and above the hints.</summary>
    public BookRect Paper => new(Panel.X + CoverBorder, Panel.Y + CoverTop, Panel.Width - (CoverBorder * 2), Panel.Height - CoverTop - CoverBottom);

    /// <summary>The width of one page.</summary>
    public int PageWidth => (Paper.Width - SpineWidth) / 2;

    /// <summary>The left page: the list.</summary>
    public BookRect LeftPage => new(Paper.X, Paper.Y, PageWidth, Paper.Height);

    /// <summary>The right page: the selected item's detail.</summary>
    public BookRect RightPage => new(Paper.X + PageWidth + SpineWidth, Paper.Y, PageWidth, Paper.Height);

    /// <summary>The spine between the pages.</summary>
    public BookRect Spine => new(Paper.X + PageWidth, Paper.Y, SpineWidth, Paper.Height);

    /// <summary>The title's line, under the ribbons.</summary>
    public int TitleTop => Paper.Y + 38;

    /// <summary>The top of the ribbons' labels.</summary>
    public int TabsTop => Panel.Y + 12;

    /// <summary>The first ribbon's left edge.</summary>
    public int TabsLeft => Paper.X + 20;

    /// <summary>The rule under the title.</summary>
    public int RuleTop => Paper.Y + 60;

    /// <summary>The top of the content.</summary>
    public int ContentTop => Paper.Y + 68;

    /// <summary>The left edge of the list (the text of a line starts here, before its indent).</summary>
    public int ContentLeft => LeftPage.X + Padding;

    /// <summary>The right edge of the list (a row's right-hand text, lamps, pips and speaker button end here).</summary>
    public int ContentRight => LeftPage.Right - Padding;

    /// <summary>The bottom of the content (the scroll position sits under it, on the paper).</summary>
    public int ContentBottom => Paper.Bottom - 28;

    /// <summary>The left edge of the right page's writing.</summary>
    public int DetailLeft => RightPage.X + Padding;

    /// <summary>The right edge of the right page's writing.</summary>
    public int DetailRight => RightPage.Right - Padding;

    /// <summary>The height of the writing on the right page (the detail scrolls within it, X03).</summary>
    public int DetailHeight => ContentBottom - ContentTop;

    /// <summary>How many whole lines of the list fit (at least one).</summary>
    public int LinesVisible => Math.Max(1, (ContentBottom - ContentTop) / LineHeight);

    /// <summary>The width of one ribbon: <see cref="TabWidth"/> when all of them fit along the paper (from its left padding), otherwise the share that does, never under <see cref="MinTabWidth"/>.</summary>
    public int TabPixels
    {
        get
        {
            var count = Enum.GetValues<BookPage>().Length;
            var room = Paper.Width - (2 * (TabsLeft - Paper.X)) - ((count - 1) * TabGap);
            return Math.Clamp(room / count, MinTabWidth, TabWidth);
        }
    }

    /// <summary>The click area of a ribbon, by page.</summary>
    /// <param name="page">The page.</param>
    public BookRect TabRect(BookPage page) => new(TabsLeft + ((int)page * (TabPixels + TabGap)), Panel.Y + 4, TabPixels, TabHeight);

    /// <summary>The page whose ribbon is at a point, or <see langword="null"/> when the point is on no ribbon.</summary>
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

    /// <summary>
    /// The turning leaf at a moment of the page turn (<paramref name="progress"/> from 0 to 1): a sheet hinged at the spine. In the first half it is on the right
    /// page (forward) or the left page (back) and narrows to nothing at the spine; in the second half it widens on the other page. Its width is
    /// <c>|cos(pi * progress)|</c> of a page.
    /// </summary>
    /// <param name="progress">How far the turn has gone, 0 to 1.</param>
    /// <param name="forward">Whether the turn goes to a later page.</param>
    public BookRect Leaf(double progress, bool forward)
    {
        var p = Math.Clamp(progress, 0, 1);
        var width = (int)Math.Round(PageWidth * Math.Abs(Math.Cos(Math.PI * p)));
        var onRight = forward ? p < 0.5 : p >= 0.5;
        return onRight
            ? new BookRect(RightPage.X, Paper.Y, width, Paper.Height)
            : new BookRect(LeftPage.Right - width, Paper.Y, width, Paper.Height);
    }
}
