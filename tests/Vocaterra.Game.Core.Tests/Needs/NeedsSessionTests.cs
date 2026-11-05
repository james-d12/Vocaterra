using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The session owns the needs and steps them on the world tick only in active play (N01, design 9.2 "Time"), raising <c>need_stage</c> when a stage changes.</summary>
public sealed class NeedsSessionTests
{
    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static IReadOnlyList<WorldEvent> StageEvents(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.NeedStage).ToList();

    public static TheoryData<PlayPause> EveryInactiveState()
    {
        var data = new TheoryData<PlayPause>();
        foreach (var pause in Enum.GetValues<PlayPause>().Where(static p => p != PlayPause.None))
        {
            data.Add(pause);
        }

        return data;
    }

    [Fact]
    public void TheNeedsFallInActivePlay()
    {
        var session = TestGame.NewSession();
        Assert.True(session.PlayActive);

        Tick(session, 600);

        Assert.True(session.Needs.Value(NeedMeter.Hunger) < 100);
        Assert.True(session.Needs.Value(NeedMeter.Thirst) < session.Needs.Value(NeedMeter.Hunger));
        Assert.Equal(100 - (10 / 36.0), session.Needs.Value(NeedMeter.Hunger), 6);
    }

    [Theory]
    [MemberData(nameof(EveryInactiveState))]
    public void NothingFallsWhilePlayIsNotActive(PlayPause pause)
    {
        var session = TestGame.NewSession();
        session.PlayPauses = pause;
        Assert.False(session.PlayActive);

        Tick(session, 1200);

        Assert.Equal(new PlayerNeeds(TestGame.Data.Needs).Capture(), session.Needs.Capture());
        Assert.True(session.Simulation.Tick >= 1200); // the world's ticks went on
    }

    [Fact]
    public void ThereIsAnInactiveStateForEachKindTheDesignNames()
    {
        var names = Enum.GetNames<PlayPause>().ToHashSet();
        foreach (var kind in new[] { "Conversation", "Lesson", "Book", "Inventory", "Crafting", "Station", "Report", "Menu", "CheckBattery", "SpeechRecording", "SpeechProcessing" })
        {
            Assert.Contains(kind, names);
        }
    }

    [Fact]
    public void TheNeedsResumeWhenPlayIsActiveAgainAndCountOnlyTheActiveTicks()
    {
        var session = TestGame.NewSession();
        Tick(session, 360);
        session.PlayPauses = PlayPause.Book | PlayPause.Inventory;
        Tick(session, 3600);
        session.PlayPauses = PlayPause.None;
        Tick(session, 360);

        Assert.Equal(100 - (12 / 36.0), session.Needs.Value(NeedMeter.Hunger), 6);
    }

    [Fact]
    public void WithTheNeedsOffTheyStayFullEvenSetOnesStay()
    {
        var session = TestGame.NewSession();
        session.NeedsEnabled = false;
        session.SetNeed(NeedMeter.Thirst, 30);

        Tick(session, 7200);

        Assert.Equal(30, session.Needs.Value(NeedMeter.Thirst));
        Assert.Equal(100, session.Needs.Value(NeedMeter.Hunger));
        Assert.Empty(StageEvents(session));
    }

    [Fact]
    public void ANeedStageEventIsRaisedWhenThirstCrosses25AndNotForTheValueSet()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 30);
        Assert.Empty(StageEvents(session)); // the value set at launch is the start, not news

        Tick(session, 5 * 24 * NeedsData.TicksPerSecond / 2);
        Assert.Empty(StageEvents(session)); // 27.5: still stage 2

        Tick(session, (5 * 24 * NeedsData.TicksPerSecond / 2) + 5);
        var events = StageEvents(session);

        var only = Assert.Single(events);
        Assert.Equal("sed", only.Detail);
        Assert.Equal(3, session.NeedStage(NeedMeter.Thirst));
    }

    [Fact]
    public void AStageChangeIsRaisedForEachNeedWithItsOwnDetail()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Hunger, 75.001);
        session.SetNeed(NeedMeter.Sleep, 75.001);
        session.SetNeed(NeedMeter.Health, 50.001);
        session.Needs.Set(NeedMeter.Health, 49); // a change the session did not see
        Tick(session, 60 * 3);

        var details = StageEvents(session).Select(static e => e.Detail ?? string.Empty).ToList();
        Assert.Equal(["hambre", "salud", "sueño"], details.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void RecoveryRaisesTheEventToo()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 10);
        session.Needs.Eat(TestGame.Data.Needs.FoodOf("water-bottle")!);

        session.SyncNeedStages();

        var only = Assert.Single(StageEvents(session));
        Assert.Equal("sed", only.Detail);
        Assert.Equal(1, session.NeedStage(NeedMeter.Thirst)); // 10 + 40 = 50: stage 1
    }

    [Fact]
    public void TheSameInputsGiveTheSameNeedsAndEvents()
    {
        (NeedsSave, string) Play()
        {
            var session = TestGame.NewSession();
            session.SetNeed(NeedMeter.Thirst, 26);
            Tick(session, 3600);
            session.PlayPauses = PlayPause.Report;
            Tick(session, 600);
            session.PlayPauses = PlayPause.None;
            Tick(session, 3600);
            return (session.Needs.Capture(), string.Join(';', StageEvents(session).Select(static e => e.Text + e.Tick)));
        }

        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void TheNewWorldEventsAreInTheListOfAllWorldEvents()
    {
        foreach (var name in new[] { "meal_eaten", "water_drunk", "need_stage", "player_collapsed", "player_died", "player_woke", "belongings_recovered" })
        {
            Assert.Contains(name, WorldEvents.All);
        }

        Assert.Equal(WorldEvents.All.Count, WorldEvents.All.Distinct().Count());
    }
}
