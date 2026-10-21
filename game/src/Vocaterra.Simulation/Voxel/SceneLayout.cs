using System.Text.Json;

namespace Vocaterra.Simulation.Voxel;

/// <summary>A layout file is malformed or cannot be built (a bad legend character, a prop on uneven ground, a point outside the grid).</summary>
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

/// <summary>What a feature-map character means for a column of terrain.</summary>
/// <param name="Key">The character in the feature map.</param>
/// <param name="Top">The block on top of the column (at the column's height).</param>
/// <param name="Sub">The block of the three layers under the top; stone is below that.</param>
/// <param name="Water">Whether the column is filled with water from just above its top up to the layout's water level.</param>
/// <param name="Plantable">Whether the tree scatter may plant a tree here.</param>
/// <param name="Reserved">Whether the column belongs to the route (path, clearing, village ground, river): trees keep a margin away from it.</param>
public sealed record LegendEntry(char Key, BlockId Top, BlockId Sub, bool Water, bool Plantable, bool Reserved);

/// <summary>
/// A prop placed on the terrain. <see cref="Type"/> is one of <c>workbench</c>, <c>fire_ring</c>, <c>stall</c>, <c>house</c>, <c>signpost</c>,
/// <c>bridge</c>, <c>bush</c>, <c>berry_bush</c> and <c>tree</c>; the other members are the type's parameters, 0 or <see langword="null"/> when unset.
/// </summary>
/// <param name="Type">The prop type.</param>
/// <param name="X">The anchor column: the centre for a fire ring, a sign, a bush and a tree; the first block of a workbench; the north-west corner of a stall and a house; the centre line's first column of a bridge.</param>
/// <param name="Z">The anchor row (see <paramref name="X"/>).</param>
/// <param name="Name">The named point the prop registers (at its reference cell), or <see langword="null"/>.</param>
/// <param name="Width">The size along X of a stall or a house; the number of lanes of a bridge (default 3).</param>
/// <param name="Depth">The size along Z of a stall or a house.</param>
/// <param name="Height">The wall height of a house (default 3); the trunk height of an explicit tree (default 4).</param>
/// <param name="Length">The number of blocks of a workbench (default 2) or the span of a bridge.</param>
/// <param name="Facing">The side a house's door is on, the side a stall's counter is on, or the direction a bridge runs.</param>
/// <param name="AlongZ">For a workbench: whether it extends along Z instead of X.</param>
public sealed record PropSpec(
    string Type, int X, int Z, string? Name, int Width, int Depth, int Height, int Length, Direction? Facing, bool AlongZ);

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

/// <summary>A named point of the route (<c>camp</c>, <c>stall</c>, <c>river</c>, ...), given by column and row; its Y is the top of the terrain (or of the water).</summary>
/// <param name="Name">The point's name.</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
public sealed record PointSpec(string Name, int X, int Z);

/// <summary>The tree scatter's parameters.</summary>
/// <param name="Attempts">How many random positions are tried.</param>
/// <param name="Max">The most trees planted.</param>
/// <param name="Spacing">The least Chebyshev distance between two trunks.</param>
/// <param name="Margin">The least Chebyshev distance between a trunk and any route cell (the legend's reserved columns, prop footprints, spawn, NPCs and points). The canopy reaches 2, so a margin of 3 keeps every leaf off the route.</param>
/// <param name="MinTrunk">The shortest trunk in blocks.</param>
/// <param name="MaxTrunk">The longest trunk in blocks.</param>
public sealed record TreeScatterSpec(int Attempts, int Max, int Spacing, int Margin, int MinTrunk, int MaxTrunk);

/// <summary>
/// A hand-authored scene as plain values, parsed from the layout JSON (see <see cref="Parse"/>). The ASCII maps use one character per
/// column, one string per row (row 0 is the north edge, character 0 the west edge): a base-36 digit in the height map (the y of the
/// column's top block) and a legend character in the feature map.
/// </summary>
public sealed class SceneLayout
{
    private readonly int[] _heights;
    private readonly char[] _features;

    private SceneLayout(
        string name,
        int sizeX,
        int sizeY,
        int sizeZ,
        int waterLevel,
        IReadOnlyDictionary<char, LegendEntry> legend,
        int[] heights,
        char[] features,
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
        Legend = legend;
        _heights = heights;
        _features = features;
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

    /// <summary>The y of the water surface block: columns whose top is lower and whose legend entry says water are filled up to it.</summary>
    public int WaterLevel { get; }

    /// <summary>The legend, by feature character.</summary>
    public IReadOnlyDictionary<char, LegendEntry> Legend { get; }

    /// <summary>The props, in the order the layout lists them (they are built in this order, later ones over earlier ones).</summary>
    public IReadOnlyList<PropSpec> Props { get; }

    /// <summary>The NPC positions.</summary>
    public IReadOnlyList<NpcSpec> Npcs { get; }

    /// <summary>The player's spawn.</summary>
    public SpawnSpec Spawn { get; }

    /// <summary>The explicitly named points (props with a name add theirs when the scene is built).</summary>
    public IReadOnlyList<PointSpec> Points { get; }

    /// <summary>The tree scatter's parameters.</summary>
    public TreeScatterSpec Trees { get; }

    /// <summary>The y of the top block of the column (x, z), from the height map.</summary>
    public int HeightAt(int x, int z) => _heights[(z * SizeX) + x];

    /// <summary>The feature character of the column (x, z).</summary>
    public char FeatureAt(int x, int z) => _features[(z * SizeX) + x];

    /// <summary>Reads a layout file.</summary>
    /// <exception cref="SceneLayoutException">The file is malformed.</exception>
    public static SceneLayout Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Parses the layout JSON. Comments and trailing commas are allowed. The shape: <c>name</c>, <c>size</c> {x, y, z}, <c>waterLevel</c>,
    /// <c>legend</c> {char: {top, sub?, water?, plantable?, reserved?}}, <c>heights</c> and <c>features</c> (arrays of one string per row),
    /// <c>props</c> [{type, x, z, name?, width?, depth?, height?, length?, facing?, axis?}], <c>npcs</c> [{name, x, z, facing}], <c>spawn</c>
    /// {x, z, facing}, <c>points</c> {name: {x, z}} and <c>trees</c> {attempts, max, spacing, margin, minTrunk, maxTrunk}. A facing is a
    /// compass word or a whole number of degrees (0 north, 90 east).
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

            var legend = ParseLegend(Require(root, "legend"));
            var heights = ParseHeights(Require(root, "heights"), sizeX, sizeY, sizeZ);
            var features = ParseFeatures(Require(root, "features"), sizeX, sizeZ, legend);
            var props = ParseProps(root);
            var npcs = ParseNpcs(root);
            var spawnElement = Require(root, "spawn");
            var spawn = new SpawnSpec(GetInt(spawnElement, "x", "spawn"), GetInt(spawnElement, "z", "spawn"), ParseYaw(spawnElement, "spawn"));
            var points = ParsePoints(root);
            var trees = ParseTrees(root);
            return new SceneLayout(name, sizeX, sizeY, sizeZ, waterLevel, legend, heights, features, props, npcs, spawn, points, trees);
        }
    }

    private static Dictionary<char, LegendEntry> ParseLegend(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SceneLayoutException("legend must be an object.");
        }

        var legend = new Dictionary<char, LegendEntry>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Length != 1)
            {
                throw new SceneLayoutException($"legend key '{property.Name}' must be one character.");
            }

            var key = property.Name[0];
            var topName = GetString(property.Value, "top") ?? throw new SceneLayoutException($"legend '{key}' needs a top block.");
            var top = BlockNamed(topName, $"legend '{key}'");
            var sub = GetString(property.Value, "sub") is { } subName ? BlockNamed(subName, $"legend '{key}'") : BlockId.Dirt;
            legend[key] = new LegendEntry(key, top, sub, GetBool(property.Value, "water"), GetBool(property.Value, "plantable"), GetBool(property.Value, "reserved"));
        }

        if (legend.Count == 0)
        {
            throw new SceneLayoutException("legend is empty.");
        }

        return legend;
    }

    private static int[] ParseHeights(JsonElement element, int sizeX, int sizeY, int sizeZ)
    {
        var rows = Rows(element, "heights", sizeX, sizeZ);
        var heights = new int[sizeX * sizeZ];
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var c = rows[z][x];
                var value = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'z' ? c - 'a' + 10 : -1;
                if (value < 0 || value >= sizeY - 8)
                {
                    throw new SceneLayoutException($"heights: '{c}' at x={x}, z={z} is not a base-36 digit below {sizeY - 8}.");
                }

                heights[(z * sizeX) + x] = value;
            }
        }

        return heights;
    }

    private static char[] ParseFeatures(JsonElement element, int sizeX, int sizeZ, Dictionary<char, LegendEntry> legend)
    {
        var rows = Rows(element, "features", sizeX, sizeZ);
        var features = new char[sizeX * sizeZ];
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var c = rows[z][x];
                if (!legend.ContainsKey(c))
                {
                    throw new SceneLayoutException($"features: '{c}' at x={x}, z={z} is not in the legend.");
                }

                features[(z * sizeX) + x] = c;
            }
        }

        return features;
    }

    private static string[] Rows(JsonElement element, string what, int sizeX, int sizeZ)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != sizeZ)
        {
            throw new SceneLayoutException($"{what} must be an array of {sizeZ} strings (one per row, north first).");
        }

        var rows = new string[sizeZ];
        var z = 0;
        foreach (var row in element.EnumerateArray())
        {
            var text = row.ValueKind == JsonValueKind.String ? row.GetString() : null;
            if (text is null || text.Length != sizeX)
            {
                throw new SceneLayoutException($"{what} row {z} must be a string of {sizeX} characters.");
            }

            rows[z++] = text;
        }

        return rows;
    }

    private static List<PropSpec> ParseProps(JsonElement root)
    {
        var props = new List<PropSpec>();
        if (!root.TryGetProperty("props", out var element))
        {
            return props;
        }

        foreach (var prop in element.EnumerateArray())
        {
            var type = GetString(prop, "type") ?? throw new SceneLayoutException("a prop needs a type.");
            var facing = prop.TryGetProperty("facing", out var f) ? ParseDirection(f, $"prop '{type}'") : (Direction?)null;
            var axis = GetString(prop, "axis");
            props.Add(new PropSpec(
                type,
                GetInt(prop, "x", $"prop '{type}'"),
                GetInt(prop, "z", $"prop '{type}'"),
                GetString(prop, "name"),
                GetOptionalInt(prop, "width") ?? 0,
                GetOptionalInt(prop, "depth") ?? 0,
                GetOptionalInt(prop, "height") ?? 0,
                GetOptionalInt(prop, "length") ?? 0,
                facing,
                string.Equals(axis, "z", StringComparison.Ordinal)));
        }

        return props;
    }

    private static List<NpcSpec> ParseNpcs(JsonElement root)
    {
        var npcs = new List<NpcSpec>();
        if (!root.TryGetProperty("npcs", out var element))
        {
            return npcs;
        }

        foreach (var npc in element.EnumerateArray())
        {
            var name = GetString(npc, "name") ?? throw new SceneLayoutException("an npc needs a name.");
            npcs.Add(new NpcSpec(name, GetInt(npc, "x", $"npc '{name}'"), GetInt(npc, "z", $"npc '{name}'"), ParseYaw(npc, $"npc '{name}'")));
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
            return new TreeScatterSpec(0, 0, 3, 3, 4, 5);
        }

        var spec = new TreeScatterSpec(
            GetOptionalInt(element, "attempts") ?? 0,
            GetOptionalInt(element, "max") ?? 0,
            GetOptionalInt(element, "spacing") ?? 3,
            GetOptionalInt(element, "margin") ?? 3,
            GetOptionalInt(element, "minTrunk") ?? 4,
            GetOptionalInt(element, "maxTrunk") ?? 5);
        if (spec.Spacing < 1 || spec.Margin < 0 || spec.MinTrunk < 1 || spec.MaxTrunk < spec.MinTrunk)
        {
            throw new SceneLayoutException("trees: spacing must be at least 1, margin at least 0 and 1 <= minTrunk <= maxTrunk.");
        }

        return spec;
    }

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

    private static bool GetBool(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int? GetOptionalInt(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static int GetInt(JsonElement owner, string name, string where) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new SceneLayoutException($"{where}: '{name}' must be a whole number.");
}
