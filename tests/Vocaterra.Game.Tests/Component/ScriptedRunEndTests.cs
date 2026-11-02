using System.Text.Json;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Scripted;

namespace Vocaterra.Game.Tests.Component;

/// <summary>
/// A scripted run cut short at the runaway limit (V26): it printed nothing and dropped the steps after the tick. The class needs a window's graphics device to
/// end a run, so the tests take the line it prints and the session log's line apart from it; the run itself is checked by hand (a run of <c>--wait 400</c>).
/// </summary>
public sealed class ScriptedRunEndTests
{
    [Fact]
    public void TheLineNamesTheTickTheStepsLeftAndTheFirstOfThem()
    {
        var line = ScriptedRunEnd.RunawayLine(18001, 12, new ScriptStep(ScriptStepKind.Say, Value: "quiero pan"), stepInProgress: false);

        Assert.Equal("scripted run stopped at tick 18001 (the limit of 18000, five minutes of play): 12 steps not started, the first: Say \"quiero pan\"; exit 2", line);
    }

    [Fact]
    public void AStepInProgressAndASingleStepAreSaid()
    {
        var line = ScriptedRunEnd.RunawayLine(18001, 1, new ScriptStep(ScriptStepKind.Leave), stepInProgress: true);

        Assert.Contains("1 step not started, the first: Leave; a step was in progress", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ARunWithNoStepLeftSaysSo()
    {
        Assert.Contains("no step was left to start", ScriptedRunEnd.RunawayLine(18001, 0, null, stepInProgress: false), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSessionLogGetsTheSameLine()
    {
        var session = new StringWriter();
        using var log = new SessionLog(session, new StringWriter(), () => DateTimeOffset.UnixEpoch);

        log.RunTruncated(18001, "scripted run stopped at tick 18001");

        var written = JsonDocument.Parse(session.ToString().Trim()).RootElement;
        Assert.Equal("run_truncated", written.GetProperty("type").GetString());
        Assert.Equal(18001, written.GetProperty("tick").GetInt64());
        Assert.Equal("scripted run stopped at tick 18001", written.GetProperty("message").GetString());
    }

    [Fact]
    public void ARunCutShortExitsNonZero() => Assert.NotEqual(0, ScriptedRunEnd.RunawayExitCode);
}
