using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Crafting;

namespace Vocaterra.Game.Tests.Component;

/// <summary>The P6b front-end pieces that need no GPU: the crafting panel's model and layout, the command line, the scripted craft step, the stack counts and the new art.</summary>
public sealed class CraftingFrontendTests
{
    private static VoxelSession NewSession() => RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);

    [Fact]
    public void TheRowsFollowTheInventoryAndTheStationsOfTheWorkbench()
    {
        var session = NewSession();
        var book = session.Recipes;
        var rows = CraftingScreen.Rows(session.Simulation, book);
        Assert.Equal(book.Entries.Count(static e => !e.Rule.Repairs), rows.Count); // a repair row (I07) shows only while something of its item is worn
        Assert.All(rows, static r => Assert.False(r.CanMake));
        Assert.Equal("Missing material", rows[book.IndexOf("antorcha")].Status(RouteScene.Shipped.Locale));

        // The torch needs two wood and the workbench in reach: the session starts away from it.
        session.Simulation.GrantItems(ItemId.Wood, 2);
        rows = CraftingScreen.Rows(session.Simulation, book);
        var torch = rows[book.IndexOf("antorcha")];
        Assert.Equal(CraftResult.MissingStation, torch.Result);
        Assert.Equal("Needs mesa", torch.Status(RouteScene.Shipped.Locale));
        Assert.Equal([2], torch.Have);
        Assert.True(torch.HasEnough(0));
    }

    [Fact]
    public void TheLayoutHitsEachRowAndNothingBetweenOrOutside()
    {
        var layout = new CraftingLayout(1280, 720, 9);

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(i, layout.RowAt(layout.RowX + 5, layout.RowY(i)));
            Assert.Equal(i, layout.RowAt(layout.RowX + layout.RowWidth - 1, layout.RowY(i) + layout.RowHeight - 1));
        }

        Assert.Null(layout.RowAt(layout.RowX + 5, layout.RowY(0) + layout.RowHeight + 1)); // the gap
        Assert.Null(layout.RowAt(layout.RowX - 1, layout.RowY(0)));
        Assert.Null(layout.RowAt(layout.RowX + 5, layout.Y));
        Assert.True(layout.Y >= 0);
        Assert.True(layout.Y + layout.Height <= 720);
    }

    [Fact]
    public void ASmallWindowShrinksTheRowsToFit()
    {
        var layout = new CraftingLayout(640, 480, 9);

        Assert.True(layout.RowHeight < CraftingLayout.MaxRowHeight);
        Assert.True(layout.Y + layout.Height <= 480 + 8);
        Assert.True(layout.Width <= 640);
    }

    private static readonly (int Width, int Height)[] Windows = [(640, 480), (800, 600), (1024, 768), (1280, 720), (1920, 1080), (2560, 1440), (3840, 2160)];
    private static readonly int[] Scales = [100, 125, 150];

    public static TheoryData<int, int, int, int> ScrollCases
    {
        get
        {
            var data = new TheoryData<int, int, int, int>();
            foreach (var (width, height) in Windows)
            {
                foreach (var scale in Scales)
                {
                    foreach (var rows in new[] { 24, 40 })
                    {
                        data.Add(width, height, scale, rows);
                    }
                }
            }

            return data;
        }
    }

    // I10b, QA-2026-11-19-ui-1: at every window size and text scale the panel, its header and its footer are on the canvas, however many rows there are; the rows that do
    // not fit are reached by scrolling, and every row has a place on the panel at some scroll.
    [Theory]
    [MemberData(nameof(ScrollCases))]
    public void TheCraftingPanelStaysOnTheCanvasAtEverySizeAndScrollsItsRows(int windowWidth, int windowHeight, int scale, int rowCount)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var shown = new HashSet<int>();
        var first = new CraftingLayout(width, height, rowCount);
        for (var scroll = 0; scroll <= first.MaxScroll; scroll++)
        {
            var layout = new CraftingLayout(width, height, rowCount, scroll);
            Assert.True(layout.Y >= 0 && layout.Y + layout.Height <= height, $"{width}x{height}: the panel ({layout.Y}, {layout.Height}) is off the canvas at scroll {scroll}");
            Assert.True(layout.X >= 0 && layout.X + layout.Width <= width);
            Assert.True(layout.VisibleRows >= 1);
            Assert.True(layout.RowHeight >= CraftingLayout.MinRowHeight);
            var lastBottom = layout.RowY(layout.Scroll + layout.VisibleRows - 1) + layout.RowHeight;
            Assert.True(lastBottom + CraftingLayout.RowGap + CraftingLayout.Padding + CraftingLayout.FooterHeight <= layout.Y + layout.Height + CraftingLayout.RowGap, "the last row runs into the footer");
            for (var i = layout.Scroll; i < layout.Scroll + layout.VisibleRows; i++)
            {
                Assert.Equal(i, layout.RowAt(layout.RowX + 3, layout.RowY(i) + 1));
                shown.Add(i);
            }

            Assert.Null(layout.RowAt(layout.RowX + 3, layout.RowY(layout.Scroll - 1)));
            if (layout.Scrolls)
            {
                var track = layout.Track;
                var thumb = layout.Thumb;
                Assert.True(thumb.Y >= track.Y && thumb.Y + thumb.Height <= track.Y + track.Height, "the thumb is outside its track");
                Assert.True(track.Y + track.Height <= layout.Y + layout.Height);
            }
        }

        Assert.Equal(rowCount, shown.Count); // every row, the last repair row included, has its turn on the panel
    }

    [Fact]
    public void ARoomyWindowShowsEveryRowWithNoScrollMark()
    {
        var layout = new CraftingLayout(1920, 1080, 24, scroll: 5);

        Assert.False(layout.Scrolls);
        Assert.Equal(24, layout.VisibleRows);
        Assert.Equal(0, layout.Scroll); // nothing to scroll: the offset is held at the top
    }

    [Fact]
    public void OpeningThePanelReadsEachShownWordOnceAndNeverCreditsASpokenUse()
    {
        var session = NewSession();
        var screen = new CraftingScreen();
        var shown = CraftingScreen.LemmasShown(session.Recipes);

        Assert.Contains("mesa", shown);
        Assert.Contains("madera", shown);
        Assert.Contains("antorcha", shown);
        Assert.Equal(shown.Count, shown.Distinct().Count());

        screen.Open(session.Words, shown);
        Assert.True(screen.IsOpen);
        var reads = session.Words.RecordOf("madera").Read;
        Assert.Equal(1, reads);
        Assert.Equal(0, session.Words.RecordOf("madera").CreditUnits);
        Assert.Equal(0, session.Words.RecordOf("madera").Spoken);
        Assert.Equal(0.0, session.Words.SaturationOf("madera"), 5); // looking does not colour

        screen.Close();
        Assert.False(screen.IsOpen);
        screen.Open(session.Words, shown);
        Assert.Equal(2, session.Words.RecordOf("madera").Read); // once per opening, not once per frame
    }

    [Fact]
    public void TheCountOfAStackIsDrawnOnADarkPlateInWhiteWithAContrastThatReads()
    {
        Assert.Equal("4", CountBadge.Text(4, showOne: false));
        Assert.Equal("1", CountBadge.Text(1, showOne: true));
        Assert.Null(CountBadge.Text(1, showOne: false));
        Assert.Null(CountBadge.Text(0, showOne: true));
        Assert.Equal("99", CountBadge.Text(99, showOne: false));

        // WCAG AAA is 7: the old white-on-pale-planks was about 1.3.
        Assert.True(CountBadge.Contrast() >= 7.0, $"The count's contrast is {CountBadge.Contrast():0.0}.");
        Assert.NotEqual(CountBadge.TextColour, CountBadge.PlateColour);
    }
}
