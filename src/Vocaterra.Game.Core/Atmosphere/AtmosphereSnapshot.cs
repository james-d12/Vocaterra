namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The sky and weather at one tick (E04): what every consumer (the sky, the light, the fog, the rain, the sound, an NPC's answer) reads, and never a kind's id for a decision.
/// </summary>
/// <param name="Hour">The hour on the 24-hour dial.</param>
/// <param name="Phase">The phase of the day.</param>
/// <param name="Daylight">The daylight, 0 to 1.</param>
/// <param name="Channels">The weather's channels, blended across a change of kind.</param>
/// <param name="KindId">The id of the kind now (for a log or a lookup of a say key, not for drawing).</param>
/// <param name="Say">The dialogue key of the kind for this phase (the night's while it is night, else the day's), or <see langword="null"/> when the data has none.</param>
public readonly record struct AtmosphereSnapshot(double Hour, DayPhase Phase, double Daylight, WeatherChannels Channels, string KindId, string? Say);
