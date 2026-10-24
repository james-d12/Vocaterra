using Vocaterra.Game;
using Vocaterra.Game.Save;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The interface scale (X08). The 2D layers are drawn into a virtual canvas of the window's size divided by the scale, then magnified to the window, so every
/// engine-free layout model keeps working in plain pixels: it is simply given a smaller canvas. The player chooses 100, 125 or 150 percent
/// (<see cref="SaveSettings.TextScaleLevels"/>): 100 is the size the layouts were made at, 150 the largest because 800 x 600 at 150 percent is a
/// 533 x 400 canvas, the smallest every layout is proven on (<see cref="MinWidth"/>, <see cref="MinHeight"/>); a 200 percent step would need a 400 x 300 canvas.
/// When a window is too small for the chosen step (a 640 x 480 window at 150 percent) the scale used is lowered until the canvas is at least that size,
/// so the layouts never see a canvas smaller than the one they were tested on. Plain numbers.
/// </summary>
public static class UiScale
{
    /// <summary>The narrowest canvas the layouts are made for (800 x 600 at 150 percent).</summary>
    public const int MinWidth = 533;

    /// <summary>The shortest canvas the layouts are made for (800 x 600 at 150 percent).</summary>
    public const int MinHeight = 400;

    /// <summary>The smallest font height in pixels any text is drawn at: the font is a 16 px bitmap font, and no secondary text goes below its native size.</summary>
    public const int MinFontSize = 16;

    /// <summary>The scale actually used (1 for 100 percent): the chosen step, lowered when the window is too small for it, never below 1.</summary>
    /// <param name="percent">The chosen step in percent.</param>
    /// <param name="windowWidth">The window's width in pixels.</param>
    /// <param name="windowHeight">The window's height in pixels.</param>
    public static double Effective(int percent, int windowWidth, int windowHeight)
    {
        var wanted = SaveSettings.SnapTextScale(percent) / 100.0;
        var room = Math.Min(windowWidth / (double)MinWidth, windowHeight / (double)MinHeight);
        return Math.Max(1.0, Math.Min(wanted, room));
    }

    /// <summary>The canvas the layers are laid out on: the window divided by the scale, rounded down.</summary>
    /// <param name="percent">The chosen step in percent.</param>
    /// <param name="windowWidth">The window's width in pixels.</param>
    /// <param name="windowHeight">The window's height in pixels.</param>
    public static (int Width, int Height) Canvas(int percent, int windowWidth, int windowHeight)
    {
        var scale = Effective(percent, windowWidth, windowHeight);
        return ((int)Math.Floor((windowWidth / scale) + 1e-9), (int)Math.Floor((windowHeight / scale) + 1e-9));
    }

    /// <summary>A window point as a canvas point (the mouse); the factors are the canvas over the window, so the whole window maps onto the whole canvas.</summary>
    /// <param name="x">The window x.</param>
    /// <param name="y">The window y.</param>
    /// <param name="windowWidth">The window's width.</param>
    /// <param name="windowHeight">The window's height.</param>
    /// <param name="canvasWidth">The canvas width.</param>
    /// <param name="canvasHeight">The canvas height.</param>
    public static (int X, int Y) ToCanvas(int x, int y, int windowWidth, int windowHeight, int canvasWidth, int canvasHeight) =>
        ((int)Math.Floor(x * (double)canvasWidth / Math.Max(1, windowWidth)), (int)Math.Floor(y * (double)canvasHeight / Math.Max(1, windowHeight)));

    /// <summary>The step after this one (100, 125, 150, then 100 again), for the F9 key.</summary>
    /// <param name="percent">The current step.</param>
    public static int Next(int percent)
    {
        var levels = SaveSettings.TextScaleLevels;
        var index = levels.ToList().IndexOf(SaveSettings.SnapTextScale(percent));
        return levels[(index + 1) % levels.Count];
    }
}
