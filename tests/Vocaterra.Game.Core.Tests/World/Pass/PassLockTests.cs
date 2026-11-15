using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Generation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Pass;

namespace Vocaterra.Game.Core.Tests.World.Pass;

/// <summary>
/// The pass's lock (B08b, D-game-34 items 6 and 15): closed until every required lesson is complete or the cheat is on, open for good after, and the rockfall's blocks gone when it opens.
/// The lessons are the world file's (<c>pass.requiredLessons</c>); nothing here names one.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class PassLockTests
{
    private const ulong Seed = 42;

    private static IReadOnlyList<string> Required => TestGame.Data.Pass.RequiredLessons;

    private static VoxelSession NewSession() => TestGame.NewSessionWithShippedLessons(Seed);

    private static IReadOnlyList<BlockPos> Rockfall(VoxelSession session) => session.Scene.PropCells[session.Data.Pass.Rockfall];

    private static bool HeapStands(VoxelSession session) => Rockfall(session).All(c => session.Scene.Grid.GetBlock(c.X, c.Y, c.Z) is BlockId.Stone or BlockId.Cobble or BlockId.MossyStone);

    private static bool HeapGone(VoxelSession session) => Rockfall(session).All(c => session.Scene.Grid.GetBlock(c.X, c.Y, c.Z) == BlockId.Air);

    /// <summary>Puts lessons into the learner's record as completed (a save's own shape), without playing them.</summary>
    private static void Complete(VoxelSession session, IEnumerable<string> ids)
    {
        var done = ids.ToHashSet(StringComparer.Ordinal);
        var save = session.Lessons.Capture();
        var lessons = save.Lessons.Select(l => done.Contains(l.Id) ? l with { Status = LessonStatus.Completed, Step = 0, Outcomes = [], EverCompleted = true } : l).ToArray();
        session.Lessons.Restore(save with { Lessons = lessons, Active = null, Replay = null });
    }

    private static List<WorldEvent> Opened(VoxelSession session) => session.Events.OfType<WorldEvent>().Where(e => e.Name == WorldEvents.PassOpened).ToList();

    private static VoxelSession Reload(VoxelSession session, SaveSettings? settings = null) =>
        SaveGame.Load(SaveGame.Save(session, settings), TestGame.Data, TestGame.Route, credit: session.Credit, bumpSession: false);

    [Fact]
    public void Given_NoLessonDone_When_TheSessionRuns_Then_ThePassIsClosedAndTheRockfallStands()
    {
        var session = NewSession();

        session.Step([]);
        session.Step([]);

        Assert.True(session.Pass.Locked);
        Assert.False(session.Pass.IsOpen);
        Assert.True(HeapStands(session));
        Assert.Empty(Opened(session));
        Assert.True(Required.Count >= 12); // all of today's lessons
    }

    [Fact]
    public void Given_AllButTheLastRequiredLesson_When_TheLastIsCompleted_Then_ThePassOpensOnTheNextTickAndTheRockfallGoes()
    {
        var session = NewSession();
        var raised = 0;
        session.Pass.Opened += () => raised++;
        Complete(session, Required.Take(Required.Count - 1));
        session.Step([]);
        Assert.False(session.Pass.IsOpen);
        Assert.True(HeapStands(session));

        Complete(session, Required);
        session.Step([]);

        Assert.True(session.Pass.IsOpen);
        Assert.True(HeapGone(session));
        Assert.Equal(1, raised);
        Assert.Equal("lessons", Assert.Single(Opened(session)).Detail);
        session.Step([]);
        Assert.Single(Opened(session)); // once
    }

    [Fact]
    public void Given_TheCheat_When_ItIsTurnedOn_Then_ThePassOpensAtOnceAndStaysOpenWhenItIsTurnedOff()
    {
        var session = NewSession();
        Assert.False(session.Pass.IsOpen);

        session.PassCheat = true;

        Assert.True(session.Pass.IsOpen);
        Assert.True(HeapGone(session));
        Assert.Equal("cheat", Assert.Single(Opened(session)).Detail);
        session.PassCheat = false;
        session.Step([]);
        Assert.True(session.Pass.IsOpen);
        Assert.True(HeapGone(session));
    }

    [Fact]
    public void Given_TheRule_When_ItIsAskedWithAnyLessonsAndTheCheat_Then_ItIsPureAndNeedsEveryLessonOrTheCheat()
    {
        var data = new PassData(["a", "b"], "rock", "edge");

        Assert.False(PassState.ShouldBeOpen(data, _ => false, cheat: false));
        Assert.False(PassState.ShouldBeOpen(data, id => id == "a", cheat: false));
        Assert.True(PassState.ShouldBeOpen(data, _ => true, cheat: false));
        Assert.True(PassState.ShouldBeOpen(data, _ => false, cheat: true));
        Assert.True(PassState.ShouldBeOpen(PassData.None, _ => false, cheat: false)); // nothing required: no lock
        Assert.False(PassState.ShouldBeOpen(data, id => id == "a", cheat: false)); // asking again changes nothing: the rule keeps no state
    }

    [Fact]
    public void Given_AnOpenPass_When_TheGameIsSavedAndLoaded_Then_ItIsOpenTheRockfallStaysGoneAndTheCheatIsNotNeeded()
    {
        var session = NewSession();
        Complete(session, Required);
        session.Step([]);
        Assert.True(session.Pass.IsOpen);

        var loaded = Reload(session);

        Assert.True(loaded.Pass.IsOpen);
        Assert.False(loaded.PassCheat);
        Assert.True(HeapGone(loaded));
        Assert.Empty(Opened(loaded)); // a load raises nothing
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        loaded.Step([]);
        Assert.True(loaded.Pass.IsOpen);
    }

    [Fact]
    public void Given_ThePassOpenedByTheCheat_When_TheGameIsSavedWithTheCheatOffAndLoaded_Then_ItIsStillOpen()
    {
        var session = NewSession();
        session.PassCheat = true;

        var loaded = Reload(session, new SaveSettings(new SoundVolumes(1, 1, 1, 1, 1), PassCheat: false));

        Assert.True(loaded.Pass.IsOpen);
        Assert.True(HeapGone(loaded));
    }

    [Fact]
    public void Given_AClosedPass_When_TheGameIsSavedAndLoaded_Then_ItIsClosedTheRockfallStandsAndItsFlagIsNotWritten()
    {
        var session = NewSession();
        var text = SaveGame.Save(session);

        var loaded = Reload(session);

        Assert.DoesNotContain("passOpen", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(loaded.Pass.IsOpen);
        Assert.True(HeapStands(loaded));
    }

    [Fact]
    public void Given_ThePassOpen_When_AWalkerSetsOutFromTheVillageRoad_Then_TheRoadReachesTheEdgeSite()
    {
        var session = NewSession();
        session.PassCheat = true;
        var scene = session.Scene;

        var walked = PassTests.Reach(scene.Grid, PassTests.RoadStart(scene), maxFloor: PassTests.PassBand(scene));

        var edge = scene.Point(session.Data.Pass.Edge);
        Assert.Contains((edge.X, edge.Z), walked);
        Assert.Contains(walked, c => c.X == scene.Grid.SizeX - 1);
    }

    [Fact]
    public void Given_ThePassOpened_When_ThePristineValleyIsBuiltAgain_Then_ItStillHasTheRockfall()
    {
        var session = NewSession();
        session.PassCheat = true;

        var fresh = TestGame.Route(Seed);

        Assert.All(fresh.PropCells[session.Data.Pass.Rockfall], c => Assert.NotEqual(BlockId.Air, fresh.Grid.GetBlock(c.X, c.Y, c.Z)));
    }

    [Fact]
    public void Given_ThePassData_When_ItIsValidatedWithAnUnknownLessonOrPropOrSite_Then_EachIsRefused()
    {
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.Contains("pass.", StringComparison.Ordinal));

        var unknownLesson = GameDataValidator.Validate(TestGame.Data.WithPass(new PassData([.. Required, "no-such-lesson"], TestGame.Data.Pass.Rockfall, TestGame.Data.Pass.Edge)));
        var unknownProp = GameDataValidator.Validate(TestGame.Data.WithPass(TestGame.Data.Pass with { Rockfall = "no-such-prop" }));
        var unknownSite = GameDataValidator.Validate(TestGame.Data.WithPass(TestGame.Data.Pass with { Edge = "no-such-site" }));
        var twice = GameDataValidator.Validate(TestGame.Data.WithPass(TestGame.Data.Pass with { RequiredLessons = [Required[0], Required[0]] }));

        Assert.Contains(unknownLesson, static p => p.Contains("'no-such-lesson'", StringComparison.Ordinal));
        Assert.Contains(unknownProp, static p => p.Contains("pass.rockfall", StringComparison.Ordinal));
        Assert.Contains(unknownSite, static p => p.Contains("pass.edge", StringComparison.Ordinal));
        Assert.Contains(twice, static p => p.Contains("twice", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_TheRequiredLessons_When_TheyAreCompared_Then_TheyAreEveryLessonTheGameHasToday()
    {
        Assert.Equal(TestGame.Data.Lessons.Lessons.Select(static l => l.Id).Order(), Required.Order());
    }

    [Fact]
    public void Given_TheCheatSetting_When_ItIsUsed_Then_ItIsOffByDefaultSavedWithTheSettingsAndShownInTheMenu()
    {
        var settings = new PlayerSettings();
        var changed = new List<SettingId>();
        settings.Changed += changed.Add;
        Assert.False(settings.PassCheat);
        Assert.False(settings.ToSave().PassCheat);

        settings.Set(SettingId.PassCheat, 1);

        Assert.True(settings.PassCheat);
        Assert.Equal([SettingId.PassCheat], changed);
        var saved = settings.ToSave();
        Assert.True(saved.PassCheat);
        Assert.True(PlayerSettings.Resolve(saved).PassCheat);
        Assert.True(PlayerSettings.Resolve(null, new LaunchSettings(PassOpen: true)).PassCheat);
        Assert.False(PlayerSettings.Resolve(null).PassCheat);
        Assert.Contains(MenuRows.Settings(settings, null), static row => row.Id == nameof(SettingId.PassCheat));
        Assert.Equal("Open the pass (cheat)", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.PassCheat).LabelKey));
    }

    [Fact]
    public void Given_TheSettingsOfASave_When_ThePassCheatIsOff_Then_TheFieldIsNotWritten()
    {
        var off = SaveGame.Serialize(SaveGame.Capture(NewSession(), new PlayerSettings().ToSave()));
        Assert.DoesNotContain("passCheat", off, StringComparison.OrdinalIgnoreCase);
    }
}
