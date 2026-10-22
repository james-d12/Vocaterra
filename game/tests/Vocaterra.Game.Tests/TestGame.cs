using Vocaterra.Game;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>Shared set-up: the game bundle loaded once, and sessions on the bundle's route for a fixed seed and a fixed stored time.</summary>
internal static class TestGame
{
    /// <summary>The seed every guide test uses unless it says otherwise.</summary>
    public const ulong Seed = 42;

    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> LoadedNouns = new(() => NounCatalog.Load(Bundle));

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>The noun catalogue of the bundle.</summary>
    public static NounCatalog Nouns => LoadedNouns.Value;

    /// <summary>The bundle's route scene built from a seed.</summary>
    public static Scene Route(ulong seed = Seed) =>
        SceneBuilder.Build(SceneLayout.Load(Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")), seed);

    /// <summary>A new session on the route for a seed, with the fixed start time.</summary>
    public static VoxelSession NewSession(ulong seed = Seed, CreditRules? credit = null) =>
        new(Bundle, seed, GuideSession.DefaultStartedAt, Nouns, Route(seed), credit ?? CreditRules.FullTyped);

    /// <summary>The live facts of a session's world (the blocks as they are now).</summary>
    public static WorldFacts3D FactsOf(VoxelSession session) => WorldFacts3D.Live(session.Scene.Grid, Nouns);

    /// <summary>Where the player stands as the guide's facts need it (the block under the body, the exact centre and the facing).</summary>
    public static GuideViewpoint ViewpointOf(VoxelSession session)
    {
        var body = session.Simulation.Player;
        return new GuideViewpoint(body.FeetBlock, body.CenterX, body.CenterZ, body.Yaw);
    }

    /// <summary>The nearest block of a catalogue noun from the player, as the guide finds it (the cell).</summary>
    public static GuideCell NearestOf(VoxelSession session, string lemma) =>
        FactsOf(session).NearestOfNoun(ViewpointOf(session), lemma)!.Cell;

    /// <summary>
    /// The nearest block of one of the kinds by brute force over the whole grid, with the guide's rule written out again: the smallest squared
    /// distance from the player's feet, then the smaller y, z and x.
    /// </summary>
    public static BlockPos NearestBlock(VoxelSession session, params BlockId[] kinds)
    {
        var grid = session.Scene.Grid;
        var from = session.Simulation.Player.FeetBlock;
        BlockPos? best = null;
        long bestDistance = long.MaxValue;
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    if (!kinds.Contains(grid.GetBlock(x, y, z)))
                    {
                        continue;
                    }

                    var distance = ((long)(x - from.X) * (x - from.X)) + ((long)(z - from.Z) * (z - from.Z));
                    if (distance < bestDistance || (distance == bestDistance && (y, z, x).CompareTo((best!.Value.Y, best.Value.Z, best.Value.X)) < 0))
                    {
                        bestDistance = distance;
                        best = new BlockPos(x, y, z);
                    }
                }
            }
        }

        return best ?? throw new InvalidOperationException("No such block in the scene.");
    }
}
