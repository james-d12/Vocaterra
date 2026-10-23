using System.Globalization;
using System.Text;
using Vocaterra.Game;
using Vocaterra.Game.Crash;
using Vocaterra.Game.Reports;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The state of the game a crash report can show: the session and tick, the rows of the state table, and a note when it is partial.</summary>
/// <param name="Session">The play session, or <see langword="null"/>.</param>
/// <param name="Tick">The simulation tick, or <see langword="null"/>.</param>
/// <param name="Rows">The state's name and value rows.</param>
/// <param name="Note">Why the state is partial, or <see langword="null"/>.</param>
public sealed record CrashGameState(int? Session, long? Tick, IReadOnlyList<(string Item, string Value)> Rows, string? Note);

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

/// <summary>What the handler wrote: the report's file and the last-chance save's file (each <see langword="null"/> when it was not written), and the save's note.</summary>
/// <param name="ReportPath">The Markdown report, or <see langword="null"/>.</param>
/// <param name="SavePath">The last-chance save, or <see langword="null"/>.</param>
/// <param name="SaveNote">What the last-chance save did, in words.</param>
public sealed record CrashOutcome(string? ReportPath, string? SavePath, string SaveNote);

/// <summary>
/// The crash handler (X05, R04): on an exception it writes a Markdown crash report into the session folder's <c>reports/</c> (beside the F8
/// reports), tries a last-chance save to <c>crash.save</c> (never over the autosave or a player's save), and prints where both went. It is
/// called from the game loop's catch and from the process's unhandled-exception and unobserved-task hooks. It never throws: a folder that cannot
/// be made, a read that fails or a full disk is said on the console and the handler returns.
/// </summary>
/// <remarks>
/// The first stopping crash is the one written; a second one while the first is being handled is ignored. An unobserved task exception does not stop
/// the game, so it writes a report and no save. The report's time comes from the clock it is given (the front-end's wall clock in the game).
/// </remarks>
public sealed class CrashHandler
{
    /// <summary>The process exit code of a crash (the game stopped).</summary>
    public const int ExitCode = 1;

    /// <summary>The longest name the report may take when counters are tried.</summary>
    public const int MaxCounter = 99;

    private readonly Func<DateTimeOffset> _clock;
    private readonly TextWriter _output;
    private readonly string _buildCommit;
    private ICrashSource? _source;
    private int _stopping;

    /// <summary>Creates a handler.</summary>
    /// <param name="clock">The clock of the report's time (a fixed one in the tests).</param>
    /// <param name="output">Where the handler says what it wrote (the standard error in the game).</param>
    /// <param name="buildCommit">The commit the build was made from, or <c>unknown</c>.</param>
    public CrashHandler(Func<DateTimeOffset> clock, TextWriter output, string buildCommit)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(buildCommit);
        _clock = clock;
        _output = output;
        _buildCommit = buildCommit;
    }

    /// <summary>Whether a stopping crash has been handled.</summary>
    public bool Stopped => Volatile.Read(ref _stopping) == 1;

    /// <summary>Gives the handler the running game, once it exists. Before that a crash writes into the default session folder's <c>reports/</c>.</summary>
    /// <param name="source">The game.</param>
    public void Attach(ICrashSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Volatile.Write(ref _source, source);
    }

    /// <summary>
    /// Handles one exception: writes the report, tries the last-chance save for a stopping crash, and prints where both went. Never throws.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="where">Where it came out (<c>the game loop</c>, <c>an unhandled exception</c>, <c>an unobserved task exception</c>).</param>
    /// <param name="stopped">Whether the game stops: a crash, not an unobserved task exception.</param>
    /// <returns>What was written, or <see langword="null"/> when this crash was not handled (a second stopping crash).</returns>
    public CrashOutcome? Handle(Exception exception, string where, bool stopped)
    {
        try
        {
            if (exception is null)
            {
                return null;
            }

            if (stopped && Interlocked.Exchange(ref _stopping, 1) == 1)
            {
                return null;
            }

            return HandleCore(exception, where, stopped);
        }
        catch (Exception problem)
        {
            try
            {
                _output.WriteLine($"crash: the crash report could not be written ({problem.GetType().Name}: {problem.Message})");
            }
            catch (Exception)
            {
                // The console itself failed: there is nowhere left to say it.
            }

            return null;
        }
    }

    private CrashOutcome HandleCore(Exception exception, string where, bool stopped)
    {
        var source = Volatile.Read(ref _source);
        var when = _clock();
        var sessionDirectory = source is { SessionDirectory.Length: > 0 } attached
            ? attached.SessionDirectory
            : SessionLog.DefaultDirectory(when.LocalDateTime, 1);

        CrashGameState? state = null;
        string? stateNote = null;
        try
        {
            state = source?.State();
        }
        catch (Exception problem)
        {
            stateNote = $"The game state was not read: {problem.GetType().Name}: {problem.Message}";
        }

        var saveNote = "not written: the game kept running (an unobserved task exception)";
        string? savePath = null;
        if (stopped)
        {
            saveNote = "not written: the game had no session to save";
            if (source is not null)
            {
                var path = Path.Combine(sessionDirectory, AutosaveFiles.CrashFileName);
                try
                {
                    Directory.CreateDirectory(sessionDirectory); // a crash before the session folder was made still gets its save
                    var reason = source.LastChanceSave(path);
                    if (reason is null)
                    {
                        savePath = path;
                        saveNote = $"written: {AutosaveFiles.CrashFileName} (the last state the game was in; the autosave and any save of yours are not touched)";
                    }
                    else
                    {
                        saveNote = $"not written: {reason}";
                    }
                }
                catch (Exception problem)
                {
                    saveNote = $"not written: {problem.GetType().Name}: {problem.Message}";
                }
            }
        }

        var logLines = TailOf(Path.Combine(sessionDirectory, SessionLog.SessionFileName), ReportBounds.LogLines);
        var reportPath = WriteReport(
            source,
            sessionDirectory,
            state,
            when,
            new CrashInput(
                stopped,
                when.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _buildCommit,
                where,
                state?.Session,
                state?.Tick,
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.ToString(),
                state?.Rows ?? [],
                Join(state?.Note, stateNote),
                saveNote,
                logLines));

        _output.WriteLine($"crash: {where}: {exception.GetType().Name}: {exception.Message}");
        _output.WriteLine(reportPath is null ? "crash: the report was not written (see above)" : $"crash: report written to {reportPath}");
        _output.WriteLine(savePath is null ? $"crash: last-chance save {saveNote}" : $"crash: last-chance save written to {savePath}");
        return new CrashOutcome(reportPath, savePath, saveNote);
    }

    // The report's file: crash-s<session>-t<tick>.md when the state is read, else crash-<local time>.md; a taken name gets a counter.
    private string? WriteReport(ICrashSource? source, string sessionDirectory, CrashGameState? state, DateTimeOffset when, CrashInput crash)
    {
        try
        {
            var directory = source?.ReportDirectory is { Length: > 0 } given ? given : Path.Combine(sessionDirectory, "reports");
            Directory.CreateDirectory(directory);
            var stem = state is { Session: { } session, Tick: { } tick }
                ? $"crash-s{session.ToString(CultureInfo.InvariantCulture)}-t{tick.ToString(CultureInfo.InvariantCulture)}"
                : "crash-" + when.LocalDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, stem + ".md");
            for (var counter = 2; File.Exists(path) && counter <= MaxCounter; counter++)
            {
                path = Path.Combine(directory, stem + "-" + counter.ToString(CultureInfo.InvariantCulture) + ".md");
            }

            File.WriteAllText(path, CrashReport.Build(crash), new UTF8Encoding(false));
            return path;
        }
        catch (Exception problem)
        {
            _output.WriteLine($"crash: the report could not be written ({problem.GetType().Name}: {problem.Message})");
            return null;
        }
    }

    // The newest lines of a file, read without a failure: a missing file has none.
    private static IReadOnlyList<string> TailOf(string path, int count)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var tail = new Queue<string>(count);
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (tail.Count == count)
                {
                    tail.Dequeue();
                }

                tail.Enqueue(line);
            }

            return tail.ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string? Join(string? first, string? second) =>
        first is null ? second : second is null ? first : first + " " + second;
}
