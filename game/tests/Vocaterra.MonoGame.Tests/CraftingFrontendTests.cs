using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The P6b front-end pieces that need no GPU: the crafting panel's model and layout, the command line, the scripted craft step, the stack counts and the new art.</summary>
public sealed class CraftingFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static VoxelSession NewSession() => RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped);

    [Fact]
    public void TheRowsFollowTheInventoryAndTheStationsOfTheWorkbench()
    {
        var session = NewSession();
        var book = session.Recipes;
        var rows = CraftingScreen.Rows(session.Simulation, book);
        Assert.Equal(book.Entries.Count, rows.Count);
        Assert.All(rows, static r => Assert.False(r.CanMake));
        Assert.Equal("Sin material", rows[book.IndexOf("antorcha")].Status);

        // The torch needs two wood and the workbench in reach: the session starts away from it.
        session.Simulation.GrantItems(ItemId.Wood, 2);
        rows = CraftingScreen.Rows(session.Simulation, book);
        var torch = rows[book.IndexOf("antorcha")];
        Assert.Equal(CraftResult.MissingStation, torch.Result);
        Assert.Equal("Sin mesa", torch.Status);
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

    [Fact]
    public void BothSlotLayersDrawTheirCountsThroughTheBadge()
    {
        var root = Repo.Path("src", "Vocaterra.MonoGame");
        foreach (var file in new[] { "HotbarLayer.cs", "InventoryLayer.cs" })
        {
            var code = File.ReadAllText(Path.Combine(root, file));
            Assert.Contains("CountBadge.Text", code, StringComparison.Ordinal);
            Assert.Contains("DrawHelpers.Badge", code, StringComparison.Ordinal);
        }
    }

}
