using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Simulation;

public sealed partial class VoxelSimulation
{
    private DayClock _clock = new(DayData.Default);
    private DayPhase _noticedPhase;

    /// <summary>The time of day (E01): a function of <see cref="Tick"/> and the clock's offset. Read only for the session, the front-end and tests; a sleep moves it with <see cref="WakeUp"/>.</summary>
    public DayClock Clock => _clock;

    /// <summary>The phase the clock is in now.</summary>
    public DayPhase Phase => _clock.PhaseAt(Tick);

    /// <summary>
    /// A host action, like <see cref="TeleportTo"/>: a sleep or a collapse wakes the player in the morning (the data's wake hour). The jump raises no
    /// <see cref="SimulationEventKind.DayPhaseChanged"/>: a phase event is a boundary crossed by the running clock.
    /// </summary>
    public void WakeUp()
    {
        _clock.WakeUp(Tick);
        _noticedPhase = Phase;
    }

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

    // Starts the clock with the bundle's numbers (the default values when a simulation is built without game data), at the phase of tick 0.
    private void StartClock(DayData? data)
    {
        _clock = new DayClock(data ?? DayData.Default);
        _noticedPhase = Phase;
    }

    // The clock is a function of the tick, so a step only notices a change of phase and tells it once.
    private void StepAtmosphere()
    {
        var phase = Phase;
        if (phase != _noticedPhase)
        {
            _noticedPhase = phase;
            _events.Add(new SimulationEvent(SimulationEventKind.DayPhaseChanged, Tick, Detail: (int)phase));
        }
    }

    // The clock's offset and held hour are part of the state: offset, a flag and the hour. A new game's state adds nothing, so the digests of runs that never slept are as they were.
    private void AppendAtmosphereDigest(IncrementalHash hash)
    {
        var state = _clock.State;
        if (state.IsDefault)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[8 + 1 + 8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, state.Offset);
        buffer[8] = state.FrozenHour is null ? (byte)0 : (byte)1;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[9..], BitConverter.DoubleToInt64Bits(state.FrozenHour ?? 0));
        hash.AppendData(buffer);
    }
}
