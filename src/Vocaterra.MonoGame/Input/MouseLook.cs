using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Vocaterra.MonoGame.Input;

/// <summary>
/// Mouse look in SDL's relative mouse mode: while the mouse is captured the pointer is hidden and held in the window, and every frame gives the
/// movement since the last frame. MonoGame 3.8 DesktopGL has no relative-mode switch of its own (<c>Mouse.IsRelativeMouseModeEXT</c> arrived in
/// a later version), so this calls SDL_SetRelativeMouseMode and SDL_GetRelativeMouseState in the SDL library MonoGame already loaded. If that
/// library cannot be reached, the pointer is hidden and warped to the middle of the window every frame instead, and the distance it had moved is
/// the movement.
/// </summary>
public sealed class MouseLook
{
    private static readonly SdlMouse? Native = LoadNative();
    private bool _captured;

    /// <summary>Whether the mouse is captured.</summary>
    public bool Captured => _captured;

    /// <summary>Whether SDL's relative mode is used (otherwise the pointer is warped to the middle of the window).</summary>
    public static bool UsesRelativeMode => Native is not null;

    private delegate int SetRelativeMouseMode(int enabled);

    private delegate uint GetRelativeMouseState(out int x, out int y);

    /// <summary>Captures or releases the mouse. Capturing discards the movement made before it.</summary>
    /// <param name="captured">Whether the mouse should be captured.</param>
    /// <param name="window">The game window, for the warp fallback.</param>
    public void SetCaptured(bool captured, GameWindow window)
    {
        if (captured == _captured)
        {
            return;
        }

        _captured = captured;
        if (Native is { } sdl)
        {
            _ = sdl.Set(captured ? 1 : 0);
            if (captured)
            {
                _ = sdl.Get(out _, out _); // drop what has piled up
            }
        }
        else if (captured)
        {
            Mouse.SetPosition(window.ClientBounds.Width / 2, window.ClientBounds.Height / 2);
        }
    }

    /// <summary>The mouse movement since the last call, in pixels (zero when not captured).</summary>
    /// <param name="window">The game window, for the warp fallback.</param>
    public (int X, int Y) Read(GameWindow window)
    {
        if (!_captured)
        {
            return (0, 0);
        }

        if (Native is { } sdl)
        {
            _ = sdl.Get(out var dx, out var dy);
            return (dx, dy);
        }

        var centreX = window.ClientBounds.Width / 2;
        var centreY = window.ClientBounds.Height / 2;
        var position = Mouse.GetState().Position;
        Mouse.SetPosition(centreX, centreY);
        return (position.X - centreX, position.Y - centreY);
    }

    private static SdlMouse? LoadNative()
    {
        try
        {
            foreach (var name in new[] { "libSDL2-2.0.so.0", "libSDL2.so", "SDL2.dll", "libSDL2-2.0.0.dylib" })
            {
                if (NativeLibrary.TryLoad(name, typeof(Microsoft.Xna.Framework.Game).Assembly, null, out var library)
                    && NativeLibrary.TryGetExport(library, "SDL_SetRelativeMouseMode", out var set)
                    && NativeLibrary.TryGetExport(library, "SDL_GetRelativeMouseState", out var get))
                {
                    return new SdlMouse(
                        Marshal.GetDelegateForFunctionPointer<SetRelativeMouseMode>(set),
                        Marshal.GetDelegateForFunctionPointer<GetRelativeMouseState>(get));
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ArgumentException or MarshalDirectiveException)
        {
            // fall through to the warp fallback
        }

        return null;
    }

    private sealed record SdlMouse(SetRelativeMouseMode Set, GetRelativeMouseState Get);
}
