using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The day clock in the session (E01): the <c>day_phase</c> world event, and a sleep or a collapse waking in the morning without moving the stored time, the day or the sitting.</summary>
public sealed class DayClockSessionTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static IReadOnlyList<WorldEvent> PhaseEvents(VoxelSession session) => session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.DayPhase).ToList();

    [Fact]
    public void TheEventNameIsDayPhaseAndIsInTheListTheTriggersCheckAgainst()
    {
        Assert.Equal("day_phase", WorldEvents.DayPhase);
        Assert.Contains(WorldEvents.DayPhase, WorldEvents.All);
    }

    [Fact]
    public void ACrossedBoundaryIsAWorldEventWhoseDetailIsTheNewPhase()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(19.99);
        session.Simulation.UnfreezeClock(); // 0.01 of an hour, 60 ticks, to dusk

        Tick(session, 30);
        Assert.Empty(PhaseEvents(session));
        Tick(session, 60);

        var dusk = Assert.Single(PhaseEvents(session));
        Assert.Equal("dusk", dusk.Detail);
        Assert.Null(dusk.Npc);
    }

    [Fact]
    public void ADayPhaseEventMatchesATriggerThatWaitsForIt()
    {
        var trigger = new Trigger(WorldEvents.DayPhase, null, "night");
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(21.99);
        session.Simulation.UnfreezeClock();

        Tick(session, 100);

        var night = Assert.Single(PhaseEvents(session));
        Assert.True(trigger.Matches(night));
        Assert.False(new Trigger(WorldEvents.DayPhase, null, "dawn").Matches(night));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASleepAndACollapseBothWakeAtTheWakeHourWithoutMovingTheStoredTimeTheDayOrTheSitting(bool collapse)
    {
        var session = SleepTests.EndedFirstSession();
        session.Simulation.FreezeClock(23); // it is night, whatever the tick
        session.Simulation.UnfreezeClock();
        var wake = TestGame.Data.Day.WakeHour;
        var time = session.StoredTime;
        var day = session.Day;
        var sitting = session.Sitting;

        SaveGame.Sleep(session, SaveGame.Capture(session), collapse);

        var simulation = session.Simulation;
        Assert.Equal(wake, simulation.Clock.HourAt(simulation.Tick), 6);
        Assert.Equal(DayPhase.Day, simulation.Phase);
        Assert.Equal(time, session.StoredTime);
        Assert.Equal(day + 1, session.Day); // a sleep moves the day itself, as it did before the clock
        Assert.Equal(sitting + 1, session.Sitting);
        Tick(session, 5);
        Assert.Empty(PhaseEvents(session)); // the jump is no boundary
    }

    [Fact]
    public void ASleepWithTheNeedsOffWakesInTheMorningToo()
    {
        var session = SleepTests.EndedFirstSession();
        session.NeedsEnabled = false;
        session.Simulation.FreezeClock(2);
        session.Simulation.UnfreezeClock();

        SaveGame.Sleep(session, SaveGame.Capture(session));

        Assert.Equal(TestGame.Data.Day.WakeHour, session.Simulation.Clock.HourAt(session.Simulation.Tick), 6);
    }

    [Fact]
    public void TheClockRunsWhetherOrNotThePlayerSleeps()
    {
        var session = TestGame.NewSession();
        var start = session.Simulation.Clock.HourAt(0);

        Tick(session, 600);

        Assert.Equal(start + (600 / 6000.0), session.Simulation.Clock.HourAt(session.Simulation.Tick), 6);
    }
}
