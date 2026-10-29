using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// The turn worker (P5): turns are submitted with their inputs fixed, evaluated one at a time on a dedicated thread and applied in order on the
/// caller's thread. These tests use a typed-only worker, so no model is needed.
/// </summary>
public sealed class TurnWorkerTests
{
    private static VoxelSession StartedSession(CreditRules? credit = null)
    {
        var session = TestGame.NewSession(credit: credit ?? CreditRules.Default);
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        return session;
    }

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

    // Everything a turn tells the player and the world, in plain values (the turn record itself holds lists, which compare by reference).
    private static string Project(GuideTurn turn) => string.Join(
        '|',
        turn.Line,
        turn.Intent,
        turn.Communicated,
        turn.Correctness,
        string.Join(';', turn.Errors.Select(static e => $"{e.Tag}:{e.Expected}:{e.Actual}")),
        string.Join(';', turn.GuideLines),
        turn.AgentState,
        string.Join(';', turn.HostEvents),
        turn.Pointer,
        turn.HostStateHash,
        turn.Input);

    [Fact]
    public void TheWorkerGivesTheSameTurnsAsTheSynchronousPathAndAppliesThemInOrder()
    {
        var sync = TestGame.NewSession(credit: CreditRules.Default);
        var cursor = TestGame.NearestBlock(sync, BlockId.Trunk);
        var lines = new[] { "¿dónde está el árbol?", "es un árbol", "yo tiene dos espada" };
        var expected = lines.Select(l => sync.Say(l, cursor)).ToList();

        using var session = StartedSession();
        var requests = lines.Select(l => session.SubmitTyped(l, cursor)).ToList();
        Assert.Equal(lines.Length, session.PendingTurns);
        var applied = Drain(session, lines.Length);

        Assert.Equal([1L, 2L, 3L], requests.Select(static r => r.Id));
        Assert.Equal(requests.Select(static r => r.Id), applied.Select(static a => a.Outcome.Request.Id));
        Assert.Equal(expected.Select(Project), applied.Select(static a => Project(a.Turn!)));
        Assert.Equal(0, session.PendingTurns);
        Assert.Equal(sync.Pointer, session.Pointer);
        Assert.Equal(sync.Words.RecordOf("árbol"), session.Words.RecordOf("árbol"));
        Assert.Equal(sync.Conversation.GuideState, session.Conversation.GuideState);
    }

    [Fact]
    public void TheStoredTimeAndTheStateDigestAreFixedWhenTheLineIsSubmittedNotWhenTheWorkerRuns()
    {
        using var session = StartedSession();
        var cursor = TestGame.NearestBlock(session, BlockId.Trunk);
        var digest = session.Simulation.ComputeStateDigest();

        var request = session.SubmitTyped("es un árbol", cursor);
        session.SkipTime(7200); // two hours pass before the answer is applied
        session.Simulation.Step([VoxelCommand.SelectSlot(3)]);
        var applied = Drain(session, 1).Single();

        Assert.Equal(GuideSession.DefaultStartedAt, request.Now.UnixSeconds);
        Assert.Equal(digest, request.HostStateHash);
        Assert.NotNull(applied.Turn);
        Assert.Equal(GuideSession.DefaultStartedAt, session.Words.RecordOf("árbol").LastCreditedAt);
        Assert.True(session.StoredTime.UnixSeconds > GuideSession.DefaultStartedAt + 7000);
    }

    [Fact]
    public void ATypedUseEarnsHalfCreditAndTheLaunchSettingGivesItFull()
    {
        using var half = StartedSession(CreditRules.Default);
        using var full = StartedSession(CreditRules.FullTyped);
        var cursor = TestGame.NearestBlock(half, BlockId.Trunk);

        _ = half.SubmitTyped("es un árbol", cursor);
        _ = full.SubmitTyped("es un árbol", cursor);
        Drain(half, 1);
        Drain(full, 1);

        Assert.Equal(WordProgress.UnitsPerUse / 2, half.Words.RecordOf("árbol").CreditUnits);
        Assert.Equal(WordProgress.UnitsPerUse, full.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void ASpokenRequestWithNoSpeechLoadedFailsWithAMessageAndChangesNothing()
    {
        using var session = StartedSession();
        var state = session.Conversation.GuideState;

        _ = session.SubmitSpeak("¿dónde está el árbol?");
        _ = session.SubmitWav("/nonexistent/file.wav");
        var applied = Drain(session, 2);

        Assert.All(applied, static a =>
        {
            Assert.Null(a.Turn);
            Assert.False(string.IsNullOrWhiteSpace(a.Outcome.Problem));
        });
        Assert.Equal(state, session.Conversation.GuideState);
        Assert.Null(session.Pointer);
        Assert.Equal(0, session.Words.RecordOf("árbol").CreditUnits);
        Assert.False(session.Worker!.CanHear);
        Assert.NotNull(session.Worker.Load!.Problem);
    }

    [Fact]
    public void NoReviewQuestionIsAskedWhileATurnIsPending()
    {
        using var session = StartedSession(CreditRules.FullTyped);
        var cursor = TestGame.NearestBlock(session, BlockId.Trunk);
        _ = session.SubmitTyped("es un árbol", cursor);
        Drain(session, 1);
        session.SkipTime(11 * 60);
        Assert.NotNull(session.BeginTalk()); // the word is due: she asks

        _ = session.SubmitTyped("hola", cursor);

        Assert.Null(session.BeginTalk()); // a turn is out: the words and her state are about to change
        Drain(session, 1);
    }

    [Fact]
    public void ASessionWithoutAWorkerCollectsNothingAndRefusesToSubmit()
    {
        var session = TestGame.NewSession();

        Assert.Empty(session.Collect());
        Assert.Equal(0, session.PendingTurns);
        Assert.Throws<InvalidOperationException>(() => session.SubmitTyped("hola"));
        session.StartWorker(loadSpeech: false);
        Assert.Throws<InvalidOperationException>(() => session.StartWorker(loadSpeech: false));
        session.StopWorker();
        Assert.Null(session.Worker);
    }

    [Fact]
    public void ASnapshotOfTheWorldDoesNotSeeLaterChanges()
    {
        var session = TestGame.NewSession();
        var camp = session.Scene.Point("camp.guide");
        var bush = new BlockPos(camp.X - 6, camp.Y + 1, camp.Z + 3); // a berry bush of the guide's camp: the fruta of the route
        session.Simulation.TeleportTo(new BlockPos(bush.X + 2, bush.Y, bush.Z));
        var before = WorldFacts.Snapshot(session.Scene.Grid, TestGame.Nouns);
        Assert.Equal(["fruta"], before.NounsAt(bush));

        Assert.True(VoxelWalker.Gather(session, bush));

        Assert.Equal(["fruta"], before.NounsAt(bush)); // the copy still has the bush with its berry
        Assert.Empty(WorldFacts.Snapshot(session.Scene.Grid, TestGame.Nouns).NounsAt(bush));
    }

    [Fact]
    public void ASnapshotAnswersLikeTheLiveWorldWithinItsRadius()
    {
        var session = TestGame.NewSession();
        var live = TestGame.FactsOf(session);
        var viewpoint = TestGame.ViewpointOf(session);
        var snapshot = WorldFacts.Snapshot(session.Scene.Grid, TestGame.Nouns);

        foreach (var noun in new[] { "árbol", "hierba", "tierra", "piedra", "arena" })
        {
            Assert.Equal(live.NearestOfNoun(viewpoint, noun), snapshot.NearestOfNoun(viewpoint, noun));
        }

        Assert.Empty(snapshot.NounsAt(new GuideCell(-1, -1, -1)));
    }
}
