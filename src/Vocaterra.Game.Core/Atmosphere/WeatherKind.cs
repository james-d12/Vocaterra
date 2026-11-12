namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// One kind of weather as a row of data (<c>game/weather.json</c>, E04, D-game-31): its id, its channels, how long a spell of it lasts, what may follow it and how
/// likely it is on waking. No code names a kind: Core blends the channels and every consumer reads the snapshot.
/// </summary>
/// <param name="Id">The id, lowercase kebab-case.</param>
/// <param name="Channels">The numbers it sets.</param>
/// <param name="MinSeconds">The shortest a spell lasts, in seconds.</param>
/// <param name="MaxSeconds">The longest a spell lasts, in seconds.</param>
/// <param name="Next">The kinds that may follow, weighted.</param>
/// <param name="MorningWeight">The weight of this kind in the draw made on waking (0 for never).</param>
/// <param name="Say">The dialogue keys of the answer to "what is the weather", or <see langword="null"/> when the data has none.</param>
public sealed record WeatherKind(string Id, WeatherChannels Channels, double MinSeconds, double MaxSeconds, IReadOnlyList<WeightedKind> Next, int MorningWeight, WeatherSay? Say = null);
