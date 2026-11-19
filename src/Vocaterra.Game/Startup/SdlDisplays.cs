using System.Runtime.InteropServices;

namespace Vocaterra.Game.Startup;

/// <summary>
/// SDL's list of displays and the crash it can cause (B46). On X11 SDL 2.32 follows the X server's RandR outputs and drops a display when its output goes away; when the
/// last output goes (the monitor sleeps or is unplugged, and on a GNOME session the Xwayland server then has no output at all) SDL has no display left, and the next
/// <c>SDL_GetDisplayForWindow</c> returns <see langword="null"/> where SDL's own code reads it unchecked: <c>X11_HideWindow</c> (in <c>SDL_DestroyWindow</c>, at the end of every run) and
/// <c>SDL_IBus_UpdateTextRect</c> (in the event pump) then die with SIGSEGV, exit 139. Both stacks were found in the core dumps of 2026-11-16 to 2026-11-19.
/// <para>
/// Two defences, neither of which touches SDL's code: a scripted run (screenshot, tests) tells SDL not to watch RandR at all (<see cref="IgnoreRandRChanges"/>), so its display never
/// goes away; and any run that finds SDL with no display at its end leaves the process instead of destroying the window (<see cref="NoneLeft"/>). A player's window in the middle
/// of a run keeps SDL's RandR handling (its multi-monitor and mode lists), so a monitor lost during play can still crash inside SDL: that is SDL's bug, not ours to catch.
/// </para>
/// </summary>
public static partial class SdlDisplays
{
    /// <summary>SDL's hint (read by SDL from the environment too) that turns the X11 RandR support off; <c>0</c> is off.</summary>
    public const string RandRVariable = "SDL_VIDEO_X11_XRANDR";

    /// <summary>
    /// Stops SDL from following the X server's outputs, for this process: set before SDL starts (the game's constructor starts it). The value the caller set wins, so
    /// <c>SDL_VIDEO_X11_XRANDR=1</c> in the environment turns this off. Returns whether the variable was set here (see <see cref="SetEnvironmentUnlessSet"/>).
    /// </summary>
    public static bool IgnoreRandRChanges()
    {
        return SetEnvironmentUnlessSet(RandRVariable, "0");
    }

    /// <summary>Sets a variable in the process's own environment block (the one native libraries read) unless it is set already. <see langword="false"/> where that is not done: not Linux, the variable was set, or the call is refused.</summary>
    public static bool SetEnvironmentUnlessSet(string name, string value)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            return GetEnvironment(name) == IntPtr.Zero && SetEnvironment(name, value, 0) == 0;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether SDL (already started) has no display left: the window must not be destroyed then (see the class), so the caller leaves the process. <see langword="false"/> when SDL
    /// cannot be asked (another platform, or the library is not found), which is the normal case with no check at all.
    /// </summary>
    public static bool NoneLeft()
    {
        try
        {
            foreach (var name in new[] { "libSDL2-2.0.so.0", "libSDL2.so" })
            {
                if (NativeLibrary.TryLoad(name, typeof(Microsoft.Xna.Framework.Game).Assembly, null, out var library) && NativeLibrary.TryGetExport(library, "SDL_GetNumVideoDisplays", out var count))
                {
                    return Marshal.GetDelegateForFunctionPointer<GetNumVideoDisplays>(count)() < 1;
                }
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or ArgumentException or MarshalDirectiveException)
        {
            // SDL cannot be asked: no check
        }

        return false;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetNumVideoDisplays();

    [LibraryImport("libc", EntryPoint = "getenv", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr GetEnvironment(string name);

    // int setenv(const char *name, const char *value, int overwrite): the value is only set when the variable is not set yet (overwrite 0). The process's own environment block is
    // the one SDL reads (SDL_getenv), so Environment.SetEnvironmentVariable, which keeps a managed copy on Unix, would not reach it.
    [LibraryImport("libc", EntryPoint = "setenv", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SetEnvironment(string name, string value, int overwrite);
}
