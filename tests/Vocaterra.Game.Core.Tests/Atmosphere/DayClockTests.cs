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
    public void Given_ADawnAndADusk_When_TheDaylightIsRead_Then_ItEasesLikeASmoothstepOfTheStraightLine()
    {
        // Half way is still one half; a quarter of the way is the smoothstep's 0.156 (a straight line would give 0.25), and the dusk mirrors the dawn.
        Assert.Equal(0.5, Clock.DaylightAt(AtHour(5)), 6);
        Assert.Equal(0.5, Clock.DaylightAt(AtHour(21)), 6);
        Assert.Equal(0.15625, Clock.DaylightAt(AtHour(4.5)), 4);
        Assert.Equal(0.84375, Clock.DaylightAt(AtHour(5.5)), 4);
        Assert.Equal(0.84375, Clock.DaylightAt(AtHour(20.5)), 4);
        Assert.Equal(0.15625, Clock.DaylightAt(AtHour(21.5)), 4);
    }

    [Fact]
    public void Given_TheWholeCycle_When_TheDaylightIsSampled_Then_ItRisesFallsAndHasNoSlopeJumpAtThePhaseEdges()
    {
        // Sampled every 100 ticks (a sixtieth of an hour). The slope between samples may only change by what a smoothstep's curvature gives: at most 6 / 12 000^2 a tick^2, so 0.0005 over
        // 100 ticks. The old straight line changed its slope by 0.0083 at each edge (a jump of 16 times that), so this bound catches any return to it.
        const int step = 100;
        var samples = Enumerable.Range(0, (int)(Clock.CycleTicks / step) + 1).Select(i => Clock.DaylightAt(AtHour(0) + (i * (long)step))).ToArray();
        for (var i = 2; i < samples.Length; i++)
        {
            var slope = samples[i] - samples[i - 1];
            var before = samples[i - 1] - samples[i - 2];
            Assert.True(Math.Abs(slope - before) <= 0.001, $"the slope jumps by {Math.Abs(slope - before):0.00000} at sample {i} (hour {i * step / (double)HourTicks:0.000})");
        }

        for (var hour = 4.0; hour < 5.99; hour += 0.1)
        {
            Assert.True(Clock.DaylightAt(AtHour(hour + 0.1)) > Clock.DaylightAt(AtHour(hour)), $"rises at {hour}");
        }

        for (var hour = 20.0; hour < 21.99; hour += 0.1)
        {
            Assert.True(Clock.DaylightAt(AtHour(hour + 0.1)) < Clock.DaylightAt(AtHour(hour)), $"falls at {hour}");
        }
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
