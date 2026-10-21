using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The turn worker (P5): turns are submitted with their inputs fixed, evaluated one at a time on a dedicated thread and applied in order on the
/// caller's thread. These tests use a typed-only worker, so no model is needed.
/// </summary>
public sealed class TurnWorkerTests
{
    private static GameSession StartedSession(CreditRules? credit = null)
    {
        var session = TestGame.NewSession(credit: credit ?? CreditRules.Default);
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        return session;
    }

    private static List<AppliedTurn> Drain(GameSession session, int count)
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
        var cursor = TestGame.BruteForceNearest(sync.Simulation.World, sync.PlayerTile, TileId.Trunk);
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
        var cursor = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Trunk);
        var digest = session.Simulation.ComputeStateDigest();

        var request = session.SubmitTyped("es un árbol", cursor);
        session.SkipTime(7200); // two hours pass before the answer is applied
        session.Simulation.Step([PlayerCommand.SelectSlot(3)]);
        var applied = Drain(session, 1).Single();

        Assert.Equal(GameSession.DefaultStartedAt, request.Now.UnixSeconds);
        Assert.Equal(digest, request.HostStateHash);
        Assert.NotNull(applied.Turn);
        Assert.Equal(GameSession.DefaultStartedAt, session.Words.RecordOf("árbol").LastCreditedAt);
        Assert.True(session.StoredTime.UnixSeconds > GameSession.DefaultStartedAt + 7000);
    }

    [Fact]
    public void ATypedUseEarnsHalfCreditAndTheLaunchSettingGivesItFull()
    {
        using var half = StartedSession(CreditRules.Default);
        using var full = StartedSession(CreditRules.FullTyped);
        var cursor = TestGame.BruteForceNearest(half.Simulation.World, half.PlayerTile, TileId.Trunk);

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
        var cursor = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Trunk);
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
        var player = session.PlayerTile;
        var before = WorldFacts.Snapshot(session.Simulation.World, player);

        // Mine a grass or dirt tile near the player with the pickaxe (slot 2 on the hotbar).
        TilePoint? target = null;
        for (var dy = 0; dy <= 3 && target is null; dy++)
        {
            for (var dx = -3; dx <= 3 && target is null; dx++)
            {
                var tile = new TilePoint(player.X + dx, player.Y + dy);
                if (session.Simulation.World.GetTile(tile.X, tile.Y) is TileId.Grass or TileId.Dirt)
                {
                    target = tile;
                }
            }
        }

        Assert.NotNull(target);
        var kind = session.Simulation.World.GetTile(target.Value.X, target.Value.Y);
        session.Simulation.Step([PlayerCommand.SelectSlot(1)]);
        for (var i = 0; i < 2000 && session.Simulation.World.GetTile(target.Value.X, target.Value.Y) == kind; i++)
        {
            session.Simulation.Step([PlayerCommand.Mine(target.Value.X, target.Value.Y)]);
        }

        Assert.Equal(TileId.Air, session.Simulation.World.GetTile(target.Value.X, target.Value.Y));
        Assert.Equal(kind, before.TileAt(target.Value)); // the copy still has the tile
        Assert.Equal(TileId.Air, WorldFacts.Snapshot(session.Simulation.World, player).TileAt(target.Value));
    }

    [Fact]
    public void ASnapshotAnswersLikeTheLiveWorldWithinItsRadius()
    {
        var session = TestGame.NewSession();
        var live = session.Facts;
        var player = session.PlayerTile;
        var snapshot = WorldFacts.Snapshot(session.Simulation.World, player);

        foreach (var noun in new[] { "árbol", "hierba", "tierra", "piedra", "arena" })
        {
            Assert.Equal(live.NearestOfNoun(player, noun), snapshot.NearestOfNoun(player, noun));
        }

        Assert.Equal(TileId.Air, snapshot.TileAt(new TilePoint(-1, -1)));
    }
}
