using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Light;

/// <summary>
/// The numbers of the light round the player (<c>game/light.json</c>, I05b, inventory plan 3.8): how the <c>light</c> stat becomes a radius and a strength, the colour of the light, and the daylight the light fades in and out between.
/// The renderer only draws what <see cref="PlayerLight.Of"/> says; retuning the light is editing this file.
/// </summary>
/// <param name="RadiusBase">The reach, in blocks, of the faintest light (any light above zero).</param>
/// <param name="RadiusPerPoint">The blocks of reach each point of the stat adds.</param>
/// <param name="StrengthPerPoint">The strength each point of the stat adds, before the cap.</param>
/// <param name="MaxStrength">The strongest the light gets, 0 to 1 (the share of a face's own colour it is added at, next to the player).</param>
/// <param name="Colour">The light's colour, each channel 0 to 1.</param>
/// <param name="DarkAtDaylight">The share of daylight at the player (0 to 1) at or below which the light is at its full strength: a moonlit night in the open, a deep cave.</param>
/// <param name="OffAtDaylight">The share of daylight at or above which the light is off: the day in the open.</param>
public sealed record PlayerLightData(
    double RadiusBase,
    double RadiusPerPoint,
    double StrengthPerPoint,
    double MaxStrength,
    (double R, double G, double B) Colour,
    double DarkAtDaylight,
    double OffAtDaylight)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/light.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Reads the light's numbers from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static PlayerLightData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the data from the file's text. The shape is checked here; the numbers are <see cref="Problems"/>'.</summary>
    /// <param name="json">The contents of <c>game/light.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static PlayerLightData Parse(string json)
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

            var colour = root.GetProperty("colour");
            return new PlayerLightData(
                root.GetProperty("radiusBase").GetDouble(),
                root.GetProperty("radiusPerPoint").GetDouble(),
                root.GetProperty("strengthPerPoint").GetDouble(),
                root.GetProperty("maxStrength").GetDouble(),
                (colour.GetProperty("r").GetDouble(), colour.GetProperty("g").GetDouble(), colour.GetProperty("b").GetDouble()),
                root.GetProperty("darkAtDaylight").GetDouble(),
                root.GetProperty("offAtDaylight").GetDouble());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>Every problem of the numbers (a reach or strength out of range, a colour channel outside 0 to 1, the daylight thresholds out of order); empty when they hold together.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        Check(RadiusBase, 0, 32, "radiusBase", problems);
        Check(RadiusPerPoint, 0, 4, "radiusPerPoint", problems);
        Check(StrengthPerPoint, 0, 1, "strengthPerPoint", problems);
        Check(MaxStrength, 0, 1, "maxStrength", problems);
        Check(Colour.R, 0, 1, "colour.r", problems);
        Check(Colour.G, 0, 1, "colour.g", problems);
        Check(Colour.B, 0, 1, "colour.b", problems);
        Check(DarkAtDaylight, 0, 1, "darkAtDaylight", problems);
        Check(OffAtDaylight, 0, 1, "offAtDaylight", problems);
        if (DarkAtDaylight >= OffAtDaylight)
        {
            problems.Add($"{RelativePath}: darkAtDaylight ({DarkAtDaylight}) must be below offAtDaylight ({OffAtDaylight}).");
        }

        return problems;
    }

    private static void Check(double value, double min, double max, string name, List<string> problems)
    {
        if (!double.IsFinite(value) || value < min || value > max)
        {
            problems.Add($"{RelativePath}: {name} must be a number from {min} to {max}.");
        }
    }
}
