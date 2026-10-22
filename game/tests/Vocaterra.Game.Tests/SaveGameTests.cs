using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The save and the load of S10: every part survives a round trip, a save written twice is byte-identical, a save that is saved again after a load is
/// the same except for the play session, a save of another seed or bundle is refused, a load runs the next session and so lifts a phrase's spacing
/// gap, a loaded game continues as the original would, and a session that is not at rest is not saved.
/// </summary>
public sealed class SaveGameTests
{
    private const long T0 = 1_000_000;

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static string SaveOf(VoxelSession session, ulong seed = TestGame.Seed) => SaveGame.Save(session, Pristine(seed));

    private static VoxelSession LoadOf(string text, CreditRules? credit = null, bool bump = true) =>
        SaveGame.Load(text, TestGame.Bundle, Pristine, TestGame.Nouns, credit: credit ?? CreditRules.FullTyped, bumpSession: bump);

    private static PhraseUse Use(string phrase, PhraseUseKind kind, long time) =>
        new(time * 60, time, phrase, kind, "shopkeeper", "stall-need", "quiero agua, por favor", null, InputKind.Spoken, true, SupportLevel.None, null);

    [Fact]
    public void EveryPartOfTheGameSurvivesASaveAndALoad()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        session.Say("¿dónde está el río?");
        session.OpenTalk("shopkeeper");
        session.Say("quiero agua, por favor");
        session.EndTalk();
        session.SkipTime(90);

        var text = SaveOf(session);
        var loaded = LoadOf(text, bump: false);

        Assert.Equal(text, SaveOf(loaded));
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(session.StoredTime, loaded.StoredTime);
        Assert.Equal(session.SkippedSeconds, loaded.SkippedSeconds);
        Assert.Equal(session.PlaySession, loaded.PlaySession);
        Assert.Equal(session.Objective.Summary, loaded.Objective.Summary);
        Assert.Equal(session.Simulation.Inventory.Slots, loaded.Simulation.Inventory.Slots);
        Assert.Equal(session.Simulation.SelectedSlot, loaded.Simulation.SelectedSlot);
        Assert.Equal(session.Simulation.Player.CenterX, loaded.Simulation.Player.CenterX);
        Assert.Equal(session.Simulation.Player.Yaw, loaded.Simulation.Player.Yaw);
        Assert.Equal(session.Pointer, loaded.Pointer);
        Assert.Equal(session.Words.Records.Values.OrderBy(static r => r.Lemma, StringComparer.Ordinal), loaded.Words.Records.Values.OrderBy(static r => r.Lemma, StringComparer.Ordinal));
        Assert.Equal(session.PhraseProgress.Records, loaded.PhraseProgress.Records);
        Assert.Equal(session.CardedLemmas(), loaded.CardedLemmas());
        Assert.Equal(session.RiverPointedOut, loaded.RiverPointedOut);
        Assert.Equal(session.NextTurnId, loaded.NextTurnId);
        foreach (var npc in NpcService.AgentIds)
        {
            Assert.Equal(session.Conversation.ContextOf(npc).AgentState, loaded.Conversation.ContextOf(npc).AgentState);
            Assert.Equal(session.Conversation.ContextOf(npc).Learner, loaded.Conversation.ContextOf(npc).Learner);
            Assert.Equal(session.Conversation.ContextOf(npc).HostStateHash, loaded.Conversation.ContextOf(npc).HostStateHash);
        }

        Assert.Equal(session.Conversation.GuideState, loaded.Conversation.GuideState);
        Assert.Equal(VoxelGridDiff.Capture(session.Scene.Grid, Pristine().Grid).Count, VoxelGridDiff.Capture(loaded.Scene.Grid, Pristine().Grid).Count);
    }

    [Fact]
    public void ASaveWrittenTwiceFromTheSameStateIsByteIdentical()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        session.Say("¿dónde está el río?");

        var first = SaveOf(session);
        var second = SaveOf(session);

        Assert.Equal(first, second);
        Assert.Equal(SaveGame.Serialize(SaveGame.Capture(session, Pristine())), first);
    }

    // A save as S19 wrote it, before S20 gave the learner the book: no book in the slots and no "bookGranted" field (S22).
    private static string SaveBeforeTheBook()
    {
        var session = TestGame.NewSession();
        Assert.True(session.Simulation.Inventory.Remove(ItemId.Book, 1));
        var text = SaveOf(session);
        var old = text.Replace(",\"bookGranted\":true", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(text, old); // the field was there and is now gone
        Assert.DoesNotContain("bookGranted", old, StringComparison.Ordinal);
        return old;
    }

    [Fact]
    public void ASaveFromBeforeTheBookGrantsTheBookOnceWhenItLoads()
    {
        var old = SaveBeforeTheBook();
        Assert.False(SaveGame.Parse(old).BookGranted);

        var first = LoadOf(old);

        Assert.Equal(1, first.Simulation.Inventory.Count(ItemId.Book)); // given on the load
        var second = LoadOf(SaveOf(first));
        Assert.Equal(1, second.Simulation.Inventory.Count(ItemId.Book)); // the next save says it was granted: still the one book, not two
        Assert.True(SaveGame.Parse(SaveOf(first)).BookGranted);

        // Once: a learner who has put the book away is not given another by a later load.
        Assert.True(first.Simulation.Inventory.Remove(ItemId.Book, 1));
        var third = LoadOf(SaveOf(first));
        Assert.Equal(0, third.Simulation.Inventory.Count(ItemId.Book));
    }

    [Fact]
    public void ASaveWithTheBookDoesNotGetASecondOne()
    {
        var text = SaveOf(TestGame.NewSession());
        var withoutField = text.Replace(",\"bookGranted\":true", string.Empty, StringComparison.Ordinal);

        var loaded = LoadOf(withoutField); // an old save that already has the book in its slots

        Assert.Equal(1, loaded.Simulation.Inventory.Count(ItemId.Book));
    }

    [Fact]
    public void ASaveAfterALoadIsTheSameExceptForTheSessionNumber()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        var first = SaveOf(session);

        var loaded = LoadOf(first);
        var second = SaveOf(loaded);

        Assert.Equal(2, loaded.PlaySession);
        Assert.Equal(SaveGame.Serialize(SaveGame.Parse(first) with { Session = 2 }), second);
    }

    [Fact]
    public void ASaveOfAnotherSeedOrBundleIsRefusedWithAClearMessage()
    {
        var text = SaveOf(TestGame.NewSession());
        var document = SaveGame.Parse(text);

        var seed = Assert.Throws<SaveException>(() => SaveGame.Load(text, TestGame.Bundle, Pristine, TestGame.Nouns, requestedSeed: 7));
        Assert.Contains("world seed 42", seed.Message, StringComparison.Ordinal);
        Assert.Contains("not 7", seed.Message, StringComparison.Ordinal);

        var bundle = Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(document with { BundleHash = new string('0', 64) }), TestGame.Bundle, Pristine, TestGame.Nouns));
        Assert.Contains("content bundle", bundle.Message, StringComparison.Ordinal);

        var version = Assert.Throws<SaveException>(() => SaveGame.Parse(SaveGame.Serialize(document with { Version = 4 })));
        Assert.Contains("version 4", version.Message, StringComparison.Ordinal);
        Assert.Contains("versions 1 to 3", version.Message, StringComparison.Ordinal);

        var format = Assert.Throws<SaveException>(() => SaveGame.Parse("{\"format\":\"something-else\",\"version\":1}"));
        Assert.Contains("not a vocaterra-save", format.Message, StringComparison.Ordinal);

        Assert.Throws<SaveException>(() => SaveGame.Parse("not json"));
    }

    [Fact]
    public void ASaveThatDoesNotRestoreToItsDigestIsRefused()
    {
        var document = SaveGame.Parse(SaveOf(TestGame.NewSession()));

        var damaged = Assert.Throws<SaveException>(() => SaveGame.Restore(document with { StateDigest = new string('0', 64) }, TestGame.Bundle, Pristine(), TestGame.Nouns));
        Assert.Contains("digest", damaged.Message, StringComparison.Ordinal);

        var late = Assert.Throws<SaveException>(() => SaveGame.Restore(document with { StoredTime = document.StoredTime + 1 }, TestGame.Bundle, Pristine(), TestGame.Nouns));
        Assert.Contains("stored time", late.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALoadRunsTheNextSessionAndTheSpacingGapOfAPhraseIsLiftedByIt()
    {
        var credit = CreditRules.Default;
        var session = TestGame.NewSession(TestGame.Seed, credit);
        _ = session.PhraseProgress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), credit);
        _ = session.PhraseProgress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + 60), credit); // a minute later: inside the ten minutes
        Assert.Equal(1, session.PhraseProgress.RecordOf("pedir-agua").CreditedUses);

        var text = SaveOf(session);

        var sameSession = LoadOf(text, credit, bump: false);
        _ = sameSession.PhraseProgress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + 60), credit);
        Assert.Equal(1, sameSession.PhraseProgress.RecordOf("pedir-agua").CreditedUses); // the gap still stands in the same session

        var nextSession = LoadOf(text, credit);
        Assert.Equal(2, nextSession.PlaySession);
        _ = nextSession.PhraseProgress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + 60), credit);
        Assert.Equal(2, nextSession.PhraseProgress.RecordOf("pedir-agua").CreditedUses); // a new session lifts it: creditable
        Assert.Equal(2, nextSession.PhraseProgress.RecordOf("pedir-agua").LastCreditedSession);
    }

    [Fact]
    public void ALoadedGameContinuesExactlyAsTheOriginalWouldInTheNextSession()
    {
        var (original, _, _) = CampObjectiveTests.PlayCamp();
        original.Say("¿dónde está el río?");
        var text = SaveOf(original);
        var loaded = LoadOf(text);

        // The original is put in the next session at the same moment the load puts the other one there; then both run the same ticks and turns.
        original.PlaySession = loaded.PlaySession;
        Continue(original);
        Continue(loaded);

        Assert.Equal(original.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(original.Objective.Summary, loaded.Objective.Summary);
        Assert.Equal(original.Simulation.Inventory.Slots, loaded.Simulation.Inventory.Slots);
        Assert.Equal(SaveOf(original), SaveOf(loaded));
    }

    [Fact]
    public void ASessionThatIsNotAtRestIsNotSaved()
    {
        var session = TestGame.NewSession();
        session.OpenTalk(NpcService.ShopkeeperAgent, teachKeyWords: true);

        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(session, Pristine()));
        session.FinishKeyWords();
        Assert.Throws<InvalidOperationException>(() => SaveGame.Capture(session, Pristine())); // the opening waits
        _ = session.TakeOpening();
        _ = SaveGame.Capture(session, Pristine());
    }

    [Fact]
    public void TheKeyWordsTaughtFlagAndTheCardedNounsAreSaved()
    {
        var session = TestGame.NewSession();
        session.OpenTalk(NpcService.ShopkeeperAgent, teachKeyWords: true);
        session.FinishKeyWords();
        _ = session.TakeOpening();
        session.EndTalk();

        var loaded = LoadOf(SaveOf(session));

        Assert.True(loaded.KeyWordsTaught);
        Assert.Contains("agua", loaded.CardedLemmas());
        Assert.Contains("pan", loaded.CardedLemmas());
    }

    [Fact]
    public void TheSceneDiffHoldsOnlyTheChunksTheGameEdited()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        var document = SaveGame.Capture(session, Pristine());
        var differing = VoxelGridDiff.Capture(session.Scene.Grid, Pristine().Grid);

        Assert.Equal(differing.Select(static edit => edit.Chunk), document.Chunks.Select(static edit => edit.Chunk));
        Assert.NotEmpty(document.Chunks);
        Assert.True(document.Chunks.Count < session.Scene.Grid.Chunks.Count / 4, $"{document.Chunks.Count} of {session.Scene.Grid.Chunks.Count} chunks");
    }

    [Fact]
    public void TheRestoredWorldMatchesTheSavedWorldBlockForBlock()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        var loaded = LoadOf(SaveOf(session));

        Assert.Equal(session.Scene.Grid.ComputeDigestHex(), loaded.Scene.Grid.ComputeDigestHex());
        var fire = TestGame.NearestBlock(session, BlockId.FireRing);
        Assert.Equal(BlockId.FireRing, loaded.Scene.Grid.GetBlock(fire.X, fire.Y, fire.Z));
        Assert.Equal(session.Scene.Grid.GetBlock(fire.X, fire.Y + 1, fire.Z), loaded.Scene.Grid.GetBlock(fire.X, fire.Y + 1, fire.Z));
    }

    [Fact]
    public void ObjectiveAndWordRestoresRefuseWhatIsNotInTheGame()
    {
        var objective = Objective.Camp();

        Assert.Throws<ArgumentException>(() => objective.Restore([("nope", 1)]));
        Assert.Throws<ArgumentException>(() => objective.Restore([("wood", 1), ("wood", 2)]));
        objective.Restore([("wood", 5), ("water", 1)]);
        Assert.Equal("Campamento: madera 5/5 · frutas 0/3 · antorcha 0/1 · agua 1/1", objective.Summary);

        var words = new WordProgress(TestGame.Nouns);
        Assert.Throws<ArgumentException>(() => words.Restore([new WordRecord("gato-no")]));
        Assert.Throws<ArgumentException>(() => words.Restore([new WordRecord("agua"), new WordRecord("agua")]));
    }

    // Walks, turns and talks for a while: the same moves for both games of the continuation test.
    private static void Continue(VoxelSession session)
    {
        var camp = session.Scene.Point("camp.guide");
        VoxelWalker.WalkTo(session, cell => cell.X >= camp.X + 5 && cell.Z <= camp.Z - 3, 3000);
        for (var i = 0; i < 180; i++)
        {
            session.Step([VoxelCommand.Look(Angles.FromDegrees(200), -4), VoxelCommand.Move(0, 1)]);
        }

        session.Say("¿qué es esto?", session.AimedCell);
        session.SkipTime(120);
        session.Say("¿dónde está el río?");
        for (var i = 0; i < 60; i++)
        {
            session.Step([VoxelCommand.Move(1, 0)]);
        }
    }
}
