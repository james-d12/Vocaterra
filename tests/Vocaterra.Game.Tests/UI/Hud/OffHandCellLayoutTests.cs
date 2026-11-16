using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Tests.UI.Common;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Hud;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>I04b: the off-hand cell left of the hotbar: set apart by a gap, clear of the needs' HUD and the bubble meter at every canvas the window sizes and scales make, and the hotbar where it was with nothing in the off-hand.</summary>
public sealed class OffHandCellLayoutTests(ITestOutputHelper output)
{
    private static float Small(string text) => text.Length * 7f;

    private static bool Apart(PixelBox a, PixelBox b) => a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y;

    public static TheoryData<int, int, int> Cases => UiScaleLayoutTests.Cases;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyCanvas_When_TheOffHandCellIsPlaced_Then_ItIsOneSlotLeftOfTheHotbarWithAGapAtTheSameHeightAndOnTheCanvas(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var hotbar = HotbarLayer.BoxFor(width, height);

        var cell = HotbarLayer.OffHandBoxFor(width, height);

        Assert.Equal(HotbarLayer.SlotSize, cell.Width);
        Assert.Equal(HotbarLayer.SlotSize, cell.Height);
        Assert.Equal(hotbar.Y, cell.Y);
        Assert.Equal(HotbarLayer.OffHandGap, hotbar.X - cell.Right);
        Assert.True(HotbarLayer.OffHandGap > HotbarLayer.Gap, "the cell is set apart by more than the hotbar's own gap");
        Assert.True(cell.X >= 0, $"the cell {cell} leaves the canvas {width}x{height}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_AnyCanvas_When_TheNeedsHudIsPlacedWithAnOffHand_Then_NothingOfItOverlapsTheOffHandCellTheHotbarOrTheBubbles(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);
        var cell = HotbarLayer.OffHandBoxFor(width, height);
        var hotbar = HotbarLayer.BoxFor(width, height);
        var spanish = TestGame.Data.Words.HudLine("need.thirst.4")!;
        var english = TestGame.Data.Locale.Text("need.thirst.4");
        var beside = 0;

        foreach (var icons in new[] { 0, 3, 4 })
        {
            foreach (var stamina in new[] { false, true })
            {
                foreach (var withLine in new[] { false, true })
                {
                    var boxes = NeedsHudLayout.Place(width, height, icons, stamina, withLine ? spanish : null, withLine ? english : null, Small, offHand: true);
                    var parts = new List<PixelBox> { boxes.Block, boxes.Health };
                    parts.AddRange(boxes.Icons);
                    if (boxes.Stamina is { } staminaBar)
                    {
                        parts.Add(staminaBar);
                    }

                    if (boxes.Line is { } line)
                    {
                        parts.Add(line);
                    }

                    beside += boxes.Beside ? 1 : 0;
                    Assert.All(parts, part =>
                    {
                        Assert.True(Apart(part, cell), $"{part} overlaps the off-hand cell {cell} on {width}x{height}");
                        Assert.True(Apart(part, hotbar), $"{part} overlaps the hotbar {hotbar} on {width}x{height}");
                        Assert.True(part.X >= 0, $"{part} leaves the canvas");
                    });

                    var raise = BreathHudLayout.RaiseAbove(width, height, boxes);
                    Assert.All(BreathHudLayout.Bubbles(width, height, raise), bubble => Assert.True(Apart(bubble, cell), $"a bubble overlaps the off-hand cell on {width}x{height}"));
                }
            }
        }

        output.WriteLine($"{width}x{height}: cell {cell}, hotbar {hotbar}, block beside the hotbar in {beside} of 12 placements");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_NoOffHandItem_When_TheNeedsHudIsPlaced_Then_ItIsExactlyWhereItWasBeforeTheCellExisted(int windowWidth, int windowHeight, int scale)
    {
        var (width, height) = UiScale.Canvas(scale, windowWidth, windowHeight);

        var without = NeedsHudLayout.Place(width, height, 3, true, "Tengo sed.", "I'm thirsty.", Small);
        var explicitFalse = NeedsHudLayout.Place(width, height, 3, true, "Tengo sed.", "I'm thirsty.", Small, offHand: false);

        Assert.Equal(without, explicitFalse);
        var hotbar = HotbarLayer.BoxFor(width, height);
        var besideBefore = hotbar.X - NeedsHudLayout.Spacing - NeedsHudLayout.BarWidth >= HudLayout.Margin; // the rule as it was before the cell
        Assert.Equal(besideBefore, without.Beside);
        Assert.Equal(besideBefore ? hotbar.X - NeedsHudLayout.Spacing - NeedsHudLayout.BarWidth : hotbar.X, without.Block.X);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1280, 720)]
    [InlineData(533, 400)]
    public void Given_ACanvas_When_TheHotbarIsPlaced_Then_ItsTenSlotsAreCentredAndNoOffHandReserveMovesThem(int width, int height)
    {
        var hotbar = HotbarLayer.BoxFor(width, height);
        var expectedWidth = (10 * HotbarLayer.SlotSize) + (9 * HotbarLayer.Gap);

        Assert.Equal(new PixelBox((width - expectedWidth) / 2, height - HotbarLayer.SlotSize - HotbarLayer.Bottom, expectedWidth, HotbarLayer.SlotSize), hotbar);
    }
}
