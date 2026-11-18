using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Tests.TestSupport;

/// <summary>
/// The hang guard of the tests that settle a streamed world (B24, review T2). A settle is CPU-bound work in a Debug build (a view takes about a minute, a host with its ring several, more under load), so no fixed
/// bound is both short enough to show a hang and long enough for a busy machine: the test settles in slices and fails only when a whole slice made no progress (nothing built, installed or ingested), which is a hang
/// and shows within two minutes; a slow machine keeps making progress and so never fails. A runaway that goes on making progress without settling is stopped after <see cref="MaxSlices"/> slices.
/// </summary>
internal static class SettleGuard
{
    /// <summary>How long one slice may run, in milliseconds (two minutes, times 4 under CI=true like every hang guard, <see cref="HangGuard"/>).</summary>
    public static double SliceMilliseconds { get; } = HangGuard.Scale(120_000);

    /// <summary>The most slices (30 slices, an hour) before a settle that keeps progressing is given up.</summary>
    public const int MaxSlices = 30;

    /// <summary>Settles in slices of <see cref="SliceMilliseconds"/> while <paramref name="progress"/> keeps rising.</summary>
    /// <param name="settle">One slice: settles for at most the given milliseconds and says whether it settled.</param>
    /// <param name="progress">A count that rises whenever the work moves on (tiles built, chunks installed, sections ingested).</param>
    /// <returns><see langword="true"/> when it settled; <see langword="false"/> after a slice without progress or after the last slice.</returns>
    public static bool Settle(Func<double, bool> settle, Func<long> progress)
    {
        var last = progress();
        for (var slice = 0; slice < MaxSlices; slice++)
        {
            if (settle(SliceMilliseconds))
            {
                return true;
            }

            var now = progress();
            if (now == last)
            {
                return false;
            }

            last = now;
        }

        return false;
    }
}
