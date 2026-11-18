using System.Text.Json;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// Reads the layout JSON into a <see cref="SceneLayout"/>: the document's shape and its checks (the shape is described on
/// <see cref="SceneLayout.Parse"/>). Every element at fault is named in the <see cref="SceneLayoutException"/>.
/// </summary>
internal static class SceneLayoutParser
{
    /// <summary>Parses the layout JSON (see <see cref="SceneLayout.Parse"/>).</summary>
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
            var pitchDegrees = GetOptionalInt(spawnElement, "pitch") ?? 0;
            if (pitchDegrees is < -80 or > 80)
            {
                throw new SceneLayoutException("spawn: pitch must be between -80 and 80 degrees.");
            }

            var spawn = new SpawnSpec(spawnX, spawnZ, ParseYaw(spawnElement, "spawn"), pitchDegrees * Angles.StepsPerTurn / 360);
            var points = ParsePoints(root);
            var trees = ParseTrees(root, byName);
            var undergrowth = ParseUndergrowth(root);
            return new SceneLayout(name, sizeX, sizeY, sizeZ, waterLevel, terrain, sites, waterfall, lake, river, paths, stamps, props, npcs, spawn, points, trees, undergrowth)
            {
                Labels = ParseLabels(root),
            };
        }
    }

    // The words the beacon shows for the named places (content-plan C05): a site's, a point's or a named prop's optional "label", in the target language.
    private static Dictionary<string, string> ParseLabels(JsonElement root)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string name, JsonElement owner, string where)
        {
            if (owner.TryGetProperty("label", out var value))
            {
                var label = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (string.IsNullOrWhiteSpace(label))
                {
                    throw new SceneLayoutException($"{where}: the label must be a word.");
                }

                if (!labels.TryAdd(name, label))
                {
                    throw new SceneLayoutException($"{where}: the place '{name}' has a label twice.");
                }
            }
        }

        foreach (var section in new[] { "sites", "points" })
        {
            if (root.TryGetProperty(section, out var places) && places.ValueKind == JsonValueKind.Object)
            {
                foreach (var place in places.EnumerateObject())
                {
                    Add(place.Name, place.Value, $"{section[..^1]} '{place.Name}'");
                }
            }
        }

        if (root.TryGetProperty("props", out var props))
        {
            foreach (var prop in props.EnumerateArray())
            {
                if (GetString(prop, "name") is { } name)
                {
                    Add(name, prop, $"prop '{name}'");
                }
                else if (prop.TryGetProperty("label", out _))
                {
                    throw new SceneLayoutException($"prop '{GetString(prop, "type")}': a label needs the prop's name.");
                }
            }
        }

        return labels;
    }

    private static TerrainSpec ParseTerrain(JsonElement element, int sizeX, int sizeY, int sizeZ)
    {
        var columns = sizeX / VoxelChunk.Size;
        var rows = sizeZ / VoxelChunk.Size;
        var relief = new int[columns * rows];
        var valley = GetOptionalInt(element, "valley") ?? 36;
        if (!element.TryGetProperty("relief", out var lines))
        {
            // A layout with no relief is flat at the valley's height: a region on procedural ground (B15) takes its height from the world and draws none.
            Array.Fill(relief, valley);
        }
        else
        {
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
            valley,
            GetOptionalInt(element, "rockLine") ?? 85,
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

            var anchors = new Dictionary<string, (int Column, int Row)>(StringComparer.Ordinal);
            if (stamp.Value.TryGetProperty("anchors", out var anchorElement))
            {
                var drawnWidth = layers.Max(static l => l.Count == 0 ? 0 : l.Max(static row => row.Length));
                var drawnDepth = layers.Max(static l => l.Count);
                foreach (var anchor in anchorElement.EnumerateObject())
                {
                    var at = anchor.Value;
                    if (at.ValueKind != JsonValueKind.Array || at.GetArrayLength() != 2 || at[0].ValueKind != JsonValueKind.Number || at[1].ValueKind != JsonValueKind.Number)
                    {
                        throw new SceneLayoutException($"{where}: the anchor '{anchor.Name}' is [column, row].");
                    }

                    var (column, row) = (at[0].GetInt32(), at[1].GetInt32());
                    if (column < 0 || column >= drawnWidth || row < 0 || row >= drawnDepth)
                    {
                        throw new SceneLayoutException($"{where}: the anchor '{anchor.Name}' lies outside the drawing.");
                    }

                    anchors[anchor.Name] = (column, row);
                }
            }

            stamps[stamp.Name] = new StampSpec(stamp.Name, legend, layers, anchors.Count == 0 ? null : anchors);
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
                blockName is null ? BlockId.Path : BlockNamed(blockName, $"prop '{type}'"),
                GetOptionalInt(prop, "floorY")));
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

    private static UndergrowthSpec ParseUndergrowth(JsonElement root)
    {
        if (!root.TryGetProperty("undergrowth", out var element))
        {
            return UndergrowthSpec.None;
        }

        var spec = new UndergrowthSpec(
            GetOptionalInt(element, "grass") ?? 0,
            GetOptionalInt(element, "fern") ?? 20,
            GetOptionalInt(element, "flowers") ?? 0,
            GetOptionalInt(element, "reeds") ?? 0,
            GetOptionalInt(element, "routeMargin") ?? 1);
        if (spec.Grass is < 0 or > 100 || spec.Fern is < 0 or > 100 || spec.Flowers is < 0 or > 100 || spec.Reeds is < 0 or > 100 || spec.RouteMargin < 0)
        {
            throw new SceneLayoutException("undergrowth: grass, fern, flowers and reeds are percents (0 to 100) and routeMargin is at least 0.");
        }

        return spec;
    }

    private static TreeScatterSpec ParseTrees(JsonElement root, Dictionary<string, SiteSpec> sites)
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
            GetOptionalInt(element, "treeLine") ?? 80,
            GetOptionalInt(element, "spruceFrom") ?? 48,
            GetOptionalInt(element, "birch") ?? 10,
            GetOptionalInt(element, "birchAway") ?? 30,
            GetOptionalInt(element, "shoreClear") ?? 0,
            Math.Max(1, GetOptionalInt(element, "shoreFade") ?? 1),
            GetOptionalInt(element, "shoreKeep") ?? 100,
            ParseClearings(element, sites),
            ParseVistas(element, sites));
        if (spec.Cell < 2 || spec.Chance is < 0 or > 100 || spec.Spacing < 1 || spec.Margin < 0 || spec.MinTrunk < 1 || spec.MaxTrunk < spec.MinTrunk)
        {
            throw new SceneLayoutException("trees: cell must be at least 2, chance 0 to 100, spacing at least 1, margin at least 0 and 1 <= minTrunk <= maxTrunk.");
        }

        return spec;
    }

    private static List<ClearingSpec> ParseClearings(JsonElement trees, Dictionary<string, SiteSpec> sites)
    {
        var list = new List<ClearingSpec>();
        if (!trees.TryGetProperty("clearings", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in element.EnumerateArray())
        {
            var siteName = GetString(item, "site") ?? throw new SceneLayoutException("trees.clearings: a clearing needs a site.");
            RequireSite(sites, siteName, "trees.clearings");
            var clearing = new ClearingSpec(siteName, GetInt(item, "radius", "trees.clearings"), Math.Max(1, GetOptionalInt(item, "fade") ?? 16), GetOptionalInt(item, "keep") ?? 0);
            if (clearing.Radius < 0 || clearing.Keep is < 0 or > 100)
            {
                throw new SceneLayoutException("trees.clearings: radius must be at least 0 and keep a percent.");
            }

            list.Add(clearing);
        }

        return list;
    }

    private static List<VistaSpec> ParseVistas(JsonElement trees, Dictionary<string, SiteSpec> sites)
    {
        var list = new List<VistaSpec>();
        if (!trees.TryGetProperty("vistas", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in element.EnumerateArray())
        {
            var siteName = GetString(item, "site") ?? throw new SceneLayoutException("trees.vistas: a vista needs a site.");
            var site = RequireSite(sites, siteName, "trees.vistas");
            if (site.Y is null)
            {
                throw new SceneLayoutException($"trees.vistas: the site '{siteName}' needs a y (the lookout's height).");
            }

            var vista = new VistaSpec(siteName, GetInt(item, "yaw", "trees.vistas"), GetInt(item, "half", "trees.vistas"), GetInt(item, "radius", "trees.vistas"), GetOptionalInt(item, "down") ?? 12);
            if (vista.HalfAngleDegrees is < 1 or > 179 || vista.Radius < 1 || vista.DownDegrees is < 0 or > 60)
            {
                throw new SceneLayoutException("trees.vistas: half must be 1 to 179, radius at least 1 and down 0 to 60 degrees.");
            }

            list.Add(vista);
        }

        return list;
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

    internal static BlockId BlockNamed(string name, string where) =>
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

