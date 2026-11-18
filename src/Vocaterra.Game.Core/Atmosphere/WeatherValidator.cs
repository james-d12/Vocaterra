using System.Text.RegularExpressions;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The checks of <c>game/weather.json</c> that need more than its shape (E04): ids unique and well formed, channels from 0 to 1, a precipitation kind exactly when the
/// intensity is above zero, spells positive and ordered, every <c>next</c> known and weighted, the start known, a kind to wake in, and a transition no longer than the
/// shortest spell. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class WeatherValidator
{
    private const string File = WeatherData.RelativePath;

    /// <summary>Every problem of the weather, in a fixed order; empty when the values hold together.</summary>
    /// <param name="weather">The weather.</param>
    public static IReadOnlyList<string> Validate(WeatherData weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        var problems = new List<string>();
        if (weather.Kinds.Count == 0)
        {
            problems.Add($"{File}: there must be at least one kind.");
            return problems;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in weather.Kinds)
        {
            if (!Id().IsMatch(kind.Id))
            {
                problems.Add($"{File}: the id '{kind.Id}' is not lowercase kebab-case.");
            }

            if (!ids.Add(kind.Id))
            {
                problems.Add($"{File}: the id '{kind.Id}' is used twice.");
            }
        }

        foreach (var kind in weather.Kinds)
        {
            CheckKind(weather, kind, ids, problems);
        }

        if (!ids.Contains(weather.Start))
        {
            problems.Add($"{File}: start '{weather.Start}' is not a kind.");
        }

        if (!weather.Kinds.Any(static k => k.MorningWeight > 0))
        {
            problems.Add($"{File}: at least one kind needs a morningWeight above zero.");
        }

        if (!double.IsFinite(weather.CloudAltitude) || weather.CloudAltitude < WeatherData.MinCloudAltitude || weather.CloudAltitude > WeatherData.MaxCloudAltitude)
        {
            problems.Add($"{File}: cloudAltitude must be from {WeatherData.MinCloudAltitude} to {WeatherData.MaxCloudAltitude} blocks (it is {weather.CloudAltitude}).");
        }

        var shortest = weather.Kinds.Min(static k => k.MinSeconds);
        if (!double.IsFinite(weather.TransitionSeconds) || weather.TransitionSeconds < 0 || weather.TransitionSeconds > shortest)
        {
            problems.Add($"{File}: transitionSeconds must be from 0 to the shortest spell ({shortest}) (it is {weather.TransitionSeconds}).");
        }

        return problems;
    }

    private static void CheckKind(WeatherData weather, WeatherKind kind, HashSet<string> ids, List<string> problems)
    {
        var c = kind.Channels;
        Unit(problems, kind, "cloudCover", c.CloudCover);
        Unit(problems, kind, "sunlight", c.Sunlight);
        Unit(problems, kind, "fog", c.Fog);
        Unit(problems, kind, "wind", c.Wind);
        Unit(problems, kind, "precipitation intensity", c.PrecipitationIntensity);
        var falls = double.IsFinite(c.PrecipitationIntensity) && c.PrecipitationIntensity > 0;
        if (falls && string.IsNullOrEmpty(c.PrecipitationKind))
        {
            problems.Add($"{File}: '{kind.Id}' has precipitation above zero and no kind of it.");
        }

        if (!falls && !string.IsNullOrEmpty(c.PrecipitationKind))
        {
            problems.Add($"{File}: '{kind.Id}' names the precipitation '{c.PrecipitationKind}' with no intensity.");
        }

        if (!double.IsFinite(kind.MinSeconds) || !double.IsFinite(kind.MaxSeconds) || kind.MinSeconds <= 0 || kind.MaxSeconds <= 0 || kind.MinSeconds > kind.MaxSeconds)
        {
            problems.Add($"{File}: '{kind.Id}' needs positive spells with minSeconds not above maxSeconds ({kind.MinSeconds}, {kind.MaxSeconds}).");
        }

        if (kind.Next.Count == 0 && weather.Kinds.Count > 1)
        {
            problems.Add($"{File}: '{kind.Id}' has no next kind.");
        }

        foreach (var next in kind.Next)
        {
            if (!ids.Contains(next.Id))
            {
                problems.Add($"{File}: '{kind.Id}' has the unknown next kind '{next.Id}'.");
            }

            if (next.Weight <= 0)
            {
                problems.Add($"{File}: the weight of '{next.Id}' after '{kind.Id}' must be positive (it is {next.Weight}).");
            }
        }

        if (kind.MorningWeight < 0)
        {
            problems.Add($"{File}: '{kind.Id}' has a negative morningWeight ({kind.MorningWeight}).");
        }
    }

    private static void Unit(List<string> problems, WeatherKind kind, string name, double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            problems.Add($"{File}: {name} of '{kind.Id}' must be from 0 to 1 (it is {value}).");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex Id();
}
