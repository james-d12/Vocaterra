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
/// <param name="Round">For a ridged field, the thousandths of the field over which the crest of every ridge is rounded (0: a sharp crest, as in the files before B02b's redo).</param>
/// <param name="ChainBlocks">For the ridges field (B02b round 3): above 0, the field is a chain: 1 on the noise's zero lines, falling in a straight slope to 0 at this many blocks from them whatever the noise's gradient, so every chain has the same width (a ridge of one octave is broad where the noise is flat and narrow where it is steep). 0: the squared ridge of one octave.</param>
/// <param name="ChainRound">The blocks over which the crest of a chain is rounded (a chain's spine is not a knife edge).</param>
public sealed record NoiseSpec(int Cell, int Octaves, int WarpCell, int WarpBlocks, int Round = 0, int ChainBlocks = 0, int ChainRound = 0);

/// <summary>
/// The continents (<c>climate.continents</c>, B02b redo): the continentalness as jittered cells, a share of them sea, with a channel of sea along every border (see <see cref="Climate.ContinentField"/>).
/// </summary>
/// <param name="Cell">The cells' side in blocks (the continents are about this wide).</param>
/// <param name="Jitter">How far a cell's point strays from its centre, in thousandths of half the cell (0 to 900).</param>
/// <param name="SeaBlock">The side, in cells, of the blocks the cells are grouped in (1 to 4): each block holds a fixed number of sea cells, so the sea's share is the same in every part of the world.</param>
/// <param name="SeaPerBlock">The cells of each block, at random, that are open sea (0 to the block's cells less one).</param>
/// <param name="Channel">The most sea each side of a border, in blocks (where the channel is widest).</param>
/// <param name="ChannelMin">The least sea each side of a border, in blocks (at least 1: the land cells are never joined, so the sea is one ocean).</param>
/// <param name="WidthCell">The cell, in blocks, of the noise that varies the channel between its least and most.</param>
/// <param name="CoastCell">The cell, in blocks, of the noise that bends the coast (a warp of the plane, so it never joins or cuts a channel).</param>
/// <param name="CoastBlocks">How far that warp moves a point, in blocks.</param>
/// <param name="ScaleBlocks">The blocks that make 1 000 thousandths of continentalness.</param>
/// <param name="RoughCell">The cell, in blocks, of the first octave of the noise added to the distance from the coast (B02b round 3: it makes the coast wander at every scale, where the cells' borders and the warps left long straight runs); 0 for none.</param>
/// <param name="RoughBlocks">How far that noise moves the coast, in blocks (about half of it is typical), held to what a channel can spare: every channel stays at least half its least width each side, so two land cells are still never joined.</param>
/// <param name="RoughOctaves">The octaves of that noise (each halves the cell and the weight).</param>
/// <param name="Salt">A number mixed into the cells' hash: another salt is another arrangement of the same kind of world (the shipped one was picked so the three maps of the owner's review have the sea share asked for).</param>
public sealed record ContinentSpec(int Cell, int Jitter, int SeaBlock, int SeaPerBlock, int Channel, int ChannelMin, int WidthCell, int CoastCell, int CoastBlocks, int ScaleBlocks, int Salt = 0, int RoughCell = 0, int RoughBlocks = 0, int RoughOctaves = 1);

/// <summary>The climate fields and how temperature falls with height (<c>climate</c> of <c>game/world.json</c>).</summary>
/// <param name="LatticeBlocks">The spacing, in blocks, of the lattice the fields are sampled on and interpolated from.</param>
/// <param name="Continentalness">Sea, coast, inland and far inland.</param>
/// <param name="Erosion">Low is rugged and high; high is flat and low.</param>
/// <param name="Ridges">Ridged noise: the crests of the ranges (0 in the valleys).</param>
/// <param name="Temperature">The temperature at sea level.</param>
/// <param name="Humidity">The humidity.</param>
/// <param name="River">The river field: a river runs where it is near zero.</param>
/// <param name="LapsePer100Blocks">The fall of the temperature, in thousandths of its range, for every 100 blocks above the sea.</param>
/// <param name="Continents">The continents that make the continentalness (the file's <c>continents</c> section; none: the continentalness is plain noise).</param>
public sealed record ClimateData(
    int LatticeBlocks,
    NoiseSpec Continentalness,
    NoiseSpec Erosion,
    NoiseSpec Ridges,
    NoiseSpec Temperature,
    NoiseSpec Humidity,
    NoiseSpec River,
    int LapsePer100Blocks,
    ContinentSpec? Continents = null);

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
/// <param name="Width">The width of the narrowest river, in blocks: the distance from the centre line over which the carve fades out, so the water itself is about half as wide (B02c: a river's width grows with the ground that drains into it, up to <see cref="DrainageSpec.WidthMax"/>).</param>
/// <param name="Depth">How deep the water stands in the middle of the narrowest river, in blocks: the bed is this far under the water's surface (B02c: deeper downstream, up to <see cref="DrainageSpec.DepthMax"/>).</param>
/// <param name="FadeFromHeight">The land height, in blocks, above which a river begins to fade out.</param>
/// <param name="FadeToHeight">The land height, in blocks, where it is gone.</param>
/// <param name="SeaFadeFrom">The continentalness, in thousandths, below which a river is gone (the sea needs no river).</param>
/// <param name="SeaFadeTo">The continentalness, in thousandths, above which a river is full.</param>
/// <param name="Valley">How the river's valley climbs the high ground to its source (the file's <c>valley</c> part; none: the river is a band of fixed width cut to the bed).</param>
/// <param name="PeakFadeFrom">The height, in blocks, that the ranges' peaks add to the ground, from which a river begins to fade out (a river does not cross a crest: it rises on each side in the foothills); with <paramref name="PeakFadeTo"/>, none by default.</param>
/// <param name="PeakFadeTo">The height, in blocks, that the peaks add where the river is gone.</param>
public sealed record RiverSpec(int Width, int Depth, int FadeFromHeight, int FadeToHeight, int SeaFadeFrom, int SeaFadeTo, RiverValleySpec? Valley = null, int PeakFadeFrom = 100000, int PeakFadeTo = 100001);

/// <summary>
/// The valley of a river on high ground (B02b redo, round 3): above <see cref="From"/> the river's water stands deeper under the ground the higher the ground is (<see cref="DepthGrowth"/>), and the valley widens with the height so
/// its sides are slopes and not walls. The water follows the valley down (see <see cref="WaterSpec"/>); the river begins where the ground fades it out.
/// </summary>
/// <param name="From">The land height, in blocks, above which the valley widens and the floor rises with the ground.</param>
/// <param name="WidthPer100Blocks">The blocks the river's width grows by for every 100 blocks of ground above <paramref name="From"/> (the valley widens as the river climbs).</param>
/// <param name="DepthGrowth">How much deeper the valley is, in thousandths of a block, for every block of ground above <paramref name="From"/>.</param>
public sealed record RiverValleySpec(int From, int WidthPer100Blocks, int DepthGrowth);

/// <summary>
/// The level of the water that is not the sea (B02b round 3): every river and lake has a surface of its own. It is the smooth ground of the column (before the hills) less the hills' amplitude, <see cref="Clearance"/> blocks and the valley's depth,
/// in steps of <see cref="Step"/> blocks above the sea level and never under it, so a river's surface steps down its valley in flat reaches (a reach is a stretch where the ground falls by less than a step) and reaches the sea at its level.
/// </summary>
/// <param name="Step">The height, in blocks, of a step of the water's surface (at least 1).</param>
/// <param name="Clearance">How many blocks under the smooth ground, besides the hills' amplitude, the surface stands.</param>
public sealed record WaterSpec(int Step, int Clearance);

/// <summary>
/// How much the peaks vary along a range (B02b redo): the peaks' height is scaled by a share that runs from <see cref="MinShare"/> to all of it over a smooth noise, so the spine of a range rises to summits and falls
/// to saddles instead of holding one height.
/// </summary>
/// <param name="Cell">The noise's cell in blocks (the summits are about this far apart).</param>
/// <param name="Octaves">The noise's octaves.</param>
/// <param name="MinShare">The thousandths of the peaks' height at a saddle.</param>
public sealed record PeakVariationSpec(int Cell, int Octaves, int MinShare);

/// <summary>
/// The drainage network (B02c, D-game-34 item 33): the rivers and lakes of the free world are not noise lines but the drainage of the land. On a grid of nodes <see cref="Spacing"/> blocks apart every node's water level is worked out
/// (the smooth ground less the hills' amplitude, the clearance and the valley's depth, <see cref="WaterSpec"/>); the water of every node runs to the lowest neighbour by a priority flood from the sea, which fills each basin to the level of its
/// outlet (a lake) and leaves every other node a lower neighbour downstream; a node that more than <see cref="SourceArea"/> nodes drain through is a river. The network is worked out for one cell of <see cref="Cell"/> blocks at a time over a window
/// <see cref="Margin"/> blocks wider on every side, so it is a pure function of the seed and the same wherever it is first asked for.
/// </summary>
/// <param name="Spacing">The distance, in blocks, between two nodes (at least 8, and a multiple of the climate's lattice).</param>
/// <param name="MaxNodes">The most land nodes one landmass may have: a world with no sea round its land is cut here and its water runs out at the cut.</param>
/// <param name="SourceArea">How many nodes must drain through a node for a river to begin there.</param>
/// <param name="FullArea">How many nodes must drain through a node for its river to be full size (wider and deeper rivers have more).</param>
/// <param name="WidthMax">The width of the largest river, in blocks (see <see cref="RiverSpec.Width"/>).</param>
/// <param name="DepthMax">The depth of the largest river, in blocks.</param>
/// <param name="Jitter">How far, in blocks, a node is moved from its grid point, so the rivers do not run along the grid.</param>
/// <param name="Bow">How far, in thousandths of a reach's length, a river bows aside between two nodes.</param>
/// <param name="LakeBasin">How deep, in blocks, the lake field dents the land the drainage runs over: a dent the land's slope does not fill is a basin that fills to its outlet.</param>
/// <param name="Relief">How far, in blocks, a smooth noise of <paramref name="ReliefCell"/> blocks moves the surface the water is routed over (not the water's level): on a plain whose smooth ground has no valleys the water would run in parallel lines that never meet, and the relief gathers it into rivers that join.</param>
/// <param name="ReliefCell">The relief noise's cell, in blocks.</param>
/// <param name="RegionClearance">How far, in blocks, beyond a region's blend margin no river and no lake begins (the headwaters start outside it).</param>
public sealed record DrainageSpec(int Spacing, int MaxNodes, int SourceArea, int FullArea, int WidthMax, int DepthMax, int Jitter, int Bow, int LakeBasin, int Relief, int ReliefCell, int RegionClearance);

/// <summary>The lakes: inland basins pulled down towards a shallow bed where the lake field is high, the land low and the erosion calm.</summary>
/// <param name="Cell">The lake field's cell in blocks.</param>
/// <param name="Octaves">The lake field's octaves.</param>
/// <param name="Threshold">The lake field's value, in thousandths, where a lake begins.</param>
/// <param name="Softness">How far above the threshold, in thousandths, the lake is full.</param>
/// <param name="Depth">How deep the water stands in the middle of the lake, in blocks.</param>
/// <param name="CalmFrom">The erosion, in thousandths, below which no lake forms.</param>
/// <param name="CalmTo">The erosion, in thousandths, above which a lake may be full.</param>
/// <param name="HeightFrom">The land height, in blocks, above which a lake begins to fade out.</param>
/// <param name="HeightTo">The land height, in blocks, where it is gone.</param>
/// <param name="InlandFrom">The continentalness, in thousandths, below which no lake forms.</param>
/// <param name="InlandTo">The continentalness, in thousandths, above which a lake may be full.</param>
public sealed record LakeSpec(int Cell, int Octaves, int Threshold, int Softness, int Depth, int CalmFrom, int CalmTo, int HeightFrom, int HeightTo, int InlandFrom, int InlandTo);

/// <summary>
/// The coast (B02b): over the land near the sea the peaks fade out (no rugged island rises from the sea) and the hills calm down, so the shore is a gentle shelf and not a cliff.
/// </summary>
/// <param name="PeaksFrom">The continentalness, in thousandths, below which the peaks show nothing.</param>
/// <param name="PeaksTo">The continentalness, in thousandths, above which the peaks show in full.</param>
/// <param name="HillsFrom">The continentalness, in thousandths, at and below which the hills keep only <paramref name="HillsFloor"/> of their amplitude.</param>
/// <param name="HillsTo">The continentalness, in thousandths, above which the hills keep all of it.</param>
/// <param name="HillsFloor">The share of the hills' amplitude, in thousandths, that remains at the coast.</param>
/// <param name="FlatFrom">The ground height, in blocks, at and below which the hills are gone (smooth plains and beaches, so no speckle of flats and pools lies just over the sea).</param>
/// <param name="FlatTo">The ground height, in blocks, above which the hills are whole again; above <paramref name="FlatFrom"/>.</param>
public sealed record CoastSpec(int PeaksFrom, int PeaksTo, int HillsFrom, int HillsTo, int HillsFloor, int FlatFrom = 0, int FlatTo = 1)
{
    /// <summary>No coast: the peaks and the hills are the same everywhere (the world files before B02b).</summary>
    public static CoastSpec None { get; } = new(-4000, -3999, -4000, -3999, 1000, 0, 1);
}

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
/// <param name="WaterData">The level of the rivers' and lakes' water (the file's <c>water</c> section; none: <see cref="WaterSpec"/>'s default of a step of 3 and a clearance of 2).</param>
/// <param name="CoastData">The coast's fading of the peaks and the hills (the file's <c>coast</c> section; none when the file has none).</param>
/// <param name="PeakVariation">How the peaks' height varies along a range (the file's <c>peakVariation</c> section; none: it does not).</param>
/// <param name="DrainageData">The drainage network the rivers and lakes are made of (the file's <c>drainage</c> section; none: the world has no river and no lake).</param>
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
    LakeSpec Lakes,
    CoastSpec? CoastData = null,
    PeakVariationSpec? PeakVariation = null,
    WaterSpec? WaterData = null,
    DrainageSpec? DrainageData = null)
{
    /// <summary>The water: the file's section, or a step of 3 and a clearance of 2.</summary>
    public WaterSpec Water => WaterData ?? new WaterSpec(3, 2);

    /// <summary>The coast: the file's section, or <see cref="CoastSpec.None"/>.</summary>
    public CoastSpec Coast => CoastData ?? CoastSpec.None;
}

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
                    Int(climate, "lapsePer100Blocks"),
                    climate.TryGetProperty("continents", out var continents)
                        ? new ContinentSpec(Int(continents, "cell"), Int(continents, "jitter"), Int(continents, "seaBlock"), Int(continents, "seaPerBlock"), Int(continents, "channel"), Int(continents, "channelMin"), Int(continents, "widthCell"), Int(continents, "coastCell"), Int(continents, "coastBlocks"), Int(continents, "scaleBlocks"), continents.TryGetProperty("salt", out var salt) ? salt.GetInt32() : 0, continents.TryGetProperty("roughCell", out var roughCell) ? roughCell.GetInt32() : 0, continents.TryGetProperty("roughBlocks", out var roughBlocks) ? roughBlocks.GetInt32() : 0, continents.TryGetProperty("roughOctaves", out var roughOctaves) ? roughOctaves.GetInt32() : 1)
                        : null),
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
            new RiverSpec(
                Int(rivers, "width"),
                Int(rivers, "depth"),
                Int(rivers, "fadeFromHeight"),
                Int(rivers, "fadeToHeight"),
                Int(rivers, "seaFadeFrom"),
                Int(rivers, "seaFadeTo"),
                rivers.TryGetProperty("valley", out var valley) ? new RiverValleySpec(Int(valley, "from"), Int(valley, "widthPer100Blocks"), Int(valley, "depthGrowth")) : null,
                rivers.TryGetProperty("peakFadeFrom", out var peakFadeFrom) ? peakFadeFrom.GetInt32() : 100000,
                rivers.TryGetProperty("peakFadeTo", out var peakFadeTo) ? peakFadeTo.GetInt32() : 100001),
            new LakeSpec(
                Int(lakes, "cell"),
                Int(lakes, "octaves"),
                Int(lakes, "threshold"),
                Int(lakes, "softness"),
                Int(lakes, "depth"),
                Int(lakes, "calmFrom"),
                Int(lakes, "calmTo"),
                Int(lakes, "heightFrom"),
                Int(lakes, "heightTo"),
                Int(lakes, "inlandFrom"),
                Int(lakes, "inlandTo")),
            height.TryGetProperty("coast", out var coast)
                ? new CoastSpec(Int(coast, "peaksFrom"), Int(coast, "peaksTo"), Int(coast, "hillsFrom"), Int(coast, "hillsTo"), Int(coast, "hillsFloor"), coast.TryGetProperty("flatFrom", out var flatFrom) ? flatFrom.GetInt32() : 0, coast.TryGetProperty("flatTo", out var flatTo) ? flatTo.GetInt32() : 1)
                : null,
            height.TryGetProperty("peakVariation", out var variation)
                ? new PeakVariationSpec(Int(variation, "cell"), Int(variation, "octaves"), Int(variation, "minShare"))
                : null,
            height.TryGetProperty("water", out var water)
                ? new WaterSpec(Int(water, "step"), Int(water, "clearance"))
                : null,
            height.TryGetProperty("drainage", out var drainage)
                ? new DrainageSpec(Int(drainage, "spacing"), Int(drainage, "maxNodes"), Int(drainage, "sourceArea"), Int(drainage, "fullArea"), Int(drainage, "widthMax"), Int(drainage, "depthMax"), Int(drainage, "jitter"), Int(drainage, "bow"), Int(drainage, "lakeBasin"), Int(drainage, "relief"), Int(drainage, "reliefCell"), Int(drainage, "regionClearance"))
                : null);
    }

    private static int Int(JsonElement owner, string name) => owner.GetProperty(name).GetInt32();

    private static NoiseSpec Noise(JsonElement owner, string name)
    {
        var field = owner.GetProperty(name);
        return new NoiseSpec(
            Int(field, "cell"),
            Int(field, "octaves"),
            field.TryGetProperty("warpCell", out var warpCell) ? warpCell.GetInt32() : 0,
            field.TryGetProperty("warpBlocks", out var warp) ? warp.GetInt32() : 0,
            field.TryGetProperty("round", out var round) ? round.GetInt32() : 0,
            field.TryGetProperty("chainBlocks", out var chain) ? chain.GetInt32() : 0,
            field.TryGetProperty("chainRound", out var chainRound) ? chainRound.GetInt32() : 0);
    }

    private static List<SplinePoint> Points(JsonElement owner, string name) =>
        [.. owner.GetProperty(name).EnumerateArray().Select(static p => new SplinePoint(p[0].GetInt32(), p[1].GetInt32()))];
}
