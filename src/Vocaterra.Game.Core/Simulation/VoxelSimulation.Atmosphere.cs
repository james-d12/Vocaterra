using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Simulation;

public sealed partial class VoxelSimulation
{
    private DayClock _clock = new(DayData.Default);
    private DayPhase _noticedPhase;
    private WeatherSystem _weather = new(WeatherData.Default, 0);
    private readonly List<int> _pendingWeather = [];

    /// <summary>The time of day (E01): a function of <see cref="Tick"/> and the clock's offset. Read only for the session, the front-end and tests; a sleep moves it with <see cref="WakeUp"/>.</summary>
    public DayClock Clock => _clock;

    /// <summary>The phase the clock is in now.</summary>
    public DayPhase Phase => _clock.PhaseAt(Tick);

    /// <summary>The weather (E04): its spells run from <see cref="Step()"/>; the front-end reads <see cref="Atmosphere"/>, not the kinds.</summary>
    public WeatherSystem Weather => _weather;

    /// <summary>The sky and weather now: the hour, the phase, the daylight and the blended channels. Every consumer reads this and never a kind's id.</summary>
    public AtmosphereSnapshot Atmosphere => AtmosphereModel.At(_clock, _weather, Tick);

    /// <summary>
    /// A host action, like <see cref="TeleportTo"/>: a sleep or a collapse wakes the player in the morning (the data's wake hour) and rolls the morning's weather
    /// (E04; a cut, no transition). The jump raises no <see cref="SimulationEventKind.DayPhaseChanged"/>: a phase event is a boundary crossed by the running clock;
    /// a change of weather raises <see cref="SimulationEventKind.WeatherChanged"/> at the next step.
    /// </summary>
    public void WakeUp()
    {
        _clock.WakeUp(Tick);
        _noticedPhase = Phase;
        NoticeWeather(_weather.RollMorning(Tick));
    }

    /// <summary>A host action for tests and screenshots: holds a weather kind (no transition) until <see cref="ReleaseWeather"/>.</summary>
    /// <param name="id">The kind's id.</param>
    /// <exception cref="ArgumentException">There is no such kind.</exception>
    public void ForceWeather(string id) => NoticeWeather(_weather.Force(id, Tick));

    /// <summary>A host action: lets a held weather run on; a new spell of the same kind begins.</summary>
    public void ReleaseWeather() => _weather.Release(Tick);

    /// <summary>A host action for tests and screenshots: holds the clock at an hour (0 to 24) until <see cref="UnfreezeClock"/>. Raises no phase event.</summary>
    /// <param name="hour">The hour to hold.</param>
    public void FreezeClock(double hour)
    {
        _clock.Freeze(hour);
        _noticedPhase = Phase;
    }

    /// <summary>A host action: lets a held clock run on from its held hour. Raises no phase event.</summary>
    public void UnfreezeClock()
    {
        _clock.Unfreeze(Tick);
        _noticedPhase = Phase;
    }

    // Starts the clock and the weather with the bundle's numbers (the default values when a simulation is built without game data), at the phase of tick 0.
    private void StartAtmosphere(DayData? day, WeatherData? weather, ulong worldSeed)
    {
        _clock = new DayClock(day ?? DayData.Default);
        _noticedPhase = Phase;
        _weather = new WeatherSystem(weather ?? WeatherData.Default, worldSeed);
    }

    // A change made between steps is told at the next one (a step clears the events first), once.
    private void NoticeWeather(bool changed)
    {
        if (changed)
        {
            _pendingWeather.Add(_weather.Data.IndexOf(_weather.State.Current));
        }
    }

    // The state the save and the digest keep: the clock's and the weather's, which is nothing while both are as in a new game.
    private AtmosphereState CaptureAtmosphere() => new(_clock.State.Offset, _clock.State.FrozenHour, _weather.IsInitial ? null : _weather.State);

    private void RestoreAtmosphere(AtmosphereState? state)
    {
        _clock.Restore(state?.Clock);
        _weather.Restore(state?.Weather);
        _pendingWeather.Clear();
        _noticedPhase = Phase; // a load raises no phase event and no weather event
    }

    // The clock is a function of the tick, so a step only notices a change of phase and tells it once; the weather ends a spell at its tick and tells a change of kind once.
    private void StepAtmosphere()
    {
        foreach (var index in _pendingWeather)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.WeatherChanged, Tick, Detail: index));
        }

        _pendingWeather.Clear();
        if (_weather.Advance(Tick))
        {
            _events.Add(new SimulationEvent(SimulationEventKind.WeatherChanged, Tick, Detail: _weather.Data.IndexOf(_weather.State.Current)));
        }

        var phase = Phase;
        if (phase != _noticedPhase)
        {
            _noticedPhase = phase;
            _events.Add(new SimulationEvent(SimulationEventKind.DayPhaseChanged, Tick, Detail: (int)phase));
        }
    }

    // The clock's offset and held hour, then the weather's state, are part of the state. A new game's state adds nothing, so the digests of runs that never slept, held a
    // clock or saw the weather change are as they were.
    private void AppendAtmosphereDigest(IncrementalHash hash)
    {
        var state = _clock.State;
        if (!state.IsDefault)
        {
            Span<byte> buffer = stackalloc byte[8 + 1 + 8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, state.Offset);
            buffer[8] = state.FrozenHour is null ? (byte)0 : (byte)1;
            BinaryPrimitives.WriteInt64LittleEndian(buffer[9..], BitConverter.DoubleToInt64Bits(state.FrozenHour ?? 0));
            hash.AppendData(buffer);
        }

        if (_weather.IsInitial)
        {
            return;
        }

        var weather = _weather.State;
        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(weather.Current + "\n" + weather.Previous + "\n"));
        Span<byte> numbers = stackalloc byte[8 + 8 + 4 + 4 + 1];
        BinaryPrimitives.WriteInt64LittleEndian(numbers, weather.SpellStart);
        BinaryPrimitives.WriteInt64LittleEndian(numbers[8..], weather.SpellEnd);
        BinaryPrimitives.WriteInt32LittleEndian(numbers[16..], weather.Spells);
        BinaryPrimitives.WriteInt32LittleEndian(numbers[20..], weather.Mornings);
        numbers[24] = weather.Held ? (byte)1 : (byte)0;
        hash.AppendData(numbers);
    }
}
