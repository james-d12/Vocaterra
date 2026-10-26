using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Persistence;
using Vocaterra.Game.UI;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Flow;

/// <summary>What a run's start made: the session folder, its log, and the autosave (none with <c>--no-autosave</c>).</summary>
/// <param name="Directory">The session folder.</param>
/// <param name="Log">The session log (S11), open.</param>
/// <param name="Autosave">The autosave (X05), or <see langword="null"/>.</param>
public sealed record SessionStarted(string Directory, SessionLog Log, AutosaveController? Autosave);

/// <summary>
/// The start of a run's session (S11, X01, X05): the session folder and its log, opened first so that everything after is in it, with the run's
/// profile; a migrated save is logged and toasted; the finished turns go to the log; the playtest's save goes in the folder; and the autosave's
/// file is the playtest's save, else <c>autosave.save</c> in the folder.
/// </summary>
public static class SessionStart
{
    /// <summary>Opens the session of a run.</summary>
    /// <param name="options">The launch options.</param>
    /// <param name="session">The session.</param>
    /// <param name="micCheck">Whether the run has a mic check.</param>
    /// <param name="toasts">Where the migration's toast goes.</param>
    /// <param name="playtest">The playtest, whose save goes in the folder.</param>
    /// <param name="saves">The game's saves, which the autosave writes.</param>
    /// <param name="applied">Takes each finished turn of the session.</param>
    /// <param name="console">Where the start's lines go.</param>
    public static SessionStarted Open(LaunchOptions options, VoxelSession session, bool micCheck, ToastQueue toasts, PlaytestFlow playtest, SaveService saves, Action<AppliedTurn> applied, Action<string> console)
    {
        var directory = options.SessionDir ?? SessionLog.DefaultDirectory(WallClock.LocalNow, session.PlaySession);
        var log = SessionLog.Open(directory, static () => WallClock.Now);
        var profile = new SessionProfile(
            MicCheck: micCheck,
            Muted: options.Muted,
            NoSpeech: options.NoSpeech,
            Greyscale: options.Greyscale,
            TypedCredit: options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full",
            Check: options.Check is { } kind ? CheckKinds.Of(kind) : null,
            Scripted: options.IsScreenshotRun,
            Playtest: options.Playtest);
        log.SessionStart(session.PlaySession, session.WorldSeed, session.Bundle.ContentHash, session.StoredTime.UnixSeconds, profile);
        console($"session log: {directory} ({SessionLog.SessionFileName}, {SessionLog.ReplayFileName})");
        if (session.Migration is { } migration)
        {
            // A save of an older bundle was carried into this one (X01): the log says what was dropped, and the player is told the game was updated.
            log.SaveMigrated(migration);
            toasts.Show(migration.Toast);
        }

        session.Applied += applied;
        playtest.Prepare(directory);
        AutosaveController? autosave = null;
        if (!options.NoAutosave)
        {
            var autosavePath = playtest.SavePath ?? Path.Combine(directory, AutosaveFiles.FileName);
            autosave = new AutosaveController(new AutosavePolicy(session.Simulation.Tick, session.Events.Count), autosavePath, saves, console, toasts.Show);
            console($"autosave: {autosavePath} (written on a lesson step, an objective step, a lesson's end, the session's end, a closed dialogue, and every {AutosavePolicy.IntervalSeconds / 60} minutes of play)");
        }
        else
        {
            console("autosave: off (--no-autosave)");
        }

        return new SessionStarted(directory, log, autosave);
    }
}
