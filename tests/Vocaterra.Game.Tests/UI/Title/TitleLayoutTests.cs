using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Title;

namespace Vocaterra.Game.Tests.UI.Title;

/// <summary>
/// The title screen's pages on the six canvases the two window sizes and the three text scales make (Q01; as the other layouts are checked, X08): every piece inside the canvas and its panel, nothing on top of anything else,
/// the hints inside the panel at a stand-in font wider than the real one, and the pointer's answers from the same layout the page is drawn with.
/// </summary>
public sealed class TitleLayoutTests
{
    private static readonly (int Width, int Height)[] Windows = [(1280, 720), (800, 600)];
    private static readonly int[] Scales = [100, 125, 150];

    private static readonly Lazy<LocaleText> English = new(() => TestGame.Data.Locale);

    /// <summary>Every window size and scale.</summary>
    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in Windows)
            {
                foreach (var scale in Scales)
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    private static float Small(string text) => text.Length * 7f;

    private static bool Inside(PixelBox box, PixelBox outer) => box.X >= outer.X && box.Y >= outer.Y && box.Right <= outer.Right && box.Bottom <= outer.Bottom;

    private static bool Apart(PixelBox a, PixelBox b) => a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y;

    private static (int Width, int Height) Canvas(int windowWidth, int windowHeight, int scale) => UiScale.Canvas(scale, windowWidth, windowHeight);

    private static void AllApart(string page, params (string Name, PixelBox Box)[] pieces)
    {
        for (var i = 0; i < pieces.Length; i++)
        {
            for (var j = i + 1; j < pieces.Length; j++)
            {
                Assert.True(Apart(pieces[i].Box, pieces[j].Box), $"{page}: {pieces[i].Name} {pieces[i].Box} meets {pieces[j].Name} {pieces[j].Box}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheMainPageFitsTheCanvasWithTheNameAboveThePanelAndTheButtonsStacked(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = Canvas(windowWidth, windowHeight, scale);
        var window = new PixelBox(0, 0, width, height);

        var layout = new TitleLayout(width, height, TitlePage.Main);

        Assert.True(Inside(layout.Panel, window), $"the panel {layout.Panel} is outside the {width}x{height} canvas");
        Assert.True(layout.NameY >= 0 && layout.NameY + TitleLayout.NameHeight <= layout.Panel.Y, "the game's name is not above the panel");
        for (var i = 0; i < TitleSlot.MainCount; i++)
        {
            Assert.True(Inside(layout.Button(i), layout.Panel), $"button {i} leaves the panel");
            if (i > 0)
            {
                Assert.True(layout.Button(i).Y >= layout.Button(i - 1).Bottom, $"button {i} meets the one above");
            }
        }

        Assert.True(layout.Button(TitleSlot.MainCount - 1).Bottom <= layout.Notice.Y, "the last button runs into the notice line");
        Assert.True(layout.Notice.Bottom <= layout.HintY, "the notice runs into the hint");
        Assert.True(Small(English.Value.Ui("titleHint")) <= layout.Panel.Width - 32);
        Assert.True(Small(English.Value.Ui("titleName")) * 6 <= width, "the name's 64 pixel text does not fit across the canvas"); // 42 px a character, wider than the font's
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheNewGamePageFitsTheCanvasAndItsRowsDoNotMeet(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = Canvas(windowWidth, windowHeight, scale);
        var window = new PixelBox(0, 0, width, height);

        var layout = new TitleLayout(width, height, TitlePage.New);

        Assert.True(Inside(layout.Panel, window), $"the panel {layout.Panel} is outside the {width}x{height} canvas");
        foreach (var (name, box) in new[] { ("name field", layout.NameField), ("seed field", layout.SeedField), ("random", layout.RandomButton), ("type", layout.TypeRow), ("create", layout.Create), ("back", layout.NewBack), ("notice", layout.Notice) })
        {
            Assert.True(Inside(box, layout.Panel), $"the {name} {box} leaves the panel");
        }

        AllApart("New", ("name", layout.NameField), ("seed", layout.SeedField), ("random", layout.RandomButton), ("type", layout.TypeRow), ("create", layout.Create), ("back", layout.NewBack), ("notice", layout.Notice));
        Assert.True(layout.NameField.Y - 20 >= layout.ListTop, "the name's label is above the page's rows");
        Assert.True(layout.SeedField.Bottom + 4 + 18 <= layout.TypeRow.Y, "the seed's hint meets the world type");
        Assert.True(layout.TerrainTop >= layout.TypeRow.Bottom, "the terrain options' place starts above the world type's end");
        Assert.True(layout.TerrainTop + TitleLayout.TerrainHeight <= layout.Notice.Y, "the terrain options' place runs into the notice");
        Assert.True(layout.Notice.Bottom <= layout.Create.Y, "the notice runs into the buttons");
        Assert.True(layout.Create.Bottom <= layout.HintY, "the buttons run into the hint");
        Assert.True(Small(English.Value.Ui("titleNewHint")) <= layout.Panel.Width - 32, "the New game hint is wider than the panel");
        Assert.True(Small(English.Value.Ui("titleSeedBlank")) <= layout.NameField.Width, "the seed's hint is wider than the fields");
        Assert.True(Small("< " + English.Value.Ui("titleTypeStreamed") + " >") <= layout.TypeRow.Right - 8 - (layout.TypeRow.X + 8 + 150), "the world type's value is cut");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheLoadPageShowsAtLeastOneWorldAndItsRowsAndButtonsDoNotMeet(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = Canvas(windowWidth, windowHeight, scale);
        var window = new PixelBox(0, 0, width, height);

        var layout = new TitleLayout(width, height, TitlePage.Load);

        Assert.True(Inside(layout.Panel, window), $"the panel {layout.Panel} is outside the {width}x{height} canvas");
        Assert.True(layout.Capacity >= 1, "no world fits");
        var last = layout.WorldRow(layout.Capacity - 1);
        Assert.True(Inside(last, layout.Panel), "the rows leave the panel");
        Assert.True(last.Bottom <= layout.Notice.Y, "the rows run into the notice");
        Assert.True(layout.Notice.Bottom <= layout.LoadButton.Y, "the notice runs into the buttons");
        AllApart("Load", ("load", layout.LoadButton), ("delete", layout.DeleteButton), ("back", layout.LoadBack));
        foreach (var box in new[] { layout.LoadButton, layout.DeleteButton, layout.LoadBack })
        {
            Assert.True(Inside(box, layout.Panel) && box.Bottom <= layout.HintY, "a button leaves the panel or meets the hint");
        }

        Assert.True(Small(English.Value.Ui("titleLoadHint")) <= layout.Panel.Width - 32, "the Load hint is wider than the panel");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheDeleteConfirmationFitsAndItsButtonsDoNotMeet(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = Canvas(windowWidth, windowHeight, scale);
        var window = new PixelBox(0, 0, width, height);

        var layout = new TitleLayout(width, height, TitlePage.ConfirmDelete);

        Assert.True(Inside(layout.Panel, window));
        AllApart("Confirm", ("message", layout.Message), ("delete", layout.ConfirmDelete), ("keep", layout.ConfirmKeep));
        Assert.True(Inside(layout.ConfirmDelete, layout.Panel) && Inside(layout.ConfirmKeep, layout.Panel) && Inside(layout.Message, layout.Panel));
        Assert.True(layout.ConfirmDelete.Bottom <= layout.HintY);
        Assert.True(Small(English.Value.Ui("titleConfirmHint")) <= layout.Panel.Width - 32);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ThePointerFindsEachThingOnEachPageWhereItIsDrawn(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = Canvas(windowWidth, windowHeight, scale);
        var metrics = new TitleMetrics(() => (width, height));
        static (int X, int Y) Middle(PixelBox box) => (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        var main = new TitleLayout(width, height, TitlePage.Main);
        for (var i = 0; i < TitleSlot.MainCount; i++)
        {
            var (x, y) = Middle(main.Button(i));
            Assert.Equal(i, metrics.SlotAt(TitlePage.Main, 0, 0, x, y));
        }

        Assert.Null(metrics.SlotAt(TitlePage.Main, 0, 0, 1, 1));

        var page = new TitleLayout(width, height, TitlePage.New);
        (PixelBox Box, int Slot)[] newSlots =
        [
            (page.NameField, TitleSlot.NameField), (page.SeedField, TitleSlot.SeedField), (page.RandomButton, TitleSlot.RandomSeed), (page.TypeRow, TitleSlot.WorldType), (page.Create, TitleSlot.Create), (page.NewBack, TitleSlot.Back),
        ];
        foreach (var (box, slot) in newSlots)
        {
            var (x, y) = Middle(box);
            Assert.Equal(slot, metrics.SlotAt(TitlePage.New, 0, 0, x, y));
        }

        var load = new TitleLayout(width, height, TitlePage.Load);
        Assert.Equal(load.Capacity, metrics.LoadCapacity);
        for (var shown = 0; shown < load.Capacity; shown++)
        {
            var (x, y) = Middle(load.WorldRow(shown));
            Assert.Equal(7 + shown, metrics.SlotAt(TitlePage.Load, 7, 50, x, y)); // scrolled by 7 of 50 worlds
            Assert.Null(metrics.SlotAt(TitlePage.Load, 0, shown, x, y)); // a row with no world under it is nothing
        }

        (PixelBox Box, int Slot)[] loadSlots = [(load.LoadButton, TitleSlot.LoadAction), (load.DeleteButton, TitleSlot.DeleteAction), (load.LoadBack, TitleSlot.Back)];
        foreach (var (box, slot) in loadSlots)
        {
            var (x, y) = Middle(box);
            Assert.Equal(slot, metrics.SlotAt(TitlePage.Load, 0, 3, x, y));
        }

        var confirm = new TitleLayout(width, height, TitlePage.ConfirmDelete);
        var (dx, dy) = Middle(confirm.ConfirmDelete);
        var (kx, ky) = Middle(confirm.ConfirmKeep);
        Assert.Equal(TitleSlot.ConfirmDelete, metrics.SlotAt(TitlePage.ConfirmDelete, 0, 1, dx, dy));
        Assert.Equal(TitleSlot.ConfirmKeep, metrics.SlotAt(TitlePage.ConfirmDelete, 0, 1, kx, ky));
        Assert.Null(metrics.SlotAt(TitlePage.Settings, 0, 1, 10, 10)); // the settings page is the menu's own
    }

    [Fact]
    public void TheEscMenusMainPageFitsFiveButtonsAtEveryScale()
    {
        foreach (var (windowWidth, windowHeight) in Windows)
        {
            foreach (var scale in Scales)
            {
                var (width, height) = Canvas(windowWidth, windowHeight, scale);
                var layout = new Vocaterra.Game.UI.Menu.MenuLayout(width, height, Vocaterra.Game.Core.Menu.MenuPage.Main);

                Assert.Equal(5, Vocaterra.Game.UI.Menu.MenuLayout.ButtonCount);
                Assert.True(Inside(layout.Button(Vocaterra.Game.UI.Menu.MenuLayout.ButtonCount - 1), layout.Panel), $"Exit leaves the panel at {width}x{height}");
                Assert.True(layout.Panel.Bottom <= height, $"the menu panel runs off the canvas at {width}x{height}");
            }
        }
    }
}
