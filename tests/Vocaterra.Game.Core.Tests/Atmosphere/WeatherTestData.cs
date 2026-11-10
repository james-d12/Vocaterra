using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary>Weather data the weather tests share: the shipped file, and a copy with a fourth kind that only the tests define.</summary>
internal static class WeatherTestData
{
    /// <summary>The shipped data (the owner's first version).</summary>
    public static WeatherData Shipped => Vocaterra.Game.Core.Tests.TestSupport.TestGame.Data.Weather;

    /// <summary>The shipped kinds plus a <c>mist</c> with channels and a precipitation style of its own, reachable from the cloudy kind and from the morning draw.</summary>
    public static WeatherData WithMist()
    {
        var mist = new WeatherKind("mist", new WeatherChannels(0.6, 0.8, 0.95, 0.05, "drizzle", 0.2), 100, 200, [new WeightedKind("sunny", 1)], 2);
        var kinds = Shipped.Kinds.Select(static k => k.Id == "cloudy" ? k with { Next = [.. k.Next, new WeightedKind("mist", 4)] } : k).Append(mist).ToList();
        return Shipped with { Kinds = kinds };
    }

    /// <summary>A short, quick data set for the tests that run many spells: 10 to 20 second spells and a 5 second transition.</summary>
    public static WeatherData Quick(WeatherData source) =>
        source with { TransitionSeconds = 5, Kinds = source.Kinds.Select(static k => k with { MinSeconds = 10, MaxSeconds = 20 }).ToList() };
}
