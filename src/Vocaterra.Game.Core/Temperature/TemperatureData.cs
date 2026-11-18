using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature's numbers (<c>game/temperature.json</c>, I06 and I06c): the terms that make up the place's temperature (each an id and its own parameters, built by the
/// <see cref="TemperatureTermRegistry"/>), what they share (the ground and the biomes' adjustments), the lag, and the bands the feeling is read against. All temperatures are whole tenths of a
/// degree Celsius. A bundle with no <c>game/temperature.json</c> does not load; <see cref="TemperatureValidator"/> checks the values.
/// </summary>
/// <param name="Terms">The terms, by id, with their rows; a term the code does not have is a validation error.</param>
/// <param name="Environment">The ground and the biomes' adjustments.</param>
/// <param name="Lag">How slowly the feeling follows the place.</param>
/// <param name="Bands">The bands, warmest first, each with a lower bound below the one before.</param>
public sealed record TemperatureData(IReadOnlyList<TemperatureTermSpec> Terms, TemperatureEnvironment Environment, TemperatureLag Lag, IReadOnlyList<TemperatureBand> Bands)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/temperature.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 2;

    /// <summary>The band a feeling falls in: the first (warmest) whose lower bound it reaches, else the coldest.</summary>
    /// <param name="feelingTenths">The feeling, in tenths of a degree.</param>
    public TemperatureBand BandFor(int feelingTenths)
    {
        foreach (var band in Bands)
        {
            if (feelingTenths >= band.FromTenths)
            {
                return band;
            }
        }

        return Bands[^1];
    }

    /// <summary>The most chill any band has (the cold icon's notches), 0 for no bands.</summary>
    public int MaxChill => Bands.Count == 0 ? 0 : Bands.Max(static b => b.Chill);

    /// <summary>The HUD line keys the bands name.</summary>
    public IEnumerable<string> LineKeys => Bands.Where(static b => b.Line is not null).Select(static b => b.Line!).Distinct(StringComparer.Ordinal);

    /// <summary>Loads <c>game/temperature.json</c> from a bundle.</summary>
    /// <param name="bundle">The bundle.</param>
    public static TemperatureData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Parses the file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="InvalidDataException">The text is not the file's shape. The values are the validator's.</exception>
    public static TemperatureData Parse(string json)
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

            var terms = root.GetProperty("terms").EnumerateArray().Select(static row => new TemperatureTermSpec(row.GetProperty("id").GetString() ?? string.Empty, row.Clone())).ToList();
            var ground = root.GetProperty("ground");
            var biomes = new SortedDictionary<string, BiomeEffect>(StringComparer.Ordinal);
            foreach (var row in root.GetProperty("biomes").EnumerateObject())
            {
                biomes[row.Name] = new BiomeEffect(Optional(row.Value, "offsetTenths", 0), Optional(row.Value, "daySwingPermille", 1000), Optional(row.Value, "weatherPermille", 1000));
            }

            return new TemperatureData(
                terms,
                new TemperatureEnvironment(ground.GetProperty("tenths").GetInt32(), ground.GetProperty("shallowBlocks").GetInt32(), ground.GetProperty("fadeBlocks").GetInt32(), biomes),
                new TemperatureLag(root.GetProperty("lag").GetProperty("secondsToSettle").GetInt32()),
                root.GetProperty("bands").EnumerateArray().Select(ParseBand).ToList());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static int Optional(JsonElement row, string name, int fallback) => row.TryGetProperty(name, out var value) ? value.GetInt32() : fallback;

    private static TemperatureBand ParseBand(JsonElement row) => new(
        row.GetProperty("id").GetString() ?? string.Empty,
        row.GetProperty("from").GetInt32(),
        row.GetProperty("walkFactor").GetDouble(),
        row.GetProperty("chill").GetInt32(),
        row.TryGetProperty("line", out var line) ? line.GetString() : null,
        row.TryGetProperty("heat", out var heat) ? heat.GetInt32() : 0,
        row.TryGetProperty("thirstFactor", out var thirst) ? thirst.GetDouble() : 1.0);
}
