using System.Runtime.InteropServices;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// Makes the calling worker thread yield to the game's own threads (B40). <see cref="ThreadPriority.BelowNormal"/> changes nothing on Linux (the runtime ignores it: the threads keep the process's nice value, which was measured),
/// so on Linux the thread's nice value is raised by <see cref="Raise"/> through <c>setpriority</c> (the kernel's per-thread value when given a thread id). Elsewhere, or when the call is refused, nothing happens.
/// </summary>
public static partial class WorkerPriority
{
    /// <summary>How much the worker's nice value is raised above the one it started with (nice 0 becomes 7: between the 5 and 10 the plan asked for).</summary>
    public const int Raise = 7;

    private const int PrioProcess = 0; // on Linux, with a thread id, one thread
    private const int Highest = 19;

    /// <summary>Raises the calling thread's nice value by <see cref="Raise"/> (at most 19), and returns the value it has now, or <see langword="null"/> where this does nothing (not Linux, or the kernel refused).</summary>
    public static int? LowerCurrentThread()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            var tid = GetTid();
            Marshal.SetLastSystemError(0);
            var current = GetPriority(PrioProcess, tid);
            if (current == -1 && Marshal.GetLastSystemError() != 0)
            {
                return null;
            }

            var wanted = Math.Min(Highest, current + Raise);
            return SetPriority(PrioProcess, tid, wanted) == 0 ? wanted : null;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [LibraryImport("libc", EntryPoint = "gettid")]
    private static partial int GetTid();

    [LibraryImport("libc", EntryPoint = "getpriority", SetLastError = true)]
    private static partial int GetPriority(int which, int who);

    [LibraryImport("libc", EntryPoint = "setpriority", SetLastError = true)]
    private static partial int SetPriority(int which, int who, int priority);
}
