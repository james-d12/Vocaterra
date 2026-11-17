using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>
/// Loads the OpenGL functions of the context MonoGame made, on that same context (B17a): SDL, which owns the window and the context, hands out the function
/// pointers (<c>SDL_GL_GetProcAddress</c>), and the binding (Silk.NET.OpenGL) calls them. No second context or window is made; the functions are valid on the thread that
/// holds MonoGame's context, which is the game loop's.
/// </summary>
public static class GlLoader
{
    private const string SdlLibrary = "libSDL2-2.0.so.0";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetProcAddressDelegate(nint name);

    /// <summary>
    /// Loads the binding's functions on the context that is current on this thread (call it from the game's thread once the graphics device exists).
    /// </summary>
    /// <param name="gl">The binding, or <see langword="null"/> when it could not be loaded.</param>
    /// <param name="error">Why not, or an empty string.</param>
    /// <returns>Whether the functions were loaded.</returns>
    public static bool TryLoad(out GL? gl, out string error)
    {
        gl = null;
        error = string.Empty;
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            error = "the OpenGL path is built for Linux and Windows only (macOS is out of scope, D-game-34 item 39)";
            return false;
        }

        try
        {
            var library = OperatingSystem.IsWindows() ? "SDL2.dll" : SdlLibrary;
            if (!NativeLibrary.TryLoad(library, out var handle))
            {
                error = $"{library} is not loaded in the process (MonoGame's SDL)";
                return false;
            }

            if (!NativeLibrary.TryGetExport(handle, "SDL_GL_GetProcAddress", out var export))
            {
                error = "SDL_GL_GetProcAddress is not exported";
                return false;
            }

            var getProc = Marshal.GetDelegateForFunctionPointer<GetProcAddressDelegate>(export);
            gl = GL.GetApi(new LamdaNativeContext(name =>
            {
                var text = Marshal.StringToHGlobalAnsi(name);
                try
                {
                    return getProc(text);
                }
                finally
                {
                    Marshal.FreeHGlobal(text);
                }
            }));
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or NotSupportedException or BadImageFormatException)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            gl = null;
            return false;
        }
    }
}
