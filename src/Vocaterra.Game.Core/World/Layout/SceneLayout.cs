using Vocaterra.Game.Core.World.Generation;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// A scene as plain values, parsed from the layout JSON (see <see cref="Parse"/>): the generator's parameters, the named sites at fixed positions, the
/// river, the lake, the waterfall, the paths, the block stamps, the props, the NPCs and the spawn. It lists no block: the
/// <see cref="SceneBuilder"/> makes the terrain from these and the seed.
/// </summary>
public sealed class SceneLayout
{
    internal SceneLayout(
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
        TreeScatterSpec trees,
        UndergrowthSpec undergrowth)
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
        Undergrowth = undergrowth;
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

    /// <summary>The undergrowth's parameters (S14).</summary>
    public UndergrowthSpec Undergrowth { get; }

    /// <summary>
    /// The beacon's words for the named places (CONTENT_PLAN C05): a site's, a point's or a named prop's <c>label</c> (<c>river</c> shows <c>río</c>). A place
    /// with none shows its own name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

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
    /// {site?, dx?, dz?, x?, z?, facing}, <c>points</c> {name: {x, z}} and <c>trees</c> {cell, chance, spacing, margin, minTrunk, maxTrunk, treeLine, spruceFrom?, birch?, birchAway?, shoreClear?, shoreFade?, shoreKeep?, clearings?: [{site, radius, fade?, keep?}], vistas?: [{site, yaw, half, radius, down?}]}
    /// and <c>undergrowth</c> {grass, fern, flowers, reeds, routeMargin} (S14). A
    /// prop, NPC or spawn is placed at a site's column plus an offset, or at a column. A facing is a compass word or a whole number of degrees.
    /// </summary>
    /// <exception cref="SceneLayoutException">The text is not valid JSON or does not describe a buildable layout.</exception>
    public static SceneLayout Parse(string json) => SceneLayoutParser.Parse(json);
}

