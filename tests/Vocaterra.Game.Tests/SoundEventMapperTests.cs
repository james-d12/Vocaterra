using Vocaterra.Game.Core;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Sound;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;
using SoundEventMapper = Vocaterra.Game.Core.Sound.SoundEventMapper;

namespace Vocaterra.Game.Tests;

/// <summary>The event-to-cue mapping of S24: what sounds for each thing that happens, and that nothing the learner does ever sounds punishing.</summary>
public sealed class SoundEventMapperTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());

    private static readonly Lazy<LessonBook> Book = new(() =>
    {
        var nouns = NounCatalog.Load(Bundle.Value);
        var sheet = PhraseSheet.Load(Bundle.Value);
        return LessonBook.Load(Bundle.Value, sheet, SpeechLines.Enumerate(Bundle.Value, nouns, sheet));
    });

    private static readonly SoundEventMapper Mapper = new(Book.Value);

    private static SimulationEvent Sim(SimulationEventKind kind, int detail = 0) => new(kind, 5, 1, 2, 3, detail, 0);

    private static GuideTurn Turn(string intent = "request_item", bool communicated = true, bool uncertain = false) => new(
        "x", intent, communicated, 1.0, [], null, [], null, "state", [], null, null, null, "hash", SlotMap.Empty, Uncertain: uncertain);

    [Theory]
    [InlineData(BlockId.Trunk, SoundMaterial.Wood)]
    [InlineData(BlockId.SpruceTrunk, SoundMaterial.Wood)]
    [InlineData(BlockId.Planks, SoundMaterial.Wood)]
    [InlineData(BlockId.Stone, SoundMaterial.Stone)]
    [InlineData(BlockId.Cobble, SoundMaterial.Stone)]
    [InlineData(BlockId.Leaves, SoundMaterial.Leaf)]
    [InlineData(BlockId.BerryBush, SoundMaterial.Leaf)]
    [InlineData(BlockId.TallGrass, SoundMaterial.Leaf)]
    [InlineData(BlockId.Grass, SoundMaterial.Soft)]
    [InlineData(BlockId.Dirt, SoundMaterial.Soft)]
    [InlineData(BlockId.Sand, SoundMaterial.Soft)]
    public void EveryBlockHasAMaterialForItsHitAndBreakSounds(BlockId block, SoundMaterial material)
    {
        Assert.Equal(material, SoundEventMapper.MaterialOf(block));
    }

    [Fact]
    public void GatheringHitsPerMaterialEveryFifteenthTickAndBreaksWithABreakSound()
    {
        BlockId At(int x, int y, int z) => (x, y, z) == (1, 2, 3) ? BlockId.Trunk : BlockId.Grass;

        Assert.Equal([SoundCue.GatherWood], Mapper.ForSimulation(Sim(SimulationEventKind.GatherProgress, 1), At));
        Assert.Empty(Mapper.ForSimulation(Sim(SimulationEventKind.GatherProgress, 2), At));
        Assert.Empty(Mapper.ForSimulation(Sim(SimulationEventKind.GatherProgress, 15), At));
        Assert.Equal([SoundCue.GatherWood], Mapper.ForSimulation(Sim(SimulationEventKind.GatherProgress, 16), At));
        Assert.Equal([SoundCue.GatherSoft], Mapper.ForSimulation(new SimulationEvent(SimulationEventKind.GatherProgress, 5, 9, 9, 9, 1, 30), At));
        Assert.Equal([SoundCue.BreakWood], Mapper.ForSimulation(Sim(SimulationEventKind.BlockRemoved, (int)BlockId.Trunk), At));
        Assert.Equal([SoundCue.BreakStone], Mapper.ForSimulation(Sim(SimulationEventKind.BlockRemoved, (int)BlockId.Stone), At));
        Assert.Equal([SoundCue.BreakLeaf], Mapper.ForSimulation(Sim(SimulationEventKind.BlockRemoved, (int)BlockId.Leaves), At));
        Assert.Equal([SoundCue.BreakSoft], Mapper.ForSimulation(Sim(SimulationEventKind.BlockRemoved, (int)BlockId.Dirt), At));
    }

    [Fact]
    public void PlacingPickingUpCraftingAndFillingTheBottleHaveTheirOwnCues()
    {
        BlockId At(int x, int y, int z) => BlockId.Air;

        Assert.Equal([SoundCue.PlaceBlock], Mapper.ForSimulation(Sim(SimulationEventKind.BlockPlaced, (int)BlockId.Torch), At));
        Assert.Equal([SoundCue.PickUp], Mapper.ForSimulation(Sim(SimulationEventKind.ItemPickedUp), At));
        Assert.Equal([SoundCue.CraftDone], Mapper.ForSimulation(Sim(SimulationEventKind.ItemCrafted), At));
        Assert.Equal([SoundCue.BottleFill], Mapper.ForSimulation(Sim(SimulationEventKind.BottleFilled), At));
        Assert.Empty(Mapper.ForSimulation(Sim(SimulationEventKind.PlayerJumped), At)); // jumping and landing are S25's footsteps
        Assert.Empty(Mapper.ForSimulation(Sim(SimulationEventKind.WaterWithoutBottle), At)); // a hint, not a sound
    }

    [Fact]
    public void LessonStepsChimeSoftlyAUnitsLastStepChimesBrighterAndTheLessonEndsWithAFlourish()
    {
        var lesson = Book.Value.Find("first-words")!;
        LessonEvent Done(int step, StepOutcome outcome = StepOutcome.Done) => new(0, 0, LessonEventKind.StepDone, lesson.Id, step, lesson.Steps[step - 1].Id, "model", outcome);

        // u1-welcome, u1-hello, u1-say-hola, u1-name | u2-intro ...
        Assert.Equal([SoundCue.StepDone], Mapper.ForGameEvent(Done(1)));
        Assert.Equal([SoundCue.StepDone], Mapper.ForGameEvent(Done(3)));
        Assert.Equal([SoundCue.UnitDone], Mapper.ForGameEvent(Done(4)));
        Assert.Equal([SoundCue.UnitDone], Mapper.ForGameEvent(Done(8))); // u2-no, the last of unit 2
        Assert.Empty(Mapper.ForGameEvent(Done(lesson.Steps.Count))); // the last step: the lesson's own cue follows
        Assert.Equal([SoundCue.StepDone], Mapper.ForGameEvent(Done(1, StepOutcome.MovedOn))); // moving on is progress, not failure
        Assert.Empty(Mapper.ForGameEvent(Done(1, StepOutcome.Skipped))); // a skip is the learner's choice: silence
        Assert.Equal([SoundCue.LessonDone], Mapper.ForGameEvent(new LessonEvent(0, 0, LessonEventKind.LessonCompleted, lesson.Id, 0, string.Empty, string.Empty)));
        Assert.Empty(Mapper.ForGameEvent(new LessonEvent(0, 0, LessonEventKind.StepStarted, lesson.Id, 1, "u1-welcome", "explain")));
        Assert.Empty(Mapper.ForGameEvent(new LessonEvent(0, 0, LessonEventKind.Retry, lesson.Id, 1, "u1-welcome", "explain")));
    }

    [Fact]
    public void TheObjectiveTheFireAndTheEndingHaveTheirCues()
    {
        Assert.Equal([SoundCue.ObjectiveStep], Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.ObjectiveStep, null, "x")));
        Assert.Equal([SoundCue.ObjectiveDone], Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.ObjectiveComplete)));
        Assert.Equal([SoundCue.FireLight], Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.FireLit)));
        Assert.Equal([SoundCue.SessionDone], Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.SessionComplete)));
        Assert.Equal([SoundCue.PickUp], Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.ItemGiven, "shopkeeper", "agua")));
        Assert.Empty(Mapper.ForGameEvent(new WorldEvent(0, 0, WorldEvents.TalkOpened, "guide"))); // the panel watcher sounds the dialogue
        Assert.Equal([SoundCue.AnswerUnderstood], Mapper.ForGameEvent(new VoiceEvent(0, 0, VoiceEventKind.MicCheckPassed, null)));
        Assert.Equal([SoundCue.AnswerNeutral], Mapper.ForGameEvent(new VoiceEvent(0, 0, VoiceEventKind.MicCheckFailed, null)));
    }

    [Fact]
    public void AnUnderstoodAnswerIsPositiveAndEverythingElseIsNeutral()
    {
        Assert.Equal([SoundCue.AnswerUnderstood], Mapper.ForTurn(Turn(), null));
        Assert.Equal([SoundCue.AnswerNeutral], Mapper.ForTurn(Turn(communicated: false), null)); // not understood
        Assert.Equal([SoundCue.AnswerNeutral], Mapper.ForTurn(Turn(uncertain: true), null)); // "try again"
        Assert.Equal([SoundCue.AnswerNeutral], Mapper.ForTurn(Turn("dont_know"), null)); // no sé
        Assert.Equal([SoundCue.AnswerNeutral], Mapper.ForTurn(Turn("ask_help"), null));
        Assert.Empty(Mapper.ForTurn(null, null)); // a failed request is silent
        Assert.Equal([SoundCue.CheckAnswered], Mapper.ForTurn(null, null, isCheck: true));
        Assert.Equal([SoundCue.CheckAnswered], Mapper.ForTurn(Turn(communicated: false), null, isCheck: true)); // whatever the answer was
    }

    [Fact]
    public void ANewKnownWordAddsTheSparkle()
    {
        var words = new WordTurnResult(["agua"], ["agua"], []);
        Assert.Equal([SoundCue.AnswerUnderstood, SoundCue.WordLearnt], Mapper.ForTurn(Turn(), words));
        Assert.Equal([SoundCue.AnswerUnderstood], Mapper.ForTurn(Turn(), new WordTurnResult(["agua"], [], [])));
    }

    [Fact]
    public void NoCueIsNamedOrCategorisedAsAFailureAndTheNeutralCueIsTheSoftestOfTheProgressCues()
    {
        var forbidden = new[] { "error", "wrong", "fail", "buzz", "bad", "incorrect", "denied" };
        foreach (var cue in SoundCues.All)
        {
            Assert.DoesNotContain(forbidden, word => SoundCues.NameOf(cue).Contains(word, StringComparison.Ordinal));
        }

        // The neutral cue is a single steady tone, quieter at its own level than every other progress cue, and shorter than a chime.
        var neutral = SoundSynth.Render(SoundCue.AnswerNeutral);
        var understood = SoundSynth.Render(SoundCue.AnswerUnderstood);
        Assert.True(SoundCatalog.Of(SoundCue.AnswerNeutral).Gain <= SoundCatalog.Of(SoundCue.AnswerUnderstood).Gain);
        Assert.True(neutral.Length <= understood.Length);
        Assert.Equal(SoundCategory.Ui, SoundCues.CategoryOf(SoundCue.AnswerNeutral));
    }

    [Fact]
    public void AFramesCuesAreFoldedSoProgressChimesDoNotPileUp()
    {
        Assert.Equal([SoundCue.StepDone], SoundEventMapper.Collapse([SoundCue.AnswerUnderstood, SoundCue.StepDone]));
        Assert.Equal([SoundCue.UnitDone], SoundEventMapper.Collapse([SoundCue.AnswerUnderstood, SoundCue.StepDone, SoundCue.UnitDone]));
        Assert.Equal([SoundCue.LessonDone], SoundEventMapper.Collapse([SoundCue.UnitDone, SoundCue.LessonDone]));
        Assert.Equal([SoundCue.SessionDone], SoundEventMapper.Collapse([SoundCue.ObjectiveDone, SoundCue.SessionDone, SoundCue.FireLight]).Where(static c => c != SoundCue.FireLight).ToArray());
        Assert.Equal([SoundCue.AnswerUnderstood, SoundCue.WordLearnt], SoundEventMapper.Collapse([SoundCue.AnswerUnderstood, SoundCue.WordLearnt]));
        Assert.Equal([SoundCue.PickUp], SoundEventMapper.Collapse([SoundCue.PickUp, SoundCue.PickUp])); // a duplicate plays once
        Assert.Equal([SoundCue.BreakWood, SoundCue.PickUp], SoundEventMapper.Collapse([SoundCue.BreakWood, SoundCue.PickUp])); // the order is kept
        Assert.Equal([SoundCue.WordLearnt], SoundEventMapper.Collapse([SoundCue.AnswerNeutral, SoundCue.WordLearnt])); // neutral gives way to anything positive
        Assert.Equal([SoundCue.AnswerNeutral], SoundEventMapper.Collapse([SoundCue.AnswerNeutral]));
    }
}
