using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature's numbers (<c>game/temperature.json</c>, I06): what each term uses and the bands the feeling is read against. All temperatures are whole tenths of a degree
/// Celsius. A bundle with no <c>game/temperature.json</c> does not load; <see cref="TemperatureValidator"/> checks the values. A term the biomes series adds brings its own data.
/// </summary>
/// <param name="BaseTenths">The valley's base temperature.</param>
/// <param name="DayAmplitudeTenths">How far the full day goes above the base, and the full night below it.</param>
/// <param name="CloudCoolingTenths">The cooling of full cloud cover.</param>
/// <param name="PrecipitationCoolingTenths">The cooling of precipitation at full intensity.</param>
/// <param name="WindCoolingTenths">The cooling of the strongest wind.</param>
/// <param name="Climate">How the world's climate cools the place (I06b).</param>
/// <param name="ShelterBonusTenths">The warmth of full cover overhead.</param>
/// <param name="ShelterOnlyInPrecipitation">Whether the cover warms only while something falls.</param>
/// <param name="FireRadiusBlocks">How far from a lit ring's middle its fire warms, in blocks across.</param>
/// <param name="FireBonusTenths">The warmth of a lit fire.</param>
/// <param name="Bands">The bands, warmest first, each with a lower bound below the one before.</param>
public sealed record TemperatureData(
    int BaseTenths,
    int DayAmplitudeTenths,
    int CloudCoolingTenths,
    int PrecipitationCoolingTenths,
    int WindCoolingTenths,
    ClimateCooling Climate,
    int ShelterBonusTenths,
    bool ShelterOnlyInPrecipitation,
    int FireRadiusBlocks,
    int FireBonusTenths,
    IReadOnlyList<TemperatureBand> Bands)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/temperature.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The band a feeling falls in: the warmest band whose lower bound it reaches, or the coldest band for a feeling below every bound.</summary>
    /// <param name="feelingTenths">The feeling in tenths of a degree.</param>
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

    /// <summary>The highest chill any band has (the cold icon's notches are out of it).</summary>
    public int MaxChill => Bands.Count == 0 ? 0 : Bands.Max(static b => b.Chill);

    /// <summary>The HUD line keys the bands name (each must be a row of <c>hudLines</c> and of the locale).</summary>
    public IEnumerable<string> LineKeys => Bands.Where(static b => b.Line is not null).Select(static b => b.Line!).Distinct(StringComparer.Ordinal);

    /// <summary>Reads the temperature from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static TemperatureData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the temperature from the file's text. The shape is checked here; the values are <see cref="TemperatureValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/temperature.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
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

            var weather = root.GetProperty("weather");
            var climate = root.GetProperty("climate");
            var shelter = root.GetProperty("shelter");
            var fire = root.GetProperty("fire");
            return new TemperatureData(
                root.GetProperty("base").GetProperty("tenths").GetInt32(),
                root.GetProperty("day").GetProperty("amplitudeTenths").GetInt32(),
                weather.GetProperty("cloudTenths").GetInt32(),
                weather.GetProperty("precipitationTenths").GetInt32(),
                weather.GetProperty("windTenths").GetInt32(),
                new ClimateCooling(climate.GetProperty("referenceThousandths").GetInt32(), climate.GetProperty("coolingTenthsPerRange").GetInt32(), climate.GetProperty("maxCoolingTenths").GetInt32()),
                shelter.GetProperty("bonusTenths").GetInt32(),
                shelter.GetProperty("onlyInPrecipitation").GetBoolean(),
                fire.GetProperty("radiusBlocks").GetInt32(),
                fire.GetProperty("bonusTenths").GetInt32(),
                root.GetProperty("bands").EnumerateArray().Select(ParseBand).ToList());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static TemperatureBand ParseBand(JsonElement row) => new(
        row.GetProperty("id").GetString() ?? string.Empty,
        row.GetProperty("from").GetInt32(),
        row.GetProperty("walkFactor").GetDouble(),
        row.GetProperty("chill").GetInt32(),
        row.TryGetProperty("line", out var line) ? line.GetString() : null);
}
