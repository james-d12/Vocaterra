using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// One kind of rock pocket: where a 3D noise field passes the threshold, inside the rows' own band of heights, the block replaces the base rock the row names (B11, B11b: pockets of gravel, sand and dirt
/// among the stone, larger granite and limestone, a darker stone in the slate).
/// </summary>
/// <param name="Block">The block's name (a name of <see cref="Voxel.BlockDefinition"/>).</param>
/// <param name="Cell">The noise's cell across, in blocks.</param>
/// <param name="CellY">The noise's cell up, in blocks.</param>
/// <param name="Octaves">The octaves (each halves the cell and the weight).</param>
/// <param name="Threshold">The noise value, in thousandths, above which the blob is (higher is rarer and smaller).</param>
/// <param name="In">The base rocks (block names) the pocket may replace, or <see langword="null"/> for the layer's stone only.</param>
/// <param name="MinY">The lowest y the pocket reaches.</param>
/// <param name="MaxY">The highest y the pocket reaches.</param>
public sealed record BlobSpec(string Block, int Cell, int CellY, int Octaves, int Threshold, IReadOnlyList<string>? In = null, int MinY = int.MinValue, int MaxY = int.MaxValue);

/// <summary>A block with a weight, for a draw among several (the rim of an entrance, the bands of a cliff).</summary>
/// <param name="Block">The block's name.</param>
/// <param name="Weight">The weight (at least 1).</param>
public sealed record BlockWeight(string Block, int Weight);

/// <summary>A patchy mask: a fractal noise (three-dimensional underground, two-dimensional on the surface) that must pass a threshold, so a rule lays its block in clumps and not one block in a speckle.</summary>
/// <param name="Cell">The noise's cell across, in blocks.</param>
/// <param name="CellY">The noise's cell up, in blocks (ignored on the surface).</param>
/// <param name="Octaves">The octaves.</param>
/// <param name="Threshold">The noise value, in thousandths, above which the rule applies.</param>
public sealed record PatchSpec(int Cell, int CellY, int Octaves, int Threshold);

/// <summary>One rule of the cave linings: a rock cell on the wall, floor or ceiling of a cave, in a zone, may become another block.</summary>
/// <param name="Block">The block it becomes.</param>
/// <param name="In">The blocks (rocks) it may replace.</param>
/// <param name="Face">Which exposed face the cell shows: <c>floor</c> (air above), <c>wall</c> (air at the side), <c>ceiling</c> (air below) or <c>any</c>.</param>
/// <param name="Zone"><c>any</c>, <c>damp</c> (near an entrance's passage) or <c>wet</c> (near water).</param>
/// <param name="Chance">The share, in thousandths, of the cells that qualify (inside the patch).</param>
/// <param name="Patch">The patchy mask, or <see langword="null"/> for none.</param>
/// <param name="MinY">The lowest y the rule applies at.</param>
/// <param name="MaxY">The highest y the rule applies at.</param>
public sealed record LiningRule(string Block, IReadOnlyList<string> In, string Face, string Zone, int Chance, PatchSpec? Patch, int MinY, int MaxY);

/// <summary>
/// The cave linings (B11b, D-game-34 item 27): rules laid over the rock a cave exposes, in order, the first that applies winning; gravel and loose stone on floors, mossy stone near the entrances' passages and
/// near water, sand and gravel where the cave lies near water.
/// </summary>
/// <param name="WetReach">How many columns from a water column count as near water.</param>
/// <param name="DampReach">How many blocks from an entrance's passage count as damp.</param>
/// <param name="Rules">The rules.</param>
public sealed record LiningSpec(int WetReach, int DampReach, IReadOnlyList<LiningRule> Rules);

/// <summary>A scattered block: laid where a column qualifies, a share of the time, inside a patch.</summary>
/// <param name="Block">The block.</param>
/// <param name="Chance">The share, in thousandths, of the qualifying columns.</param>
/// <param name="Patch">The patchy mask, or <see langword="null"/>.</param>
public sealed record ScatterRule(string Block, int Chance, PatchSpec? Patch);

/// <summary>Bare stone on a cliff: a column that stands <see cref="Drop"/> over a neighbour shows banded rock on the exposed layers.</summary>
/// <param name="Drop">How far a column stands over its lowest neighbour (water counts as its surface) to be a cliff column.</param>
/// <param name="BandThickness">The thickness of one band, in blocks.</param>
/// <param name="ZoneCell">The size, in blocks, of the zones that draw their bands apart.</param>
/// <param name="Tilt">How far, in blocks, the bands rise and fall across the face.</param>
/// <param name="Bands">The rocks of the bands, drawn by weight.</param>
public sealed record CliffSpec(int Drop, int BandThickness, int ZoneCell, int Tilt, IReadOnlyList<BlockWeight> Bands);

/// <summary>Scree: loose stone on steep ground and at a cliff's foot.</summary>
/// <param name="SteepDrop">How far a column stands over its lowest neighbour to be steep (below a cliff).</param>
/// <param name="FootReach">How many columns from a cliff column the foot reaches.</param>
/// <param name="Rules">The blocks laid, the first that applies winning.</param>
public sealed record ScreeSpec(int SteepDrop, int FootReach, IReadOnlyList<ScatterRule> Rules);

/// <summary>Gravel and sand in the beds and on the banks of rivers.</summary>
/// <param name="MinWeight">How much a river must have carved a column, in thousandths of its full carve, for it to count.</param>
/// <param name="Rules">The blocks laid, the first that applies winning.</param>
public sealed record StreambedSpec(int MinWeight, IReadOnlyList<ScatterRule> Rules);

/// <summary>The bare stone and loose stone of the surface (B11b): cliffs, scree and stream beds.</summary>
/// <param name="Cliff">The cliff faces.</param>
/// <param name="Scree">The scree.</param>
/// <param name="Streambed">The stream beds.</param>
public sealed record SurfaceRockSpec(CliffSpec Cliff, ScreeSpec Scree, StreambedSpec Streambed);

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
/// The cave entrances (B11b): natural passages a body walks down from the surface into the cave network. The world is cut into sites <see cref="Cell"/> blocks across; <see cref="Chance"/> thousandths of
/// them try to open a passage, from the best of <see cref="Attempts"/> spots (the passage the ground covers most: a hillside beats level ground) at least <see cref="Margin"/> blocks inside the site.
/// A passage is a winding path of cells (it bends by up to <see cref="Bend"/> sixteenths of a table step a block, changing its mind every <see cref="BendSegmentMin"/> to <see cref="BendSegmentMax"/> blocks and
/// never turning more than <see cref="MaxTurn"/> table steps from its first heading) whose floor drops one block every 1 to <see cref="RunMax"/> blocks in stretches of <see cref="SegmentMin"/> to
/// <see cref="SegmentMax"/> (and runs level for <see cref="LevelShare"/> thousandths of the stretches), so no step is over one block, until it meets a cave at least <see cref="MinDrop"/> blocks under the
/// mouth (within <see cref="MaxLength"/> blocks), then a landing of <see cref="Landing"/> blocks. Its width runs from <see cref="WidthMin"/> to <see cref="WidthMax"/> blocks and its headroom from
/// <see cref="HeadroomMin"/> to <see cref="HeadroomMax"/>, both drifting along the path, and its walls and ceiling are carved by a noise of <see cref="Roughness"/> thousandths of a block (cell
/// <see cref="RoughnessCell"/>), so they bulge and are never flat; <see cref="Ledges"/> thousandths of the side columns are a block higher than the path's floor. The first <see cref="FunnelLength"/> blocks
/// are wider by <see cref="Funnel"/> thousandths, and where the ground lies at most <see cref="BowlDepth"/> blocks over the path's floor the ground is cut away in a bowl that rises one block for every
/// <see cref="BowlSlope"/> thousandths of a block outwards from the tube's edge, so the mouth on level ground is a ragged sloping dip (a sinkhole) and not a slot. A site opens only on dry procedural land at least <see cref="MinHeightAboveSea"/> over the sea with no water or
/// river bank within <see cref="WaterReach"/> columns of the passage, outside every region and its margin. No tree or plant stands at the mouth. Gaps in a floor are filled with <see cref="FloorBlocks"/>.
/// </summary>
/// <param name="Cell">The sites' spacing, in blocks.</param>
/// <param name="Chance">The share of the sites that try, in thousandths.</param>
/// <param name="Margin">How far inside its site the mouth lies, in blocks.</param>
/// <param name="Attempts">How many spots a site tries; the passage the ground covers most wins.</param>
/// <param name="MinHeightAboveSea">How far over the sea the ground at the mouth must be.</param>
/// <param name="WaterReach">How many columns round the passage hold no water and no river bank.</param>
/// <param name="MinDrop">How far under the mouth the passage must reach a cave.</param>
/// <param name="MaxLength">The longest passage, in blocks along its path.</param>
/// <param name="Landing">The level blocks after the passage meets its cave.</param>
/// <param name="WidthMin">The narrowest the passage is, in blocks across (at least 2).</param>
/// <param name="WidthMax">The widest the passage is, in blocks across.</param>
/// <param name="HeadroomMin">The lowest the passage's roof is over its floor (at least 3).</param>
/// <param name="HeadroomMax">The highest the passage's roof is over its floor.</param>
/// <param name="Plunge">How many blocks from the mouth the floor drops a block at every block (a steep start, so the opening is a hole and not a long trench).</param>
/// <param name="SegmentMin">The fewest blocks of a stretch of one slope.</param>
/// <param name="SegmentMax">The most blocks of a stretch of one slope.</param>
/// <param name="RunMax">The gentlest slope: one block down every this many blocks along (a stretch draws from 1 to this).</param>
/// <param name="LevelShare">The share of the stretches that run level, in thousandths.</param>
/// <param name="Bend">The sharpest bend, in sixteenths of a step of the 64-step turn a block.</param>
/// <param name="BendSegmentMin">The fewest blocks before the passage changes its bend.</param>
/// <param name="BendSegmentMax">The most blocks before it changes its bend.</param>
/// <param name="MaxTurn">The most the heading may stray from its first, in steps of the 64-step turn.</param>
/// <param name="Roughness">How far the noise moves the walls, in thousandths of a block.</param>
/// <param name="RoughnessCell">The noise's cell, in blocks.</param>
/// <param name="Ledges">The share of the side columns raised one block, in thousandths.</param>
/// <param name="Funnel">How much wider the mouth is, in thousandths.</param>
/// <param name="FunnelLength">The blocks the widening lasts.</param>
/// <param name="BowlDepth">The deepest the ground may lie over the path's floor for the bowl to be cut, in blocks.</param>
/// <param name="BowlSlope">How far out, in thousandths of a block, the bowl's side runs for each block it rises.</param>
/// <param name="FaceRise">How many blocks the ground must climb within a few blocks ahead of a spot for a mouth to open sideways into it as a hill or cliff face (B11d); 0 for no face mouths.</param>
/// <param name="FloorBlocks">The rocks that fill a gap in the floor, drawn by weight.</param>
public sealed record EntranceSpec(
    int Cell,
    int Chance,
    int Margin,
    int Attempts,
    int MinHeightAboveSea,
    int WaterReach,
    int MinDrop,
    int MaxLength,
    int Landing,
    int WidthMin,
    int WidthMax,
    int HeadroomMin,
    int HeadroomMax,
    int Plunge,
    int SegmentMin,
    int SegmentMax,
    int RunMax,
    int LevelShare,
    int Bend,
    int BendSegmentMin,
    int BendSegmentMax,
    int MaxTurn,
    int Roughness,
    int RoughnessCell,
    int Ledges,
    int Funnel,
    int FunnelLength,
    int BowlDepth,
    int BowlSlope,
    int FaceRise,
    IReadOnlyList<BlockWeight> FloorBlocks);

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
/// <param name="Linings">The cave linings (B11b), or <see langword="null"/> for none.</param>
/// <param name="SurfaceRock">The bare and loose stone of the surface (B11b), or <see langword="null"/> for none.</param>
public sealed record UndergroundData(
    StrataSpec Strata,
    FloorSpec Floor,
    CaveSpec Caves,
    EntranceSpec Entrances,
    LavaSpec Lava,
    IReadOnlyList<OreSpec> Ores,
    ValleySpec? Valley = null,
    LiningSpec? Linings = null,
    SurfaceRockSpec? SurfaceRock = null)
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
                    [.. strata.GetProperty("blobs").EnumerateArray().Select(ParseBlob)]),
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
                ParseEntrances(entrances),
                new LavaSpec(Text(lava, "block"), Int(lava, "level")),
                [.. root.GetProperty("ores").EnumerateArray().Select(ParseOre)],
                root.TryGetProperty("valley", out var valley) ? ParseValley(valley) : null,
                root.TryGetProperty("linings", out var linings) ? ParseLinings(linings) : null,
                root.TryGetProperty("surfaceRock", out var surfaceRock) ? ParseSurfaceRock(surfaceRock) : null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static BlobSpec ParseBlob(JsonElement blob) => new(
        Text(blob, "block"),
        Int(blob, "cell"),
        Int(blob, "cellY"),
        Int(blob, "octaves"),
        Int(blob, "threshold"),
        blob.TryGetProperty("in", out var rocks) ? [.. rocks.EnumerateArray().Select(static e => e.GetString() ?? string.Empty)] : null,
        blob.TryGetProperty("minY", out var low) ? low.GetInt32() : int.MinValue,
        blob.TryGetProperty("maxY", out var high) ? high.GetInt32() : int.MaxValue);

    private static EntranceSpec ParseEntrances(JsonElement e) => new(
        Int(e, "cell"),
        Int(e, "chance"),
        Int(e, "margin"),
        Int(e, "attempts"),
        Int(e, "minHeightAboveSea"),
        Int(e, "waterReach"),
        Int(e, "minDrop"),
        Int(e, "maxLength"),
        Int(e, "landing"),
        Int(e, "widthMin"),
        Int(e, "widthMax"),
        Int(e, "headroomMin"),
        Int(e, "headroomMax"),
        Int(e, "plunge"),
        Int(e, "segmentMin"),
        Int(e, "segmentMax"),
        Int(e, "runMax"),
        Int(e, "levelShare"),
        Int(e, "bend"),
        Int(e, "bendSegmentMin"),
        Int(e, "bendSegmentMax"),
        Int(e, "maxTurn"),
        Int(e, "roughness"),
        Int(e, "roughnessCell"),
        Int(e, "ledges"),
        Int(e, "funnel"),
        Int(e, "funnelLength"),
        Int(e, "bowlDepth"),
        Int(e, "bowlSlope"),
        Int(e, "faceRise"),
        ParseWeights(e.GetProperty("floorBlocks")));

    private static IReadOnlyList<BlockWeight> ParseWeights(JsonElement list) => [.. list.EnumerateArray().Select(static b => new BlockWeight(Text(b, "block"), Int(b, "weight")))];

    private static PatchSpec? ParsePatch(JsonElement owner) =>
        owner.TryGetProperty("patch", out var p) ? new PatchSpec(Int(p, "cell"), p.TryGetProperty("cellY", out var y) ? y.GetInt32() : Int(p, "cell"), Int(p, "octaves"), Int(p, "threshold")) : null;

    private static LiningSpec ParseLinings(JsonElement l) => new(
        Int(l, "wetReach"),
        Int(l, "dampReach"),
        [.. l.GetProperty("rules").EnumerateArray().Select(static r => new LiningRule(
            Text(r, "block"),
            [.. r.GetProperty("in").EnumerateArray().Select(static e => e.GetString() ?? string.Empty)],
            Text(r, "face"),
            Text(r, "zone"),
            Int(r, "chance"),
            ParsePatch(r),
            Int(r, "minY"),
            Int(r, "maxY")))]);

    private static IReadOnlyList<ScatterRule> ParseScatter(JsonElement rules) => [.. rules.EnumerateArray().Select(static r => new ScatterRule(Text(r, "block"), Int(r, "chance"), ParsePatch(r)))];

    private static SurfaceRockSpec ParseSurfaceRock(JsonElement s)
    {
        var cliff = s.GetProperty("cliff");
        var scree = s.GetProperty("scree");
        var streambed = s.GetProperty("streambed");
        return new SurfaceRockSpec(
            new CliffSpec(Int(cliff, "drop"), Int(cliff, "bandThickness"), Int(cliff, "zoneCell"), Int(cliff, "tilt"), ParseWeights(cliff.GetProperty("bands"))),
            new ScreeSpec(Int(scree, "steepDrop"), Int(scree, "footReach"), ParseScatter(scree.GetProperty("rules"))),
            new StreambedSpec(Int(streambed, "minWeight"), ParseScatter(streambed.GetProperty("rules"))));
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
