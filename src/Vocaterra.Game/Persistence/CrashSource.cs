using System.Globalization;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Persistence;

/// <summary>The crash handler's view of the game (X05): the game's state row by row, and the last-chance save. Both run after an exception, so they catch what they read.</summary>
public sealed class CrashSource : ICrashSource
{
    private readonly VoxelSession _session;
    private readonly DialogueModel _dialogue;
    private readonly ReportPanel _report;
    private readonly AutosaveController? _autosave;
    private readonly SaveService _saves;

    /// <summary>Creates the view of a running game.</summary>
    /// <param name="sessionDirectory">The session folder.</param>
    /// <param name="reportDirectory">Where the reports go (<c>--report-dir</c>), or <see langword="null"/>.</param>
    /// <param name="session">The session.</param>
    /// <param name="dialogue">The dialogue panel (a save holds no open dialogue).</param>
    /// <param name="report">The report panel, which names the open screen.</param>
    /// <param name="autosave">The autosave, or <see langword="null"/> with <c>--no-autosave</c>.</param>
    /// <param name="saves">The game's saves.</param>
    public CrashSource(string sessionDirectory, string? reportDirectory, VoxelSession session, DialogueModel dialogue, ReportPanel report, AutosaveController? autosave, SaveService saves)
    {
        SessionDirectory = sessionDirectory;
        ReportDirectory = reportDirectory;
        _session = session;
        _dialogue = dialogue;
        _report = report;
        _autosave = autosave;
        _saves = saves;
    }

    /// <inheritdoc />
    public string SessionDirectory { get; }

    /// <inheritdoc />
    public string? ReportDirectory { get; }

    /// <summary>The state of the game as far as it can be read, row by row: a row that fails says so and the others are still shown.</summary>
    public CrashGameState State()
    {
        var rows = new List<(string Item, string Value)>();
        var notes = new List<string>();
        void Row(string item, Func<string> value)
        {
            try
            {
                rows.Add((item, value()));
            }
            catch (Exception ex)
            {
                notes.Add($"{item} was not read ({ex.GetType().Name})");
            }
        }

        int? session = null;
        long? tick = null;
        try
        {
            session = _session.PlaySession;
            tick = _session.Simulation.Tick;
        }
        catch (Exception ex)
        {
            notes.Add($"the session and tick were not read ({ex.GetType().Name})");
        }

        Row("Bundle", () => $"{_session.Bundle.Manifest.BundleId}, hash {_session.Bundle.ContentHash}");
        Row("Seed", () => _session.WorldSeed.ToString(CultureInfo.InvariantCulture));
        Row("Position", () =>
        {
            var body = _session.Simulation.Player;
            return string.Create(CultureInfo.InvariantCulture, $"column {body.CenterX / (double)VoxelPhysics.UnitsPerBlock:0.0}, row {body.CenterZ / (double)VoxelPhysics.UnitsPerBlock:0.0}, feet at height {body.FeetY / (double)VoxelPhysics.UnitsPerBlock:0.0} (blocks)");
        });
        Row("Objective", () => _session.Objective.Summary);
        Row("Lesson", () => _session.Lessons.Active is { } active ? $"{active.Id}, step {_session.Lessons.StepNumber} of {active.Steps.Count}" : "none");
        Row("Dialogue open", () => _dialogue.IsOpen ? "yes" : "no");
        Row("Open panel", () => _report.OpenPanel);
        Row("Turns on the worker", () => _session.PendingTurns.ToString(CultureInfo.InvariantCulture));
        Row("Last autosave tick", () => _autosave is null ? "off" : _autosave.LastSavedTick.ToString(CultureInfo.InvariantCulture));
        return new CrashGameState(session, tick, rows, notes.Count == 0 ? null : string.Join("; ", notes) + ".");
    }

    /// <summary>
    /// The last-chance save: the document F6 writes, to the crash file. Only at rest, and not with a dialogue open (the autosave's rule: a save
    /// holds no open dialogue). Returns <see langword="null"/> when written, else why not. Never throws.
    /// </summary>
    public string? LastChanceSave(string path)
    {
        try
        {
            if (!_session.CanSave)
            {
                return "the game was not at rest (a turn was on the worker, or the key words were on show)";
            }

            if (_dialogue.IsOpen)
            {
                return "a dialogue was open, and a save holds no open dialogue";
            }

            SaveFile.Write(path, SaveGame.Serialize(_saves.Capture()));
            return null;
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
