using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.Tests.UI;

/// <summary>The interface canvas: the window divided by the text size, the steps of the size, and the mouse brought onto the canvas.</summary>
public sealed class UiCanvasTests
{
    [Fact]
    public void TheCanvasIsTheWindowAtTheTextSizeAndFollowsAResize()
    {
        var window = (Width: 1280, Height: 720);
        var canvas = new UiCanvas(100, () => window);
        Assert.Equal((1280, 720), (canvas.Width, canvas.Height));
        Assert.Equal(125, canvas.Next());
        Assert.Equal(UiScale.Canvas(125, 1280, 720), (canvas.Width, canvas.Height));
        window = (1920, 1080);
        Assert.Equal(UiScale.Canvas(125, 1920, 1080), (canvas.Width, canvas.Height));
        Assert.Equal(UiScale.Canvas(125, 800, 600), canvas.For(800, 600));
    }

    [Fact]
    public void TheTextSizeStepsThroughItsLevelsAndBackToTheFirst()
    {
        var canvas = new UiCanvas(100, () => (1280, 720));
        Assert.Equal([125, 150, 100], new[] { canvas.Next(), canvas.Next(), canvas.Next() });
        Assert.Equal(100, canvas.TextPercent);
    }

    [Fact]
    public void TheMouseIsBroughtOntoTheCanvasWithItsButtonsAndWheel()
    {
        var canvas = new UiCanvas(150, () => (1280, 720));
        var raw = new MouseState(640, 360, 240, ButtonState.Pressed, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
        var scaled = canvas.ToCanvas(raw);
        Assert.Equal(UiScale.ToCanvas(640, 360, 1280, 720, canvas.Width, canvas.Height), (scaled.X, scaled.Y));
        Assert.Equal(new Point(scaled.X, scaled.Y), canvas.ToCanvas(new Point(640, 360)));
        Assert.Equal(240, scaled.ScrollWheelValue);
        Assert.Equal(ButtonState.Pressed, scaled.LeftButton);
        Assert.Equal(ButtonState.Pressed, scaled.RightButton);
    }
}
