using Vocaterra.Game.Reporting;

namespace Vocaterra.Game.Tests;

/// <summary>The crash report's text (X05, R04): a fixed exception gives fixed text, the log is cut to the newest lines, and a fence holds a fence.</summary>
public sealed class CrashReportTests
{
    private const string Stack = "   at Vocaterra.MonoGame.VoxelGame.StepOnce() in VoxelGame.cs:line 880\n   at Vocaterra.MonoGame.VoxelGame.Update()";

    private static CrashInput Fixed(IReadOnlyList<string>? log = null, IReadOnlyList<(string Item, string Value)>? state = null) => new(
        Stopped: true,
        SavedAt: "2026-10-23 10:15:00",
        BuildCommit: "abc1234",
        Where: "the game loop",
        Session: 2,
        Tick: 4681,
        ExceptionType: "System.InvalidOperationException",
        Message: "debug crash: --debug-crash-after 4680 asked for it (a test hook, not a bug)",
        Stack: Stack,
        State: state ?? [("Bundle", "game-v1, hash 1a2b3c"), ("Seed", "42"), ("Lesson", "none"), ("Dialogue open", "no")],
        StateNote: null,
        SaveNote: "written: crash.save (the last state the game was in; the autosave and any save of yours are not touched)",
        LogLines: log ?? ["{\"type\":\"session_start\"}", "{\"type\":\"session_end\"}"]);

    [Fact]
    public void AFixedExceptionGivesTheFixedReport()
    {
        var expected = """
            # Crash report: InvalidOperationException

            - **Kind:** Crash (the game stopped)
            - **Saved:** 2026-10-23 10:15:00 (local time)
            - **Session:** 2, tick 4681
            - **Build commit:** abc1234
            - **Where:** the game loop

            ## Exception

            **System.InvalidOperationException**: debug crash: --debug-crash-after 4680 asked for it (a test hook, not a bug)

            ```text
               at Vocaterra.MonoGame.VoxelGame.StepOnce() in VoxelGame.cs:line 880
               at Vocaterra.MonoGame.VoxelGame.Update()
            ```

            ## Game state

            | Item | Value |
            | --- | --- |
            | Bundle | game-v1, hash 1a2b3c |
            | Seed | 42 |
            | Lesson | none |
            | Dialogue open | no |

            ## Last-chance save

            written: crash.save (the last state the game was in; the autosave and any save of yours are not touched)

            ## Session log (the newest 200 lines at most)

            ```jsonl
            {"type":"session_start"}
            {"type":"session_end"}
            ```

            """.ReplaceLineEndings("\n");

        Assert.Equal(expected, CrashReport.Build(Fixed()).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TheTitleIsTheExceptionsNameWithoutItsNamespace()
    {
        Assert.Equal("NullReferenceException", CrashReport.Title("System.NullReferenceException"));
        Assert.Equal("Plain", CrashReport.Title("Plain"));
        Assert.Equal("Unknown exception", CrashReport.Title("  "));
    }

    [Fact]
    public void AStoppedCrashIsSaidAndAnUnobservedTaskExceptionSaysTheGameWentOn()
    {
        var unobserved = Fixed() with { Stopped = false, Where = "an unobserved task exception", SaveNote = "not written: the game kept running (an unobserved task exception)" };
        var text = CrashReport.Build(unobserved);

        Assert.Contains("- **Kind:** Unobserved task exception (the game kept running)", text, StringComparison.Ordinal);
        Assert.Contains("- **Where:** an unobserved task exception", text, StringComparison.Ordinal);
        Assert.Contains("not written: the game kept running", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableStateSaysSoAndTheHeaderSaysUnknown()
    {
        var text = CrashReport.Build(Fixed() with
        {
            Session = null,
            Tick = null,
            State = [],
            StateNote = "The game state was not read: InvalidOperationException: the world is being built.",
        });

        Assert.Contains("- **Session:** unknown, tick unknown", text, StringComparison.Ordinal);
        Assert.Contains("The game state could not be read.", text, StringComparison.Ordinal);
        Assert.Contains("The game state was not read: InvalidOperationException: the world is being built.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("| Item | Value |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLogIsCutToItsNewestLinesAndKeepsItsOrder()
    {
        var log = Enumerable.Range(1, ReportBounds.LogLines + 50).Select(n => $"{{\"n\":{n}}}").ToList();

        var text = CrashReport.Build(Fixed(log));

        Assert.DoesNotContain("{\"n\":50}\n", text, StringComparison.Ordinal);
        Assert.Contains("{\"n\":51}\n", text, StringComparison.Ordinal);
        Assert.Contains($"{{\"n\":{ReportBounds.LogLines + 50}}}\n", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("{\"n\":51}", StringComparison.Ordinal) < text.IndexOf($"{{\"n\":{ReportBounds.LogLines + 50}}}", StringComparison.Ordinal));
    }

    [Fact]
    public void AFenceLongerThanAnyBacktickRunInTheLogOrTheStackHoldsIt()
    {
        var log = new[] { "{\"text\":\"```\"}" };
        var text = CrashReport.Build(Fixed(log) with { Stack = "at x() in `````" });

        Assert.Contains("````jsonl\n", text, StringComparison.Ordinal); // three backticks in the log line: a fence of four
        Assert.Contains("``````text\n", text, StringComparison.Ordinal); // five in the stack: a fence of six
    }

    [Fact]
    public void AMessageWithLineBreaksStaysOnOneLineInTheTable()
    {
        var text = CrashReport.Build(Fixed(state: [("Objective", "first\nsecond | third")]));

        Assert.Contains("| Objective | first second \\| third |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCrashReportNamesAreTheReportKindsWordsAndNoClockIsRead()
    {
        // The report's file name is the front-end's; the game layer only formats what it is given, so the same input is the same text.
        Assert.Equal(CrashReport.Build(Fixed()), CrashReport.Build(Fixed()));
        Assert.Equal("bug", ReportKinds.Word(ReportKind.Bug));
    }

    [Fact]
    public void NullInputIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => CrashReport.Build(null!));
        Assert.Throws<ArgumentNullException>(() => CrashReport.Title(null!));
    }
}
