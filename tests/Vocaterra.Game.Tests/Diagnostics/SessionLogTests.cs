using System.Text.Json;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Diagnostics;

namespace Vocaterra.Game.Tests.Diagnostics;

/// <summary>
/// The session log (S11): one JSON object per line, with the wall time from the clock it is given (a fake one here), and a shape for each kind of
/// event. The replay log is the core's, numbered from 1, and a turn names its line.
/// </summary>
public sealed class SessionLogTests
{
    private static readonly DateTimeOffset Stamp = DateTimeOffset.Parse("2026-10-22T09:30:00+01:00", System.Globalization.CultureInfo.InvariantCulture);

    private static VoxelSession NewSession() => RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.Default);

    private static SessionProfile Profile(string? check = null) => new(MicCheck: false, Muted: true, NoSpeech: true, Greyscale: false, TypedCredit: "half", Check: check, Scripted: true);

    private static (SessionLog Log, StringWriter Session, StringWriter Replay) Memory(int replayLines = 0)
    {
        var session = new StringWriter();
        var replay = new StringWriter();
        return (new SessionLog(session, replay, () => Stamp, replayLines), session, replay);
    }

    private static List<JsonElement> Lines(StringWriter writer) =>
        writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static line => JsonDocument.Parse(line).RootElement.Clone()).ToList();

    private static List<AppliedTurn> Drain(VoxelSession session, int count)
    {
        var all = new List<AppliedTurn>();
        var deadline = Environment.TickCount64 + 30000;
        while (all.Count < count)
        {
            all.AddRange(session.Collect());
            if (all.Count >= count)
            {
                break;
            }

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} turns came back.");
            Thread.Sleep(2);
        }

        return all;
    }

    private static CheckResult PickResult() =>
        new("hear.pedir-agua", CheckItemKind.Comprehension, "pedir-agua", CheckInput.Pick, 1, true, null, null, false, false, [], null, 812.4, null);

    [Fact]
    public void TheSessionSummaryNamesTheDriveRateOnlyWhenItIsNotOne()
    {
        var (log, session, _) = Memory();
        log.SessionSummary(["a"], 10);
        log.SessionSummary(["a"], 20, 1.75);
        var lines = Lines(session);
        Assert.False(lines[0].TryGetProperty("needsRate", out _));
        Assert.Equal(1.75, lines[1].GetProperty("needsRate").GetDouble());
    }

    [Fact]
    public void EveryLineIsOneObjectWithItsTypeAndTheWallTime()
    {
        var (log, session, _) = Memory();
        log.SessionStart(1, 42, "abcdef123456", 1_760_000_000, Profile());
        log.CheckStart("pre", 1, 10);
        log.CheckResult(PickResult(), "pre", 1, 0);
        log.CheckEnd("pre", 1, 0);
        log.Dispose();

        var lines = Lines(session);

        Assert.Equal(4, lines.Count);
        foreach (var line in lines)
        {
            Assert.True(line.TryGetProperty("type", out _));
            Assert.Equal(Stamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture), line.GetProperty("t").GetString());
        }
    }

    [Fact]
    public void TheSessionStartNamesTheSeedBundleSessionAndProfile()
    {
        var (log, session, _) = Memory();
        log.SessionStart(2, 42, "abcdef123456", 1_760_000_000, Profile("pre"));

        var start = Lines(session).Single();

        Assert.Equal("session_start", start.GetProperty("type").GetString());
        Assert.Equal(2, start.GetProperty("session").GetInt32());
        Assert.Equal(42, start.GetProperty("seed").GetInt32());
        Assert.Equal("abcdef123456", start.GetProperty("bundle").GetString());
        Assert.Equal(1_760_000_000, start.GetProperty("stored_time").GetInt64());
        var profile = start.GetProperty("profile");
        Assert.True(profile.GetProperty("muted").GetBoolean());
        Assert.Equal("pre", profile.GetProperty("check").GetString());
        Assert.Equal("half", profile.GetProperty("typed_credit").GetString());
    }

    [Fact]
    public void ASkippedRunSaysSoAtItsStartAndAnOrdinaryOneDoesNot()
    {
        var (log, session, _) = Memory();
        log.SessionStart(4, 42, "abcdef123456", 1_760_000_000, Profile(), skippedTo: 4);
        log.SessionStart(5, 42, "abcdef123456", 1_760_000_000, Profile());

        var starts = Lines(session);

        Assert.Equal(4, starts[0].GetProperty("skipped_to").GetInt32()); // --skip-to-session (V12c): no learner's session
        Assert.False(starts[1].TryGetProperty("skipped_to", out _));
    }

    [Fact]
    public void TheEndOfASessionBegunByASleepCountsOnlyItsOwnEvents()
    {
        var game = RouteScene.NewSessionWithShippedLessons(TestGame.Bundle, TestGame.Nouns, CreditRules.Default);
        _ = game.Simulation.GrantItems(ItemId.Wood, 5); // the camp counts the wood and the fruit in the bag (#32)
        _ = game.Simulation.GrantItems(ItemId.Berry, 3);
        foreach (var (name, detail, times) in new[] { (WorldEvents.ItemPickedUp, "madera", 5), (WorldEvents.ItemPickedUp, "fruta", 3), (WorldEvents.TorchAtRing, "firering", 1), (WorldEvents.BottleFilled, (string?)null, 1) })
        {
            for (var i = 0; i < times; i++)
            {
                _ = game.HostEvent(name, detail);
            }
        }

        game.Lessons.CountAsCompleted("first-words", 1);
        game.SkipToEnding(); // the player's own fire (V12c's skip): session 1 is complete
        var (before, beforeLines, _) = Memory();
        before.End(game, game.Simulation.Tick);
        Assert.Equal(JsonValueKind.Number, Lines(beforeLines).Single(static l => l.GetProperty("type").GetString() == "session_end").GetProperty("objective_complete_tick").ValueKind);

        SaveGame.Sleep(game, SaveGame.Capture(game, RouteScene.Build()));
        var (log, session, _) = Memory();
        log.End(game, game.Simulation.Tick);

        var end = Lines(session).Single(static l => l.GetProperty("type").GetString() == "session_end");
        Assert.Equal(2, end.GetProperty("session").GetInt32());
        Assert.True(end.GetProperty("objective_complete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, end.GetProperty("objective_complete_tick").ValueKind); // the camp was completed in session 1: a load's end line says the same
    }

    [Fact]
    public void ACheckResultLineHasTheAnswerAndTheCheckKind()
    {
        var (log, session, _) = Memory();
        var grade = new CheckGrader(TestGame.Bundle, 42).GradeTyped(
            TestGame.Data.Checks.Single(static i => i.Id == "say.pedir-agua"),
            "Quiero agua, por favor.",
            InputKind.Typed,
            new Vocaterra.Nlp.Core.Determinism.StoredTimestamp(GuideSession.DefaultStartedAt),
            "hash");
        var production = new CheckResult("say.pedir-agua", CheckItemKind.Production, "pedir-agua", CheckInput.Typed, null, grade.Correct, grade.Transcript, grade.Intent, grade.Communicated, grade.DontKnow, grade.ErrorTags, null, 230, grade.Entry);

        log.CheckResult(PickResult(), "pre", 1, 0);
        log.CheckResult(production, "post", 1, 4800);
        log.Dispose();

        var pick = Lines(session)[0];
        Assert.Equal("check_result", pick.GetProperty("type").GetString());
        Assert.Equal("pre", pick.GetProperty("check").GetString());
        Assert.Equal("comprehension", pick.GetProperty("kind").GetString());
        Assert.Equal("pick", pick.GetProperty("input").GetString());
        Assert.Equal(2, pick.GetProperty("option").GetInt32());
        Assert.True(pick.GetProperty("correct").GetBoolean());
        Assert.Equal(JsonValueKind.Null, pick.GetProperty("transcript").ValueKind);
        Assert.Equal(812.4, pick.GetProperty("latency_ms").GetDouble());
        Assert.Equal(JsonValueKind.Null, pick.GetProperty("replay").ValueKind);

        var typed = Lines(session)[1];
        Assert.Equal("production", typed.GetProperty("kind").GetString());
        Assert.Equal("typed", typed.GetProperty("input").GetString());
        Assert.Equal("request_item", typed.GetProperty("intent").GetString());
        Assert.True(typed.GetProperty("communicated").GetBoolean());
        Assert.Equal(1, typed.GetProperty("replay").GetInt32());
        Assert.Equal(1, log.ReplayLines);
    }

    [Fact]
    public void ACheckStartAndEndCountTheirAnswers()
    {
        var (log, session, _) = Memory();
        log.CheckStart("recall", 2, 10);
        log.CheckEnd("recall", 7, 3);
        log.Dispose();

        var lines = Lines(session);

        Assert.Equal("check_start", lines[0].GetProperty("type").GetString());
        Assert.Equal(10, lines[0].GetProperty("items").GetInt32());
        Assert.Equal(7, lines[1].GetProperty("answered").GetInt32());
        Assert.Equal(3, lines[1].GetProperty("skipped").GetInt32());
    }

    [Fact]
    public void ATurnLineCarriesTheInputVerdictLatencyAndPhraseClassification()
    {
        var (log, session, replay) = Memory();
        using var game = NewSession();
        game.OpenTalk("shopkeeper");
        var worker = game.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        _ = game.SubmitTyped("quiero agua, por favor");
        var applied = Drain(game, 1).Single();

        log.Turn(applied, game); // the Applied event's moment: the turn's events are logged, nothing else has been written since
        log.Pump(game);
        log.Dispose();

        var lines = Lines(session);
        var turn = lines.Single(static l => l.GetProperty("type").GetString() == "turn");
        Assert.Equal("shopkeeper", turn.GetProperty("npc").GetString());
        Assert.Equal("stall-need", turn.GetProperty("prompt").GetString());
        Assert.Equal("typed", turn.GetProperty("input").GetString());
        Assert.Equal("quiero agua, por favor", turn.GetProperty("transcript").GetString());
        Assert.Equal("request_item", turn.GetProperty("intent").GetString());
        Assert.True(turn.GetProperty("communicated").GetBoolean());
        Assert.Equal(2, turn.GetProperty("replay").GetInt32()); // line 1 is the shopkeeper's opening host turn
        Assert.True(turn.GetProperty("latency_ms").GetDouble() >= 0);
        Assert.Equal("pedir-agua", turn.GetProperty("phrase").GetString());
        Assert.Equal("supported", turn.GetProperty("classification").GetString()); // the card is up at the stall's first question
        Assert.Equal("english-and-card", turn.GetProperty("support").GetString()); // the level the phrase had when it was used: the first rung

        var phrase = lines.Single(static l => l.GetProperty("type").GetString() == "phrase");
        Assert.Equal("pedir-agua", phrase.GetProperty("phrase").GetString());
        Assert.Equal("typed", phrase.GetProperty("input").GetString());

        // The replay record of the turn is the first line of replay.jsonl, and the turn names it.
        Assert.Equal(2, replay.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(2, log.ReplayLines);
    }

    [Fact]
    public void VoiceWorldAndPhraseEventsEachHaveTheirLine()
    {
        var (log, session, _) = Memory();
        var game = NewSession();
        game.OpenTalk("shopkeeper");
        game.LogVoice(VoiceEventKind.Hint);
        game.LogVoice(VoiceEventKind.Replay, "No sé");

        log.Pump(game);
        log.Dispose();

        var lines = Lines(session);
        Assert.Contains(lines, static l => l.GetProperty("type").GetString() == "world" && l.GetProperty("name").GetString() == WorldEvents.TalkOpened);
        Assert.Contains(lines, static l => l.GetProperty("type").GetString() == "voice" && l.GetProperty("kind").GetString() == "hint");
        var replayed = lines.Single(static l => l.GetProperty("type").GetString() == "voice" && l.GetProperty("kind").GetString() == "replay");
        Assert.Equal("shopkeeper", replayed.GetProperty("npc").GetString());
        Assert.Equal("No sé", replayed.GetProperty("detail").GetString());
    }

    [Fact]
    public void TheSessionEndNamesTheObjectiveAndThePhrases()
    {
        var (log, session, _) = Memory();
        var game = NewSession();
        game.LogVoice(VoiceEventKind.NoSe);

        log.End(game, 1200);

        var end = Lines(session).Last();
        Assert.Equal("session_end", end.GetProperty("type").GetString());
        Assert.Equal(1200, end.GetProperty("tick").GetInt64());
        Assert.False(end.GetProperty("objective_complete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, end.GetProperty("objective_complete_tick").ValueKind);
        Assert.Equal(4, end.GetProperty("objective").GetArrayLength());
        Assert.Equal("madera", end.GetProperty("objective")[0].GetProperty("step").GetString());
    }

    [Fact]
    public void AFolderKeepsItsLogsAndANewRunAppendsAndNumbersOnFromTheReplayLines()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vocaterra-log-" + Path.GetRandomFileName());
        try
        {
            using (var first = SessionLog.Open(directory, () => Stamp))
            {
                first.SessionStart(1, 42, "h", 0, Profile());
            }

            using (var second = SessionLog.Open(directory, () => Stamp))
            {
                Assert.Equal(0, second.ReplayLines);
                second.SessionStart(2, 42, "h", 0, Profile("recall"));
            }

            var lines = File.ReadAllLines(Path.Combine(directory, SessionLog.SessionFileName));
            Assert.Equal(2, lines.Length);
            Assert.Contains("\"check\":\"recall\"", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultFolderIsNamedByTheLocalDateTimeAndTheSession()
    {
        var folder = SessionLog.DefaultDirectory(new DateTime(2026, 10, 22, 9, 30, 15), 3);

        Assert.EndsWith(Path.Combine("vocaterra", "sessions", "20261022-093015-s3"), folder, StringComparison.Ordinal);
    }
}
