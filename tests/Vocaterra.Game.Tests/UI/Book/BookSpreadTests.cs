using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Book;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Tests.UI.Book;

// The book as a book (S28): the spread's geometry at both window sizes, the ribbons' hit-testing, the page turn's state, the verbs page's list and selection,
// and that nothing is written outside its page.
public sealed class BookSpreadTests
{
    private static readonly (int Width, int Height)[] Sizes = [(1280, 720), (800, 600)];

    public static TheoryData<int, int> WindowSizes => new() { { 1280, 720 }, { 800, 600 } };

    private static bool Inside(BookRect inner, BookRect outer) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private static bool Overlap(BookRect a, BookRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void TheSpreadIsTwoPagesOnPaperWithASpineBetweenThemInsideTheCoverInsideTheWindow(int width, int height)
    {
        var g = new BookGeometry(width, height);
        var window = new BookRect(0, 0, width, height);

        Assert.True(Inside(g.Panel, window));
        Assert.True(Inside(g.Paper, g.Panel));
        Assert.True(g.Paper.X - g.Panel.X >= BookGeometry.CoverBorder, "the cover shows at the sides");
        Assert.True(g.Panel.Bottom - g.Paper.Bottom >= BookGeometry.CoverBottom, "the cover shows under the paper, where the hints are written");
        Assert.True(Inside(g.LeftPage, g.Paper));
        Assert.True(Inside(g.RightPage, g.Paper));
        Assert.True(Inside(g.Spine, g.Paper));
        Assert.Equal(g.LeftPage.Right, g.Spine.X);
        Assert.Equal(g.Spine.Right, g.RightPage.X);
        Assert.Equal(g.LeftPage.Width, g.RightPage.Width);
        Assert.False(Overlap(g.LeftPage, g.RightPage));
        Assert.Equal((g.Paper.Width - BookGeometry.SpineWidth) / 2, g.PageWidth);
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void TheLeftPageListsAndTheRightPageDetailsEachInsideItsOwnPageWithItsPadding(int width, int height)
    {
        var g = new BookGeometry(width, height);

        Assert.Equal(g.LeftPage.X + BookGeometry.Padding, g.ContentLeft);
        Assert.Equal(g.LeftPage.Right - BookGeometry.Padding, g.ContentRight);
        Assert.Equal(g.RightPage.X + BookGeometry.Padding, g.DetailLeft);
        Assert.Equal(g.RightPage.Right - BookGeometry.Padding, g.DetailRight);
        Assert.True(g.ContentTop > g.RuleTop && g.RuleTop > g.TitleTop);
        Assert.True(g.ContentBottom <= g.Paper.Bottom - 20, "the scroll position is written under the list, on the paper");
        Assert.True(g.ContentBottom <= g.Panel.Bottom - 40, "the key hints sit under the content, on the cover");
        Assert.True(g.ContentRight - g.ContentLeft >= 330, "the list is wide enough for a row with a button: the lessons' heading keeps most of its row");
        Assert.True(g.DetailRight - g.DetailLeft >= 330);
    }

    [Theory]
    [InlineData(1280, 720, 28)]
    [InlineData(1200, 720, 28)]
    [InlineData(800, 600, 22)]
    public void TheListHoldsTwentyEightRowsAtTheDefaultSizeAndTwentyTwoAt800By600(int width, int height, int rows)
    {
        // U07: the 32 px title and its rule take 80 px at the top of the paper (the content began at 68), so the list lost a row at each size (29 and 23 before).
        var g = new BookGeometry(width, height);

        Assert.Equal(rows, g.LinesVisible);
        Assert.Equal(g.Paper.Y + 80, g.ContentTop);
        Assert.Equal(g.Paper.Y + 36, g.TitleTop);
        Assert.Equal(g.Paper.Y + 72, g.RuleTop);
        Assert.True(g.ContentTop + (g.LinesVisible * BookGeometry.LineHeight) <= g.ContentBottom);
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void TheThirtyTwoPixelTitleFitsBetweenTheRibbonsAndTheRuleAndTheDetailsFirstRowStartsUnderTheRule(int width, int height)
    {
        var g = new BookGeometry(width, height);

        Assert.True(g.RuleTop - g.TitleTop >= 32 + 4, "a 32 px line of text and a gap above the rule");
        Assert.True(g.ContentTop - g.RuleTop >= 8, "the first row is under the rule");
        Assert.True(g.TitleTop >= Enum.GetValues<BookPage>().Select(g.TabRect).Max(static r => r.Bottom + 6), "under the lit ribbon, which hangs 6 px lower");
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void NoRibbonLeavesTheCoverOrTouchAnotherAndTheLongestLabelFitsItsRibbon(int width, int height)
    {
        var g = new BookGeometry(width, height);
        var ribbons = Enum.GetValues<BookPage>().Select(g.TabRect).ToList();

        Assert.All(ribbons, ribbon => Assert.True(Inside(ribbon, g.Panel)));
        for (var i = 0; i < ribbons.Count; i++)
        {
            for (var j = i + 1; j < ribbons.Count; j++)
            {
                Assert.False(Overlap(ribbons[i], ribbons[j]), $"ribbons {i} and {j} overlap");
            }
        }

        // They hang from the top edge: they begin above the paper and end above the title (the lit one hangs a little lower still).
        Assert.All(ribbons, ribbon => Assert.True(ribbon.Y < g.Paper.Y && ribbon.Bottom + 6 <= g.TitleTop));
        Assert.True(ribbons.Max(static r => r.Right) <= g.Panel.Right - BookGeometry.Padding);

        // "What I said" is the longest label; at the font's 16 px a character is under 9 px wide.
        var longest = BookTabs.Labels(RouteScene.Shipped.Locale).Max(static l => l.Length);
        Assert.True((longest * 9) + 8 <= BookGeometry.TabWidth, $"{longest} characters in {BookGeometry.TabWidth} px");
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void AClickOnARibbonFindsItsPageAndAClickOnThePaperOrBetweenRibbonsFindsNone(int width, int height)
    {
        var g = new BookGeometry(width, height);
        foreach (var page in Enum.GetValues<BookPage>())
        {
            var ribbon = g.TabRect(page);
            Assert.Equal(page, g.TabAt(ribbon.X, ribbon.Y)); // the corners belong to the ribbon
            Assert.Equal(page, g.TabAt(ribbon.X + (ribbon.Width / 2), ribbon.Y + (ribbon.Height / 2)));
            Assert.Equal(page, g.TabAt(ribbon.Right - 1, ribbon.Bottom - 1));
            Assert.NotEqual<BookPage?>(page, g.TabAt(ribbon.Right, ribbon.Y + 4)); // the next pixel is the gap, never this ribbon again
        }

        var first = g.TabRect(BookPage.Nouns);
        var second = g.TabRect(BookPage.Verbs);
        Assert.Null(g.TabAt(first.Right + 1, first.Y + 4)); // in the gap between two ribbons
        Assert.Equal(BookGeometry.TabGap, second.X - first.Right);
        Assert.Null(g.TabAt(first.X + 4, first.Y - 1)); // above a ribbon
        Assert.Null(g.TabAt(first.X + 4, first.Bottom)); // under it, on the paper
        Assert.Null(g.TabAt(g.ContentLeft, g.ContentTop)); // the list
        Assert.Null(g.TabAt(g.DetailLeft, g.ContentTop)); // the detail
        Assert.Null(g.TabAt(0, 0));
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void TheLeafIsHingedAtTheSpineAndStaysOnThePaper(int width, int height)
    {
        var g = new BookGeometry(width, height);

        // Forward: it starts as the whole right page, narrows to nothing at the spine (halfway), then widens on the left page.
        var start = g.Leaf(0, forward: true);
        Assert.Equal(g.RightPage.X, start.X);
        Assert.Equal(g.PageWidth, start.Width);
        Assert.Equal(0, g.Leaf(0.5, forward: true).Width);
        var end = g.Leaf(1, forward: true);
        Assert.Equal(g.LeftPage.Right, end.Right);
        Assert.Equal(g.PageWidth, end.Width);

        // Back is the mirror: from the left page to the right one.
        Assert.Equal(g.LeftPage.Right, g.Leaf(0, forward: false).Right);
        Assert.Equal(g.RightPage.X, g.Leaf(1, forward: false).X);

        for (var i = 0; i <= 20; i++)
        {
            foreach (var forward in new[] { true, false })
            {
                var leaf = g.Leaf(i / 20.0, forward);
                Assert.True(Inside(leaf, g.Paper), $"progress {i / 20.0}");
                Assert.False(Overlap(leaf, g.Spine), "a leaf is on one page or the other, never across the spine");
            }
        }
    }

    [Fact]
    public void TurningToAnotherPageStartsAPageTurnThatEndsAfterItsDurationOnTheFrameClock()
    {
        var book = new BookModel();
        book.Open(new WordProgress(TestGame.Nouns));
        Assert.False(book.IsTurning); // opening is not a turn

        book.Show(BookPage.Verbs);
        Assert.True(book.IsTurning);
        Assert.True(book.TurnForward);
        Assert.Equal(0, book.TurnProgress);

        book.Advance(BookModel.TurnSeconds / 2);
        Assert.True(book.IsTurning);
        Assert.Equal(0.5, book.TurnProgress, 3);
        Assert.Equal(BookPage.Verbs, book.Page); // the page is already the new one: the leaf only dresses the change

        book.Advance(BookModel.TurnSeconds); // more than is left
        Assert.False(book.IsTurning);
        Assert.Equal(0, book.TurnProgress);
    }

    [Fact]
    public void ThePageTurnGoesTheWayTheKeyOrTheRibbonSaysAndSomeChangesAreNotTurns()
    {
        var book = new BookModel();
        book.Open(new WordProgress(TestGame.Nouns));

        book.Show(BookPage.Nouns); // the page already on screen
        Assert.False(book.IsTurning);

        book.Turn(forward: false); // Shift+Tab or Left on the first page wraps to the last: a turn back
        Assert.Equal(BookPage.Said, book.Page);
        Assert.True(book.IsTurning);
        Assert.False(book.TurnForward);
        book.FinishTurn();

        book.Show(BookPage.Verbs); // a click on an earlier ribbon turns back
        Assert.False(book.TurnForward);
        book.FinishTurn();
        book.Show(BookPage.Lessons);
        Assert.True(book.TurnForward);
        book.FinishTurn();

        book.Show(BookPage.Phrases, animate: false); // the scripted run's opening page
        Assert.False(book.IsTurning);
        Assert.Equal(BookPage.Phrases, book.Page);

        book.Show(BookPage.Said);
        book.Advance(0.05);
        book.Close();
        Assert.False(book.IsTurning); // closing ends it
        Assert.Equal(0, book.TurnProgress);
        book.Advance(1); // a closed book is not turned
        Assert.Equal(0, book.TurnProgress);
    }

    [Fact]
    public void ANegativeFrameTimeNeverRunsThePageTurnBackwards()
    {
        var book = new BookModel();
        book.Open(new WordProgress(TestGame.Nouns));
        book.Show(BookPage.Verbs);
        book.Advance(0.1);
        var before = book.TurnProgress;

        book.Advance(-5);

        Assert.Equal(before, book.TurnProgress);
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void TheTablesCellsStayInsideTheRightPageAndUnderTheListsTop(int width, int height)
    {
        var g = new BookGeometry(width, height);
        var detailWidth = g.DetailRight - g.DetailLeft;
        var top = g.ContentTop + 38 + BookGeometry.LineHeight + 10; // under the verb at 32 px and its English (the layer's own offsets)
        var area = new BookRect(g.DetailLeft, g.ContentTop, detailWidth, g.ContentBottom - g.ContentTop);
        for (var tenses = 1; tenses <= ConjugationTable.AllColumns.Count; tenses++)
        {
            // The layer lays the tenses out in blocks of as many columns as the page holds (X08), each block a table of its own under the one before; the first is checked.
            var columns = ConjugationLayout.ColumnsPerBlock(detailWidth, tenses);
            Assert.True(top + ConjugationLayout.Height + (3 * BookGeometry.LineHeight) <= g.ContentBottom, "the table and a few lines under it fit the page");
            var cells = new List<BookRect>();
            for (var c = 0; c < columns; c++)
            {
                cells.Add(ConjugationLayout.Header(g.DetailLeft, top, detailWidth, columns, c));
                for (var r = 0; r < ConjugationLayout.PersonRows; r++)
                {
                    cells.Add(ConjugationLayout.Cell(g.DetailLeft, top, detailWidth, columns, r, c));
                }
            }

            for (var r = 0; r < ConjugationLayout.PersonRows; r++)
            {
                cells.Add(ConjugationLayout.Label(g.DetailLeft, top, r));
            }

            Assert.All(cells, cell => Assert.True(Inside(cell, area), $"{cell} is outside {area} with {columns} columns"));
            Assert.True(ConjugationLayout.ColumnWidth(detailWidth, columns) >= 70, "a column holds a form such as van a comer (about 70 px at the font, or two short lines)");
            Assert.False(Overlap(ConjugationLayout.Label(g.DetailLeft, top, 0), ConjugationLayout.Cell(g.DetailLeft, top, detailWidth, columns, 0, 0)));
        }
    }

    [Fact]
    public void TheVerbsPageListsOneRowPerVerbAndTheSelectionMovesAmongThemWithoutAnyButton()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var recordings = BookRecordings.None;
        Assert.Empty(BookPages.ListFor(session, BookPage.Verbs, recordings)); // no verbs met yet: an empty list, and the layer says so

        var full = BookPages.Verbs(RouteScene.Shipped.Locale, VerbsOfAFixedRecord(), TestGame.Data.Glosses, TestGame.Data.Phrases, TestGame.Bundle.SpanishMorphology());
        var list = BookPages.InListOnly(full);
        Assert.Equal(["beber", "ir"], list.Where(static l => l.Verb is not null).Select(static l => l.Verb));
        Assert.Equal([BookLineKind.Entry, BookLineKind.Entry, BookLineKind.Gap, BookLineKind.Meta], list.Select(static l => l.Kind)); // the verbs, a gap and the quiet note
        Assert.True(full.Count > list.Count, "the page's own lines (the forms and their last sentences) are still built, for the right page");
        Assert.All(list, static l => Assert.Null(l.Speak));
        Assert.Equal([0, 1], BookSpeakers.Selectable(list));
        Assert.Empty(BookSpeakers.Speakable(list));
    }

    [Fact]
    public void ABookWithNoVerbMetStillOpensItsVerbsPageAndTheOtherPagesKeepTheirOwnLists()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var recordings = BookRecordings.None;

        foreach (var page in new[] { BookPage.Nouns, BookPage.Phrases, BookPage.Lessons, BookPage.Said })
        {
            Assert.Equal(BookPages.ForPage(session, page, recordings).Count(static l => l.InList), BookPages.ListFor(session, page, recordings).Count); // the list is the page less its details
        }

        Assert.Null(BookPages.VerbTable(session, "querer")); // not met: no table
        Assert.Empty(BookPages.TaughtForms(session)); // no lesson has been started
    }

    [Fact]
    public void ABlockIsAnEntryWithItsDetailLinesAndTheListNeverHoldsADetail()
    {
        var lines = new List<BookLine>
        {
            new(BookTone.Known, "Known", Kind: BookLineKind.Section),
            new(BookTone.Known, "el agua"),
            new(BookTone.Hint, "heard 1", Kind: BookLineKind.Detail),
            new(BookTone.Hint, "last: x", Kind: BookLineKind.Detail),
            BookLine.Gap,
            new(BookTone.Known, "la fruta"),
            new(BookTone.Hint, "heard 2", Kind: BookLineKind.Detail),
        };

        Assert.Equal(["el agua", "heard 1", "last: x"], BookPages.BlockOf(lines, 1).Select(static l => l.Text));
        Assert.Equal(["el agua", "heard 1", "last: x"], BookPages.BlockOf(lines, 3).Select(static l => l.Text)); // any line of the item finds the whole item
        Assert.Empty(BookPages.BlockOf(lines, 0)); // a section is no item
        Assert.Empty(BookPages.BlockOf(lines, 4)); // a gap is no item
        Assert.Equal(["la fruta", "heard 2"], BookPages.BlockOf(lines, 5).Select(static l => l.Text));
        Assert.Empty(BookPages.BlockOf(lines, 99));
        Assert.Empty(BookPages.BlockOf(lines, -1));

        // The list drops the details, so a row's index is not a line's: BlockOfRow counts the rows of the list.
        var list = BookPages.InListOnly(lines);
        Assert.Equal(["Known", "el agua", string.Empty, "la fruta"], list.Select(static l => l.Text));
        Assert.Equal(["la fruta", "heard 2"], BookPages.BlockOfRow(lines, 3).Select(static l => l.Text));
        Assert.Equal(["el agua", "heard 1", "last: x"], BookPages.BlockOfRow(lines, 1).Select(static l => l.Text));
        Assert.Empty(BookPages.BlockOfRow(lines, 2));
        Assert.Equal([1, 3], BookSpeakers.Selectable(list)); // only the entries are selected: the section and the gap are not rows to choose
    }

    [Fact]
    public void ASectionIsOneRowSoTheButtonsOfTheEntriesAfterItStayWhereTheirRowsAre()
    {
        // The list's index is the row's: a section band is one row, and the scroll and the speaker buttons count rows exactly as the layer draws them.
        var g = new BookGeometry(1280, 720);
        var list = new List<BookLine>
        {
            new(BookTone.Learning, "Learning", Kind: BookLineKind.Section),
            new(BookTone.Seen, "la fruta", Speak: "card.fruta"),
            BookLine.Gap,
            new(BookTone.Unseen, "Not met yet", Kind: BookLineKind.Section),
            new(BookTone.Seen, "la madera", Speak: "card.madera"),
        };
        var fruta = BookSpeakers.ButtonRect(g, g.ContentTop + BookGeometry.LineHeight);
        var madera = BookSpeakers.ButtonRect(g, g.ContentTop + (4 * BookGeometry.LineHeight));
        Assert.Equal(0, BookSpeakers.At(g, list, 0, fruta.X + 2, fruta.Y + 2));
        Assert.Equal(1, BookSpeakers.At(g, list, 0, madera.X + 2, madera.Y + 2));
        Assert.Null(BookSpeakers.At(g, list, 0, fruta.X + 2, g.ContentTop + 2)); // the band above it has no button
        Assert.Equal([1, 4], BookSpeakers.Speakable(list));
        Assert.Equal([1, 4], BookSpeakers.Selectable(list));
    }

    [Fact]
    public void ANewLaunchFlagOpensAVerbsTableAndASizeSetsTheWindow()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--book-verb", "IR", "--size", "800x600"], out var options, out var error), error);
        Assert.Equal("ir", options.BookVerb);
        Assert.Equal(BookPage.Verbs, options.BookStart);
        Assert.True(options.Journal);
        Assert.Equal(800, options.WindowWidth);
        Assert.Equal(600, options.WindowHeight);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--size", "big"], out _, out error));
        Assert.Contains("--size", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--size", "100x100"], out _, out _));
    }

    private static IReadOnlyList<WordRow> VerbsOfAFixedRecord()
    {
        var record = new LearnerRecord();
        record.Append(new InputEntry(1, 1, GuideSession.DefaultStartedAt + 1, InputSource.Typed, "bebí agua, voy", "guide", "guide-open", InputPlace.Dialogue, null, "state_action", true, false, [],
        [
            new GuideWord("bebí", new TextSpan(0, 4), "beber", "verb", WordRole.Word, "1", "sg", "pret", "ind"),
            new GuideWord("voy", new TextSpan(11, 3), "ir", "verb", WordRole.Word, "1", "sg", "pres", "ind"),
        ]));
        return record.Verbs;
    }
}
