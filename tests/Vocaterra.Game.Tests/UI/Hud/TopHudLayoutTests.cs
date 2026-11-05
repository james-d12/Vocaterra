using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Game.Core.Dialogue;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>
/// The top of the HUD (U01): the objective tracker (top right, three lines and a "+n" at most), the tip box with its controls hint (top left, under the debug overlay when
/// F3 shows it) and the debug overlay never overlap one another, nor the needs' bars and icons or the hotbar, at 1280 x 720 and 800 x 600 at 100, 125 and 150 percent.
/// </summary>
public sealed class TopHudLayoutTests
{
    private static float Small(string text) => text.Length * 7f;

    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in new[] { (1280, 720), (800, 600) })
            {
                foreach (var scale in new[] { 100, 125, 150 })
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    private static bool Apart(PixelBox a, PixelBox b) => !ConversationLayout.Overlaps(a, b);

    private static TrackerView Tracker(int lines, int more)
    {
        var camp = new[]
        {
            new ObjectivePiece("Campamento", ObjectivePieceKind.Title), new ObjectivePiece(" (camp): ", ObjectivePieceKind.Support), new ObjectivePiece("madera", ObjectivePieceKind.Word),
            new ObjectivePiece(" (wood) 0/5 · ", ObjectivePieceKind.Count), new ObjectivePiece("agua", ObjectivePieceKind.Word), new ObjectivePiece(" (water) 0/1", ObjectivePieceKind.Count),
        };
        var order = new[] { new ObjectivePiece("Bring Luz five wood (madera) for her oven at the bakery", ObjectivePieceKind.Support) };
        var all = new List<TrackerLine> { new(camp, [new ObjectivePiece("Campamento: 0/2", ObjectivePieceKind.Title)]) };
        while (all.Count < lines)
        {
            all.Add(new TrackerLine(order, order));
        }

        return new TrackerView(all, more);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheTrackerTheTipBoxTheOverlayTheNeedsAndTheHotbarStayApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var canvas = new PixelBox(0, 0, width, height);
        var tipText = TestGame.Data.Locale.Text("tip.needs"); // the longest of the tips
        var hintWidth = (int)Small(TestGame.Data.Locale.Ui("controlsHint"));
        var hotbar = NeedsHudLayout.HotbarBox(width, height);
        var needs = NeedsHudLayout.Place(width, height, NeedsHudLayout.MaxIcons, stamina: true, "Tengo mucha sed.", "I am very thirsty.", Small);

        foreach (var overlayOn in new[] { false, true })
        {
            // The overlay is drawn only on a canvas at least MinCanvasWidth wide (X08); its panel is 4 px in, three lines tall.
            var overlay = overlayOn && width >= VoxelDebugOverlay.MinCanvasWidth ? new PixelBox(4, 4, 300, (VoxelDebugOverlay.LineCount * 20) + 8) : (PixelBox?)null;
            var rowsOfTip = TextWrap.Wrap(tipText, HudLayout.TipTextWidth(width), Small).Count;
            var tip = HudLayout.TipBlock(width, overlay?.Bottom ?? 0, rowsOfTip, hintWidth);
            if (overlay is { } panel)
            {
                Assert.True(Apart(panel, tip.Block), $"the tip block {tip.Block} overlaps the debug overlay {panel} at {width}x{height}");
            }

            foreach (var (lines, more) in new[] { (1, 0), (2, 0), (3, 0), (3, 1) })
            {
                var tracker = Tracker(lines, more);
                var rows = HudLayout.TrackerRows(tracker, p => Small(p.Text), (float)(width * ObjectiveLayer.WidthShare), width - (2 * HudLayout.Margin) - 16);
                Assert.True(rows.Count <= HudLayout.MaxObjectiveRows, $"{rows.Count} tracker rows");
                var widest = rows.Max(row => row.Sum(p => (int)Small(p.Text)));
                var box = HudLayout.ObjectiveBox(width, widest, rows.Count, tip.Block);
                var label = $"{width}x{height}, overlay {overlayOn}, {lines} lines +{more}";
                Assert.True(box.X >= 0 && box.Right <= canvas.Right && box.Bottom <= canvas.Bottom, $"{label}: the tracker {box} is outside the canvas");
                Assert.True(Apart(box, tip.Block), $"{label}: the tracker {box} overlaps the tip block {tip.Block}");
                if (overlay is { } o)
                {
                    Assert.True(Apart(box, o), $"{label}: the tracker overlaps the overlay");
                }

                Assert.True(Apart(box, hotbar), $"{label}: the tracker overlaps the hotbar");
                Assert.True(Apart(box, needs.Block), $"{label}: the tracker overlaps the needs {needs.Block}");
                Assert.True(Apart(tip.Block, needs.Block) && Apart(tip.Block, hotbar), $"{label}: the tip block overlaps the needs or the hotbar");
                Assert.All(needs.Icons, icon => Assert.True(Apart(box, icon) && Apart(tip.Block, icon)));
                Assert.True(Apart(box, needs.Health) && Apart(tip.Block, needs.Health));
            }
        }
    }

    [Fact]
    public void AMoreCountGoesAfterTheLastRowAndAFullLineGivesWayToItsShortForm()
    {
        var tracker = Tracker(3, 2);
        var rows = HudLayout.TrackerRows(tracker, p => Small(p.Text), 10_000f, 10_000f);
        Assert.Equal(3, rows.Count);
        Assert.Equal(" +2", rows[^1][^1].Text);
        var narrow = HudLayout.TrackerRows(Tracker(2, 0), p => Small(p.Text), 200f, 200f);
        Assert.Equal("Campamento: 0/2", narrow[0].Single().Text); // the checklist is too wide for the row: its title and the count of steps done
    }
}
