using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Simulation.Tests.Voxel;

/// <summary>Scenes and worlds for the voxel tests: the draft route loaded by path, and hand-built flat worlds.</summary>
internal static class VoxelTestSupport
{
    public const int Units = VoxelPhysics.UnitsPerBlock;

    /// <summary>The y of the top ground block of <see cref="FlatScene"/>; feet stand at <c>GroundTop + 1</c> blocks.</summary>
    public const int GroundTop = 9;

    private static readonly Lazy<SceneLayout> Draft = new(() => SceneLayout.Load(RouteDraftPath()));

    /// <summary>The layout of <c>game/content/game-v1/game/scene.json</c>.</summary>
    public static SceneLayout RouteDraft => Draft.Value;

    /// <summary>Builds the draft route for a seed.</summary>
    public static Scene BuildRoute(ulong seed) => SceneBuilder.Build(RouteDraft, seed);

    /// <summary>The path of the draft route, found by walking up from the test output to the game's folder (the one that holds <c>content/game-v1</c>).</summary>
    public static string RouteDraftPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "content", "game-v1", "manifest.json")))
            {
                return Path.Combine(directory.FullName, "content", "game-v1", "game", "scene.json");
            }
        }

        throw new InvalidOperationException("content/game-v1 was not found above the test assembly.");
    }

    /// <summary>
    /// A flat scene of 64 x 32 x 64 blocks: stone from y 0 to <see cref="GroundTop"/>, air above; the spawn is at (32, 10, 32) facing north.
    /// <paramref name="customise"/> may edit the grid.
    /// </summary>
    public static Scene FlatScene(Action<VoxelGrid>? customise = null)
    {
        var grid = new VoxelGrid(64, 32, 64);
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                for (var y = 0; y <= GroundTop; y++)
                {
                    grid.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }

        customise?.Invoke(grid);
        return new Scene("flat", 0, grid, new SpawnPoint(new BlockPos(32, GroundTop + 1, 32), 0), [], new SortedDictionary<string, BlockPos>(StringComparer.Ordinal), 0, 0);
    }

    /// <summary>A simulation on <see cref="FlatScene"/>.</summary>
    public static VoxelSimulation FlatSim(Action<VoxelGrid>? customise = null) => VoxelSimulation.Create(FlatScene(customise));

    /// <summary>Steps <paramref name="ticks"/> times with the same commands.</summary>
    public static void Run(VoxelSimulation sim, int ticks, params VoxelCommand[] commands)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Step(commands);
        }
    }
}
