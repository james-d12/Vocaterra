using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.World.Noise;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World;

/// <summary>The vertical range of the world and its sea (<c>vertical</c> of <c>game/world.json</c>): blocks, y from the floor to the ceiling.</summary>
/// <param name="MinY">The lowest block row (a multiple of 16: whole chunk layers).</param>
/// <param name="MaxY">One above the highest block row (a multiple of 16).</param>
/// <param name="SeaLevel">The water's surface: every column whose ground is below it fills to it.</param>
public sealed record VerticalRange(int MinY, int MaxY, int SeaLevel);

/// <summary>One climate field's noise: fractal noise from <see cref="Cell"/> blocks down over <see cref="Octaves"/> octaves, with an optional domain warp.</summary>
/// <param name="Cell">The first octave's cell in blocks.</param>
/// <param name="Octaves">The octaves (each halves the cell and the weight).</param>
/// <param name="WarpCell">The warp field's cell in blocks (read only when <paramref name="WarpBlocks"/> is above zero).</param>
/// <param name="WarpBlocks">The farthest the warp shifts a point, in blocks; 0 for no warp.</param>
public sealed record NoiseSpec(int Cell, int Octaves, int WarpCell, int WarpBlocks);

/// <summary>The climate fields and how temperature falls with height (<c>climate</c> of <c>game/world.json</c>).</summary>
/// <param name="LatticeBlocks">The spacing, in blocks, of the lattice the fields are sampled on and interpolated from.</param>
/// <param name="Continentalness">Sea, coast, inland and far inland.</param>
/// <param name="Erosion">Low is rugged and high; high is flat and low.</param>
/// <param name="Ridges">Ridged noise: the crests of the ranges (0 in the valleys).</param>
/// <param name="Temperature">The temperature at sea level.</param>
/// <param name="Humidity">The humidity.</param>
/// <param name="River">The river field: a river runs where it is near zero.</param>
/// <param name="LapsePer100Blocks">The fall of the temperature, in thousandths of its range, for every 100 blocks above the sea.</param>
public sealed record ClimateData(
    int LatticeBlocks,
    NoiseSpec Continentalness,
    NoiseSpec Erosion,
    NoiseSpec Ridges,
    NoiseSpec Temperature,
    NoiseSpec Humidity,
    NoiseSpec River,
    int LapsePer100Blocks);

/// <summary>The surface detail on top of the three splines.</summary>
/// <param name="HillsCell">The first hills octave's cell in blocks.</param>
/// <param name="HillsOctaves">The hills octaves.</param>
/// <param name="CalmAmplitude">The hills' amplitude in blocks where the range factor is 0 (eroded flats).</param>
/// <param name="RuggedAmplitude">The hills' amplitude in blocks where the range factor is 1.</param>
public sealed record DetailSpec(int HillsCell, int HillsOctaves, int CalmAmplitude, int RuggedAmplitude);

/// <summary>The terraces of the steep middle slopes.</summary>
/// <param name="Step">The shelf height in blocks (1 or less: none).</param>
/// <param name="FromHeight">The height where shelves start to show, in blocks.</param>
/// <param name="ToHeight">The height where they are fully formed, in blocks.</param>
/// <param name="RidgesFrom">The ridges value, in thousandths, where the shelves start to fade out towards the crest.</param>
/// <param name="RidgesTo">The ridges value, in thousandths, where they are gone (the crest is crags, not shelves).</param>
public sealed record TerraceSpec(int Step, int FromHeight, int ToHeight, int RidgesFrom, int RidgesTo);

/// <summary>The jagged crags of the highest ridges.</summary>
/// <param name="Amplitude">The crags' height in blocks.</param>
/// <param name="Cell">The crag noise's cell in blocks.</param>
/// <param name="RidgesFrom">The ridges value, in thousandths, where crags begin.</param>
/// <param name="RidgesTo">The ridges value, in thousandths, where they are full.</param>
/// <param name="HeightFrom">The height, in blocks, where crags begin.</param>
/// <param name="HeightTo">The height, in blocks, where they are full.</param>
public sealed record CragSpec(int Amplitude, int Cell, int RidgesFrom, int RidgesTo, int HeightFrom, int HeightTo);

/// <summary>The rivers: carved where the river field is near zero.</summary>
/// <param name="Width">How far from zero, in thousandths, the river field still carves.</param>
/// <param name="BedHeight">The bed's height in blocks (below the sea level).</param>
/// <param name="FadeFromHeight">The land height, in blocks, above which a river begins to fade out.</param>
/// <param name="FadeToHeight">The land height, in blocks, where it is gone.</param>
/// <param name="SeaFadeFrom">The continentalness, in thousandths, below which a river is gone (the sea needs no river).</param>
/// <param name="SeaFadeTo">The continentalness, in thousandths, above which a river is full.</param>
public sealed record RiverSpec(int Width, int BedHeight, int FadeFromHeight, int FadeToHeight, int SeaFadeFrom, int SeaFadeTo);

/// <summary>The lakes: inland basins pulled down towards a shallow bed where the lake field is high, the land low and the erosion calm.</summary>
/// <param name="Cell">The lake field's cell in blocks.</param>
/// <param name="Octaves">The lake field's octaves.</param>
/// <param name="Threshold">The lake field's value, in thousandths, where a lake begins.</param>
/// <param name="Softness">How far above the threshold, in thousandths, the lake is full.</param>
/// <param name="BedHeight">The bed's height in blocks (below the sea level).</param>
/// <param name="CalmFrom">The erosion, in thousandths, below which no lake forms.</param>
/// <param name="CalmTo">The erosion, in thousandths, above which a lake may be full.</param>
/// <param name="HeightFrom">The land height, in blocks, above which a lake begins to fade out.</param>
/// <param name="HeightTo">The land height, in blocks, where it is gone.</param>
/// <param name="InlandFrom">The continentalness, in thousandths, below which no lake forms.</param>
/// <param name="InlandTo">The continentalness, in thousandths, above which a lake may be full.</param>
public sealed record LakeSpec(int Cell, int Octaves, int Threshold, int Softness, int BedHeight, int CalmFrom, int CalmTo, int HeightFrom, int HeightTo, int InlandFrom, int InlandTo);

/// <summary>
/// The height model's data: the three splines (the base from the continentalness, the range factor from the erosion, the peaks from the ridges), the detail, the caps, and the terraces, crags,
/// rivers and lakes. A spline's inputs are thousandths of the field's range; the base's and the peaks' outputs are blocks; the range factor's output is thousandths (0 to 1 000).
/// </summary>
/// <param name="Base">The ground's base height by continentalness.</param>
/// <param name="RangeFactor">How much of the peaks shows, by erosion.</param>
/// <param name="Peaks">The height the ridges add (at full range factor), by ridges.</param>
/// <param name="Detail">The hills.</param>
/// <param name="SoftCapAbove">The height, in blocks, above which the ground rises at half rate.</param>
/// <param name="Clamp">The highest ground, in blocks.</param>
/// <param name="Terraces">The terraces.</param>
/// <param name="Crags">The crags.</param>
/// <param name="Rivers">The rivers.</param>
/// <param name="Lakes">The lakes.</param>
public sealed record HeightData(
    IReadOnlyList<SplinePoint> Base,
    IReadOnlyList<SplinePoint> RangeFactor,
    IReadOnlyList<SplinePoint> Peaks,
    DetailSpec Detail,
    int SoftCapAbove,
    int Clamp,
    TerraceSpec Terraces,
    CragSpec Crags,
    RiverSpec Rivers,
    LakeSpec Lakes);

/// <summary>The thresholds snow and trees follow, read against the climate temperature at the height in question (<see cref="Climate.IClimateSampler.TemperatureAt"/>).</summary>
/// <param name="SnowBelowTemperature">Snow lies where the temperature, in thousandths, is below this.</param>
/// <param name="TreeLineBelowTemperature">No tree grows where the temperature, in thousandths, is below this (the tree line).</param>
public sealed record AltitudeData(int SnowBelowTemperature, int TreeLineBelowTemperature);

/// <summary>
/// The procedural world's numbers (<c>game/world.json</c>, B01, D-game-34): the vertical range and the sea level, the climate fields, the height model's splines, and the snow and tree
/// thresholds. Every length is blocks and every field value thousandths of its range, so the file holds whole numbers only and the generation is integer maths. A bundle with no
/// <c>game/world.json</c> does not load. <see cref="WorldGenValidator"/> checks the values.
/// </summary>
/// <param name="Vertical">The vertical range and the sea level.</param>
/// <param name="Climate">The climate fields.</param>
/// <param name="Height">The height model.</param>
/// <param name="Altitude">The snow and tree-line thresholds.</param>
/// <param name="RegionBlend">How the land is eased towards an authored region's edge (the <c>regionBlend</c> section, B06; the default when the file has none).</param>
public sealed record WorldGenData(VerticalRange Vertical, ClimateData Climate, HeightData Height, AltitudeData Altitude, RegionBlendData? RegionBlend = null)
{
    /// <summary>The blend's numbers: the file's <c>regionBlend</c> section, or <see cref="Regions.RegionBlendData.Default"/>.</summary>
    public RegionBlendData Blend => RegionBlend ?? RegionBlendData.Default;

    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/world.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Reads the world's numbers from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static WorldGenData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the world's numbers from the file's text. The shape is checked here; the values are <see cref="WorldGenValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/world.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static WorldGenData Parse(string json)
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

            var vertical = root.GetProperty("vertical");
            var climate = root.GetProperty("climate");
            var height = root.GetProperty("height");
            var altitude = root.GetProperty("altitude");
            return new WorldGenData(
                new VerticalRange(Int(vertical, "minY"), Int(vertical, "maxY"), Int(vertical, "seaLevel")),
                new ClimateData(
                    Int(climate, "latticeBlocks"),
                    Noise(climate, "continentalness"),
                    Noise(climate, "erosion"),
                    Noise(climate, "ridges"),
                    Noise(climate, "temperature"),
                    Noise(climate, "humidity"),
                    Noise(climate, "river"),
                    Int(climate, "lapsePer100Blocks")),
                ParseHeight(height),
                new AltitudeData(Int(altitude, "snowBelowTemperature"), Int(altitude, "treeLineBelowTemperature")),
                RegionBlendData.Read(root));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static HeightData ParseHeight(JsonElement height)
    {
        var detail = height.GetProperty("detail");
        var terraces = height.GetProperty("terraces");
        var crags = height.GetProperty("crags");
        var rivers = height.GetProperty("rivers");
        var lakes = height.GetProperty("lakes");
        return new HeightData(
            Points(height, "base"),
            Points(height, "rangeFactor"),
            Points(height, "peaks"),
            new DetailSpec(Int(detail, "hillsCell"), Int(detail, "hillsOctaves"), Int(detail, "calmAmplitude"), Int(detail, "ruggedAmplitude")),
            Int(height, "softCapAbove"),
            Int(height, "clamp"),
            new TerraceSpec(Int(terraces, "step"), Int(terraces, "fromHeight"), Int(terraces, "toHeight"), Int(terraces, "ridgesFrom"), Int(terraces, "ridgesTo")),
            new CragSpec(Int(crags, "amplitude"), Int(crags, "cell"), Int(crags, "ridgesFrom"), Int(crags, "ridgesTo"), Int(crags, "heightFrom"), Int(crags, "heightTo")),
            new RiverSpec(Int(rivers, "width"), Int(rivers, "bedHeight"), Int(rivers, "fadeFromHeight"), Int(rivers, "fadeToHeight"), Int(rivers, "seaFadeFrom"), Int(rivers, "seaFadeTo")),
            new LakeSpec(
                Int(lakes, "cell"),
                Int(lakes, "octaves"),
                Int(lakes, "threshold"),
                Int(lakes, "softness"),
                Int(lakes, "bedHeight"),
                Int(lakes, "calmFrom"),
                Int(lakes, "calmTo"),
                Int(lakes, "heightFrom"),
                Int(lakes, "heightTo"),
                Int(lakes, "inlandFrom"),
                Int(lakes, "inlandTo")));
    }

    private static int Int(JsonElement owner, string name) => owner.GetProperty(name).GetInt32();

    private static NoiseSpec Noise(JsonElement owner, string name)
    {
        var field = owner.GetProperty(name);
        return new NoiseSpec(
            Int(field, "cell"),
            Int(field, "octaves"),
            field.TryGetProperty("warpCell", out var warpCell) ? warpCell.GetInt32() : 0,
            field.TryGetProperty("warpBlocks", out var warp) ? warp.GetInt32() : 0);
    }

    private static List<SplinePoint> Points(JsonElement owner, string name) =>
        [.. owner.GetProperty(name).EnumerateArray().Select(static p => new SplinePoint(p[0].GetInt32(), p[1].GetInt32()))];
}
