using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.TestSupport;

namespace Vocaterra.Game.Tests.Component;

// The book's pages, tabs, scroll and screen geometry (S20): tab order, scrolling, the tab a click lands on, and that it reads at 1280 x 720.
public sealed class BookModelTests
{
    [Fact]
    public void TheTabsAreInTheOrderNounsVersesPhrasesLessonsAndWhatISaid()
    {
        Assert.Equal(["Nouns", "Verbs", "Phrases", "Lessons", "What I said"], BookTabs.Labels);
        Assert.Equal(BookPage.Verbs, BookTabs.Turn(BookPage.Nouns, forward: true));
        Assert.Equal(BookPage.Phrases, BookTabs.Turn(BookPage.Verbs, forward: true));
        Assert.Equal(BookPage.Lessons, BookTabs.Turn(BookPage.Phrases, forward: true));
        Assert.Equal(BookPage.Said, BookTabs.Turn(BookPage.Lessons, forward: true));
        Assert.Equal(BookPage.Nouns, BookTabs.Turn(BookPage.Said, forward: true)); // Tab wraps round
        Assert.Equal(BookPage.Said, BookTabs.Turn(BookPage.Nouns, forward: false)); // Left goes back, and wraps
        Assert.Equal(BookPage.Lessons, BookTabs.Turn(BookPage.Said, forward: false));
    }

    [Fact]
    public void TurningThePageWithTabLeftAndRightVisitsEveryPageInOrder()
    {
        var model = OpenBook();
        var seen = new List<BookPage> { model.Page };
        for (var i = 0; i < 5; i++)
        {
            model.Turn(forward: true);
            seen.Add(model.Page);
        }

        Assert.Equal(new[] { BookPage.Nouns, BookPage.Verbs, BookPage.Phrases, BookPage.Lessons, BookPage.Said, BookPage.Nouns }, seen);
        model.Turn(forward: false);
        Assert.Equal(BookPage.Said, model.Page);
    }

    [Fact]
    public void AClosedBookHasNoPageToTurnAndOpeningItAgainStartsOnTheNounsPage()
    {
        var model = new BookModel();
        model.Show(BookPage.Verbs);
        Assert.False(model.IsOpen);
        Assert.Equal(BookPage.Nouns, model.Page);

        model.Scroll(9); // no effect while closed
        Assert.Equal(0, model.Offset);
    }

    [Fact]
    public void TheScrollMovesInLinesAndStaysInsideThePage()
    {
        var model = OpenBook();
        model.Scroll(5);
        Assert.Equal(5, model.Offset);
        model.Scroll(-100);
        Assert.Equal(0, model.Offset); // never above the first line

        model.Scroll(40);
        model.ClampTo(total: 30, visible: 10);
        Assert.Equal(20, model.Offset); // the last line is at the bottom
        model.ClampTo(total: 5, visible: 10);
        Assert.Equal(0, model.Offset); // a page that fits does not scroll
    }

    [Fact]
    public void TurningThePageGoesBackToTheTopOfTheNewPage()
    {
        var model = OpenBook();
        model.Scroll(12);
        model.Show(BookPage.Nouns); // the same page keeps its place
        Assert.Equal(12, model.Offset);

        model.Turn(forward: true);
        Assert.Equal(0, model.Offset);
    }

    [Fact]
    public void TheRightPageScrollsInPixelsAndStaysInsideItsDetail()
    {
        var model = OpenBook();
        model.ScrollDetail(40);
        Assert.Equal(40, model.DetailOffset);
        model.ScrollDetail(-100);
        Assert.Equal(0, model.DetailOffset); // never above the top

        model.ScrollDetail(500);
        model.ClampDetail(content: 300, view: 200);
        Assert.Equal(100, model.DetailOffset); // the last of the detail is at the bottom of the page
        model.ClampDetail(content: 150, view: 200);
        Assert.Equal(0, model.DetailOffset); // a detail that fits does not scroll

        var closed = new BookModel();
        closed.ScrollDetail(40); // no effect while closed
        Assert.Equal(0, closed.DetailOffset);
    }

    [Fact]
    public void TheMoreBelowMarkerShowsOnlyWhileWritingLiesUnderThePage()
    {
        Assert.True(DetailScroll.MoreBelow(offset: 0, content: 300, view: 200));
        Assert.True(DetailScroll.MoreBelow(offset: 50, content: 300, view: 200));
        Assert.False(DetailScroll.MoreBelow(offset: 100, content: 300, view: 200)); // at the end
        Assert.False(DetailScroll.MoreBelow(offset: 0, content: 150, view: 200)); // it all fits
    }

    [Fact]
    public void TheRightPageScrollStartsAtTheTopWhenTheSelectionOrThePageChanges()
    {
        var model = OpenBook();
        model.SelectRow(2);
        model.ScrollDetail(60);
        model.SelectRow(2); // the same row keeps its place
        Assert.Equal(60, model.DetailOffset);
        model.SelectRow(4); // another row starts at its top
        Assert.Equal(0, model.DetailOffset);

        model.ScrollDetail(60);
        model.MoveSelection(0, count: 5); // no move, so no change
        Assert.Equal(60, model.DetailOffset);
        model.MoveSelection(-1, count: 5);
        Assert.Equal(0, model.DetailOffset);

        model.ScrollDetail(30);
        model.Show(BookPage.Verbs); // another page starts at its top
        Assert.Equal(0, model.DetailOffset);
        model.ScrollDetail(30);
        model.Show(BookPage.Verbs); // the same page keeps its place
        Assert.Equal(30, model.DetailOffset);
    }

    [Fact]
    public void TheDetailOfALineKeepsItsScrollUntilTheLineChanges()
    {
        var model = OpenBook();
        model.ScrollDetail(40);
        model.KeepDetail(7); // the first frame after the book opens keeps the scroll it was given
        Assert.Equal(40, model.DetailOffset);
        model.KeepDetail(7);
        Assert.Equal(40, model.DetailOffset);
        model.KeepDetail(9); // a detail with no row selected (the first line on screen) moved on
        Assert.Equal(0, model.DetailOffset);
    }

    [Fact]
    public void TheBookDetailFlagScrollsTheRightPageAndNeedsAPositiveNumber()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--book-detail", "4"], out var options, out var error), error);
        Assert.Equal(4, options.BookDetailLines);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--book-detail", "0"], out _, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TheScrollClampIsNeverNegative(int offset)
    {
        Assert.Equal(0, BookScroll.Clamp(offset, total: 40, visible: 10));
        Assert.Equal(0, BookScroll.Clamp(7, total: 3, visible: 10));
        Assert.Equal(30, BookScroll.Clamp(99, total: 40, visible: 10));
    }

    [Fact]
    public void AtTwelveEightyBySevenTwentyTheBookReadsAndFitsItsTabs()
    {
        var geometry = new BookGeometry(1280, 720);
        var panel = geometry.Panel;

        Assert.Equal(BookGeometry.MaxPanelWidth, panel.Width);
        Assert.True(geometry.LinesVisible >= 25, $"{geometry.LinesVisible} lines");
        Assert.True(geometry.TabRect(BookPage.Said).Right <= panel.Right - BookGeometry.Padding, "the last tab fits the panel");
        Assert.True(geometry.ContentBottom <= panel.Bottom - 40, "the key hints sit under the content");
        Assert.InRange(panel.Y, 0, 720);
        Assert.True(panel.Bottom <= 720);
    }

    [Fact]
    public void TheFontCoversEveryCharacterTheBookDraws()
    {
        var cmap = TrueTypeCmap.Load(Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf"));
        var strings = new List<string>
        {
            BookTabs.Title, BookTabs.KeyHints, "lines 1 to 29 of 80   wheel, Page Up, Page Down", "Known: 3 of 18   ·   Session 1", "Without help: 0   ·   Session 1",
            BookVerbLabels.Separator, "¿quieres? — do you want?", "¿estás? — are you?", "he/she/it wants", "you all want! (you all)", "let's want", "that I want",
            "completed in session 2", "in progress: 1 of 2 units done", "not started", "help used: 0   I don't know: 0   repeats asked: 0   turns: 0",
            "unit 1", "2 of 2 steps", "lesson First words with Luna, u1", "said, talk to The guide", "typed", "button", "used 1   correct 1", "forms: agua 1",
            "last: \"¿quieres pan?\" = Would you like bread?", "...", "Session 2",

            // S28: the spread's headings, the empty pages and the conjugation table.
            "Your book  -  Verbs", "Highlighted: said (x = times). Plain: taught. Dash: not yet.", "What you said", "Choose a verb on the left page.",
            "No verbs yet. Use a verb in a sentence and it comes here.", "Nothing said yet.", "Nothing here yet.", "x12", "-", "more below",
        };
        strings.AddRange(ConjugationTable.AllColumns.Select(static c => c.Title));
        strings.AddRange(ConjugationTable.Persons.Select(static p => p.Label));
        strings.AddRange(Enum.GetValues<BookPage>().Select(static page => $"{BookTabs.Title}  -  {BookTabs.LabelOf(page)}"));
        strings.AddRange(BookTabs.Labels);
        strings.AddRange(Enum.GetValues<BookPage>().Select(static page => BookTabs.LabelOf(page)));
        var missing = strings.SelectMany(static s => s).Where(c => !cmap.Covers(c)).Distinct().Select(static c => $"U+{(int)c:X4} {c}").ToList();
        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("--book", null)]
    [InlineData("--journal", null)]
    [InlineData("--journal-phrases", "phrases")]
    [InlineData("--book-page", "nouns")]
    [InlineData("--book-page", "verbs")]
    [InlineData("--book-page", "lessons")]
    [InlineData("--book-page", "said")]
    public void TheBookFlagsOpenTheBookAndTheirPage(string flag, string? page)
    {
        var args = flag == "--book-page" ? new[] { "--screenshot", "a.png", flag, page! } : new[] { "--screenshot", "a.png", flag };
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        Assert.True(options.Journal);
        Assert.Equal(page is null ? null : (BookPage?)Enum.Parse<BookPage>(page, ignoreCase: true), options.BookStart);
    }

    [Fact]
    public void ABookPageNamedWrongIsRefused()
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--book-page", "maps"], out _, out var error));
        Assert.Contains("nouns, verbs, phrases, lessons or said", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AClickOnATabTurnsToThatPageAndAClickBesideTheTabsTurnsNothing()
    {
        var geometry = new BookGeometry(1280, 720);
        foreach (var page in Enum.GetValues<BookPage>())
        {
            var tab = geometry.TabRect(page);
            Assert.Equal(page, geometry.TabAt(tab.X + 4, tab.Y + 4));
        }

        Assert.Null(geometry.TabAt(0, 0));
        Assert.Null(geometry.TabAt(geometry.ContentTop, geometry.ContentTop)); // the content, not a tab
    }

    [Fact]
    public void ANarrowWindowKeepsTheBookInsideItsEdges()
    {
        var geometry = new BookGeometry(800, 600);
        Assert.True(geometry.Panel.X >= 0);
        Assert.True(geometry.Panel.Right <= 800);
        Assert.True(geometry.LinesVisible >= 20);
    }

    private static BookModel OpenBook()
    {
        var words = new WordProgress(NounCatalog.Load(GameBundle.Load()));
        var model = new BookModel();
        model.Open(words);
        return model;
    }
}
