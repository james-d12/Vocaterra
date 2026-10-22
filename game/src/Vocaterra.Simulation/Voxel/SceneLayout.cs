using System.Text.Json;

namespace Vocaterra.Simulation.Voxel;

/// <summary>A layout file is malformed or cannot be built (a bad site name, a prop on uneven ground, a point outside the grid).</summary>
public sealed class SceneLayoutException : FormatException
{
    /// <summary>Creates the exception with a message that names the layout element at fault.</summary>
    public SceneLayoutException(string message)
        : base(message)
    {
    }
}

/// <summary>A compass direction on the ground plane: north is -Z, east is +X, south is +Z, west is -X.</summary>
public enum Direction
{
    /// <summary>Towards -Z (the top of a top-down map).</summary>
    North = 0,

    /// <summary>Towards +X.</summary>
    East = 1,

    /// <summary>Towards +Z.</summary>
    South = 2,

    /// <summary>Towards -X.</summary>
    West = 3,
}

/// <summary>
/// A named place at a fixed position (S13): <c>camp.player</c>, <c>camp.guide</c>, <c>village</c>, <c>bridge</c>, <c>lake</c> and so on. The position
/// never depends on the seed. A site with a <paramref name="Radius"/> flattens the terrain: the ground is level at <paramref name="Y"/> (or at the
/// height the terrain has there when none is given) out to the radius and blends into the surrounding terrain over <paramref name="Blend"/> more blocks.
/// </summary>
/// <param name="Name">The site's name; it is also the name of a scene point.</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Y">The y of the top block of the level ground, or <see langword="null"/> to take the terrain's own height at the centre.</param>
/// <param name="Radius">The radius of the level ground in blocks (0: the site only names a place).</param>
/// <param name="Blend">The width in blocks of the slope from the level ground into the terrain.</param>
public sealed record SiteSpec(string Name, int X, int Z, int? Y, int Radius, int Blend);

/// <summary>The shape of the land before the sites, the river and the paths are cut into it.</summary>
/// <param name="Relief">The macro relief: the height in blocks at every chunk-column corner (x = 16 * column, z = 16 * row), row by row; smooth between the corners.</param>
/// <param name="ReliefColumns">The columns of <paramref name="Relief"/> (the grid's width in chunks).</param>
/// <param name="ReliefRows">The rows of <paramref name="Relief"/> (the grid's depth in chunks).</param>
/// <param name="Valley">The height of the valley floor; the seed's noise is strongest on the mountains and weakest at this height.</param>
/// <param name="RockLine">The height above which the ground is bare stone.</param>
/// <param name="SnowLine">The height above which gentle ground is snow.</param>
/// <param name="Terrace">The height in blocks of one step of the stepped cliff faces (0: no terracing).</param>
/// <param name="Ceiling">The highest the ground goes.</param>
public sealed record TerrainSpec(int[] Relief, int ReliefColumns, int ReliefRows, int Valley, int RockLine, int SnowLine, int Terrace, int Ceiling);

/// <summary>
/// The waterfall: a cliff with a sheet of water falling from its lip into a pool, where the river begins. The site is the lip (the first column
/// beyond the cliff's edge, facing south); the plateau behind it is raised to <paramref name="Top"/>.
/// </summary>
/// <param name="Site">The site at the foot of the lip: the water column that falls is at this x, in the row of the site.</param>
/// <param name="Top">The height of the plateau and the lip.</param>
/// <param name="Width">The width of the falling water in blocks.</param>
/// <param name="Plateau">The half-width in blocks of the raised plateau along the cliff.</param>
/// <param name="PoolRadius">The radius of the pool at the foot.</param>
public sealed record WaterfallSpec(string Site, int Top, int Width, int Plateau, int PoolRadius);

/// <summary>The lake: an irregular basin round a site, filled to the world's water level.</summary>
/// <param name="Site">The site at its middle.</param>
/// <param name="RadiusX">The half-width along x.</param>
/// <param name="RadiusZ">The half-depth along z.</param>
/// <param name="Depth">How far the middle of the bed lies under the water.</param>
public sealed record LakeSpec(string Site, int RadiusX, int RadiusZ, int Depth);

/// <summary>The river: a smooth curve through the waypoints, from the waterfall's pool to the lake.</summary>
/// <param name="HalfWidth">Half the width of the water in blocks.</param>
/// <param name="Depth">The depth of the water in the middle.</param>
/// <param name="Bank">The width in blocks of the slope from the water up to the land.</param>
/// <param name="Path">The waypoints (x, z), the first at the waterfall's pool, the last in the lake.</param>
public sealed record RiverSpec(int HalfWidth, int Depth, int Bank, IReadOnlyList<(int X, int Z)> Path);

/// <summary>How a path takes the ground.</summary>
public enum PathMode
{
    /// <summary>The path's height changes evenly from the height of its first site to the height of its last (a switchback trail down a mountainside).</summary>
    Grade,

    /// <summary>The path follows the terrain's own height, smoothed so that it never steps more than one block (a road over the valley floor).</summary>
    Follow,
}

/// <summary>A path of trodden ground between two sites, cut into the terrain; trees keep off it.</summary>
/// <param name="Name">The path's name.</param>
/// <param name="From">The site it starts at.</param>
/// <param name="To">The site it ends at.</param>
/// <param name="Via">The waypoints between the two sites (x, z); the path is a straight line from one to the next.</param>
/// <param name="HalfWidth">Half the width in blocks (1 gives a path three blocks wide).</param>
/// <param name="Mode">How the path takes the ground.</param>
public sealed record PathSpec(string Name, string From, string To, IReadOnlyList<(int X, int Z)> Via, int HalfWidth, PathMode Mode);

/// <summary>A small block stamp: layers of rows of characters, bottom layer first, north row first, west column first; a space leaves the cell alone.</summary>
/// <param name="Name">The stamp's name.</param>
/// <param name="Legend">The block of each character.</param>
/// <param name="Layers">The layers from the ground up; each layer is a list of rows.</param>
public sealed record StampSpec(string Name, IReadOnlyDictionary<char, BlockId> Legend, IReadOnlyList<IReadOnlyList<string>> Layers);

/// <summary>
/// A prop placed on the terrain. <see cref="Type"/> is one of <c>workbench</c>, <c>fire_ring</c>, <c>stall</c>, <c>house</c>, <c>signpost</c>,
/// <c>bridge</c>, <c>bush</c>, <c>berry_bush</c>, <c>tree</c>, <c>paving</c> and <c>stamp</c>; the other members are the type's parameters, 0 or
/// <see langword="null"/> when unset. A layout gives the position as a site and an offset; the parser turns it into the column.
/// </summary>
/// <param name="Type">The prop type.</param>
/// <param name="X">The anchor column: the centre for a fire ring, a sign, a bush and a tree; the first block of a workbench; the north-west corner of a stall, a house, a paving and a stamp; the centre line's first column of a bridge.</param>
/// <param name="Z">The anchor row (see <paramref name="X"/>).</param>
/// <param name="Name">The named point the prop registers (at its reference cell), or <see langword="null"/>.</param>
/// <param name="Width">The size along X of a stall, a house or a paving; the number of lanes of a bridge (default 3).</param>
/// <param name="Depth">The size along Z of a stall, a house or a paving.</param>
/// <param name="Height">The wall height of a house (default 3); the trunk height of an explicit tree (default 4).</param>
/// <param name="Length">The number of blocks of a workbench (default 2) or the span of a bridge.</param>
/// <param name="Facing">The side a house's door is on, the side a stall's counter is on, the direction a bridge runs, or the way a stamp is turned (north: as drawn).</param>
/// <param name="AlongZ">For a workbench: whether it extends along Z instead of X.</param>
/// <param name="Stamp">For a stamp: the name of the stamp.</param>
/// <param name="Block">For a paving: the block laid.</param>
public sealed record PropSpec(
    string Type, int X, int Z, string? Name, int Width, int Depth, int Height, int Length, Direction? Facing, bool AlongZ, string? Stamp = null, BlockId Block = BlockId.Path);

/// <summary>An NPC position: the feet cell is (X, terrain height + 1, Z).</summary>
/// <param name="Name">The NPC's name (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>).</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps.</param>
public sealed record NpcSpec(string Name, int X, int Z, int Yaw);

/// <summary>The player's spawn: the feet cell is (X, terrain height + 1, Z).</summary>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps.</param>
public sealed record SpawnSpec(int X, int Z, int Yaw);

/// <summary>A named point of the scene beside the sites, given by column and row; its Y is the top of the terrain (or of the water).</summary>
/// <param name="Name">The point's name.</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
public sealed record PointSpec(string Name, int X, int Z);

/// <summary>
/// The forest: one tree is tried in every <paramref name="Cell"/> x <paramref name="Cell"/> square of the ground, at a jittered position, and kept
/// with a chance of <paramref name="Chance"/> percent (less on higher ground) unless it is too near another trunk or the route.
/// </summary>
/// <param name="Cell">The side of the square one tree is tried in.</param>
/// <param name="Chance">The percent chance a tree is kept on the valley floor.</param>
/// <param name="Spacing">The least Chebyshev distance between two trunks.</param>
/// <param name="Margin">The least Chebyshev distance between a trunk and any route cell (paths, sites' level ground, props, spawn, NPCs and points). The canopy reaches 2, so a margin of 3 keeps every leaf off the route.</param>
/// <param name="MinTrunk">The shortest trunk in blocks.</param>
/// <param name="MaxTrunk">The longest trunk in blocks.</param>
/// <param name="TreeLine">The height above which no tree grows.</param>
public sealed record TreeScatterSpec(int Cell, int Chance, int Spacing, int Margin, int MinTrunk, int MaxTrunk, int TreeLine);

/// <summary>
/// A scene as plain values, parsed from the layout JSON (see <see cref="Parse"/>): the generator's parameters, the named sites at fixed positions, the
/// river, the lake, the waterfall, the paths, the block stamps, the props, the NPCs and the spawn. It lists no block: the
/// <see cref="SceneBuilder"/> makes the terrain from these and the seed.
/// </summary>
public sealed class SceneLayout
{
    private SceneLayout(
        string name,
        int sizeX,
        int sizeY,
        int sizeZ,
        int waterLevel,
        TerrainSpec terrain,
        IReadOnlyList<SiteSpec> sites,
        WaterfallSpec? waterfall,
        LakeSpec? lake,
        RiverSpec? river,
        IReadOnlyList<PathSpec> paths,
        IReadOnlyDictionary<string, StampSpec> stamps,
        IReadOnlyList<PropSpec> props,
        IReadOnlyList<NpcSpec> npcs,
        SpawnSpec spawn,
        IReadOnlyList<PointSpec> points,
        TreeScatterSpec trees)
    {
        Name = name;
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        WaterLevel = waterLevel;
        Terrain = terrain;
        Sites = sites;
        Waterfall = waterfall;
        Lake = lake;
        River = river;
        Paths = paths;
        Stamps = stamps;
        Props = props;
        Npcs = npcs;
        Spawn = spawn;
        Points = points;
        Trees = trees;
    }

    /// <summary>The layout's name.</summary>
    public string Name { get; }

    /// <summary>The grid width (east); a multiple of 16.</summary>
    public int SizeX { get; }

    /// <summary>The grid height (up); a multiple of 16.</summary>
    public int SizeY { get; }

    /// <summary>The grid depth (south); a multiple of 16.</summary>
    public int SizeZ { get; }

    /// <summary>The y of the lake's water surface block: lower ground that is not a river bed is filled with water up to it.</summary>
    public int WaterLevel { get; }

    /// <summary>The shape of the land.</summary>
    public TerrainSpec Terrain { get; }

    /// <summary>The named sites, in the order the layout lists them.</summary>
    public IReadOnlyList<SiteSpec> Sites { get; }

    /// <summary>The waterfall, or <see langword="null"/>.</summary>
    public WaterfallSpec? Waterfall { get; }

    /// <summary>The lake, or <see langword="null"/>.</summary>
    public LakeSpec? Lake { get; }

    /// <summary>The river, or <see langword="null"/>.</summary>
    public RiverSpec? River { get; }

    /// <summary>The paths, in the order the layout lists them.</summary>
    public IReadOnlyList<PathSpec> Paths { get; }

    /// <summary>The block stamps by name.</summary>
    public IReadOnlyDictionary<string, StampSpec> Stamps { get; }

    /// <summary>The props, in the order the layout lists them (they are built in this order, later ones over earlier ones).</summary>
    public IReadOnlyList<PropSpec> Props { get; }

    /// <summary>The NPC positions.</summary>
    public IReadOnlyList<NpcSpec> Npcs { get; }

    /// <summary>The player's spawn.</summary>
    public SpawnSpec Spawn { get; }

    /// <summary>The explicitly named points beside the sites (props with a name add theirs when the scene is built).</summary>
    public IReadOnlyList<PointSpec> Points { get; }

    /// <summary>The forest's parameters.</summary>
    public TreeScatterSpec Trees { get; }

    /// <summary>The site called <paramref name="name"/>, or <see langword="null"/>.</summary>
    public SiteSpec? Site(string name)
    {
        foreach (var site in Sites)
        {
            if (string.Equals(site.Name, name, StringComparison.Ordinal))
            {
                return site;
            }
        }

        return null;
    }

    /// <summary>Reads a layout file.</summary>
    /// <exception cref="SceneLayoutException">The file is malformed.</exception>
    public static SceneLayout Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Parses the layout JSON (comments and trailing commas are allowed). The shape: <c>schemaVersion</c> 2, <c>name</c>, <c>size</c> {x, y, z},
    /// <c>waterLevel</c>, <c>terrain</c> {relief (one string of base-32 digits per chunk row: the height at each chunk corner divided by 4), valley,
    /// rockLine, snowLine, terrace, ceiling}, <c>sites</c> {name: {x, z, y?, radius?, blend?}}, <c>waterfall</c> {site, top, width, plateau, pool},
    /// <c>lake</c> {site, rx, rz, depth}, <c>river</c> {halfWidth, depth, bank, path: [[x, z]...]}, <c>paths</c> [{name, from, to, via: [[x, z]...],
    /// halfWidth, mode: grade|follow}], <c>stamps</c> {name: {legend: {char: block}, layers: [[row...]...]}}, <c>props</c> [{type, site?, dx?, dz?, x?, z?,
    /// name?, width?, depth?, height?, length?, facing?, axis?, stamp?, block?}], <c>npcs</c> [{name, site?, dx?, dz?, x?, z?, facing}], <c>spawn</c>
    /// {site?, dx?, dz?, x?, z?, facing}, <c>points</c> {name: {x, z}} and <c>trees</c> {cell, chance, spacing, margin, minTrunk, maxTrunk, treeLine}. A
    /// prop, NPC or spawn is placed at a site's column plus an offset, or at a column. A facing is a compass word or a whole number of degrees.
    /// </summary>
    /// <exception cref="SceneLayoutException">The text is not valid JSON or does not describe a buildable layout.</exception>
    public static SceneLayout Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new SceneLayoutException($"The layout is not valid JSON: {e.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new SceneLayoutException("The layout must be a JSON object.");
            }

            if (GetOptionalInt(root, "schemaVersion") != 2)
            {
                throw new SceneLayoutException("The layout's schemaVersion must be 2 (the generated world of S13).");
            }

            var name = GetString(root, "name") ?? "scene";
            var size = Require(root, "size");
            var sizeX = GetInt(size, "x", "size");
            var sizeY = GetInt(size, "y", "size");
            var sizeZ = GetInt(size, "z", "size");
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0 || sizeX % VoxelChunk.Size != 0 || sizeY % VoxelChunk.Size != 0 || sizeZ % VoxelChunk.Size != 0)
            {
                throw new SceneLayoutException($"size must be a positive multiple of {VoxelChunk.Size} in x, y and z.");
            }

            var waterLevel = GetInt(root, "waterLevel", "layout");
            if (waterLevel < 0 || waterLevel >= sizeY)
            {
                throw new SceneLayoutException($"waterLevel {waterLevel} lies outside the grid.");
            }

            var terrain = ParseTerrain(Require(root, "terrain"), sizeX, sizeY, sizeZ);
            var sites = ParseSites(Require(root, "sites"), sizeX, sizeZ);
            var byName = sites.ToDictionary(static s => s.Name, StringComparer.Ordinal);
            var waterfall = ParseWaterfall(root, byName);
            var lake = ParseLake(root, byName);
            var river = ParseRiver(root, sizeX, sizeZ);
            var paths = ParsePaths(root, byName, sizeX, sizeZ);
            var stamps = ParseStamps(root);
            var props = ParseProps(root, byName);
            var npcs = ParseNpcs(root, byName);
            var spawnElement = Require(root, "spawn");
            var (spawnX, spawnZ) = ParsePlace(spawnElement, byName, "spawn");
            var spawn = new SpawnSpec(spawnX, spawnZ, ParseYaw(spawnElement, "spawn"));
            var points = ParsePoints(root);
            var trees = ParseTrees(root);
            return new SceneLayout(name, sizeX, sizeY, sizeZ, waterLevel, terrain, sites, waterfall, lake, river, paths, stamps, props, npcs, spawn, points, trees);
        }
    }

    private static TerrainSpec ParseTerrain(JsonElement element, int sizeX, int sizeY, int sizeZ)
    {
        var columns = sizeX / VoxelChunk.Size;
        var rows = sizeZ / VoxelChunk.Size;
        var relief = new int[columns * rows];
        var lines = Require(element, "relief");
        if (lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() != rows)
        {
            throw new SceneLayoutException($"terrain.relief must be an array of {rows} strings (one per chunk row, north first).");
        }

        var z = 0;
        foreach (var line in lines.EnumerateArray())
        {
            var text = line.ValueKind == JsonValueKind.String ? line.GetString() : null;
            if (text is null || text.Length != columns)
            {
                throw new SceneLayoutException($"terrain.relief row {z} must be a string of {columns} characters.");
            }

            for (var x = 0; x < columns; x++)
            {
                var c = text[x];
                var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'v' ? c - 'a' + 10 : -1;
                if (digit < 0 || digit * 4 >= sizeY - 4)
                {
                    throw new SceneLayoutException($"terrain.relief: '{c}' at column {x}, row {z} is not a base-32 digit (0 to 9, a to v) below the grid's height.");
                }

                relief[(z * columns) + x] = digit * 4;
            }

            z++;
        }

        var ceiling = GetOptionalInt(element, "ceiling") ?? (sizeY - 8);
        if (ceiling < 8 || ceiling > sizeY - 4)
        {
            throw new SceneLayoutException($"terrain.ceiling {ceiling} must leave room under the top of the grid.");
        }

        return new TerrainSpec(
            relief,
            columns,
            rows,
            GetOptionalInt(element, "valley") ?? 36,
            GetOptionalInt(element, "rockLine") ?? 85,
            GetOptionalInt(element, "snowLine") ?? 100,
            GetOptionalInt(element, "terrace") ?? 6,
            ceiling);
    }

    private static List<SiteSpec> ParseSites(JsonElement element, int sizeX, int sizeZ)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SceneLayoutException("sites must be an object.");
        }

        var sites = new List<SiteSpec>();
        foreach (var property in element.EnumerateObject())
        {
            var where = $"site '{property.Name}'";
            var x = GetInt(property.Value, "x", where);
            var z = GetInt(property.Value, "z", where);
            if (x < 0 || x >= sizeX || z < 0 || z >= sizeZ)
            {
                throw new SceneLayoutException($"{where}: column ({x}, {z}) lies outside the grid.");
            }

            sites.Add(new SiteSpec(property.Name, x, z, GetOptionalInt(property.Value, "y"), GetOptionalInt(property.Value, "radius") ?? 0, GetOptionalInt(property.Value, "blend") ?? 0));
        }

        if (sites.Count == 0)
        {
            throw new SceneLayoutException("sites is empty.");
        }

        return sites;
    }

    private static WaterfallSpec? ParseWaterfall(JsonElement root, Dictionary<string, SiteSpec> sites)
    {
        if (!root.TryGetProperty("waterfall", out var element))
        {
            return null;
        }

        var site = GetString(element, "site") ?? throw new SceneLayoutException("waterfall needs a site.");
        RequireSite(sites, site, "waterfall");
        return new WaterfallSpec(site, GetInt(element, "top", "waterfall"), GetOptionalInt(element, "width") ?? 3, GetOptionalInt(element, "plateau") ?? 18, GetOptionalInt(element, "pool") ?? 7);
    }

    private static LakeSpec? ParseLake(JsonElement root, Dictionary<string, SiteSpec> sites)
    {
        if (!root.TryGetProperty("lake", out var element))
        {
            return null;
        }

        var site = GetString(element, "site") ?? throw new SceneLayoutException("lake needs a site.");
        RequireSite(sites, site, "lake");
        return new LakeSpec(site, GetInt(element, "rx", "lake"), GetInt(element, "rz", "lake"), GetOptionalInt(element, "depth") ?? 5);
    }

    private static RiverSpec? ParseRiver(JsonElement root, int sizeX, int sizeZ)
    {
        if (!root.TryGetProperty("river", out var element))
        {
            return null;
        }

        var path = ParseColumns(Require(element, "path"), "river.path", sizeX, sizeZ);
        if (path.Count < 2)
        {
            throw new SceneLayoutException("river.path needs at least two waypoints.");
        }

        var spec = new RiverSpec(GetOptionalInt(element, "halfWidth") ?? 3, GetOptionalInt(element, "depth") ?? 3, GetOptionalInt(element, "bank") ?? 4, path);
        if (spec.HalfWidth < 1 || spec.Depth < 1 || spec.Bank < 1)
        {
            throw new SceneLayoutException("river: halfWidth, depth and bank must be at least 1.");
        }

        return spec;
    }

    private static List<PathSpec> ParsePaths(JsonElement root, Dictionary<string, SiteSpec> sites, int sizeX, int sizeZ)
    {
        var paths = new List<PathSpec>();
        if (!root.TryGetProperty("paths", out var element))
        {
            return paths;
        }

        foreach (var path in element.EnumerateArray())
        {
            var name = GetString(path, "name") ?? throw new SceneLayoutException("a path needs a name.");
            var from = GetString(path, "from") ?? throw new SceneLayoutException($"path '{name}' needs a from site.");
            var to = GetString(path, "to") ?? throw new SceneLayoutException($"path '{name}' needs a to site.");
            RequireSite(sites, from, $"path '{name}'");
            RequireSite(sites, to, $"path '{name}'");
            var via = path.TryGetProperty("via", out var v) ? ParseColumns(v, $"path '{name}' via", sizeX, sizeZ) : [];
            var mode = GetString(path, "mode") switch
            {
                "grade" => PathMode.Grade,
                "follow" or null => PathMode.Follow,
                var other => throw new SceneLayoutException($"path '{name}': mode must be grade or follow, not '{other}'."),
            };
            var half = GetOptionalInt(path, "halfWidth") ?? 1;
            if (half < 0)
            {
                throw new SceneLayoutException($"path '{name}': halfWidth must not be negative.");
            }

            paths.Add(new PathSpec(name, from, to, via, half, mode));
        }

        return paths;
    }

    private static Dictionary<string, StampSpec> ParseStamps(JsonElement root)
    {
        var stamps = new Dictionary<string, StampSpec>(StringComparer.Ordinal);
        if (!root.TryGetProperty("stamps", out var element))
        {
            return stamps;
        }

        foreach (var stamp in element.EnumerateObject())
        {
            var where = $"stamp '{stamp.Name}'";
            var legend = new Dictionary<char, BlockId>();
            foreach (var entry in Require(stamp.Value, "legend").EnumerateObject())
            {
                if (entry.Name.Length != 1 || entry.Value.ValueKind != JsonValueKind.String)
                {
                    throw new SceneLayoutException($"{where}: a legend entry is one character and a block name.");
                }

                legend[entry.Name[0]] = BlockNamed(entry.Value.GetString()!, where);
            }

            var layers = new List<IReadOnlyList<string>>();
            foreach (var layer in Require(stamp.Value, "layers").EnumerateArray())
            {
                var rows = new List<string>();
                foreach (var row in layer.EnumerateArray())
                {
                    var text = row.GetString() ?? throw new SceneLayoutException($"{where}: a row must be a string.");
                    foreach (var c in text)
                    {
                        if (c != ' ' && !legend.ContainsKey(c))
                        {
                            throw new SceneLayoutException($"{where}: '{c}' is not in the legend.");
                        }
                    }

                    rows.Add(text);
                }

                layers.Add(rows);
            }

            if (layers.Count == 0)
            {
                throw new SceneLayoutException($"{where} has no layers.");
            }

            stamps[stamp.Name] = new StampSpec(stamp.Name, legend, layers);
        }

        return stamps;
    }

    private static List<PropSpec> ParseProps(JsonElement root, Dictionary<string, SiteSpec> sites)
    {
        var props = new List<PropSpec>();
        if (!root.TryGetProperty("props", out var element))
        {
            return props;
        }

        foreach (var prop in element.EnumerateArray())
        {
            var type = GetString(prop, "type") ?? throw new SceneLayoutException("a prop needs a type.");
            var (x, z) = ParsePlace(prop, sites, $"prop '{type}'");
            var facing = prop.TryGetProperty("facing", out var f) ? ParseDirection(f, $"prop '{type}'") : (Direction?)null;
            var axis = GetString(prop, "axis");
            var blockName = GetString(prop, "block");
            props.Add(new PropSpec(
                type,
                x,
                z,
                GetString(prop, "name"),
                GetOptionalInt(prop, "width") ?? 0,
                GetOptionalInt(prop, "depth") ?? 0,
                GetOptionalInt(prop, "height") ?? 0,
                GetOptionalInt(prop, "length") ?? 0,
                facing,
                string.Equals(axis, "z", StringComparison.Ordinal),
                GetString(prop, "stamp"),
                blockName is null ? BlockId.Path : BlockNamed(blockName, $"prop '{type}'")));
        }

        return props;
    }

    private static List<NpcSpec> ParseNpcs(JsonElement root, Dictionary<string, SiteSpec> sites)
    {
        var npcs = new List<NpcSpec>();
        if (!root.TryGetProperty("npcs", out var element))
        {
            return npcs;
        }

        foreach (var npc in element.EnumerateArray())
        {
            var name = GetString(npc, "name") ?? throw new SceneLayoutException("an npc needs a name.");
            var (x, z) = ParsePlace(npc, sites, $"npc '{name}'");
            npcs.Add(new NpcSpec(name, x, z, ParseYaw(npc, $"npc '{name}'")));
        }

        return npcs;
    }

    private static List<PointSpec> ParsePoints(JsonElement root)
    {
        var points = new List<PointSpec>();
        if (!root.TryGetProperty("points", out var element))
        {
            return points;
        }

        foreach (var point in element.EnumerateObject())
        {
            points.Add(new PointSpec(point.Name, GetInt(point.Value, "x", $"point '{point.Name}'"), GetInt(point.Value, "z", $"point '{point.Name}'")));
        }

        return points;
    }

    private static TreeScatterSpec ParseTrees(JsonElement root)
    {
        if (!root.TryGetProperty("trees", out var element))
        {
            return new TreeScatterSpec(4, 0, 3, 3, 4, 5, 0);
        }

        var spec = new TreeScatterSpec(
            GetOptionalInt(element, "cell") ?? 4,
            GetOptionalInt(element, "chance") ?? 0,
            GetOptionalInt(element, "spacing") ?? 3,
            GetOptionalInt(element, "margin") ?? 3,
            GetOptionalInt(element, "minTrunk") ?? 4,
            GetOptionalInt(element, "maxTrunk") ?? 5,
            GetOptionalInt(element, "treeLine") ?? 80);
        if (spec.Cell < 2 || spec.Chance is < 0 or > 100 || spec.Spacing < 1 || spec.Margin < 0 || spec.MinTrunk < 1 || spec.MaxTrunk < spec.MinTrunk)
        {
            throw new SceneLayoutException("trees: cell must be at least 2, chance 0 to 100, spacing at least 1, margin at least 0 and 1 <= minTrunk <= maxTrunk.");
        }

        return spec;
    }

    private static List<(int X, int Z)> ParseColumns(JsonElement element, string where, int sizeX, int sizeZ)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new SceneLayoutException($"{where} must be an array of [x, z] pairs.");
        }

        var list = new List<(int X, int Z)>();
        foreach (var pair in element.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2 || !pair[0].TryGetInt32(out var x) || !pair[1].TryGetInt32(out var z))
            {
                throw new SceneLayoutException($"{where} must be an array of [x, z] pairs of whole numbers.");
            }

            if (x < 0 || x >= sizeX || z < 0 || z >= sizeZ)
            {
                throw new SceneLayoutException($"{where}: ({x}, {z}) lies outside the grid.");
            }

            list.Add((x, z));
        }

        return list;
    }

    /// <summary>A place: <c>site</c> plus <c>dx</c> and <c>dz</c>, or <c>x</c> and <c>z</c>.</summary>
    private static (int X, int Z) ParsePlace(JsonElement owner, Dictionary<string, SiteSpec> sites, string where)
    {
        if (GetString(owner, "site") is { } siteName)
        {
            var site = RequireSite(sites, siteName, where);
            return (site.X + (GetOptionalInt(owner, "dx") ?? 0), site.Z + (GetOptionalInt(owner, "dz") ?? 0));
        }

        return (GetInt(owner, "x", where), GetInt(owner, "z", where));
    }

    private static SiteSpec RequireSite(Dictionary<string, SiteSpec> sites, string name, string where) =>
        sites.TryGetValue(name, out var site) ? site : throw new SceneLayoutException($"{where}: there is no site '{name}'.");

    private static BlockId BlockNamed(string name, string where) =>
        BlockDefinition.FromName(name)?.Id ?? throw new SceneLayoutException($"{where}: unknown block '{name}'.");

    private static Direction ParseDirection(JsonElement element, string where)
    {
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        return text switch
        {
            "north" => Direction.North,
            "east" => Direction.East,
            "south" => Direction.South,
            "west" => Direction.West,
            _ => throw new SceneLayoutException($"{where}: facing must be north, east, south or west."),
        };
    }

    private static int ParseYaw(JsonElement owner, string where)
    {
        if (!owner.TryGetProperty("facing", out var element))
        {
            throw new SceneLayoutException($"{where} needs a facing.");
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var degrees))
        {
            return Angles.FromDegrees(degrees);
        }

        return (int)ParseDirection(element, where) * Angles.QuarterTurn;
    }

    private static JsonElement Require(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) ? value : throw new SceneLayoutException($"missing '{name}'.");

    private static string? GetString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetOptionalInt(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static int GetInt(JsonElement owner, string name, string where) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new SceneLayoutException($"{where}: '{name}' must be a whole number.");
}
