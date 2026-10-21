using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The P4 front-end pieces that need no GPU: the greyscale copy, the journal's rows and model, the toasts, the scripted cursor and the launch options.</summary>
public sealed class WordsFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static GameSession NewSession() => new(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value);

    [Fact]
    public void TheGreyCopyTakesTheLumaOfEachPixelAndKeepsItsAlpha()
    {
        byte[] colour =
        [
            255, 0, 0, 255,      // red: 0.299 * 255 = 76
            0, 255, 0, 128,      // green, half transparent: 0.587 * 255 = 150
            0, 0, 255, 0,        // blue, transparent: 0.114 * 255 = 29
            255, 255, 255, 255,  // white stays white
            10, 10, 10, 255,     // grey stays grey
        ];
        var before = (byte[])colour.Clone();

        var grey = Greyscale.ToLuma(colour);

        Assert.Equal(
            [76, 76, 76, 255, 150, 150, 150, 128, 29, 29, 29, 0, 255, 255, 255, 255, 10, 10, 10, 255],
            grey);
        Assert.Equal(before, colour);
        Assert.Equal(76, Greyscale.Luma(255, 0, 0));
        Assert.Equal(255, Greyscale.Luma(255, 255, 255));
    }

    [Fact]
    public void AGreyCopyOfTheTileArtIsGreyAndKeepsTheShapeOfEachTile()
    {
        var pixels = new List<byte>();
        foreach (var definition in Vocaterra.Simulation.Tiles.TileDefinition.All)
        {
            pixels.AddRange(TileArt.Pixels(definition.Id));
        }

        var grey = Greyscale.ToLuma([.. pixels]);

        Assert.Equal(pixels.Count, grey.Length);
        for (var i = 0; i < grey.Length; i += 4)
        {
            Assert.Equal(grey[i], grey[i + 1]);
            Assert.Equal(grey[i], grey[i + 2]);
            Assert.Equal(pixels[i + 3], grey[i + 3]);
        }
    }

    [Fact]
    public void AnRgbaBufferWithAPartialPixelIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Greyscale.ToLuma([1, 2, 3]));
    }

    [Fact]
    public void ToastsShowInOrderForFourSecondsEach()
    {
        var toasts = new ToastQueue();
        toasts.Show("first");
        toasts.Show("second");

        Assert.Equal("first", toasts.Current(0)!.Text);
        Assert.Equal("first", toasts.Current(ToastQueue.Duration - 1)!.Text);
        Assert.Equal("second", toasts.Current(ToastQueue.Duration)!.Text);
        Assert.Equal(0, toasts.Waiting);
        Assert.Null(toasts.Current(ToastQueue.Duration * 2));
    }

    [Fact]
    public void TheJournalListsEveryNounUnseenAtFirstAndKnownAfterThreeTurns()
    {
        var session = NewSession();
        var rows = JournalRows.Build(session.Words);
        Assert.Equal(session.Nouns.Nouns.Count, rows.Count);
        Assert.All(rows, row => Assert.Equal("¿?", row.Label));
        Assert.All(rows, row => Assert.Equal(0, row.Pips));

        var tree = session.Facts.NearestOfNoun(session.PlayerTile, "árbol")!.Tile;
        for (var i = 0; i < WordProgress.UsesToKnow; i++)
        {
            session.SkipTime(i == 0 ? 0 : 61 * 60); // the gaps of D-game-6 as amended
            session.Say("es un árbol", tree);
        }

        rows = JournalRows.Build(session.Words);
        var first = rows[0];
        Assert.Equal("árbol", first.Lemma);
        Assert.Equal("el árbol", first.Label);
        Assert.True(first.Known);
        Assert.Equal(WordProgress.UsesToKnow, first.Pips);
        Assert.Equal(3, first.Record.Spoken);
        Assert.Equal(1.0, first.Saturation);
        Assert.Equal(WordStage.Known, rows.Single(static r => r.Lemma == "árbol").Record.Stage);
    }

    [Fact]
    public void OpeningTheJournalCountsEachSeenLabelAsRead()
    {
        var session = NewSession();
        session.Say("es un árbol");
        var journal = new JournalModel();

        journal.Open(session.Words);
        journal.Open(session.Words); // already open: no second read
        Assert.True(journal.IsOpen);
        Assert.Equal(1, session.Words.RecordOf("árbol").Read);
        Assert.Equal(0, session.Words.RecordOf("piedra").Read);

        journal.Close();
        Assert.False(journal.IsOpen);
    }

    [Fact]
    public void TheScriptedCursorFindsTheNearestTreeForAnAccentlessNoun()
    {
        var session = NewSession();
        var expected = session.Facts.NearestOfNoun(session.PlayerTile, "árbol")!.Tile;

        Assert.Equal(expected, new ScriptedCursor(null, "arbol").Resolve(session));
        Assert.Equal(expected, new ScriptedCursor(null, "árbol").Resolve(session));
        Assert.Equal(new TilePoint(40, 30), new ScriptedCursor(new TilePoint(40, 30), null).Resolve(session));
        Assert.Null(new ScriptedCursor(null, null).Resolve(session));
        Assert.Equal("arbol", ScriptedCursor.Fold("Árbol"));
    }

    [Fact]
    public void TheJournalAndPointFlagsNeedAScreenshotAndTheCursorTakesOneOfThem()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--say", "es un árbol", "--point-nearest", "arbol", "--journal"], out var options, out var error));
        Assert.Empty(error);
        Assert.Equal("arbol", options.PointNearest);
        Assert.True(options.Journal);
        Assert.Null(options.Point);

        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--point", "40,30"], out options, out _));
        Assert.Equal(new TilePoint(40, 30), options.Point);

        Assert.False(LaunchOptions.TryParse(["--journal"], out _, out var needsScreenshot));
        Assert.Contains("--screenshot", needsScreenshot);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--point", "forty"], out _, out var malformed));
        Assert.Contains("--point", malformed);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--point", "1,2", "--point-nearest", "arbol"], out _, out var both));
        Assert.Contains("not both", both);
    }
}
