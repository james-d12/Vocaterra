using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>One kind of rock blob in the stone layer: where a 3D noise field passes the threshold, the block replaces the layer's stone.</summary>
/// <param name="Block">The block's name (a name of <see cref="Voxel.BlockDefinition"/>).</param>
/// <param name="Cell">The noise's cell across, in blocks.</param>
/// <param name="CellY">The noise's cell up, in blocks.</param>
/// <param name="Octaves">The octaves (each halves the cell and the weight).</param>
/// <param name="Threshold">The noise value, in thousandths, above which the blob is (higher is rarer and smaller).</param>
public sealed record BlobSpec(string Block, int Cell, int CellY, int Octaves, int Threshold);

/// <summary>
/// How far the underground reaches into the valley's own stone (B13): the strata, the ores and the caves are laid into the valley's stone only where every block is buried. All distances are in
/// blocks and measured as a cube (the largest of the three axes), so a ball of the same radius is kept clear too.
/// </summary>
/// <param name="SurfaceDistance">How far a changed block stays from any air cell of the valley (the sky, a house's room, a cliff face).</param>
/// <param name="WaterDistance">How far a changed block stays from any water cell, so water never pours in (the lake bed, the river's bed, the falls).</param>
/// <param name="BorderMargin">How many columns in from the valley's edge nothing changes (the land outside is procedural and has its own caves).</param>
/// <param name="SiteMargin">How far a changed block stays from a site's level ground, a named point, an NPC or the spawn (measured from the surface there).</param>
/// <param name="PathMargin">How far a changed block stays from a trodden path.</param>
/// <param name="PropMargin">How far a changed block stays from a block of a prop (a house, a well, a bridge, a lamp).</param>
/// <param name="VillageSite">The site the village stands round (its level ground and its blend are the village's footprint).</param>
/// <param name="VillageMargin">How far a changed block stays from the village's footprint.</param>
/// <param name="PassPaths">The paths of the pass (the cutting through the ring to the east edge).</param>
/// <param name="PassProps">The props of the pass (the rockfall).</param>
/// <param name="PassMargin">How far a changed block stays from the pass's paths and props.</param>
public sealed record ValleySpec(
    int SurfaceDistance,
    int WaterDistance,
    int BorderMargin,
    int SiteMargin,
    int PathMargin,
    int PropMargin,
    string VillageSite,
    int VillageMargin,
    IReadOnlyList<string> PassPaths,
    IReadOnlyList<string> PassProps,
    int PassMargin);

/// <summary>
/// The rock layers: stone from the surface down to <see cref="DitherToY"/>, the deep rock below <see cref="DitherFromY"/> and between them a change dithered cell by cell (the share of deep rock
/// falls in a straight line from all at <see cref="DitherFromY"/> to none at <see cref="DitherToY"/>), so the boundary is ragged. Blobs of other rock lie in the stone layer.
/// </summary>
/// <param name="Stone">The ordinary rock's block name.</param>
/// <param name="Deep">The deep rock's block name.</param>
/// <param name="DitherFromY">The highest y that is always deep rock is just below this; the dither starts here.</param>
/// <param name="DitherToY">The lowest y that is always stone; the dither ends here.</param>
/// <param name="BlobLattice">The spacing, in blocks, of the lattice the blob noise is sampled on and interpolated from.</param>
/// <param name="Blobs">The blobs, first match wins.</param>
public sealed record StrataSpec(string Stone, string Deep, int DitherFromY, int DitherToY, int BlobLattice, IReadOnlyList<BlobSpec> Blobs);

/// <summary>The unbreakable floor: bedrock from <see cref="BottomY"/> up, <see cref="MinThickness"/> to <see cref="MaxThickness"/> blocks thick, ragged column by column.</summary>
/// <param name="Block">The floor's block name.</param>
/// <param name="BottomY">The y of the floor's lowest block (the world's own bottom).</param>
/// <param name="MinThickness">The thinnest the floor is, in blocks.</param>
/// <param name="MaxThickness">The thickest the floor is, in blocks.</param>
public sealed record FloorSpec(string Block, int BottomY, int MinThickness, int MaxThickness);

/// <summary>
/// The cheese caverns: large chambers where one 3D noise field passes a threshold that rises with height, so they lie deep. The threshold is <see cref="DeepThreshold"/> at and below
/// <see cref="DeepY"/>, <see cref="ShallowThreshold"/> at and above <see cref="ShallowY"/>, and a straight line between.
/// </summary>
/// <param name="Cell">The noise's cell across, in blocks.</param>
/// <param name="CellY">The noise's cell up, in blocks.</param>
/// <param name="Octaves">The octaves.</param>
/// <param name="DeepThreshold">The threshold, in thousandths, deep down.</param>
/// <param name="DeepY">The height at and below which the threshold is the deep one.</param>
/// <param name="ShallowThreshold">The threshold, in thousandths, high up (1 000 or more: none).</param>
/// <param name="ShallowY">The height at and above which the threshold is the shallow one.</param>
public sealed record CheeseSpec(int Cell, int CellY, int Octaves, int DeepThreshold, int DeepY, int ShallowThreshold, int ShallowY);

/// <summary>The spaghetti tunnels: long winding tunnels where two 3D noise fields are both within <see cref="Thickness"/> of zero.</summary>
/// <param name="Cell">The noise's cell across, in blocks.</param>
/// <param name="CellY">The noise's cell up, in blocks.</param>
/// <param name="Octaves">The octaves.</param>
/// <param name="Thickness">How far from zero, in thousandths, each field may be: the tunnel's width.</param>
public sealed record SpaghettiSpec(int Cell, int CellY, int Octaves, int Thickness);

/// <summary>
/// The caves: both kinds are sampled on a coarse lattice and interpolated. A cave's top is kept <see cref="SurfaceCover"/> blocks under the ground (except at an entrance) and at or below
/// <see cref="TopY"/>; under an authored region it stays at or below <see cref="UnderRegionCeiling"/>; and it stays solid within <see cref="WaterMargin"/> blocks of any water cell and
/// <see cref="BedClearance"/> blocks under the bed of a water column.
/// </summary>
/// <param name="LatticeXz">The lattice spacing across, in blocks.</param>
/// <param name="LatticeY">The lattice spacing up, in blocks.</param>
/// <param name="TopY">The highest y a cave reaches.</param>
/// <param name="SurfaceCover">The solid blocks kept between a cave's top and the ground.</param>
/// <param name="Cheese">The caverns.</param>
/// <param name="Spaghetti">The tunnels.</param>
/// <param name="UnderRegionCeiling">The highest y a cave reaches under an authored region (the region's own chunks start at 0).</param>
/// <param name="WaterMargin">How many blocks round a water cell stay solid.</param>
/// <param name="BedClearance">How many blocks under the bed of a water column stay solid.</param>
public sealed record CaveSpec(
    int LatticeXz,
    int LatticeY,
    int TopY,
    int SurfaceCover,
    CheeseSpec Cheese,
    SpaghettiSpec Spaghetti,
    int UnderRegionCeiling,
    int WaterMargin,
    int BedClearance);

/// <summary>
/// The cave entrances: the places where a tunnel may open to the sky. A column is in an entrance zone where a 2D noise field passes the threshold, and the zone opens only on dry procedural land
/// (the ground at least <see cref="MinHeightAboveSea"/> over the sea, no water within <see cref="WaterReach"/> columns, outside every region and its margin) and only over a cave: a shaft is cut
/// from the ground down to the highest cave air within <see cref="ProbeDepth"/> blocks under the cave's top.
/// </summary>
/// <param name="Cell">The mask noise's cell, in blocks.</param>
/// <param name="Octaves">The octaves.</param>
/// <param name="Threshold">The noise value, in thousandths, above which a column is in a zone.</param>
/// <param name="MinHeightAboveSea">How far over the sea the ground must be.</param>
/// <param name="WaterReach">How many columns round an entrance hold no water.</param>
/// <param name="ProbeDepth">How far under a cave's top, in blocks, the shaft looks for cave air.</param>
public sealed record EntranceSpec(int Cell, int Octaves, int Threshold, int MinHeightAboveSea, int WaterReach, int ProbeDepth);

/// <summary>Lava: cave air at or below the level is lava.</summary>
/// <param name="Block">The lava's block name.</param>
/// <param name="Level">The highest y that holds lava.</param>
public sealed record LavaSpec(string Block, int Level);

/// <summary>One band of an ore: where its veins' centres lie (a triangular spread over the range with its peak) and how many a chunk column tries.</summary>
/// <param name="MinY">The lowest y.</param>
/// <param name="MaxY">The highest y.</param>
/// <param name="PeakY">The y the spread peaks at.</param>
/// <param name="Veins">The veins tried in a chunk column (16 x 16 blocks, every layer), their heights drawn from the spread.</param>
public sealed record OreBand(int MinY, int MaxY, int PeakY, int Veins);

/// <summary>One ore (a row of <c>ores</c>): its block, the rocks its veins may replace, their size and its bands.</summary>
/// <param name="Id">The row's id: also the key of its random draws.</param>
/// <param name="Block">The ore's block name.</param>
/// <param name="Replaces">The block names its veins may replace; no other block (air, water, an ore) is ever replaced.</param>
/// <param name="SizeMin">The fewest blocks a vein tries to hold.</param>
/// <param name="SizeMax">The most blocks a vein tries to hold.</param>
/// <param name="Bands">The bands.</param>
public sealed record OreSpec(string Id, string Block, IReadOnlyList<string> Replaces, int SizeMin, int SizeMax, IReadOnlyList<OreBand> Bands);

/// <summary>
/// The underground (<c>game/underground.json</c>, B11, D-game-34): the rock layers and their blobs, the bedrock floor, the ores, the caves and their entrances, and the lava. Every number is
/// a whole number (lengths in blocks, noise values in thousandths of their range), so the generation is integer maths. A new ore or rock is a row; no id of one is named in the source.
/// <see cref="UndergroundValidator"/> checks the values. A bundle without the file has no underground.
/// </summary>
/// <param name="Strata">The rock layers.</param>
/// <param name="Floor">The bedrock floor.</param>
/// <param name="Caves">The caves.</param>
/// <param name="Entrances">The entrances.</param>
/// <param name="Lava">The lava.</param>
/// <param name="Ores">The ores.</param>
/// <param name="Valley">How far the underground reaches into the valley's own stone (B13), or <see langword="null"/> for a valley of plain stone.</param>
public sealed record UndergroundData(StrataSpec Strata, FloorSpec Floor, CaveSpec Caves, EntranceSpec Entrances, LavaSpec Lava, IReadOnlyList<OreSpec> Ores, ValleySpec? Valley = null)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/underground.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Reads the underground from the bundle's verified host file, or <see langword="null"/> when the bundle has none.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The file is malformed.</exception>
    public static UndergroundData? Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.TryReadHostFile(RelativePath, out _) ? Parse(GameData.HostText(bundle, RelativePath)) : null;
    }

    /// <summary>Builds the underground from the file's text. The shape is checked here; the values are <see cref="UndergroundValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/underground.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static UndergroundData Parse(string json)
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

            var strata = root.GetProperty("strata");
            var floor = root.GetProperty("floor");
            var caves = root.GetProperty("caves");
            var cheese = caves.GetProperty("cheese");
            var spaghetti = caves.GetProperty("spaghetti");
            var entrances = root.GetProperty("entrances");
            var lava = root.GetProperty("lava");
            return new UndergroundData(
                new StrataSpec(
                    Text(strata, "stone"),
                    Text(strata, "deep"),
                    Int(strata, "ditherFromY"),
                    Int(strata, "ditherToY"),
                    Int(strata, "blobLattice"),
                    [.. strata.GetProperty("blobs").EnumerateArray().Select(static b => new BlobSpec(Text(b, "block"), Int(b, "cell"), Int(b, "cellY"), Int(b, "octaves"), Int(b, "threshold")))]),
                new FloorSpec(Text(floor, "block"), Int(floor, "bottomY"), Int(floor, "minThickness"), Int(floor, "maxThickness")),
                new CaveSpec(
                    Int(caves, "latticeXz"),
                    Int(caves, "latticeY"),
                    Int(caves, "topY"),
                    Int(caves, "surfaceCover"),
                    new CheeseSpec(Int(cheese, "cell"), Int(cheese, "cellY"), Int(cheese, "octaves"), Int(cheese, "deepThreshold"), Int(cheese, "deepY"), Int(cheese, "shallowThreshold"), Int(cheese, "shallowY")),
                    new SpaghettiSpec(Int(spaghetti, "cell"), Int(spaghetti, "cellY"), Int(spaghetti, "octaves"), Int(spaghetti, "thickness")),
                    Int(caves, "underRegionCeiling"),
                    Int(caves, "waterMargin"),
                    Int(caves, "bedClearance")),
                new EntranceSpec(Int(entrances, "cell"), Int(entrances, "octaves"), Int(entrances, "threshold"), Int(entrances, "minHeightAboveSea"), Int(entrances, "waterReach"), Int(entrances, "probeDepth")),
                new LavaSpec(Text(lava, "block"), Int(lava, "level")),
                [.. root.GetProperty("ores").EnumerateArray().Select(ParseOre)],
                root.TryGetProperty("valley", out var valley) ? ParseValley(valley) : null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static ValleySpec ParseValley(JsonElement valley) => new(
        Int(valley, "surfaceDistance"),
        Int(valley, "waterDistance"),
        Int(valley, "borderMargin"),
        Int(valley, "siteMargin"),
        Int(valley, "pathMargin"),
        Int(valley, "propMargin"),
        Text(valley, "villageSite"),
        Int(valley, "villageMargin"),
        [.. valley.GetProperty("passPaths").EnumerateArray().Select(static e => e.GetString() ?? string.Empty)],
        [.. valley.GetProperty("passProps").EnumerateArray().Select(static e => e.GetString() ?? string.Empty)],
        Int(valley, "passMargin"));

    private static OreSpec ParseOre(JsonElement ore)
    {
        var size = ore.GetProperty("size");
        return new OreSpec(
            Text(ore, "id"),
            Text(ore, "block"),
            [.. ore.GetProperty("replaces").EnumerateArray().Select(static e => e.GetString() ?? string.Empty)],
            Int(size, "min"),
            Int(size, "max"),
            [.. ore.GetProperty("bands").EnumerateArray().Select(static b => new OreBand(Int(b, "minY"), Int(b, "maxY"), Int(b, "peakY"), Int(b, "veins")))]);
    }

    private static int Int(JsonElement owner, string name) => owner.GetProperty(name).GetInt32();

    private static string Text(JsonElement owner, string name) => owner.GetProperty(name).GetString() ?? string.Empty;
}
