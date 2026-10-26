using System.Collections.ObjectModel;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// A built scene: the voxel grid, the player's spawn, the NPC placements and the named points (<c>camp</c>, <c>stall</c>, <c>river</c> ...).
/// A named point is the block on top of the terrain at that column (the ground block, or the water surface block over a river), so its Y is
/// one lower than the feet cell of someone standing there. <see cref="VoxelSimulation"/> edits the grid in place; build a new scene for a
/// fresh run (it is cheap).
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
        int plantCount = 0)
    {
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

    /// <summary>How many trees the scatter planted (explicit tree props are not counted).</summary>
    public int TreeCount { get; }

    /// <summary>How many undergrowth blocks (tall grass, ferns, reeds, flowers) the scene builder planted (S14).</summary>
    public int PlantCount { get; }

    /// <summary>The y of the water surface block.</summary>
    public int WaterLevel { get; }

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

