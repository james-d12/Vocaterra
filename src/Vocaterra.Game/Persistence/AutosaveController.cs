using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Persistence;

/// <summary>
/// The autosave (X05): the policy says whether a save is due, and it is taken only at rest. The save is the document F6 writes, written to a
/// temporary file and renamed over the old one (<see cref="SaveFile.Write"/>), so a crash during the write leaves the last autosave in place. A
/// write that fails is said and the game goes on: the next safe point, or the next five minutes, saves again.
/// </summary>
public sealed class AutosaveController
{
    private readonly AutosavePolicy _policy;
    private readonly SaveService _saves;
    private readonly Action<string> _log;
    private readonly Action<string> _toast;

    /// <summary>Creates the autosave of a run.</summary>
    /// <param name="policy">When a save is due.</param>
    /// <param name="path">The autosave file: the playtest's <c>playtest.save</c>, else <c>autosave.save</c> in the session folder.</param>
    /// <param name="saves">The game's saves.</param>
    /// <param name="log">Where a line about the autosave goes (the console).</param>
    /// <param name="toast">Shows the player that the game was saved.</param>
    public AutosaveController(AutosavePolicy policy, string path, SaveService saves, Action<string> log, Action<string> toast)
    {
        _policy = policy;
        Path = path;
        _saves = saves;
        _log = log;
        _toast = toast;
    }

    /// <summary>The autosave file.</summary>
    public string Path { get; }

    /// <summary>The tick of the last autosave (the start, before the first).</summary>
    public long LastSavedTick => _policy.LastSavedTick;

    /// <summary>
    /// Once a frame: saves when the policy says a save is due and the game is <paramref name="atRest"/> (no turn on the worker, no key words
    /// standing, no dialogue open, no voice being recorded, no check battery, mic check or report panel).
    /// </summary>
    public void Run(long tick, IReadOnlyList<GameEvent> events, bool atRest)
    {
        var decision = _policy.Decide(tick, events, atRest);
        if (!decision.Save)
        {
            return;
        }

        try
        {
            var started = MonotonicClock.Milliseconds();
            var document = _saves.Capture();
            var text = SaveGame.Serialize(document);
            SaveFile.Write(Path, text);
            _log($"autosave: {Path} written at {decision.Reason} (session {document.Session}, tick {document.Tick}, stored time {document.StoredTime}, world digest {document.StateDigest[..16]}, {System.Text.Encoding.UTF8.GetByteCount(text)} bytes, {MonotonicClock.Milliseconds() - started:0} ms)");
            _toast("Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _log($"autosave: not written at {decision.Reason}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
