using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;

namespace Vocaterra.MonoGame.Tests;

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

    // The book is opened by one method (VoxelGame.OpenBook), called by J and by the book item's use. The keys and the item are read by the game
    // loop, which a test cannot drive, so the test reads the source as the architecture tests do: both paths reach OpenBook.
    [Fact]
    public void JAndTheBookItemBothOpenTheSameBook()
    {
        var source = File.ReadAllText(Repo.Path("src", "Vocaterra.MonoGame", "VoxelGame.cs"));
        var journal = source.IndexOf("Pressed(Keys.J))", StringComparison.Ordinal);
        Assert.True(journal >= 0, "the J key is read");
        Assert.Contains("OpenBook();", source[journal..(journal + 120)], StringComparison.Ordinal);

        var item = source.IndexOf("Item: ItemId.Book", StringComparison.Ordinal);
        Assert.True(item >= 0, "the book item is used");
        Assert.Contains("OpenBook();", source[item..(item + 200)], StringComparison.Ordinal);
    }

    private static BookModel OpenBook()
    {
        var words = new WordProgress(NounCatalog.Load(GameBundle.Load()));
        var model = new BookModel();
        model.Open(words);
        return model;
    }
}
