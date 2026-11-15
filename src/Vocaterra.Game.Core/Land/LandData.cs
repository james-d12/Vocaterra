using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Land;

/// <summary>A disc round a scene site where placing is refused (<c>protectedSites</c>).</summary>
/// <param name="Site">The site's name in <c>scene.json</c>.</param>
/// <param name="Protection">Which hint the player reads.</param>
/// <param name="Margin">Blocks added to the site's radius (and blend).</param>
/// <param name="IncludeBlend">Whether the site's blend is part of the disc.</param>
public sealed record LandSite(string Site, Protection Protection, int Margin, bool IncludeBlend);

/// <summary>Columns within a square margin of a column that holds one of the blocks, where placing is refused (<c>protectedBlocks</c>).</summary>
/// <param name="Blocks">The block names (<see cref="Voxel.BlockDefinition.Name"/>).</param>
/// <param name="Protection">Which hint the player reads.</param>
/// <param name="Margin">Blocks of margin on every side.</param>
public sealed record LandBlocks(IReadOnlyList<string> Blocks, Protection Protection, int Margin);

/// <summary>A disc round a scene site where a plot may lie (<c>eligible</c>).</summary>
/// <param name="Site">The site's name in <c>scene.json</c>.</param>
/// <param name="Radius">The disc's radius in blocks.</param>
public sealed record LandMeadow(string Site, int Radius);

/// <summary>
/// The land rules' numbers (<c>game/land.json</c>, Y09, D-game-32 (c)): where placing is refused, where a plot may lie, and the plot's size, price and slope.
/// <see cref="LandRules"/> turns them and the scene into a mask; <see cref="LandValidator"/> checks the values against the scene. A bundle with no <c>game/land.json</c> does not load.
/// </summary>
/// <param name="ProtectedSites">The protected discs, in the file's order (the first that holds a column names its protection).</param>
/// <param name="ProtectedBlocks">The protected squares round blocks, after the discs in the same order of precedence.</param>
/// <param name="Eligible">The meadows: a plot lies wholly in one of them.</param>
/// <param name="PlotWidth">The plot's east-west size in blocks.</param>
/// <param name="PlotDepth">The plot's north-south size in blocks.</param>
/// <param name="PlotPrice">The plot's price in coins (the mayor's, Y11).</param>
/// <param name="MaxSlope">The most the plot's highest and lowest ground may differ, in blocks.</param>
public sealed record LandData(
    IReadOnlyList<LandSite> ProtectedSites,
    IReadOnlyList<LandBlocks> ProtectedBlocks,
    IReadOnlyList<LandMeadow> Eligible,
    int PlotWidth,
    int PlotDepth,
    int PlotPrice,
    int MaxSlope)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/land.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Reads the rules from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static LandData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the rules from the file's text. The shape is checked here; the values are <see cref="LandValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/land.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static LandData Parse(string json)
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

            var sites = root.GetProperty("protectedSites").EnumerateArray()
                .Select(static e => new LandSite(Text(e, "site"), ProtectionOf(e), e.GetProperty("margin").GetInt32(), e.TryGetProperty("includeBlend", out var blend) && blend.GetBoolean()))
                .ToList();
            var blocks = root.GetProperty("protectedBlocks").EnumerateArray()
                .Select(static e => new LandBlocks([.. e.GetProperty("blocks").EnumerateArray().Select(static b => b.GetString() ?? string.Empty)], ProtectionOf(e), e.GetProperty("margin").GetInt32()))
                .ToList();
            var eligible = root.GetProperty("eligible").EnumerateArray()
                .Select(static e => new LandMeadow(Text(e, "site"), e.GetProperty("radius").GetInt32()))
                .ToList();
            var plot = root.GetProperty("plot");
            return new LandData(
                sites,
                blocks,
                eligible,
                plot.GetProperty("width").GetInt32(),
                plot.GetProperty("depth").GetInt32(),
                plot.GetProperty("price").GetInt32(),
                plot.GetProperty("maxSlope").GetInt32());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;

    private static Protection ProtectionOf(JsonElement element)
    {
        var name = Text(element, "protection");
        return ProtectionNames.Parse(name) ?? throw new InvalidDataException($"{RelativePath}: the protection '{name}' is none of village, camp, road.");
    }
}
