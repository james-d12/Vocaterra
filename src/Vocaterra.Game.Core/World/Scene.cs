using System.Collections.ObjectModel;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
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
        RiverCurrents? currents = null)
    {
        Currents = currents ?? RiverCurrents.None;
        Labels = labels ?? new Dictionary<string, string>(StringComparer.Ordinal);
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
        TreeCount = source.TreeCount;
        WaterLevel = source.WaterLevel;
        PlantCount = source.PlantCount;
        Currents = source.Currents;
    }

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

