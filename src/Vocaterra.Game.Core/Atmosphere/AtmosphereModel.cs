namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>Builds the <see cref="AtmosphereSnapshot"/> from the clock and the weather (E04).</summary>
public static class AtmosphereModel
{
    /// <summary>The snapshot at a tick.</summary>
    /// <param name="clock">The day clock.</param>
    /// <param name="weather">The weather.</param>
    /// <param name="tick">The simulation's tick.</param>
    public static AtmosphereSnapshot At(DayClock clock, WeatherSystem weather, long tick)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(weather);
        var phase = clock.PhaseAt(tick);
        var kind = weather.Current;
        var say = kind.Say is null ? null : (phase == DayPhase.Night ? kind.Say.Night : kind.Say.Day);
        return new AtmosphereSnapshot(clock.HourAt(tick), phase, clock.DaylightAt(tick), weather.ChannelsAt(tick), kind.Id, say);
    }
}
