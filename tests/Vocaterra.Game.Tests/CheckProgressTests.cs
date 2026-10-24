using Vocaterra.Game.Language.Checks;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Game.Session;

namespace Vocaterra.Game.Tests;

/// <summary>
/// A check is measurement, not practice (S11): a graded answer goes through the worker and comes back as a grade, and nothing of it reaches the
/// learner model, the phrase progress, the conversations, the events or the world.
/// </summary>
public sealed class CheckProgressTests
{
    // Everything a player could see change: the progress, the conversations, the events, the world and the objective, as plain values.
    private static string Snapshot(VoxelSession session) => string.Join(
        '|',
        session.Events.Count,
        session.Conversation.Records.Count,
        string.Join(";", session.PhraseProgress.Records.Select(static r => r.ToString())),
        session.Conversation.ContextOf("shopkeeper").AgentState.StateId,
        session.Conversation.ContextOf("shopkeeper").Learner.Skills.Count,
        session.Conversation.ContextOf("guide").Learner.Skills.Count,
        session.Objective.Summary,
        session.Simulation.ComputeStateDigest(),
        session.PendingTurns);

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

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} check answers came back.");
            Thread.Sleep(2);
        }

        return all;
    }

    [Fact]
    public void ACheckAnswerThroughTheWorkerIsGradedAndChangesNothingElse()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        var before = Snapshot(session);
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.pedir-agua");

        _ = session.SubmitCheck(item, TurnSource.Typed, "Quiero agua, por favor.", null, null);
        var applied = Drain(session, 1).Single();

        Assert.Same(item, applied.Outcome.Request.Check);
        Assert.NotNull(applied.Outcome.Grade);
        Assert.True(applied.Outcome.Grade!.Correct);
        Assert.Null(applied.Turn);
        Assert.Null(applied.Words);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void ACheckAnswerThatIsNotAGameTurnLeavesTheTurnHistoryAlone()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.donde-esta-el-rio");
        var records = session.Conversation.Records.Count;

        _ = session.SubmitCheck(item, TurnSource.Button, GuideSession.DontKnowLine, null, null);
        var applied = Drain(session, 1).Single();

        Assert.True(applied.Outcome.Grade!.DontKnow);
        Assert.Equal(records, session.Conversation.Records.Count);
        Assert.Empty(session.Events);
        Assert.Equal(0, session.PhraseProgress.Records.Sum(static r => r.DontKnow));
    }

    [Fact]
    public void ASpokenCheckNeedsTheRecognizerAndSaysSoWithoutAGrade()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.pedir-agua");

        _ = session.SubmitCheck(item, TurnSource.Clip, null, PcmClipForTest(), null);
        var applied = Drain(session, 1).Single();

        Assert.Null(applied.Outcome.Grade);
        Assert.NotNull(applied.Outcome.Problem);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void ASubmitForAComprehensionTypeIsRefusedEvenBeforeTheWorkerRuns()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "hear.pedir-agua");

        _ = Assert.Throws<ArgumentException>(() => session.SubmitCheck(item, TurnSource.Typed, "Quiero agua, por favor.", null, null));
        var production = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.pedir-agua");
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => session.SubmitCheck(production, TurnSource.Choice, null, null, null));
    }

    // A clip the recognizer never sees (no speech is loaded): one sample, 16 kHz mono.
    private static Vocaterra.Nlp.Core.Audio.PcmClip PcmClipForTest() => Vocaterra.Nlp.Core.Audio.PcmClip.FromSamples(new short[1600]);
}
