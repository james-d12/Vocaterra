using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Tests.UI.Common;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Game.UI.Inventory;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>
/// N05: the needs' HUD (bars, drive icons, the stage line) and the inventory's list of needs on the six canvases the two window sizes and three scales make: nothing leaves the
/// canvas, the bars, icons and line never overlap the hotbar, the objective tracker at its tallest, or the tip box (and the line under it), and what is shown follows the meters.
/// </summary>
public sealed class NeedsHudLayoutTests(ITestOutputHelper output)
{
    private static float Small(string text) => text.Length * 7f; // a stand-in font wider than the real one, as UiScaleLayoutTests uses

    private static bool Apart(PixelBox a, PixelBox b) => a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y;

    private static bool Inside(PixelBox box, PixelBox outer) => box.X >= outer.X && box.Y >= outer.Y && box.Right <= outer.Right && box.Bottom <= outer.Bottom;

    public static TheoryData<int, int, int> Cases => UiScaleLayoutTests.Cases;

    // The widest stage lines the game has: the longest Spanish and the longest English among them.
    private static (string Spanish, string English) LongestLine()
    {
        var data = TestGame.Data;
        var keys = Vocaterra.Game.Core.Content.GameDataValidator.HudLineKeys(data).Where(static k => k.StartsWith("need.", StringComparison.Ordinal)).ToList();
        var spanish = keys.Select(k => data.Words.HudLine(k)!).MaxBy(static t => t.Length)!;
        var english = keys.Select(k => data.Locale.Text(k)).MaxBy(static t => t.Length)!;
        return (spanish, english);
    }

    // The tip box and the "Esc: menu" line under it, as TipLayer draws them for the needs tip (the longest of the first-time tips).
    private static PixelBox TipArea(int canvasWidth)
    {
        var text = TestGame.Data.Locale.Text("tip.needs");
        var width = Math.Min(480, canvasWidth / 2);
        var rows = TextWrap.Wrap(text, width - 16, Small);
        var height = 8 + 20 + (rows.Count * 20) + 20;
        return new PixelBox(TipLayer.Margin, TipLayer.Margin, width, height + 6 + 20);
    }

    // The checklist at its tallest: three rows, as wide as the canvas lets it be.
    private static PixelBox TrackerArea(int canvasWidth) => HudLayout.ObjectiveBox(canvasWidth, canvasWidth, HudLayout.MaxObjectiveRows);

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheBarsTheIconsAndTheStageLineNeverOverlapTheHotbarTheTrackerOrTheTipAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var canvas = new PixelBox(0, 0, width, height);
        var hotbar = HotbarLayer.BoxFor(width, height);
        var tracker = TrackerArea(width);
        var tip = TipArea(width);
        var (spanish, english) = LongestLine();

        foreach (var icons in new[] { 0, 1, 2, 3 })
        {
            foreach (var stamina in new[] { false, true })
            {
                foreach (var withLine in new[] { false, true })
                {
                    var boxes = NeedsHudLayout.Place(width, height, icons, stamina, withLine ? spanish : null, withLine ? english : null, Small);
                    var parts = new List<(string Name, PixelBox Box)> { ("block", boxes.Block), ("health", boxes.Health) };
                    if (boxes.Stamina is { } staminaBox)
                    {
                        parts.Add(("stamina", staminaBox));
                    }

                    parts.AddRange(boxes.Icons.Select(static (box, i) => ("icon " + i, box)));
                    if (boxes.Line is { } line)
                    {
                        parts.Add(("line", line));
                    }

                    var label = $"{width}x{height} icons {icons} stamina {stamina} line {withLine}";
                    Assert.Equal(icons, boxes.Icons.Count);
                    Assert.Equal(stamina, boxes.Stamina is not null);
                    Assert.Equal(withLine, boxes.Line is not null);
                    foreach (var (name, box) in parts)
                    {
                        Assert.True(Inside(box, canvas), $"{label}: {name} {box} is outside the canvas");
                        Assert.True(Apart(box, hotbar), $"{label}: {name} {box} overlaps the hotbar {hotbar}");
                        Assert.True(Apart(box, tracker), $"{label}: {name} {box} overlaps the objective tracker {tracker}");
                        Assert.True(Apart(box, tip), $"{label}: {name} {box} overlaps the tip {tip}");
                    }

                    Assert.True(Inside(boxes.Health, boxes.Block), $"{label}: the health bar is outside its block");
                    Assert.All(boxes.Icons, icon => Assert.True(Inside(icon, boxes.Block) && Apart(icon, boxes.Health), $"{label}: an icon {icon} overlaps the bars"));
                    if (boxes.Stamina is { } stamina2)
                    {
                        Assert.True(Inside(stamina2, boxes.Block) && Apart(stamina2, boxes.Health), $"{label}: the bars overlap");
                    }

                    if (boxes.Line is { } lineBox)
                    {
                        Assert.True(Apart(lineBox, boxes.Block), $"{label}: the line overlaps the block");
                    }
                }
            }
        }

        var worst = NeedsHudLayout.Place(width, height, 3, true, spanish, english, Small);
        output.WriteLine($"{width}x{height}: block {worst.Block} ({(worst.Beside ? "beside the hotbar" : "above its left end")}), line {worst.Line}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheBubblesStandClearOfTheNeedsHudAtEveryScaleAndMoveOnlyWhereTheyWouldOverlap(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var hotbar = HotbarLayer.BoxFor(width, height);
        var tracker = TrackerArea(width);
        var (spanish, english) = LongestLine();
        var moved = 0;

        foreach (var icons in new[] { 0, 1, 2, 3 })
        {
            foreach (var stamina in new[] { false, true })
            {
                foreach (var withLine in new[] { false, true })
                {
                    var needs = NeedsHudLayout.Place(width, height, icons, stamina, withLine ? spanish : null, withLine ? english : null, Small);
                    var raise = BreathHudLayout.RaiseAbove(width, height, needs);
                    var bubbles = BreathHudLayout.Bubbles(width, height, raise);
                    var row = new PixelBox(bubbles[0].X, bubbles[0].Y, bubbles[^1].Right - bubbles[0].X, BreathHudLayout.BubbleSize);
                    var label = $"{width}x{height} icons {icons} stamina {stamina} line {withLine} raise {raise}";
                    Assert.True(Apart(row, needs.Block), $"{label}: the bubbles overlap the needs block {needs.Block}");
                    if (needs.Line is { } line)
                    {
                        Assert.True(Apart(row, line), $"{label}: the bubbles overlap the stage line {line}");
                    }

                    Assert.True(Apart(row, hotbar), $"{label}: the bubbles overlap the hotbar");
                    Assert.True(Apart(row, tracker), $"{label}: the bubbles overlap the objective tracker");
                    Assert.True(Inside(row, new PixelBox(0, 0, width, height)), $"{label}: the bubbles leave the canvas");
                    if (raise > 0)
                    {
                        moved++;
                        Assert.True(raise >= BreathHudLayout.Lift, label);
                    }
                    else
                    {
                        Assert.Equal(BreathHudLayout.Bubbles(width, height)[0].Y, bubbles[0].Y);
                    }
                }
            }
        }

        output.WriteLine($"{width}x{height}: the bubbles are lifted in {moved} of 16 cases");
        Assert.Equal(0, BreathHudLayout.RaiseAbove(width, height, null));
    }

    [Fact]
    public void TheBlockStandsBesideTheHotbarWhereThereIsRoomAndAboveItsLeftEndWhereThereIsNot()
    {
        Assert.True(NeedsHudLayout.Place(1280, 720, 1, true, null, null, Small).Beside);
        Assert.True(NeedsHudLayout.Place(800, 600, 1, true, null, null, Small).Beside);
        Assert.False(NeedsHudLayout.Place(640, 480, 1, true, null, null, Small).Beside);
        Assert.False(NeedsHudLayout.Place(533, 400, 1, true, null, null, Small).Beside);
    }

    [Fact]
    public void OnlyTheHealthBarRemainsWhenTheDrivesAreHealthyAndStaminaIsFull()
    {
        var boxes = NeedsHudLayout.Place(1280, 720, 0, false, null, null, Small);
        Assert.Equal(boxes.Health, boxes.Block);
        Assert.Null(boxes.Stamina);
        Assert.Empty(boxes.Icons);
        Assert.Null(boxes.Line);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheInventoryHoldsTheFiveNeedsUnderItsSlotsAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var layout = new InventoryLayout(width, height, 40, 5);
        var canvas = new PixelBox(0, 0, width, height);

        Assert.True(Inside(layout.Panel, canvas), $"the panel {layout.Panel} is outside the canvas {width}x{height}");
        var cells = Enumerable.Range(0, 40).Select(layout.Cell).ToList();
        Assert.All(cells, cell => Assert.True(Inside(cell, layout.Panel)));
        var lastRowBottom = cells.Max(static c => c.Bottom);
        Assert.True(lastRowBottom <= layout.NeedsTop, "the needs' list starts under the last row of slots");
        var needsBottom = layout.NeedsTop + (5 * layout.NeedsRowStep);
        Assert.True(needsBottom <= layout.Panel.Bottom - layout.HintsOffset, "the needs' list ends above the key hints");
        Assert.True(layout.HintsOffset >= 20 && layout.Panel.Bottom - layout.HintsOffset + 14 <= layout.Panel.Bottom, "the key hints stay inside the panel");
        Assert.True(layout.DividerY >= 26, "the title sits above the rule");
        output.WriteLine($"{width}x{height}: slots {layout.CellWidth} x {layout.CellHeight}, compact {layout.Compact}, tight {layout.Tight}, panel {layout.Panel}");
    }

    [Fact]
    public void WithoutTheNeedsTheInventoryIsWhatItWas()
    {
        var plain = new InventoryLayout(800, 600, 40);
        Assert.False(plain.Tight);
        Assert.Equal(44, plain.GridTop - plain.Panel.Y);
        Assert.Equal(36, plain.DividerY);
        Assert.Equal(26, plain.HintsOffset);
        Assert.Equal(440, plain.Panel.Height);
    }

    [Fact]
    public void TheDriveIconsAreShapesOnTheirGridOfEighteen()
    {
        foreach (var drive in new[] { Drive.Hunger, Drive.Thirst, Drive.Sleep })
        {
            var runs = NeedIcons.Shape(drive);
            Assert.True(runs.Count >= 8, $"{drive} has a shape");
            Assert.All(runs, run => Assert.True(run.Y is >= 0 and < NeedIcons.Size && run.X0 >= 0 && run.X1 < NeedIcons.Size && run.X0 <= run.X1));
            Assert.True(runs.Sum(static r => r.X1 - r.X0 + 1) >= 60, $"{drive}'s icon is not a speck");
        }

        Assert.Equal(3, NeedIcons.Marks(Drive.Hunger).Count);
        Assert.Empty(NeedIcons.Marks(Drive.Thirst));
    }
}
