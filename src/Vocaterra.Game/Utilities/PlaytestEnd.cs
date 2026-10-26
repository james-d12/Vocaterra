using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Utilities;

/// <summary>
/// The end of a playtest run (S12, the playtest profile of <c>--playtest</c>): when the post-check starts on its own, when the end key starts it,
/// and where the save of the run goes. Plain functions over values the game passes in, so the rules are tested without a window.
/// </summary>
public static class PlaytestEnd
{
    /// <summary>The save the playtest writes into its session folder; the next-day recall loads it with <c>--load</c>.</summary>
    public const string SaveFileName = "playtest.save";

    /// <summary>The key that ends the run (the facilitator's end key; the screen labels it).</summary>
    public const string EndKeyName = "F10";

    /// <summary>The save file of a session folder.</summary>
    /// <param name="sessionFolder">The folder the session log is written to.</param>
    public static string SaveIn(string sessionFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionFolder);
        return Path.Combine(sessionFolder, SaveFileName);
    }

    /// <summary>
    /// Where F6 writes the save (S18): the <c>--save</c> file when one is given; in a playtest the session folder's <c>playtest.save</c>, the file
    /// the run ends with (before S18 F6 wrote <c>vocaterra.save.json</c> into the working folder, the build folder for a tester); otherwise the
    /// fallback.
    /// </summary>
    /// <param name="savePath">The <c>--save</c> file, or <see langword="null"/>.</param>
    /// <param name="playtestSave">The playtest's save file in its session folder, or <see langword="null"/> outside a playtest.</param>
    /// <param name="fallback">The save file of an ordinary run.</param>
    public static string ManualSavePath(string? savePath, string? playtestSave, string fallback) => savePath ?? playtestSave ?? fallback;

    /// <summary>Whether the guide has made her transfer offer (the event logged when she asks "¿Quieres agua?" after the camp is done).</summary>
    /// <param name="events">The session's events.</param>
    public static bool TransferOffered(IEnumerable<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.OfType<WorldEvent>().Any(static e => string.Equals(e.Name, WorldEvents.TransferOffered, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether the post-check starts by itself: a playtest with the world running (no battery and no mic check on), the post-check not started, the
    /// session complete (the camp done and the player's own fire lit), the transfer offered and no talk open (a talk still open finishes first; the end key does not wait for it).
    /// </summary>
    /// <param name="playtest">Whether the run is the playtest profile.</param>
    /// <param name="worldRunning">Whether the world is on screen (no battery, no mic check).</param>
    /// <param name="started">Whether the post-check has started already.</param>
    /// <param name="campDone">Whether the session is complete (S17): every step of the camp objective is done and the fire at the player's own camp is lit.</param>
    /// <param name="transferOffered">Whether the guide has made her transfer offer.</param>
    /// <param name="talkOpen">Whether the dialogue panel is open.</param>
    public static bool StartsOnItsOwn(bool playtest, bool worldRunning, bool started, bool campDone, bool transferOffered, bool talkOpen) =>
        playtest && worldRunning && !started && campDone && transferOffered && !talkOpen;

    /// <summary>Whether the end key starts the post-check now: a playtest, the world on screen, the post-check not started yet.</summary>
    /// <param name="playtest">Whether the run is the playtest profile.</param>
    /// <param name="worldRunning">Whether the world is on screen (no battery, no mic check).</param>
    /// <param name="started">Whether the post-check has started already.</param>
    public static bool EndKeyStarts(bool playtest, bool worldRunning, bool started) => playtest && worldRunning && !started;
}
