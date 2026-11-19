using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>
/// X01, saves survive content updates: a save made from another content bundle loads into this one (the progress kept where its ids exist, the rest dropped and
/// reported), strict mode still refuses, a save of this very bundle loads exactly as before, and the world's digest is
/// reported rather than required. "Another bundle" is a document with another bundle hash and ids the bundle does not have (or lacks ones it has).
/// </summary>
public sealed class SaveMigrationTests
{
    private const string OtherBundle = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static SaveDocument CampDocument()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        session.Say("¿dónde está el río?");
        session.OpenTalk("shopkeeper");
        session.Say("quiero agua, por favor");
        session.EndTalk();
        return SaveGame.Capture(session);
    }

    private static VoxelSession Load(SaveDocument document, bool migrate, Func<ulong, Scene>? pristine = null, bool bump = false) =>
        SaveGame.Load(SaveGame.Serialize(document), TestGame.Data, pristine ?? Pristine, credit: CreditRules.FullTyped, bumpSession: bump, migrate: migrate);

    private static SaveDocument FromOtherBundle(SaveDocument document) => document with { BundleHash = OtherBundle };

    // The camp checklist's steps of a save, the objective the HUD shows: a save keys every counted step, and since village-plan V10 the fisher's order has one too.
    private static ObjectiveSave[] CampSteps(SaveDocument document) => [.. document.Objective.Where(static o => o.Key.StartsWith("camp.", StringComparison.Ordinal))];

    [Fact]
    public void ASaveFromAnotherBundleLoadsKeepsWhatExistsAndDropsWhatDoesNot()
    {
        var document = CampDocument();
        var firstPhrase = document.Phrases[0];
        var missing = document.Phrases[1];
        var older = FromOtherBundle(document) with
        {
            Phrases = [.. document.Phrases.Skip(2), firstPhrase with { Id = "gone-phrase" }, .. new[] { document.Phrases[0] }],
            Words = [.. document.Words, document.Words[0] with { Lemma = "gone-noun" }],
            Carded = [.. document.Carded, "gone-noun"],
            Objective = [.. document.Objective, new ObjectiveSave("gone-step", 2)],
            Tips = (document.Tips ?? new TipsSave([], [], null, 0)) with { Shown = ["gone-tip"] },
            Npcs = [.. document.Npcs.Select(static n => n with { Learner = n.Learner with { Skills = [.. n.Learner.Skills, new SkillSave("phrase.gone-phrase", 0.5, 1, 0), new SkillSave("word.gone-noun", 0.5, 1, 0)] } })],
            DeferredSkills = [new DeferredSkillSave("shopkeeper", "phrase.gone-phrase")],
        };

        var loaded = Load(older, migrate: true);

        // What exists is kept as it was.
        Assert.Equal(document.Words.Count, loaded.Words.Records.Count);
        Assert.Equal(CampSteps(document).Length, loaded.Objective.Steps.Count);
        Assert.Equal(CampSteps(document).Select(static o => o.Count), loaded.Objective.Steps.Select(static s => s.Count));
        Assert.Equal(document.Inventory, loaded.Simulation.Inventory.Slots.Select(static s => SlotSave.Of(s, TestItems.Catalogue)));
        Assert.Equal(document.Learner!.History.Count, loaded.Learner.History.Count);
        Assert.Equal(document.Learner.Words.Count, loaded.Learner.Rows.Count);
        Assert.Equal(document.Session, loaded.Day); // bump off here: the session number is kept
        Assert.Equal(document.StoredTime, loaded.StoredTime.UnixSeconds);
        Assert.Equal(document.Phrases[2], PhraseSaveOf(loaded.PhraseProgress.RecordOf(document.Phrases[2].Id)));

        // A row the saved document did not hold starts empty, as in a fresh game.
        Assert.Equal(TestGame.NewSession().PhraseProgress.RecordOf(missing.Id), loaded.PhraseProgress.RecordOf(missing.Id));

        // What does not exist is dropped, and the report says so.
        var report = loaded.Migration;
        Assert.NotNull(report);
        Assert.Equal(OtherBundle, report!.SavedBundle);
        Assert.Equal(TestGame.Bundle.ContentHash, report.CurrentBundle);
        Assert.Contains("gone-phrase", report.Find("phrases")!.Dropped);
        Assert.Contains("gone-noun", report.Find("words")!.Dropped);
        Assert.Contains("gone-noun", report.Find("carded nouns")!.Dropped);
        Assert.Contains("gone-step", report.Find("objective steps")!.Dropped);
        Assert.Contains("gone-tip", report.Find("tips")!.Dropped);
        Assert.Contains(report.Find("learner-model skills")!.Dropped, static d => d.EndsWith("phrase.gone-phrase", StringComparison.Ordinal));
        Assert.Contains("phrase.gone-phrase", report.Find("deferred skills")!.Dropped);
        Assert.Equal(document.Phrases.Count - 2, report.Find("phrases")!.Kept - 1); // all rows but the one that was left out, and the invented one is not among them
        Assert.Contains(report.Lines(), static l => l.Contains("dropped phrases: gone-phrase", StringComparison.Ordinal));
        Assert.True(report.DroppedCount >= 6);
        Assert.Equal("The game was updated. Your progress was kept; " + report.DroppedCount + " old records no longer in the game were dropped.", report.Toast);
        Assert.DoesNotContain(loaded.Words.Records.Keys, static k => k == "gone-noun");
    }

    private static PhraseSave PhraseSaveOf(PhraseRecord r) => new(
        r.Id, r.Level, r.CardShown, r.Supported, r.Unaided, r.ChoiceOnly, r.DontKnow, r.Transfer, r.FailForward, r.Slips, r.Heard, r.Answered, r.StepUnits,
        r.CreditedUses, r.LastCreditedAt, r.LastCreditedSession, r.LastUseAt, r.LastUseSession);

    [Fact]
    public void StrictModeStillRefusesASaveOfAnotherBundleWithAClearMessage()
    {
        var older = FromOtherBundle(CampDocument());

        var error = Assert.Throws<SaveException>(() => Load(older, migrate: false));

        Assert.Contains("content bundle 0123456789ab", error.Message, StringComparison.Ordinal);
        Assert.Contains("strict save mode", error.Message, StringComparison.Ordinal);
        Assert.Contains("--strict-save", error.Message, StringComparison.Ordinal);
        Assert.Throws<SaveException>(() => SaveGame.Check(older, TestGame.Bundle));
        SaveGame.Check(older, TestGame.Bundle, migrate: true); // let through
    }

    [Fact]
    public void ASaveOfTheSameBundleLoadsExactlyAsBeforeWithOrWithoutMigration()
    {
        var document = CampDocument();
        var text = SaveGame.Serialize(document);

        var strict = SaveGame.Load(text, TestGame.Data, Pristine, bumpSession: false);
        var migrating = SaveGame.Load(text, TestGame.Data, Pristine, bumpSession: false, migrate: true);

        Assert.Null(strict.Migration);
        Assert.Null(migrating.Migration);
        Assert.Equal(text, SaveGame.Save(strict));
        Assert.Equal(text, SaveGame.Save(migrating));
        Assert.Equal(document.StateDigest, migrating.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void ASaveOfTheSameBundleWithAWrongDigestIsStillRefusedWhenMigrationIsOn()
    {
        var document = CampDocument() with { StateDigest = new string('0', 64) };

        var error = Assert.Throws<SaveException>(() => Load(document, migrate: true));

        Assert.Contains("digest", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorldsEditsComeBackAndTheDigestMatchesWhenTheGenerationIsUnchanged()
    {
        var document = CampDocument();

        var loaded = Load(FromOtherBundle(document), migrate: true);

        var report = loaded.Migration!;
        Assert.True(report.WorldDigestMatched);
        Assert.True(report.Find(SaveMigration.WorldArea)!.Kept > 0);
        Assert.Empty(report.Find(SaveMigration.WorldArea)!.Dropped);
        Assert.Equal(document.StateDigest, loaded.Simulation.ComputeStateDigest());
        Assert.Equal(SaveGame.Restore(document, TestGame.Data, Pristine(), bumpSession: false).Scene.Grid.ComputeDigestHex(), loaded.Scene.Grid.ComputeDigestHex());
    }

    [Fact]
    public void ADifferentlyGeneratedWorldIsNotFalselyRefusedAndEditsAreAppliedByPosition()
    {
        var document = CampDocument();
        var strictPristine = Pristine();
        var edited = document.Chunks[0];
        var cell = FindAirCell(strictPristine.Grid, edited);
        var untouched = strictPristine.Grid.Chunks.First(c => document.Chunks.All(e => (e.X, e.Y, e.Z) != (c.Coord.X, c.Coord.Y, c.Coord.Z))).Coord;
        var other = new BlockPos(untouched.X << VoxelChunk.Shift, untouched.Y << VoxelChunk.Shift, untouched.Z << VoxelChunk.Shift);
        var otherBlock = strictPristine.Grid.GetBlock(other.X, other.Y, other.Z) == BlockId.Stone ? BlockId.Dirt : BlockId.Stone;
        var changedWorld = (Func<ulong, Scene>)(seed =>
        {
            var scene = Pristine(seed);
            VoxelGridDiff.SetCell(scene.Grid, cell.X, cell.Y, cell.Z, BlockId.Stone); // the new generation has a stone where the old world had air, in a chunk the player edited
            VoxelGridDiff.SetCell(scene.Grid, other.X, other.Y, other.Z, otherBlock); // and a changed block in a chunk the player never touched
            return scene;
        });

        var refused = Assert.Throws<SaveException>(() => Load(document, migrate: false, changedWorld));
        Assert.Contains("digest", refused.Message, StringComparison.Ordinal); // same bundle hash: a world that does not restore is damaged, as before

        var loaded = Load(FromOtherBundle(document), migrate: true, changedWorld);

        var report = loaded.Migration!;
        Assert.False(report.WorldDigestMatched);
        Assert.True(report.Find(SaveMigration.WorldArea)!.Kept > 0); // the gathers and placed blocks elsewhere came back
        Assert.NotEmpty(report.Find(SaveMigration.WorldArea)!.Dropped); // the old air over the new stone is the old generation, not the player's edit
        Assert.Equal(otherBlock, loaded.Scene.Grid.GetBlock(other.X, other.Y, other.Z));
        Assert.Equal(BlockId.Stone, loaded.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z)); // the new world stays where the old world is not the player's
        Assert.Contains(report.Notes, static n => n.Contains("digest differs", StringComparison.Ordinal));
        Assert.Equal(CampSteps(document).Select(static o => o.Count), loaded.Objective.Steps.Select(static s => s.Count));
    }

    [Fact]
    public void APlayerInsideABlockOfTheNewWorldStartsWhereANewGameStarts()
    {
        var document = CampDocument();
        var feet = new BlockPos(
            document.Player.MinX / VoxelPhysics.UnitsPerBlock,
            document.Player.MinY / VoxelPhysics.UnitsPerBlock,
            document.Player.MinZ / VoxelPhysics.UnitsPerBlock);
        var changedWorld = (Func<ulong, Scene>)(seed =>
        {
            var scene = Pristine(seed);
            VoxelGridDiff.SetCell(scene.Grid, feet.X, feet.Y, feet.Z, BlockId.Stone);
            VoxelGridDiff.SetCell(scene.Grid, feet.X, feet.Y + 1, feet.Z, BlockId.Stone);
            return scene;
        });

        var loaded = Load(FromOtherBundle(document), migrate: true, changedWorld);

        Assert.Contains(loaded.Migration!.Notes, static n => n.Contains("starts where a new game starts", StringComparison.Ordinal));
        Assert.Equal(Pristine().Spawn.Feet.X, loaded.Simulation.Player.FeetBlock.X);
    }

    [Fact]
    public void ALessonKeepsItsCompletedStateAndItsDoneStepsThatStillExist()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        var document = SaveGame.Capture(session);
        var book = session.Lessons.Book.Lessons;
        var inProgress = book[0];
        var completed = book[1];
        var ids = inProgress.Steps.Select(static s => s.Id).ToArray();
        var doneIds = completed.Steps.Select(static s => s.Id).ToArray();
        var lessons = document.Lessons!;

        LessonProgressSave Edit(LessonProgressSave p) =>
            string.Equals(p.Id, inProgress.Id, StringComparison.Ordinal)
                ? p with
                {
                    Status = LessonStatus.InProgress,
                    Step = 3,
                    Attempts = 2,
                    Outcomes = [new(ids[0], StepOutcome.Done), new("a-step-since-removed", StepOutcome.Done), new(ids[1], StepOutcome.Skipped), new(ids[2], StepOutcome.Done)],
                }
                : string.Equals(p.Id, completed.Id, StringComparison.Ordinal)
                    ? p with
                    {
                        Status = LessonStatus.Completed,
                        Step = doneIds.Length - 1,
                        EverCompleted = true,
                        CompletedSession = 1,
                        Outcomes = [.. doneIds.Select(static id => new StepOutcomeSave(id, StepOutcome.Done)), new StepOutcomeSave("another-removed-step", StepOutcome.Done)],
                    }
                    : p;

        var older = FromOtherBundle(document) with
        {
            Lessons = new LessonsSave([.. lessons.Lessons.Select(Edit), new LessonProgressSave("a-lesson-since-removed", LessonStatus.Completed, 0, [], 0, false)], inProgress.Id),
        };

        var loaded = Load(older, migrate: true);

        var kept = loaded.Lessons.Record.Single(r => string.Equals(r.LessonId, inProgress.Id, StringComparison.Ordinal));
        Assert.Equal(LessonStatus.InProgress, kept.Status);
        Assert.Equal(inProgress.Id, loaded.Lessons.Active?.Id);
        Assert.Equal(ids[3], loaded.Lessons.Current!.Id); // goes on at the step after the three finished ones that still exist
        Assert.Equal(new[] { ids[0], ids[1], ids[2] }, kept.Units.Select(static u => u.StepId));
        Assert.Equal(StepOutcome.Skipped, kept.Units[1].Outcome);
        var done = loaded.Lessons.Record.Single(r => string.Equals(r.LessonId, completed.Id, StringComparison.Ordinal));
        Assert.Equal(LessonStatus.Completed, done.Status);
        Assert.Equal(doneIds.Length, done.Units.Count);
        var report = loaded.Migration!;
        Assert.Contains("a-lesson-since-removed", report.Find("lessons")!.Dropped);
        Assert.Contains(report.Find("lesson steps")!.Dropped, d => d.EndsWith("a-step-since-removed", StringComparison.Ordinal));
        Assert.Contains(report.Find("lesson steps")!.Dropped, d => d.EndsWith("another-removed-step", StringComparison.Ordinal));
        Assert.Equal(loaded.Lessons.Record.Count, report.Find("lessons")!.Kept);
    }

    [Fact]
    public void AStepInsertedBeforeAFinishedOneIsDoneAndTheFinishedOneAfterItIsDoneAgain()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        var document = SaveGame.Capture(session);
        var lesson = session.Lessons.Book.Lessons[0];
        var ids = lesson.Steps.Select(static s => s.Id).ToArray();
        // The save finished steps 0 and 2 but not 1 (a step that did not exist then): the lesson goes on at step 1 and step 2 is done again.
        var older = FromOtherBundle(document) with
        {
            Lessons = new LessonsSave(
                [.. document.Lessons!.Lessons.Select(p => string.Equals(p.Id, lesson.Id, StringComparison.Ordinal)
                    ? p with { Status = LessonStatus.InProgress, Step = 2, Outcomes = [new(ids[0], StepOutcome.Done), new(ids[2], StepOutcome.Done)] }
                    : p)],
                lesson.Id),
        };

        var loaded = Load(older, migrate: true);

        Assert.Equal(ids[1], loaded.Lessons.Current!.Id);
        Assert.Equal(new[] { ids[0] }, loaded.Lessons.Record.Single(r => string.Equals(r.LessonId, lesson.Id, StringComparison.Ordinal)).Units.Select(static u => u.StepId));
        Assert.Contains(loaded.Migration!.Find("lesson steps")!.Dropped, d => d.Contains(ids[2], StringComparison.Ordinal) && d.Contains("done again", StringComparison.Ordinal));
    }

    [Fact]
    public void ALessonReplayOrActiveMarkOfALessonThatIsGoneIsDropped()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        var document = SaveGame.Capture(session);
        var older = FromOtherBundle(document) with { Lessons = new LessonsSave(document.Lessons!.Lessons, null, "a-lesson-since-removed") };

        var loaded = Load(older, migrate: true);

        Assert.Null(loaded.Lessons.ReplayHud);
        Assert.Contains(loaded.Migration!.Notes, static n => n.Contains("a-lesson-since-removed", StringComparison.Ordinal));
    }

    [Fact]
    public void ASaveOfThisVersionLoadsAcrossBundles()
    {
        var document = CampDocument();
        var other = FromOtherBundle(document) with { Words = [.. document.Words, document.Words[0] with { Lemma = "gone-noun" }] };

        var loaded = Load(other, migrate: true);

        Assert.NotNull(loaded.Migration);
        Assert.Contains("gone-noun", loaded.Migration!.Find("words")!.Dropped);
        Assert.Equal(CampSteps(document).Select(static o => o.Count), loaded.Objective.Steps.Select(static s => s.Count));
        Assert.Equal(document.Learner!.History.Count, loaded.Learner.History.Count);
        Assert.True(loaded.Simulation.Inventory.Count(ItemId.Book) >= 1);
        Assert.Throws<SaveException>(() => Load(other, migrate: false));
    }

    [Fact]
    public void TheFixtureLoadsStrictAndAcrossBundles()
    {
        var text = SaveFixture.Read("save-v11.json");

        var strict = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);
        Assert.Null(strict.Migration);

        var other = SaveGame.Serialize(SaveGame.Parse(text) with { BundleHash = OtherBundle });
        var loaded = SaveGame.Load(other, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, migrate: true);
        Assert.NotNull(loaded.Migration);
        Assert.True(loaded.Migration!.WorldDigestMatched);
        Assert.Equal(strict.Learner.History.Count, loaded.Learner.History.Count);
        Assert.Equal(1, loaded.Day); // the day as saved (V25)
    }

    [Fact]
    public void AMigratedGameSavesAsAnOrdinarySaveOfThisBundle()
    {
        var loaded = Load(FromOtherBundle(CampDocument()), migrate: true);

        var again = SaveGame.Parse(SaveGame.Save(loaded));

        Assert.Equal(TestGame.Bundle.ContentHash, again.BundleHash);
        Assert.Equal(SaveGame.CurrentVersion, again.Version);
        var reloaded = SaveGame.Load(SaveGame.Serialize(again), TestGame.Data, Pristine, bumpSession: false);
        Assert.Null(reloaded.Migration);
    }

    [Fact]
    public void AReportWithNothingDroppedSaysSoPlainly()
    {
        var loaded = Load(FromOtherBundle(CampDocument()), migrate: true);

        var report = loaded.Migration!;

        Assert.Equal(0, report.DroppedCount);
        Assert.Equal("The game was updated. Your progress was kept.", report.Toast);
        Assert.StartsWith("migrate: the save was made from bundle 0123456789ab", report.Lines()[0], StringComparison.Ordinal);
    }

    // A cell of air in an edited chunk that is air in the saved world as well (so the saved chunk differs from the new world there only if the new world changes it).
    private static BlockPos FindAirCell(VoxelGrid grid, ChunkEdit edit)
    {
        var origin = new VoxelChunkCoord(edit.X, edit.Y, edit.Z);
        for (var position = VoxelChunk.Volume - 1; position >= 0; position--)
        {
            var x = (origin.X << VoxelChunk.Shift) + (position & (VoxelChunk.Size - 1));
            var z = (origin.Z << VoxelChunk.Shift) + ((position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1));
            var y = (origin.Y << VoxelChunk.Shift) + (position >> (2 * VoxelChunk.Shift));
            if (grid.GetBlock(x, y, z) == BlockId.Air && grid.GetBlock(x, y - 1, z) == BlockId.Air)
            {
                return new BlockPos(x, y, z);
            }
        }

        throw new InvalidOperationException("no air in the chunk");
    }
}
