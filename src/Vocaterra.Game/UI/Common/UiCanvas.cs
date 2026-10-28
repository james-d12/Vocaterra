using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Vocaterra.Game.UI.Common;

/// <summary>
/// The canvas the 2D layers are laid out on (X08): the window divided by the interface scale, and the mouse brought onto it. The text size in
/// percent is the save's setting; F9 steps it.
/// </summary>
public sealed class UiCanvas
{
    private readonly Func<(int Width, int Height)> _window;

    /// <summary>Creates the canvas of a window.</summary>
    /// <param name="textPercent">The interface text size in percent (one of the save's levels).</param>
    /// <param name="window">The window's size now (it can change at any frame).</param>
    public UiCanvas(int textPercent, Func<(int Width, int Height)> window)
    {
        TextPercent = textPercent;
        _window = window;
    }

    /// <summary>The interface text size in percent.</summary>
    public int TextPercent { get; private set; }

    /// <summary>The canvas width for the window as it is now.</summary>
    public int Width => Size.Width;

    /// <summary>The canvas height for the window as it is now.</summary>
    public int Height => Size.Height;

    private (int Width, int Height) Size
    {
        get
        {
            var (width, height) = _window();
            return UiScale.Canvas(TextPercent, width, height);
        }
    }

    /// <summary>The canvas for a window of <paramref name="width"/> by <paramref name="height"/>.</summary>
    public (int Width, int Height) For(int width, int height) => UiScale.Canvas(TextPercent, width, height);

    /// <summary>Steps the text size to the next level (100, 125, 150 percent, then 100 again) and returns it.</summary>
    public int Next()
    {
        TextPercent = UiScale.Next(TextPercent);
        return TextPercent;
    }

    /// <summary>A point of the window on the canvas.</summary>
    public Point ToCanvas(Point window)
    {
        var (width, height) = _window();
        var (x, y) = UiScale.ToCanvas(window.X, window.Y, width, height, Width, Height);
        return new Point(x, y);
    }

    /// <summary>The mouse with its pointer on the canvas.</summary>
    public MouseState ToCanvas(MouseState raw)
    {
        var point = ToCanvas(raw.Position);
        return new MouseState(point.X, point.Y, raw.ScrollWheelValue, raw.LeftButton, raw.MiddleButton, raw.RightButton, raw.XButton1, raw.XButton2);
    }
}
