namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>The dialogue keys of a weather kind's answer (E04 carries them, E07 fills and validates them): one for the day phases and one for the night.</summary>
/// <param name="Day">The key used while the phase is not night.</param>
/// <param name="Night">The key used while the phase is night.</param>
public sealed record WeatherSay(string Day, string Night);
