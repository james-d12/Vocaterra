using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The weather as saved, deterministic world state (E04, D-game-31). It is in one kind at a time for a spell; when the tick reaches the spell's end it draws the next kind
/// by the current kind's weights and the new spell's length from its range, both from <see cref="WorldRng"/> keyed by the world seed, a fixed pass name and a counter, so the
/// same seed gives the same weather and nothing else is random. A restored game long past a spell's end changes once, at the next tick, not once per tick missed.
/// <see cref="BlendAt"/> says how far the channels have moved from the previous kind to the current one. Kinds are rows of <see cref="WeatherData"/>: nothing here names one.
/// </summary>
public sealed class WeatherSystem
{
    private const string SpellPass = "weather";
    private const string StartPass = "weather-start";
    private const string MorningPass = "weather-morning";
    private const string ReleasePass = "weather-release";

    private readonly ulong _seed;
    private readonly long _transitionTicks;
    private readonly WeatherState _initial;
    private WeatherState _state;

    /// <summary>Starts the weather in its first spell, as a new game has it.</summary>
    /// <param name="data">The kinds and the timing.</param>
    /// <param name="seed">The world seed.</param>
    public WeatherSystem(WeatherData data, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        _seed = seed;
        _transitionTicks = (long)Math.Round(data.TransitionSeconds * DayClock.TicksPerSecond, MidpointRounding.AwayFromZero);
        var start = data.Kind(data.Start);
        _initial = new WeatherState(start.Id, start.Id, 0, SpellTicks(start, WorldRng.For(seed, StartPass)), 0, 0, false);
        _state = _initial;
    }

    /// <summary>The kinds and the timing.</summary>
    public WeatherData Data { get; }

    /// <summary>What the weather keeps: the state of a new game until something changes it.</summary>
    public WeatherState State => _state;

    /// <summary>Whether the state is the one of a new game (the digest and the save add nothing for it).</summary>
    public bool IsInitial => _state == _initial;

    /// <summary>The kind now.</summary>
    public WeatherKind Current => Data.Kind(_state.Current);

    /// <summary>The kind the transition leaves.</summary>
    public WeatherKind Previous => Data.Kind(_state.Previous);

    /// <summary>Whether a test or a screenshot holds the kind.</summary>
    public bool IsHeld => _state.Held;

    /// <summary>
    /// How far the channels have moved from the previous kind to the current one at a tick: 0 to 1 across the transition from the spell's start, and 1 when nothing is being
    /// left (a cut: a morning, a forced kind, the first spell, or a spell of the same kind again).
    /// </summary>
    /// <param name="tick">The simulation's tick.</param>
    public double BlendAt(long tick)
    {
        if (_transitionTicks <= 0 || string.Equals(_state.Previous, _state.Current, StringComparison.Ordinal))
        {
            return 1;
        }

        return Math.Clamp((tick - _state.SpellStart) / (double)_transitionTicks, 0, 1);
    }

    /// <summary>The blended channels at a tick.</summary>
    /// <param name="tick">The simulation's tick.</param>
    public WeatherChannels ChannelsAt(long tick) => WeatherChannels.Blend(Previous.Channels, Current.Channels, BlendAt(tick));

    /// <summary>Ends the spell when the tick has reached its end: draws the next kind and the new length. A held kind stays.</summary>
    /// <param name="tick">The simulation's tick.</param>
    /// <returns>Whether the kind changed.</returns>
    public bool Advance(long tick)
    {
        if (_state.Held || tick < _state.SpellEnd)
        {
            return false;
        }

        var spells = _state.Spells + 1;
        var rng = WorldRng.For(_seed, SpellPass, spells);
        var current = Current;
        var next = current.Next.Count == 0 ? current : Data.Kind(Pick(current.Next.Select(static n => (n.Id, n.Weight)).ToList(), rng));
        _state = new WeatherState(next.Id, current.Id, tick, tick + SpellTicks(next, rng), spells, _state.Mornings, false);
        return !string.Equals(current.Id, next.Id, StringComparison.Ordinal);
    }

    /// <summary>
    /// Draws the weather of a morning by the kinds' morning weights and starts it with no transition (a sleep is a cut). A held kind stays.
    /// </summary>
    /// <param name="tick">The simulation's tick.</param>
    /// <returns>Whether the kind changed.</returns>
    public bool RollMorning(long tick)
    {
        if (_state.Held)
        {
            return false;
        }

        var mornings = _state.Mornings + 1;
        var rng = WorldRng.For(_seed, MorningPass, mornings);
        var kind = Data.Kind(Pick(Data.Kinds.Where(static k => k.MorningWeight > 0).Select(static k => (k.Id, k.MorningWeight)).ToList(), rng));
        var changed = !string.Equals(kind.Id, _state.Current, StringComparison.Ordinal);
        _state = new WeatherState(kind.Id, kind.Id, tick, tick + SpellTicks(kind, rng), _state.Spells, mornings, false);
        return changed;
    }

    /// <summary>Holds a kind, with no transition, until <see cref="Release"/> (for tests and screenshots).</summary>
    /// <param name="id">The kind's id.</param>
    /// <param name="tick">The simulation's tick.</param>
    /// <returns>Whether the kind changed.</returns>
    /// <exception cref="ArgumentException">There is no such kind.</exception>
    public bool Force(string id, long tick)
    {
        var kind = Data.Kind(id);
        var changed = !string.Equals(kind.Id, _state.Current, StringComparison.Ordinal);
        _state = new WeatherState(kind.Id, kind.Id, tick, long.MaxValue, _state.Spells, _state.Mornings, true);
        return changed;
    }

    /// <summary>Lets a held kind run on: its spell starts again at this tick with a length drawn for it. The kind does not change.</summary>
    /// <param name="tick">The simulation's tick.</param>
    public void Release(long tick)
    {
        if (!_state.Held)
        {
            return;
        }

        var spells = _state.Spells + 1;
        var kind = Current;
        _state = new WeatherState(kind.Id, kind.Id, tick, tick + SpellTicks(kind, WorldRng.For(_seed, ReleasePass, spells)), spells, _state.Mornings, false);
    }

    /// <summary>Puts a state back (a load, a sleep's restore).</summary>
    /// <param name="state">The state, or <see langword="null"/> for a new game's.</param>
    /// <exception cref="ArgumentOutOfRangeException">The state names an unknown kind or holds a counter or a tick that cannot be.</exception>
    public void Restore(WeatherState? state)
    {
        if (state is null)
        {
            _state = _initial;
            return;
        }

        if (!Data.Kinds.Any(k => k.Id == state.Current) || !Data.Kinds.Any(k => k.Id == state.Previous))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state.Current, "The weather names a kind the data does not have.");
        }

        if (state.Spells < 0 || state.Mornings < 0 || state.SpellStart < 0 || state.SpellEnd < state.SpellStart)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The weather's counters or ticks cannot be.");
        }

        _state = state;
    }

    private static string Pick(IReadOnlyList<(string Id, int Weight)> choices, WorldRng rng)
    {
        var roll = rng.NextInt(choices.Sum(static c => c.Weight));
        foreach (var (id, weight) in choices)
        {
            if (roll < weight)
            {
                return id;
            }

            roll -= weight;
        }

        return choices[^1].Id;
    }

    private static long SpellTicks(WeatherKind kind, WorldRng rng)
    {
        var min = Math.Max(1L, (long)Math.Round(kind.MinSeconds * DayClock.TicksPerSecond, MidpointRounding.AwayFromZero));
        var max = Math.Max(min, (long)Math.Round(kind.MaxSeconds * DayClock.TicksPerSecond, MidpointRounding.AwayFromZero));
        return min + (long)(rng.NextUInt64() % (ulong)(max - min + 1));
    }
}
