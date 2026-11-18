using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>
/// The biomes (<c>game/biomes.json</c>, B01, D-game-34): ordered rows, the first whose conditions hold wins, and the last must hold for every column. A new biome is a row; no id of
/// one is named in the source. A bundle with no <c>game/biomes.json</c> does not load. <see cref="BiomeValidator"/> checks the values.
/// </summary>
/// <param name="DitherTemperature">How far, in thousandths, the border jitter moves a column's temperature either way before the rows are tried.</param>
/// <param name="DitherHumidity">How far, in thousandths, it moves the humidity.</param>
/// <param name="Rows">The rows, in file order.</param>
public sealed record BiomeData(int DitherTemperature, int DitherHumidity, IReadOnlyList<BiomeRow> Rows)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/biomes.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The row with this id.</summary>
    /// <param name="id">The id.</param>
    /// <exception cref="ArgumentException">There is no such row.</exception>
    public BiomeRow Row(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal)) ?? throw new ArgumentException($"There is no biome '{id}'.", nameof(id));
    }

    /// <summary>Reads the biomes from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static BiomeData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the biomes from the file's text. The shape is checked here; the values are <see cref="BiomeValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/biomes.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static BiomeData Parse(string json)
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

            var dither = root.GetProperty("dither");
            var rows = root.GetProperty("biomes").EnumerateArray().Select(ParseRow).ToList();
            return new BiomeData(dither.GetProperty("temperature").GetInt32(), dither.GetProperty("humidity").GetInt32(), rows);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static BiomeRow ParseRow(JsonElement row, int index)
    {
        var trees = TreeRule.None;
        if (row.TryGetProperty("trees", out var treeRow))
        {
            trees = new TreeRule(
                treeRow.GetProperty("density").GetInt32(),
                [.. treeRow.GetProperty("species").EnumerateArray().Select(static e => new TreeEntry(
                    e.GetProperty("species").GetString() ?? string.Empty,
                    e.GetProperty("weight").GetInt32(),
                    e.TryGetProperty("crown", out var crown) ? crown.GetString() : null))]);
        }

        var decor = DecorRule.None;
        if (row.TryGetProperty("decor", out var decorRow))
        {
            decor = new DecorRule(
                decorRow.GetProperty("density").GetInt32(),
                [.. decorRow.GetProperty("blocks").EnumerateObject().Select(static p => new DecorEntry(p.Name, p.Value.GetInt32()))]);
        }

        return new BiomeRow(
            index,
            row.GetProperty("id").GetString() ?? string.Empty,
            row.TryGetProperty("when", out var when) ? ParseWhen(when) : BiomeConditions.None,
            row.GetProperty("top").GetString() ?? string.Empty,
            row.GetProperty("filler").GetString() ?? string.Empty,
            row.GetProperty("fillerDepth").GetInt32(),
            row.TryGetProperty("underwater", out var underwater) ? underwater.GetString() : null,
            trees,
            decor,
            row.TryGetProperty("snowCap", out var snow) && snow.GetBoolean(),
            row.TryGetProperty("baseTemperature", out var baseTemperature) ? baseTemperature.GetInt32() : null,
            row.TryGetProperty("surfaceStrategy", out var surface) ? surface.GetString() ?? string.Empty : StrategyRegistry.PlainSurface,
            row.TryGetProperty("decorStrategy", out var decorStrategy) ? decorStrategy.GetString() ?? string.Empty : StrategyRegistry.UndergrowthDecoration,
            row.TryGetProperty("ambient", out var ambient) ? [.. ambient.EnumerateObject().Select(static p => new BedShare(p.Name, p.Value.GetInt32()))] : null);
    }

    private static BiomeConditions ParseWhen(JsonElement when) => new(
        when.TryGetProperty("water", out var water) ? water.GetBoolean() : null,
        Range(when, "continentalness"),
        Range(when, "erosion"),
        Range(when, "ridges"),
        Range(when, "temperature"),
        Range(when, "humidity"),
        Range(when, "river"),
        Range(when, "height"),
        Range(when, "surfaceTemperature"));

    private static IntRange Range(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var range))
        {
            return IntRange.Any;
        }

        return new IntRange(
            range.TryGetProperty("min", out var min) ? min.GetInt32() : int.MinValue,
            range.TryGetProperty("max", out var max) ? max.GetInt32() : int.MaxValue);
    }
}
