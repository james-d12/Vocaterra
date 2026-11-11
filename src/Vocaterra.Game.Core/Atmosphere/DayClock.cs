namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The time of day (E01): a pure function of the simulation's tick plus an integer offset. The position in the cycle is
/// <c>(startTicks + tick + offset) mod cycleTicks</c>, all in integer ticks, so no float is accumulated and two runs of the same commands agree to the tick.
/// The hour, the phase and the daylight are read from that position. The state kept (and saved and digested) is the offset and the hour a test holds the clock at, nothing else.
/// </summary>
public sealed class DayClock
{
    /// <summary>The simulation's ticks a second (60): the cycle's length in seconds is turned into ticks with it.</summary>
    public const int TicksPerSecond = 60;

    private readonly long _cycle;
    private readonly long _start;
    private readonly long _dawn;
    private readonly long _day;
    private readonly long _dusk;
    private readonly long _night;
    private readonly long _wake;
    private long _offset;
    private double? _frozenHour;

    /// <summary>Makes a clock at the state of a new game.</summary>
    /// <param name="data">The cycle's numbers.</param>
    public DayClock(DayData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        _cycle = Math.Max(1, (long)Math.Round(data.CycleSeconds * TicksPerSecond, MidpointRounding.AwayFromZero));
        _start = HourToTicks(data.StartHour);
        _dawn = HourToTicks(data.DawnHour);
        _day = HourToTicks(data.DayHour);
        _dusk = HourToTicks(data.DuskHour);
        _night = HourToTicks(data.NightHour);
        _wake = HourToTicks(data.WakeHour);
    }

    /// <summary>The cycle's numbers.</summary>
    public DayData Data { get; }

    /// <summary>The ticks in one full day and night.</summary>
    public long CycleTicks => _cycle;

    /// <summary>What the clock keeps besides the tick: the offset and the held hour.</summary>
    public DayClockState State => new(_offset, _frozenHour);

    /// <summary>Whether the clock is held at an hour.</summary>
    public bool IsFrozen => _frozenHour is not null;

    /// <summary>The position in the cycle at a tick, 0 up to <see cref="CycleTicks"/>.</summary>
    /// <param name="tick">The simulation's tick.</param>
    public long PositionAt(long tick) => _frozenHour is { } frozen ? HourToTicks(frozen) % _cycle : Mod(_start + tick + _offset);

    /// <summary>The hour on the 24-hour dial at a tick, 0 up to 24.</summary>
    /// <param name="tick">The simulation's tick.</param>
    public double HourAt(long tick) => PositionAt(tick) * DayData.HoursPerDay / _cycle;

    /// <summary>The phase at a tick.</summary>
    /// <param name="tick">The simulation's tick.</param>
    public DayPhase PhaseAt(long tick)
    {
        var position = PositionAt(tick);
        if (position >= _night || position < _dawn)
        {
            return DayPhase.Night;
        }

        if (position < _day)
        {
            return DayPhase.Dawn;
        }

        return position < _dusk ? DayPhase.Day : DayPhase.Dusk;
    }

    /// <summary>
    /// The daylight at a tick: 0 through the night, rising across dawn, 1 through the day and falling across dusk. Across a dawn or a dusk it follows a smoothstep of the straight line from
    /// 0 to 1 (E11), so it eases out of the night and into the day with no slope jump at the phase's edges, and is still exactly 0.5 half way through.
    /// </summary>
    /// <param name="tick">The simulation's tick.</param>
    public double DaylightAt(long tick)
    {
        var position = PositionAt(tick);
        return PhaseAt(tick) switch
        {
            DayPhase.Dawn => Ease((position - _dawn) / (double)(_day - _dawn)),
            DayPhase.Day => 1,
            DayPhase.Dusk => Ease(1 - ((position - _dusk) / (double)(_night - _dusk))),
            _ => 0,
        };
    }

    /// <summary>Sets the offset so the hour is the data's wake hour at this tick (a sleep or a collapse). A held clock stays held.</summary>
    /// <param name="tick">The simulation's tick now.</param>
    public void WakeUp(long tick) => _offset = Mod(_wake - _start - tick);

    /// <summary>Holds the clock at an hour, for a test or a screenshot. Hours 0 up to 24 (24 is 0 again).</summary>
    /// <param name="hour">The hour to hold.</param>
    /// <exception cref="ArgumentOutOfRangeException">The hour is not from 0 to 24.</exception>
    public void Freeze(double hour)
    {
        if (!double.IsFinite(hour) || hour < 0 || hour > DayData.HoursPerDay)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "An hour is from 0 to 24.");
        }

        _frozenHour = hour;
    }

    /// <summary>Lets a held clock run again from the held hour: the offset is set so the hour at this tick is the held one.</summary>
    /// <param name="tick">The simulation's tick now.</param>
    public void Unfreeze(long tick)
    {
        if (_frozenHour is { } frozen)
        {
            _offset = Mod((HourToTicks(frozen) % _cycle) - _start - tick);
            _frozenHour = null;
        }
    }

    /// <summary>Puts a state back (a load, a sleep's restore).</summary>
    /// <param name="state">The state, or <see langword="null"/> for a new game's.</param>
    /// <exception cref="ArgumentOutOfRangeException">The held hour is not from 0 to 24.</exception>
    public void Restore(DayClockState? state)
    {
        state ??= DayClockState.Default;
        if (state.FrozenHour is { } frozen && (!double.IsFinite(frozen) || frozen < 0 || frozen > DayData.HoursPerDay))
        {
            throw new ArgumentOutOfRangeException(nameof(state), frozen, "The held hour is not from 0 to 24.");
        }

        _offset = Mod(state.Offset);
        _frozenHour = state.FrozenHour;
    }

    // The smoothstep of a straight run from 0 to 1: flat at both ends, 0.5 at one half.
    private static double Ease(double t) => t * t * (3 - (2 * t));

    private long HourToTicks(double hour) => (long)Math.Round(hour / DayData.HoursPerDay * _cycle, MidpointRounding.AwayFromZero);

    private long Mod(long value) => ((value % _cycle) + _cycle) % _cycle;
}
