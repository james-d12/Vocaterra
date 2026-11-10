using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary>The day clock's arithmetic (E01): the hour, the phase and the daylight as a function of the tick and an offset, the wrap at the end of a cycle, the wake-up and the held hour.</summary>
public sealed class DayClockTests
{
    // The shipped values: 2400 s is 144 000 ticks, so one hour is 6 000 ticks; dawn 4, day 6, dusk 20, night 22, start 8, wake 7.
    private const long HourTicks = 6000;

    private static readonly DayClock Clock = new(DayData.Default);

    private static long AtHour(double hour) => (long)(hour * HourTicks) - (8 * HourTicks); // the tick at which a clock that starts at 8 shows the hour

    [Fact]
    public void ANewGameStartsAtTheStartHour()
    {
        Assert.Equal(8, Clock.HourAt(0), 6);
        Assert.Equal(DayPhase.Day, Clock.PhaseAt(0));
        Assert.True(Clock.State.IsDefault);
    }

    [Theory]
    [InlineData(0, 0, DayPhase.Night)]
    [InlineData(3.99, 0, DayPhase.Night)]
    [InlineData(4, 0, DayPhase.Dawn)]
    [InlineData(5, 0.5, DayPhase.Dawn)]
    [InlineData(6, 1, DayPhase.Day)]
    [InlineData(13, 1, DayPhase.Day)]
    [InlineData(19.99, 1, DayPhase.Day)]
    [InlineData(20, 1, DayPhase.Dusk)]
    [InlineData(21, 0.5, DayPhase.Dusk)]
    [InlineData(22, 0, DayPhase.Night)]
    [InlineData(23.5, 0, DayPhase.Night)]
    public void TheHourGivesThePhaseAndTheDaylight(double hour, double daylight, DayPhase phase)
    {
        var clock = new DayClock(DayData.Default);
        clock.Freeze(hour);

        Assert.Equal(phase, clock.PhaseAt(12345));
        Assert.Equal(hour, clock.HourAt(12345), 3);
        Assert.Equal(daylight, clock.DaylightAt(12345), 3);
    }

    [Fact]
    public void TheDaylightIsZeroThroughTheNightRisesAcrossDawnAndFallsAcrossDusk()
    {
        double Of(double hour) => Clock.DaylightAt(AtHour(hour));

        Assert.Equal(0, Of(2));
        Assert.True(Of(4.5) < Of(5) && Of(5) < Of(5.5));
        Assert.Equal(1, Of(12));
        Assert.True(Of(20.5) > Of(21) && Of(21) > Of(21.5));
        Assert.Equal(0, Of(22.5));
        Assert.All(Enumerable.Range(0, 240), i => Assert.InRange(Clock.DaylightAt(i * 600L), 0, 1));
    }

    [Fact]
    public void TheClockWrapsAtTheEndOfACycleAndANightFollowsDusk()
    {
        var midnight = AtHour(24);

        Assert.Equal(0, Clock.HourAt(midnight), 6);
        Assert.Equal(DayPhase.Night, Clock.PhaseAt(midnight));
        Assert.Equal(Clock.HourAt(1234), Clock.HourAt(1234 + Clock.CycleTicks), 9);
        Assert.Equal(Clock.PositionAt(77), Clock.PositionAt(77 + (5 * Clock.CycleTicks)));
        Assert.Equal(144000, Clock.CycleTicks);
    }

    [Fact]
    public void TheCycleIsAboutThirtyMinutesOfLightAndTenOfNight()
    {
        var phases = Enumerable.Range(0, 2400).Select(i => Clock.PhaseAt(i * 60L)).ToList(); // one sample a second
        var light = phases.Count(static p => p != DayPhase.Night);

        Assert.Equal(1800, light);
        Assert.Equal(600, phases.Count - light);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3.5)]
    [InlineData(12)]
    [InlineData(22)]
    [InlineData(23.99)]
    public void WakeUpFromAnyHourSetsTheHourToTheWakeHourAtThatTickAndTheClockRunsOnFromThere(double fromHour)
    {
        var clock = new DayClock(DayData.Default);
        var tick = AtHour(fromHour) + (3L * clock.CycleTicks);
        Assert.Equal(fromHour, clock.HourAt(tick), 3);

        clock.WakeUp(tick);

        Assert.Equal(7, clock.HourAt(tick), 6);
        Assert.Equal(DayPhase.Day, clock.PhaseAt(tick));
        Assert.Equal(8, clock.HourAt(tick + HourTicks), 6);
        Assert.InRange(clock.State.Offset, 0, clock.CycleTicks - 1);
    }

    [Fact]
    public void WakeUpAtATickBeforeTheCycleBeganStillGivesTheWakeHour()
    {
        var clock = new DayClock(DayData.Default);

        clock.WakeUp(0);

        Assert.Equal(7, clock.HourAt(0), 6);
    }

    [Fact]
    public void AHeldClockStaysAtItsHourWhateverTheTick()
    {
        var clock = new DayClock(DayData.Default);

        clock.Freeze(21.5);

        Assert.True(clock.IsFrozen);
        Assert.Equal(21.5, clock.HourAt(0), 6);
        Assert.Equal(21.5, clock.HourAt(987654), 6);
        Assert.Equal(DayPhase.Dusk, clock.PhaseAt(987654));
        Assert.Equal(new DayClockState(0, 21.5), clock.State);
    }

    [Fact]
    public void UnfreezeLetsTheClockRunOnFromTheHeldHour()
    {
        var clock = new DayClock(DayData.Default);
        clock.Freeze(10);

        clock.Unfreeze(5000);

        Assert.False(clock.IsFrozen);
        Assert.Equal(10, clock.HourAt(5000), 6);
        Assert.Equal(11, clock.HourAt(5000 + HourTicks), 6);
        Assert.Null(clock.State.FrozenHour);
    }

    [Fact]
    public void UnfreezeOfAClockThatRunsChangesNothing()
    {
        var clock = new DayClock(DayData.Default);

        clock.Unfreeze(900);

        Assert.True(clock.State.IsDefault);
    }

    [Fact]
    public void AnHourOutOfTheDialCannotBeHeld()
    {
        var clock = new DayClock(DayData.Default);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Freeze(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Freeze(24.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Freeze(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Restore(new DayClockState(0, 30)));
    }

    [Fact]
    public void TheStateRestoresToTheSameHourAndTheSameDigestPart()
    {
        var clock = new DayClock(DayData.Default);
        clock.WakeUp(31000);
        var state = clock.State;

        var other = new DayClock(DayData.Default);
        other.Restore(state);

        Assert.Equal(clock.HourAt(70000), other.HourAt(70000));
        Assert.Equal(state, other.State);
    }

    [Fact]
    public void TheClockIsAPureFunctionOfTheTick()
    {
        var a = new DayClock(DayData.Default);
        var b = new DayClock(DayData.Default);
        a.WakeUp(1000);
        b.WakeUp(1000);

        foreach (var tick in new long[] { 0, 1, 59999, 1000000, 99999999 })
        {
            Assert.Equal(a.PositionAt(tick), b.PositionAt(tick));
            Assert.Equal(a.DaylightAt(tick), b.DaylightAt(tick));
        }
    }

    [Theory]
    [InlineData(DayPhase.Dawn, "dawn")]
    [InlineData(DayPhase.Day, "day")]
    [InlineData(DayPhase.Dusk, "dusk")]
    [InlineData(DayPhase.Night, "night")]
    public void EachPhaseHasItsTriggerName(DayPhase phase, string name) => Assert.Equal(name, DayPhaseNames.Of(phase));
}
