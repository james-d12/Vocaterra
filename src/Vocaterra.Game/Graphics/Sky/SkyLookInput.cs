using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// What <see cref="SkyLook.At"/> needs to know about the moment (E02): the hour on the 24-hour dial, the daylight from 0 (night) to 1 (full day) and the phase. It is a record so it can
/// grow: the weather (E05) adds the clouds' cover, the sunlight and the fog, each with a default (fair weather) that leaves the look as the day cycle draws it.
/// </summary>
/// <param name="Hour">The hour on the 24-hour dial, 0 up to 24.</param>
/// <param name="Daylight">The daylight, 0 up to 1 (<see cref="DayClock.DaylightAt"/>).</param>
/// <param name="Phase">The phase of the day.</param>
/// <param name="CloudCover">The weather's cloud cover, 0 to 1 (<see cref="WeatherChannels.CloudCover"/>); fair weather is <see cref="SkyWeather.FairCover"/>.</param>
/// <param name="Sunlight">The weather's sunlight, 0 to 1; 1 is the clear day's.</param>
/// <param name="Fog">The weather's fog, 0 to 1; 0 is none.</param>
/// <param name="CloudAltitude">The height of the clouds' undersides in blocks, from the weather's data (<see cref="WeatherData.CloudAltitude"/>, B14).</param>
public readonly record struct SkyLookInput(double Hour, double Daylight, DayPhase Phase, double CloudCover = SkyWeather.FairCover, double Sunlight = 1, double Fog = 0, double CloudAltitude = CloudField.Altitude)
{
    /// <summary>The input for a snapshot of the atmosphere (E05): the clock's hour, daylight and phase, and the weather's cloud cover, sunlight and fog. This is what the game builds each frame.</summary>
    /// <param name="snapshot">The simulation's atmosphere now.</param>
    /// <param name="cloudAltitude">The height of the clouds' undersides, from the weather's data.</param>
    public static SkyLookInput At(in AtmosphereSnapshot snapshot, double cloudAltitude = CloudField.Altitude) => new(snapshot.Hour, snapshot.Daylight, snapshot.Phase, snapshot.Channels.CloudCover, snapshot.Channels.Sunlight, snapshot.Channels.Fog, cloudAltitude);

    /// <summary>The input for a tick, read from the clock (fair weather): its hour, its daylight and its phase.</summary>
    /// <param name="clock">The day clock of the simulation.</param>
    /// <param name="tick">The simulation's tick.</param>
    public static SkyLookInput At(DayClock clock, long tick)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new SkyLookInput(clock.HourAt(tick), clock.DaylightAt(tick), clock.PhaseAt(tick));
    }
}
