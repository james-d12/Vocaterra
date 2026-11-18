using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Tests.Diagnostics;

/// <summary>
/// The report script (<c>scripts/playtest-report.sh</c>, S11) on committed fixture session folders: the summary's sections, the gate lines with
/// their evidence, and a refusal of a folder with no session log. It runs bash and Python 3 (standard library); without them the tests do nothing.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class PlaytestReportTests
{
    private static string Script => System.IO.Path.GetFullPath(Repo.Path("scripts", "playtest-report.sh"));

    private static string Fixture(string name) => Repo.Path("tests", "Vocaterra.Game.Tests", "Fixtures", name);

    private static bool Available(string tool)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(tool, "--version") { RedirectStandardOutput = true, RedirectStandardError = true });
            process?.WaitForExit((int)HangGuard.Scale(10000));
            return process is { ExitCode: 0 };
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static (int Code, string Output) Run(params string[] folders)
    {
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Script);
        foreach (var folder in folders)
        {
            start.ArgumentList.Add(folder);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void TheReportSummarisesTheFixtureSessionsAndItsGatesWithTheirEvidence()
    {
        if (!Available("bash") || !Available("python3"))
        {
            return;
        }

        var (code, output) = Run(Fixture("session-pre-and-world"), Fixture("session-recall"));

        Assert.Equal(0, code);
        Assert.Contains("== " + Fixture("session-pre-and-world"), output, StringComparison.Ordinal);
        Assert.Contains("== " + Fixture("session-recall"), output, StringComparison.Ordinal);
        Assert.Contains("== TOTAL (2 folders)", output, StringComparison.Ordinal);
        Assert.Contains("Completion: objective 4/4 steps; complete in 36 s of play", output, StringComparison.Ordinal);
        Assert.Contains("Checks pre production: correct 4/4", output, StringComparison.Ordinal);
        Assert.Contains("Checks recall production: correct 3/4, dont_know 1", output, StringComparison.Ordinal);
        Assert.Contains("Transfer uses: 2", output, StringComparison.Ordinal);
        Assert.Contains("Gate Playable (evidence): objective complete", output, StringComparison.Ordinal);
        Assert.Contains("Gate Learning signal: evidence only", output, StringComparison.Ordinal);
        Assert.Contains("Gate Transfer: evidence only", output, StringComparison.Ordinal);
        Assert.Contains("Gate Enjoyable: observer notes needed", output, StringComparison.Ordinal);
        Assert.Contains("Evidence only:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportPrintsTheMeasuredDurationsOfTheLessonTheWalkTheChecksAndTheWholeSession()
    {
        if (!Available("bash") || !Available("python3"))
        {
            return;
        }

        var (code, output) = Run(Fixture("session-timed"), Fixture("session-recall"));

        Assert.Equal(0, code);
        Assert.Contains("run, session 1: whole session 23 min 20 s", output, StringComparison.Ordinal); // 10:00:00 to 10:23:20
        Assert.Contains("lesson: 4 min 30 s", output, StringComparison.Ordinal); // 10:03:10 to 10:07:40
        Assert.Contains("walk: 11 min 45 s (to the objective's completion, after the lesson)", output, StringComparison.Ordinal); // 10:07:40 to 10:19:25
        Assert.Contains("check pre: 2 min 30 s (20 answered, 0 skipped)", output, StringComparison.Ordinal);
        Assert.Contains("check post: 3 min 30 s (19 answered, 1 skipped)", output, StringComparison.Ordinal);
        Assert.Contains("lesson: not run in this session", output, StringComparison.Ordinal); // the recall fixture has no lesson
        Assert.Contains("Durations over 2 runs", output, StringComparison.Ordinal);
        Assert.Contains("lesson: median 4 min 30 s, 4 min 30 s to 4 min 30 s over 1 run", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderWithoutASessionLogIsRefused()
    {
        if (!Available("bash") || !Available("python3"))
        {
            return;
        }

        var (code, output) = Run(Repo.Path("content"));

        Assert.NotEqual(0, code);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void NoArgumentsPrintsTheUsage()
    {
        if (!Available("bash"))
        {
            return;
        }

        var (code, _) = Run();

        Assert.Equal(2, code);
    }
}
