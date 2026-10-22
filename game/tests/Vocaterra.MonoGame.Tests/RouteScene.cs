using Vocaterra.Game;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The bundle's route as a test session: the scene built from a seed, and a voxel session over it at the fixed start time.</summary>
internal static class RouteScene
{
    /// <summary>The scene of the bundle's route for a seed.</summary>
    public static Scene Build(ulong seed = 42) =>
        SceneBuilder.Build(SceneLayout.Load(System.IO.Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")), seed);

    /// <summary>A new session on the route at the fixed start time.</summary>
    public static VoxelSession NewSession(ContentBundle bundle, NounCatalog nouns, CreditRules? credit = null, ulong seed = 42) =>
        new(bundle, seed, GuideSession.DefaultStartedAt, nouns, Build(seed), credit ?? CreditRules.Default);
}
