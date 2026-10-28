namespace Vocaterra.Game.Core.Reporting;

/// <summary>
/// What the crash handler needs from the running game (X05). Both members are called after an exception, so they must never throw: each one
/// catches what it reads and says so in its result.
/// </summary>
public interface ICrashSource
{
    /// <summary>The session folder: the session log and the autosave are in it. Empty when the game has none.</summary>
    string SessionDirectory { get; }

    /// <summary>Where the reports go (<c>--report-dir</c>), or <see langword="null"/> for the session folder's <c>reports/</c>.</summary>
    string? ReportDirectory { get; }

    /// <summary>The game's state as far as it can be read, never throwing.</summary>
    CrashGameState State();

    /// <summary>Writes the last-chance save to <paramref name="path"/>. Returns <see langword="null"/> when it was written, else why it was not. Never throws.</summary>
    /// <param name="path">The file (<c>crash.save</c> in the session folder).</param>
    string? LastChanceSave(string path);
}
