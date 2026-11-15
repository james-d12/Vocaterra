using System.Collections.ObjectModel;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.Water.Currents;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// A built scene: the voxel grid, the player's spawn, the NPC placements and the named points (<c>camp</c>, <c>stall</c>, <c>river</c> ...).
/// A named point is the block on top of the terrain at that column (the ground block, or the water surface block over a river), so its Y is
/// one lower than the feet cell of someone standing there. <see cref="VoxelSimulation"/> edits the grid in place; build a new scene for a
/// fresh run, or <see cref="Clone"/> one that no run has touched (a build of the bundle's 384 x 128 x 384 route takes about 0.6 s, a clone
/// copies its 38 MB of blocks).
/// </summary>
public sealed class Scene
{
    internal Scene(
        string name,
        ulong seed,
        VoxelGrid grid,
        SpawnPoint spawn,
        IReadOnlyList<NpcPlacement> npcs,
        SortedDictionary<string, BlockPos> points,
        int treeCount,
        int waterLevel,
        int plantCount = 0,
        IReadOnlyDictionary<string, string>? labels = null,
        IReadOnlyDictionary<string, IReadOnlyList<BlockPos>>? propCells = null,
        RiverCurrents? currents = null,
        int[]? groundHeights = null,
        TemplateBlockIndex? template = null)
    {
        GroundHeights = groundHeights;
        Template = template;
        Currents = currents ?? RiverCurrents.None;
        Labels = labels ?? new Dictionary<string, string>(StringComparer.Ordinal);
        PropCells = propCells ?? new Dictionary<string, IReadOnlyList<BlockPos>>(StringComparer.Ordinal);
        _propOf = PropCells.SelectMany(static p => p.Value.Select(cell => (cell, p.Key))).GroupBy(static t => t.cell).ToDictionary(static g => g.Key, static g => g.First().Key);
        Name = name;
        Seed = seed;
        Grid = grid;
        Spawn = spawn;
        Npcs = npcs;
        Points = new ReadOnlyDictionary<string, BlockPos>(points);
        TreeCount = treeCount;
        WaterLevel = waterLevel;
        PlantCount = plantCount;
    }

    private Scene(Scene source, VoxelGrid grid)
    {
        Name = source.Name;
        Seed = source.Seed;
        Grid = grid;
        Spawn = source.Spawn;
        Npcs = source.Npcs;
        Points = source.Points;
        Labels = source.Labels;
        PropCells = source.PropCells;
        _propOf = source._propOf;
        TreeCount = source.TreeCount;
        WaterLevel = source.WaterLevel;
        PlantCount = source.PlantCount;
        Currents = source.Currents;
        GroundHeights = source.GroundHeights;
        Template = source.Template;
        Places = source.Places;
        TemplateDigest = source.TemplateDigest;
        ChunkSource = source.ChunkSource;
    }

    private readonly Dictionary<BlockPos, string> _propOf;

    /// <summary>The ground height of every column the scene builder made, row by row (x fastest), with trees left out: the edge profile of the region the scene is (B06); <see langword="null"/> for a scene that was not built from a layout.</summary>
    internal int[]? GroundHeights { get; }

    /// <summary>Where the rarer kinds of block stood as the builder left them (the fire rings' stones, the roads), for the finders that must not read the world's chunks (B06).</summary>
    public TemplateBlockIndex? Template { get; }

    /// <summary>
    /// The positions of the blocks of a kind that stand in the world now, found without reading the world's chunks: the template's positions that still hold the kind, then those of the kind
    /// in the chunks that were changed (the player's edits, a loaded save's), which are the only other places a block of the kind can be (B06: a streamed world has no chunk list to scan).
    /// A scene with no template has none of the first.
    /// </summary>
    /// <param name="kind">The block kind (not air; not a kind that fills many chunks).</param>
    public IReadOnlyList<BlockPos> CurrentBlocksOfKind(BlockId kind)
    {
        var found = new List<BlockPos>();
        var seen = new HashSet<BlockPos>();
        foreach (var cell in Template?.Of(kind) ?? [])
        {
            if (Grid.GetBlock(cell.X, cell.Y, cell.Z) == kind && seen.Add(cell))
            {
                found.Add(cell);
            }
        }

        var bit = 1UL << (int)kind;
        foreach (var chunk in Grid.ChangedChunks())
        {
            if ((chunk.KindMask & bit) == 0)
            {
                continue;
            }

            var origin = chunk.Coord.Origin;
            var blocks = chunk.Blocks;
            for (var i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == kind)
                {
                    var cell = new BlockPos(origin.X + (i & (VoxelChunk.Size - 1)), origin.Y + (i >> (2 * VoxelChunk.Shift)), origin.Z + ((i >> VoxelChunk.Shift) & (VoxelChunk.Size - 1)));
                    if (seen.Add(cell))
                    {
                        found.Add(cell);
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The place query of the world (B06, <see cref="IPlaceInfo"/>): climate, temperature, biome, region, natural surface and whether a point is underground, read from the pure model and never from
    /// the edited grid, so any thread may ask. <see langword="null"/> for a scene that <see cref="WorldFactory"/> did not make (a test's own).
    /// </summary>
    public IPlaceInfo? Places { get; private init; }

    /// <summary>The digest of the region template the world copies its valley from (<see cref="WorldStateHeader.TemplateDigest"/>), or <see langword="null"/> for a scene <see cref="WorldFactory"/> did not make.</summary>
    public byte[]? TemplateDigest { get; private init; }

    /// <summary>The pure source of the streamed world's chunks, or <see langword="null"/> for a bounded scene.</summary>
    public IChunkSource? ChunkSource { get; private init; }

    /// <summary>A scene like this one on another grid, with the world's place query, template digest and chunk source set (<see cref="WorldFactory"/>).</summary>
    internal Scene WithWorld(VoxelGrid grid, IPlaceInfo places, byte[] templateDigest, IChunkSource? source) =>
        new(this, grid) { Places = places, TemplateDigest = templateDigest, ChunkSource = source };

    /// <summary>The layout's name.</summary>
    public string Name { get; }

    /// <summary>The world seed the tree scatter was drawn from.</summary>
    public ulong Seed { get; }

    /// <summary>The blocks.</summary>
    public VoxelGrid Grid { get; }

    /// <summary>The player's start.</summary>
    public SpawnPoint Spawn { get; }

    /// <summary>The NPC placements, in the layout's order.</summary>
    public IReadOnlyList<NpcPlacement> Npcs { get; }

    /// <summary>The named points, ordered by name (ordinal).</summary>
    public IReadOnlyDictionary<string, BlockPos> Points { get; }

    /// <summary>The beacon's words for the named places, from the layout (<see cref="Layout.SceneLayout.Labels"/>).</summary>
    public IReadOnlyDictionary<string, string> Labels { get; }

    /// <summary>
    /// The cells a named stamp placed (W07: the oven is the stamp named <c>oven</c>), by the prop's name, in the order the stamp was drawn. A prop with no name has no entry.
    /// A cell the player later mines stays listed: the lookup is asked only of a block the crosshair is on.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<BlockPos>> PropCells { get; }

    /// <summary>The name of the stamp that placed a cell, or <see langword="null"/> when the cell is no named stamp's.</summary>
    /// <param name="cell">The cell.</param>
    public string? PropAt(BlockPos cell) => _propOf.TryGetValue(cell, out var name) ? name : null;

    /// <summary>The word the beacon shows for a named place: its label (<c>river</c> shows <c>río</c>), or the place's own name when it has none.</summary>
    /// <param name="pointName">The place.</param>
    public string LabelOf(string pointName) => Labels.TryGetValue(pointName, out var label) ? label : pointName;

    /// <summary>How many trees the scatter planted (explicit tree props are not counted).</summary>
    public int TreeCount { get; }

    /// <summary>How many undergrowth blocks (tall grass, ferns, reeds, flowers) the scene builder planted (S14).</summary>
    public int PlantCount { get; }

    /// <summary>The river's current (water-plan 4.6): built with the scene from the layout, shared by clones, never saved.</summary>
    public RiverCurrents Currents { get; }

    /// <summary>The y of the water surface block.</summary>
    public int WaterLevel { get; }

    /// <summary>
    /// A copy with its own grid (<see cref="VoxelGrid.Clone"/>), so a run edits it apart from this scene. The spawn, the NPC placements, the
    /// named points and their labels are read-only and shared.
    /// </summary>
    internal Scene Clone() => new(this, Grid.Clone());

    /// <summary>The named point <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">The scene has no such point.</exception>
    public BlockPos Point(string name) => Points.TryGetValue(name, out var point) ? point : throw new KeyNotFoundException($"The scene has no point '{name}'.");

    /// <summary>The placement of the NPC called <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">The scene has no such NPC.</exception>
    public NpcPlacement Npc(string name)
    {
        foreach (var npc in Npcs)
        {
            if (string.Equals(npc.Name, name, StringComparison.Ordinal))
            {
                return npc;
            }
        }

        throw new KeyNotFoundException($"The scene has no NPC '{name}'.");
    }
}

