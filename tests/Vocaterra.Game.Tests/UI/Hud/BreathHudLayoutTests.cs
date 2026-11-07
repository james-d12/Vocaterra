using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>Where the bubble meter stands (<see cref="BreathHudLayout"/>, H04): ten bubbles in a row above the right half of the hotbar.</summary>
public sealed class BreathHudLayoutTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    [InlineData(533, 400)]
    public void Given_A_Canvas_When_Placed_Then_There_Are_Ten_Bubbles_In_One_Row(int width, int height)
    {
        var boxes = BreathHudLayout.Bubbles(width, height);

        Assert.Equal(BreathHudLayout.Count, boxes.Count);
        Assert.All(boxes, box => Assert.Equal(boxes[0].Y, box.Y));
        Assert.All(boxes, box => Assert.Equal(BreathHudLayout.BubbleSize, box.Width));
        Assert.All(boxes, box => Assert.Equal(BreathHudLayout.BubbleSize, box.Height));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    [InlineData(533, 400)]
    public void Given_A_Canvas_When_Placed_Then_The_Row_Is_In_The_Right_Half_Above_The_Hotbar(int width, int height)
    {
        var boxes = BreathHudLayout.Bubbles(width, height);

        Assert.All(boxes, box => Assert.True(box.X > width / 2, "a bubble sits in the left half"));
        Assert.All(boxes, box => Assert.True(box.Right <= width, "a bubble runs off the canvas"));
        Assert.All(boxes, box => Assert.True(box.Bottom <= height - HudLayout.HotbarTop, "a bubble overlaps the hotbar"));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(533, 400)]
    public void Given_A_Canvas_When_Placed_Then_The_Row_Ends_At_The_Hotbars_Right_Edge(int width, int height)
    {
        var boxes = BreathHudLayout.Bubbles(width, height);
        var hotbarWidth = (Vocaterra.Game.Core.Items.Inventory.HotbarSize * HotbarLayer.SlotSize) + ((Vocaterra.Game.Core.Items.Inventory.HotbarSize - 1) * HotbarLayer.Gap);
        var hotbarRight = ((width - hotbarWidth) / 2) + hotbarWidth;

        Assert.Equal(hotbarRight, boxes[^1].Right);
    }

    [Fact]
    public void Given_A_Canvas_When_Placed_Then_The_Bubbles_Are_Evenly_Spaced_Without_Overlap()
    {
        var boxes = BreathHudLayout.Bubbles(1280, 720);

        for (var i = 1; i < boxes.Count; i++)
        {
            Assert.Equal(BreathHudLayout.BubbleSize + BreathHudLayout.BubbleGap, boxes[i].X - boxes[i - 1].X);
        }
    }

    [Fact]
    public void Given_A_Raise_When_Placed_Then_The_Row_Moves_Up_By_That_Many_Pixels()
    {
        var flat = BreathHudLayout.Bubbles(800, 600);
        var raised = BreathHudLayout.Bubbles(800, 600, raise: 30);

        Assert.Equal(flat[0].Y - 30, raised[0].Y);
        Assert.Equal(flat[0].X, raised[0].X);
    }
}
