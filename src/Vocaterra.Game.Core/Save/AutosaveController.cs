using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// The autosave (X05): the policy says whether a save is due, and it is taken only at rest. The save is the document F6 writes, written to a
/// temporary file and renamed over the old one (<see cref="SaveFile.Write"/>), so a crash during the write leaves the last autosave in place. A
/// write that fails is said and the game goes on: the next safe point, or the next five minutes, saves again.
/// </summary>
public sealed class AutosaveController
{
    private AutosavePolicy _policy;
    private readonly SaveService _saves;
    private readonly Action<string> _log;
    private readonly Action<string> _toast;
    private readonly Func<double> _milliseconds;

    /// <summary>The id of the toast an autosave shows (<c>ui.autosaved</c>; the host turns it into the support language's text, C04).</summary>
    public const string SavedToastId = "autosaved";

    /// <summary>Creates the autosave of a run.</summary>
    /// <param name="policy">When a save is due.</param>
    /// <param name="path">The autosave file: the playtest's <c>playtest.save</c>, else <c>autosave.save</c> in the session folder.</param>
    /// <param name="saves">The game's saves.</param>
    /// <param name="log">Where a line about the autosave goes (the console).</param>
    /// <param name="toast">Shows the player that the game was saved.</param>
    /// <param name="milliseconds">The host's monotonic clock in milliseconds (only differences mean anything): how long a save took, for its log line.</param>
    public AutosaveController(AutosavePolicy policy, string path, SaveService saves, Action<string> log, Action<string> toast, Func<double> milliseconds)
    {
        _policy = policy;
        Path = path;
        _saves = saves;
        _log = log;
        _toast = toast;
        _milliseconds = milliseconds;
    }

    /// <summary>Whether the autosave writes (<c>--no-autosave</c> starts it off; the menu's Autosave row switches it). When off, <see cref="Run"/> and <see cref="WriteNow"/> do nothing.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>
    /// Switches the autosave on or off. Turning it on starts the policy again from the tick and the events given, so it does not write at once for everything that happened while it was off.
    /// </summary>
    /// <param name="on">Whether it writes.</param>
    /// <param name="tick">The simulation tick now.</param>
    /// <param name="eventsSeen">How many events the session holds now.</param>
    public void SetEnabled(bool on, long tick, int eventsSeen)
    {
        if (on && !Enabled)
        {
            _policy = new AutosavePolicy(tick, eventsSeen);
        }

        Enabled = on;
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
        if (!Enabled)
        {
            return;
        }

        var decision = _policy.Decide(tick, events, atRest);
        if (!decision.Save)
        {
            return;
        }

        Write(decision.Reason!, toast: true);
    }

    /// <summary>
    /// Writes the autosave now, whatever the policy says, for a moment the game names (V12c: <c>sleep</c>, the play session as it ends at the player's fire, so that a load
    /// of the autosave starts the session the sleep starts). The game must be at rest; the events so far count as seen. No toast: the moment has its own.
    /// </summary>
    /// <param name="tick">The simulation tick now.</param>
    /// <param name="eventsSeen">How many events the session holds now.</param>
    /// <param name="reason">The moment, for the console line.</param>
    public void WriteNow(long tick, int eventsSeen, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        if (!Enabled)
        {
            return;
        }

        _policy = new AutosavePolicy(tick, eventsSeen);
        Write(reason, toast: false);
    }

    /// <summary>
    /// Starts the policy again as a run's start does (V12c): the player slept at their fire, and the next play session begins in this run as a load would begin it, from
    /// this tick and with the events the session already holds seen. The file stays the run's.
    /// </summary>
    /// <param name="tick">The simulation tick now.</param>
    /// <param name="eventsSeen">How many events the session holds now.</param>
    public void Restart(long tick, int eventsSeen) => _policy = new AutosavePolicy(tick, eventsSeen);

    private void Write(string reason, bool toast)
    {
        try
        {
            var started = _milliseconds();
            var document = _saves.Capture();
            var text = SaveGame.Serialize(document);
            SaveFile.Write(Path, text);
            _log($"autosave: {Path} written at {reason} (session {document.Session}, tick {document.Tick}, stored time {document.StoredTime}, world digest {document.StateDigest[..16]}, {System.Text.Encoding.UTF8.GetByteCount(text)} bytes, {_milliseconds() - started:0} ms)");
            if (toast)
            {
                _toast(SavedToastId);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _log($"autosave: not written at {reason}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
