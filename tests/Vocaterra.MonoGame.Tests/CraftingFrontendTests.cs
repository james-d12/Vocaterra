using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The P6b front-end pieces that need no GPU: the crafting panel's model and layout, the command line, the scripted craft step, the stack counts and the new art.</summary>
public sealed class CraftingFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static GameSession NewSession() => new(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value, credit: CreditRules.FullTyped);

    [Fact]
    public void TheCraftFlagsParseAsStepsInOrder()
    {
        var args = new[] { "--screenshot", "x.png", "--craft", "mesa", "--place", "mesa@496,182", "--craft", "puerta", "--crafting" };

        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        Assert.Equal(
            [ScriptStepKind.Craft, ScriptStepKind.Place, ScriptStepKind.Craft, ScriptStepKind.CraftingPanel],
            options.Steps.Select(static s => s.Kind));
        Assert.Equal("mesa", options.Steps[0].Item);
        Assert.Equal("puerta", options.Steps[2].Item);
    }

    [Fact]
    public void CraftNeedsAnItemAndTheScreenshotRun()
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "x.png", "--craft", " "], out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.False(LaunchOptions.TryParse(["--craft", "mesa"], out _, out var needs));
        Assert.Contains("--screenshot", needs, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--crafting"], out _, out _));
    }

    [Fact]
    public void TheRowsFollowTheInventoryAndTheStations()
    {
        var session = NewSession();
        var book = session.Recipes;
        var rows = CraftingScreen.Rows(session.Simulation, book);
        Assert.Equal(book.Entries.Count, rows.Count);
        Assert.All(rows, static r => Assert.False(r.CanMake));
        Assert.Equal("Sin material", rows[book.IndexOf("mesa")].Status);

        session.Simulation.GrantItems(ItemId.Wood, 10);
        rows = CraftingScreen.Rows(session.Simulation, book);
        var table = rows[book.IndexOf("mesa")];
        Assert.True(table.CanMake);
        Assert.Equal("Hacer", table.Status);
        Assert.Equal([10], table.Have);
        Assert.True(table.HasEnough(0));

        // A door needs a table in reach: the wood alone is not enough.
        session.Simulation.GrantItems(ItemId.Wood, 6);
        var door = CraftingScreen.Rows(session.Simulation, book)[book.IndexOf("puerta")];
        Assert.Equal(CraftResult.MissingStation, door.Result);
        Assert.Equal("Sin mesa", door.Status);
    }

    [Fact]
    public void AClickOnARowSendsOneCraftForThatRecipe()
    {
        var session = NewSession();
        session.Simulation.GrantItems(ItemId.Wood, 12);
        var layout = new CraftingLayout(1280, 720, session.Recipes.Entries.Count);
        var row = layout.RowAt(layout.RowX + 20, layout.RowY(0) + (layout.RowHeight / 2));
        Assert.Equal(0, row);

        var input = new LiveWorldInput();
        input.QueueCraft(row!.Value);
        var commands = new List<PlayerCommand>();
        input.Fill(commands);
        Assert.Equal([PlayerCommand.Craft(0)], commands);

        session.Step(commands);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Table));
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Wood));

        commands.Clear();
        input.Fill(commands);
        Assert.Empty(commands); // taken once
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
        Assert.Contains("horno", shown);
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
    public void ACraftStepOfTheScriptMakesTheItemAndReportsIt()
    {
        var session = NewSession();
        session.Simulation.GrantItems(ItemId.Wood, 10);
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var script = new ScriptedDialogue(session, dialogue, [ScriptStep.Craft("mesa"), ScriptStep.Craft("puerta"), ScriptStep.Craft("nube"), ScriptStep.CraftingPanel()]);
        var input = new FocusGatedInput(script, focus);
        var commands = new List<PlayerCommand>();
        var sawPanel = false;
        for (var tick = 0; tick < 200 && !script.IsDone; tick++)
        {
            CommandMapper.Fill(input.Poll(session.Simulation.Tick), commands);
            commands.AddRange(script.Commands);
            sawPanel |= script.TakeCraftingRequest() == true;
            session.Step(commands);
        }

        Assert.True(script.IsDone);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Table));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Door)); // no table in reach: refused (the world has none placed)
        Assert.True(sawPanel);
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

    [Fact]
    public void EveryItemHasAnIconThatIsATileOrATool()
    {
        foreach (var definition in ItemDefinition.All)
        {
            var tile = TileArt.TileOfItem(definition.Id);
            if (definition.PlacesTile is { } placed)
            {
                Assert.Equal(placed, tile);
            }
            else
            {
                Assert.Null(tile);
                Assert.InRange(TileArt.ToolIndexOf(definition.Id), 0, TileArt.ToolCount - 1);
            }
        }

        Assert.Equal(3, TileArt.ToolCount);
        Assert.Equal(2, TileArt.ToolIndexOf(ItemId.Sword));
    }

    [Theory]
    [InlineData(TileId.Furnace)]
    [InlineData(TileId.Glass)]
    [InlineData(TileId.Brick)]
    public void TheNewTilesHaveArtThatIsNotEmpty(TileId tile)
    {
        var pixels = TileArt.Pixels(tile);

        Assert.Equal(TileArt.Size * TileArt.Size * 4, pixels.Length);
        Assert.Contains(Enumerable.Range(0, TileArt.Size * TileArt.Size), i => pixels[(i * 4) + 3] == 255);
    }

    [Fact]
    public void TheSwordIconHasPixels()
    {
        var pixels = TileArt.ToolPixels(2);

        Assert.Contains(Enumerable.Range(0, TileArt.Size * TileArt.Size), i => pixels[(i * 4) + 3] == 255);
    }
}
