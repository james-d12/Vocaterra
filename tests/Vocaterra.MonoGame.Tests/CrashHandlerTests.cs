using System.Text;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The crash handler (X05, R04): both files are written, the folder is made when it is missing, nothing throws, and a second stopping crash is not written.</summary>
public sealed class CrashHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vocaterra-x05-" + Path.GetRandomFileName());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A test that made nothing has nothing to delete.
        }
    }

    private static readonly DateTimeOffset Fixed = new(2026, 10, 23, 10, 15, 0, TimeSpan.Zero);

    // A game stand-in: the state, the last-chance save and the folders are the test's to choose.
    private sealed class FakeGame(string sessionDirectory, Func<string, string?> save, Func<CrashGameState> state, string? reportDirectory = null) : ICrashSource
    {
        public string SessionDirectory { get; } = sessionDirectory;

        public string? ReportDirectory { get; } = reportDirectory;

        public CrashGameState State() => state();

        public string? LastChanceSave(string path) => save(path);
    }

    private static CrashGameState Readable() => new(2, 4681, [("Lesson", "first-words, step 3 of 7"), ("Seed", "42")], null);

    private static string? WriteSave(string path)
    {
        SaveFile.Write(path, "{\"format\":\"vocaterra-save\"}");
        return null;
    }

    private CrashHandler Handler(StringBuilder output) =>
        new(() => Fixed, new StringWriter(output), "abc1234");

    [Fact]
    public void AStoppingCrashWritesTheReportAndTheLastChanceSaveAndSaysWhereBoth()
    {
        var session = Path.Combine(_root, "session");
        var output = new StringBuilder();
        var handler = Handler(output);
        handler.Attach(new FakeGame(session, WriteSave, Readable));

        var outcome = handler.Handle(new InvalidOperationException("boom"), "the game loop", stopped: true);

        Assert.NotNull(outcome);
        var report = Path.Combine(session, "reports", "crash-s2-t4681.md");
        Assert.Equal(report, outcome.ReportPath);
        Assert.Equal(Path.Combine(session, "crash.save"), outcome.SavePath);
        var text = File.ReadAllText(report);
        Assert.StartsWith("# Crash report: InvalidOperationException\n", text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("- **Build commit:** abc1234", text, StringComparison.Ordinal);
        // The time is the clock's, shown in the machine's local time (as the F8 reports show theirs).
        Assert.Contains($"- **Saved:** {Fixed.LocalDateTime:yyyy-MM-dd HH:mm:ss} (local time)", text, StringComparison.Ordinal);
        Assert.Contains("| Lesson | first-words, step 3 of 7 |", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(session, "crash.save")));
        Assert.Contains("crash: report written to " + report, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("crash: last-chance save written to " + Path.Combine(session, "crash.save"), output.ToString(), StringComparison.Ordinal);
        Assert.True(handler.Stopped);
    }

    [Fact]
    public void TheReportsFolderIsMadeWhenItIsMissing()
    {
        var session = Path.Combine(_root, "never", "made", "session");
        var output = new StringBuilder();
        var handler = Handler(output);
        handler.Attach(new FakeGame(session, _ => "the game was not at rest", () => throw new InvalidOperationException("not yet")));

        var outcome = handler.Handle(new IOException("disk"), "the game loop", stopped: true);

        Assert.NotNull(outcome);
        Assert.NotNull(outcome.ReportPath);
        Assert.True(File.Exists(outcome.ReportPath));
        Assert.Null(outcome.SavePath);
        Assert.Contains("not written: the game was not at rest", File.ReadAllText(outcome.ReportPath), StringComparison.Ordinal);
        Assert.Contains("was not read", File.ReadAllText(outcome.ReportPath), StringComparison.Ordinal); // the state's failure is in the report
        Assert.Contains("crash-", Path.GetFileName(outcome.ReportPath), StringComparison.Ordinal); // no tick: the time names it
    }

    [Fact]
    public void AReportThatCannotBeWrittenIsSaidAndNothingThrows()
    {
        // The session folder is under a file: no folder can be made there.
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "a file, not a folder");
        var output = new StringBuilder();
        var handler = Handler(output);
        handler.Attach(new FakeGame(Path.Combine(blocker, "session"), _ => null, Readable));

        var outcome = handler.Handle(new InvalidOperationException("boom"), "the game loop", stopped: true);

        Assert.NotNull(outcome);
        Assert.Null(outcome.ReportPath);
        Assert.Contains("the report could not be written", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFailingSaveIsNotedAndTheReportStillCarriesTheException()
    {
        var session = Path.Combine(_root, "session");
        var output = new StringBuilder();
        var handler = Handler(output);
        handler.Attach(new FakeGame(session, _ => throw new UnauthorizedAccessException("read-only"), Readable));

        var outcome = handler.Handle(new InvalidOperationException("boom"), "the game loop", stopped: true);

        Assert.NotNull(outcome!.ReportPath);
        Assert.Null(outcome.SavePath);
        Assert.Contains("not written: UnauthorizedAccessException: read-only", File.ReadAllText(outcome.ReportPath!), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCrashReadsTheSessionLogTail()
    {
        var session = Path.Combine(_root, "session");
        Directory.CreateDirectory(session);
        File.WriteAllLines(Path.Combine(session, SessionLog.SessionFileName), Enumerable.Range(1, 3).Select(n => $"{{\"line\":{n}}}"));
        var handler = Handler(new StringBuilder());
        handler.Attach(new FakeGame(session, _ => null, Readable));

        var outcome = handler.Handle(new InvalidOperationException("boom"), "the game loop", stopped: true);

        var text = File.ReadAllText(outcome!.ReportPath!);
        Assert.Contains("{\"line\":1}\n{\"line\":2}\n{\"line\":3}\n", text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void ASecondStoppingCrashIsNotWrittenAndAnUnobservedTaskExceptionKeepsTheGameGoing()
    {
        var session = Path.Combine(_root, "session");
        var handler = Handler(new StringBuilder());
        handler.Attach(new FakeGame(session, WriteSave, Readable));

        Assert.NotNull(handler.Handle(new InvalidOperationException("first"), "the game loop", stopped: true));
        Assert.Null(handler.Handle(new InvalidOperationException("second"), "an unhandled exception", stopped: true));

        var reports = Directory.GetFiles(Path.Combine(session, "reports"), "*.md");
        Assert.Single(reports);

        var other = new CrashHandler(() => Fixed, new StringWriter(), "abc1234");
        other.Attach(new FakeGame(Path.Combine(_root, "other"), WriteSave, Readable));
        var task = other.Handle(new InvalidOperationException("task"), "an unobserved task exception", stopped: false);
        Assert.NotNull(task);
        Assert.Null(task.SavePath); // no save: the game is still running
        Assert.False(other.Stopped);
        Assert.Contains("kept running", File.ReadAllText(task.ReportPath!), StringComparison.Ordinal);
    }

    [Fact]
    public void ANullExceptionIsNotWrittenAndNothingThrows()
    {
        var handler = new CrashHandler(() => Fixed, new StringWriter(), "abc1234");

        Assert.Null(handler.Handle(null!, "the game loop", stopped: true));
        Assert.False(handler.Stopped);
    }

    [Fact]
    public void ABrokenGameNeverMakesTheHandlerThrow()
    {
        var session = Path.Combine(_root, "session");
        var output = new StringBuilder();
        var handler = Handler(output);
        handler.Attach(new ThrowingGame(session));

        var outcome = handler.Handle(new InvalidOperationException("boom"), "the game loop", stopped: true);

        Assert.NotNull(outcome); // the report's folder could not be read, so no report; the handler said so and went on
        Assert.Null(outcome.ReportPath);
        Assert.Contains("the report could not be written", output.ToString(), StringComparison.Ordinal);
    }

    private sealed class ThrowingGame(string sessionDirectory) : ICrashSource
    {
        public string SessionDirectory { get; } = sessionDirectory;

        public string? ReportDirectory => throw new ObjectDisposedException("game");

        public CrashGameState State() => throw new InvalidOperationException("the world is being built");

        public string? LastChanceSave(string path) => throw new InvalidOperationException("never called in this test");
    }
}
