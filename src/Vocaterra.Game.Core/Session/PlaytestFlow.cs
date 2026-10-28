using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The playtest profile (S12, S18): the pre-check, the run, then the post-check, which starts by itself when the camp is done, the fire at the
/// player's camp is lit and the transfer offered, or at once with F10. Its save goes to the session folder; its thank-you screen waits for Esc. A
/// next-day recall runs its check and ends.
/// </summary>
public sealed class PlaytestFlow
{
    /// <summary>The thank-you note of the playtest's post-check (the screen's closing lines; the English is for the facilitator and the tester alike).</summary>
    public const string ThankYou = "Thank you for playing. Your answers are in the session log.";

    /// <summary>The thank-you note of the next-day recall of a playtest.</summary>
    public const string RecallThankYou = "Thank you. Your answers are in the session log.";

    private readonly bool _recall;
    private readonly bool _scriptedRun;
    private readonly int _scriptLength;
    private readonly VoxelSession _session;
    private readonly CheckBattery _checks;
    private readonly SaveService _saves;
    private readonly PlaytestHooks _hooks;
    private readonly Action<string> _console;
    private bool _started;
    private bool _saveDue;
    private bool _saved;

    /// <summary>Creates the playtest of a run (a run that is not a playtest has one that does nothing).</summary>
    /// <param name="enabled">Whether the run is a playtest (<c>--playtest</c>).</param>
    /// <param name="recall">Whether it is the next-day recall.</param>
    /// <param name="scriptedRun">Whether the run is scripted (its post-check answers from the script).</param>
    /// <param name="scriptLength">The number of steps of the world script.</param>
    /// <param name="session">The session.</param>
    /// <param name="checks">The check battery.</param>
    /// <param name="saves">The game's saves.</param>
    /// <param name="hooks">What the playtest needs from the game around it.</param>
    /// <param name="console">Where the playtest's lines go.</param>
    public PlaytestFlow(bool enabled, bool recall, bool scriptedRun, int scriptLength, VoxelSession session, CheckBattery checks, SaveService saves, PlaytestHooks hooks, Action<string> console)
    {
        Enabled = enabled;
        _recall = recall;
        _scriptedRun = scriptedRun;
        _scriptLength = scriptLength;
        _session = session;
        _checks = checks;
        _saves = saves;
        _hooks = hooks;
        _console = console;
    }

    /// <summary>Whether the run is a playtest.</summary>
    public bool Enabled { get; }

    /// <summary>The playtest's save in the session folder, or <see langword="null"/> (not a playtest, or before <see cref="Prepare"/>).</summary>
    public string? SavePath { get; private set; }

    /// <summary>The file F6 writes in a playtest (its own save), or <see langword="null"/> outside one.</summary>
    public string? ManualSaveFile => Enabled ? SavePath : null;

    /// <summary>Whether the playtest's thank-you screen is up: Esc then closes the game.</summary>
    public bool ShowsThankYou => Enabled && _checks.EndsRun && _checks.Done;

    /// <summary>Once the session folder is known: the playtest's save goes there, and the console says what the run will do.</summary>
    public void Prepare(string sessionDirectory)
    {
        if (!Enabled)
        {
            return;
        }

        SavePath = PlaytestEnd.SaveIn(sessionDirectory);
        _console(_recall
            ? $"playtest: the next-day recall of the save from session {_session.PlaySession - 1}; the run ends after the check"
            : $"playtest: the pre-check, the run, then the post-check when the camp is done, the fire at the player's camp is lit and the transfer offered (or F10); the save is {SavePath}");
    }

    /// <summary>Once a frame: the post-check starts by itself once the camp is done, the fire lit and the transfer offered (at a step boundary of a scripted run).</summary>
    public void StartIfDue(bool atStepBoundary, bool worldRunning, bool dialogueOpen)
    {
        if (Enabled && atStepBoundary
            && PlaytestEnd.StartsOnItsOwn(true, worldRunning, _started, _session.SessionComplete, PlaytestEnd.TransferOffered(_session.Events), dialogueOpen))
        {
            StartPost("the camp is done, the player's fire is lit and the transfer offered");
        }
    }

    /// <summary>F10: the post-check starts at once, when the world is on screen and it has not started.</summary>
    public void EndKey(bool worldRunning)
    {
        if (Enabled && PlaytestEnd.EndKeyStarts(true, worldRunning, _started))
        {
            StartPost("the end key");
        }
    }

    /// <summary>Once a frame: the save of a finished post-check is written as soon as the session is at rest.</summary>
    public void SaveIfDue()
    {
        if (_saveDue && _session.CanSave)
        {
            _saved = _saves.Write(SavePath!, "at the end of the playtest");
            _saveDue = !_saved;
        }
    }

    /// <summary>The playtest's post- or recall-check is over: the world script is done with its steps, the thank-you screen says where the save is, and a post-check saves the run.</summary>
    public void Ended(CheckScreen check)
    {
        _hooks.SkipScenarioTo(_scriptLength);
        if (_recall)
        {
            check.Closing = RecallThankYou;
            return;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(SavePath!)) ?? string.Empty;
        check.Closing = $"{ThankYou}\nSaved: {PlaytestEnd.SaveFileName} in the session folder {folder}.";
        _saveDue = true;
    }

    /// <summary>At the quit: a playtest that ended before its post-check is saved all the same (a recall is not).</summary>
    public void SaveAtQuit()
    {
        if (Enabled && !_recall && !_saved && SavePath is { } path)
        {
            _hooks.DrainForSave();
            _saved = _saves.Write(path, "at the quit (the playtest ended before its post-check)");
        }
    }

    // The post-check (S12): the world stops while it runs; a scripted run answers it from the script's next step.
    private void StartPost(string why)
    {
        _started = true;
        _hooks.ClosePanels();
        _console($"playtest: the post-check starts ({why}) at tick {_session.Simulation.Tick}; {_session.Objective.Summary}");
        _checks.Begin(CheckKind.Post, _scriptedRun, _hooks.ScenarioIndex());
    }
}
