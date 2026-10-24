namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// When a scripted screenshot may be taken (S12). A window's first frames can be drawn at the default 800 x 480 before the resize to the size the
/// game asked for, so a picture taken then was not the scene's: the gate waits until the back buffer and the client area both have the wanted size.
/// A window the system never resizes must not hang a script, so after <see cref="PatienceFrames"/> frames the picture is taken anyway.
/// </summary>
public static class ScreenshotGate
{
    /// <summary>The frames a scripted run waits for the wanted size before it takes the picture anyway (ten seconds at 60 frames a second).</summary>
    public const int PatienceFrames = 600;

    /// <summary>Whether the back buffer and the client area both have the size the game asked for.</summary>
    /// <param name="backWidth">The back buffer's width.</param>
    /// <param name="backHeight">The back buffer's height.</param>
    /// <param name="wantedWidth">The width asked for (the preferred back buffer width).</param>
    /// <param name="wantedHeight">The height asked for (the preferred back buffer height).</param>
    /// <param name="clientWidth">The window's client width.</param>
    /// <param name="clientHeight">The window's client height.</param>
    public static bool IsFinalSize(int backWidth, int backHeight, int wantedWidth, int wantedHeight, int clientWidth, int clientHeight) =>
        backWidth == wantedWidth && backHeight == wantedHeight && clientWidth == wantedWidth && clientHeight == wantedHeight;

    /// <summary>Whether a picture may be taken now: at the final size, or after the patience of frames is spent.</summary>
    /// <param name="finalSize">Whether <see cref="IsFinalSize"/> holds this frame.</param>
    /// <param name="framesWaited">The frames waited so far for the final size.</param>
    public static bool MayShoot(bool finalSize, int framesWaited) => finalSize || framesWaited >= PatienceFrames;
}
