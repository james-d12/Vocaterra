using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The weather's kinds and its timing (<c>game/weather.json</c>, E04, D-game-31): every kind is a row, the kind a new game starts in, and how long a change from one kind to
/// the next takes. A bundle with no <c>game/weather.json</c> does not load; a simulation built without game data uses <see cref="Default"/>. <see cref="WeatherValidator"/>
/// checks the values.
/// </summary>
/// <param name="Kinds">The kinds, in file order (a simulation event names a kind by its index here).</param>
/// <param name="Start">The id of the kind a new game starts in.</param>
/// <param name="TransitionSeconds">The seconds a change takes to blend the channels of the old kind into the new one's.</param>
public sealed record WeatherData(IReadOnlyList<WeatherKind> Kinds, string Start, double TransitionSeconds)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/weather.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>One neutral kind that never changes, used where a simulation is built with no game data (the only place of the source that names a kind).</summary>
    public static WeatherData Default { get; } = new(
        [new WeatherKind("clear", WeatherChannels.Dry(0.42, 1, 0, 0.2), 600, 600, [], 1)],
        "clear",
        0);

    /// <summary>The kind with this id.</summary>
    /// <param name="id">The id.</param>
    /// <exception cref="ArgumentException">There is no such kind.</exception>
    public WeatherKind Kind(string id) => Kinds[IndexOf(id)];

    /// <summary>The index of the kind with this id.</summary>
    /// <param name="id">The id.</param>
    /// <exception cref="ArgumentException">There is no such kind.</exception>
    public int IndexOf(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        for (var i = 0; i < Kinds.Count; i++)
        {
            if (string.Equals(Kinds[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentException($"There is no weather kind '{id}'.", nameof(id));
    }

    /// <summary>Reads the weather from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static WeatherData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the weather from the file's text. The shape is checked here; the values are <see cref="WeatherValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/weather.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static WeatherData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var kinds = root.GetProperty("kinds").EnumerateArray().Select(ParseKind).ToList();
            return new WeatherData(kinds, root.GetProperty("start").GetString() ?? string.Empty, root.GetProperty("transitionSeconds").GetDouble());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static WeatherKind ParseKind(JsonElement row)
    {
        var precipitationKind = string.Empty;
        var precipitationIntensity = 0.0;
        if (row.TryGetProperty("precipitation", out var precipitation))
        {
            precipitationKind = precipitation.GetProperty("kind").GetString() ?? string.Empty;
            precipitationIntensity = precipitation.GetProperty("intensity").GetDouble();
        }

        var next = new List<WeightedKind>();
        if (row.TryGetProperty("next", out var nextRow))
        {
            next.AddRange(nextRow.EnumerateObject().Select(static p => new WeightedKind(p.Name, p.Value.GetInt32())));
        }

        WeatherSay? say = null;
        if (row.TryGetProperty("say", out var sayRow))
        {
            say = new WeatherSay(sayRow.GetProperty("day").GetString() ?? string.Empty, sayRow.GetProperty("night").GetString() ?? string.Empty);
        }

        return new WeatherKind(
            row.GetProperty("id").GetString() ?? string.Empty,
            new WeatherChannels(
                row.GetProperty("cloudCover").GetDouble(),
                row.GetProperty("sunlight").GetDouble(),
                row.GetProperty("fog").GetDouble(),
                row.GetProperty("wind").GetDouble(),
                precipitationKind,
                precipitationIntensity),
            row.GetProperty("minSeconds").GetDouble(),
            row.GetProperty("maxSeconds").GetDouble(),
            next,
            row.GetProperty("morningWeight").GetInt32(),
            say);
    }
}
