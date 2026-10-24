using Microsoft.Xna.Framework;

namespace Vocaterra.MonoGame.Graphics;

/// <summary>
/// The matrix the 2D layers are drawn under (X08): the window over the canvas, set by the game for each frame (the identity at 100 percent). A picture from the block atlas is
/// not drawn under it: scaled by 1.5 its corners fall between device pixels and the sampler picks up the neighbouring cell's row (a light line under the icon), so the atlas
/// draws the picture in window pixels instead, rounded to whole pixels, as it does at 100 percent.
/// </summary>
public static class UiTransform
{
    /// <summary>The horizontal factor, window pixels over canvas pixels.</summary>
    public static float ScaleX { get; private set; } = 1f;

    /// <summary>The vertical factor.</summary>
    public static float ScaleY { get; private set; } = 1f;

    /// <summary>Whether the layers are drawn at their own size.</summary>
    public static bool IsIdentity => ScaleX == 1f && ScaleY == 1f;

    /// <summary>The matrix to begin the sprite batch with.</summary>
    public static Matrix Matrix => Matrix.CreateScale(ScaleX, ScaleY, 1f);

    /// <summary>Sets the factors for the frame.</summary>
    /// <param name="windowWidth">The window's width.</param>
    /// <param name="windowHeight">The window's height.</param>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    public static void Set(int windowWidth, int windowHeight, int canvasWidth, int canvasHeight)
    {
        ScaleX = windowWidth / (float)canvasWidth;
        ScaleY = windowHeight / (float)canvasHeight;
    }

    /// <summary>A canvas rectangle in window pixels, with its edges rounded to whole pixels.</summary>
    /// <param name="area">The rectangle in canvas pixels.</param>
    public static Rectangle ToWindow(Rectangle area)
    {
        var left = (int)MathF.Round(area.Left * ScaleX);
        var top = (int)MathF.Round(area.Top * ScaleY);
        var right = (int)MathF.Round(area.Right * ScaleX);
        var bottom = (int)MathF.Round(area.Bottom * ScaleY);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }
}
