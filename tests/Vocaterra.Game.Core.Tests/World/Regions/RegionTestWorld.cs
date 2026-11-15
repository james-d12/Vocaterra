using System.Collections.Concurrent;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>Shared set-up of the region tests (B06): the valley frozen into a region once per seed, and the world composed over it. The sources are pure, so one per seed serves every test.</summary>
internal static class RegionTestWorld
{
    /// <summary>The seed of the composite world's chunk tests.</summary>
    public const ulong Seed = 42;

    private static readonly ConcurrentDictionary<ulong, Lazy<AuthoredReliefRegion>> Valleys = new();
    private static readonly ConcurrentDictionary<ulong, Lazy<WorldComposition>> Compositions = new();

    /// <summary>The valley of a seed as a region, chunks included for the default seed and only its heights for the others.</summary>
    public static AuthoredReliefRegion Valley(ulong seed = Seed) =>
        Valleys.GetOrAdd(seed, static s => new Lazy<AuthoredReliefRegion>(() => AuthoredReliefRegion.Freeze(WorldFactory.ValleyRegionId, RouteScene.Build(s), keepChunks: s == Seed))).Value;

    /// <summary>The composed world over the valley of a seed.</summary>
    public static WorldComposition Composition(ulong seed = Seed) =>
        Compositions.GetOrAdd(seed, static s => new Lazy<WorldComposition>(() => WorldFactory.Compose(TestGame.Data, s, new RegionCatalog([Valley(s)])))).Value;

    /// <summary>The ground of a column as the composed world's height model makes it.</summary>
    public static ColumnShape Shape(WorldComposition world, int x, int z)
    {
        var g = world.Procedural.World;
        return g.Height.Shape(x, z, g.Climate.At(x, z));
    }

    /// <summary>The valley's footprint in blocks (0 to 383 on each side for the shipped layout), read from the region.</summary>
    public static RegionBounds Bounds => Valley().Bounds;

    /// <summary>A small region of another size and place, built only here: the test world of the front-end tests (its layout widened to 128 x 64 x 96 blocks and its level ground raised to y 40), frozen at chunk (40, -30).</summary>
    public static AuthoredReliefRegion Small() => SmallRegion.Value;

    private static readonly Lazy<AuthoredReliefRegion> SmallRegion = new(static () =>
    {
        var json = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Tests", "Fixtures", "clearing-route.json"))
            .Replace("\"size\": { \"x\": 96, \"y\": 32, \"z\": 96 }", "\"size\": { \"x\": 128, \"y\": 64, \"z\": 96 }", StringComparison.Ordinal)
            .Replace("\"waterLevel\": 4", "\"waterLevel\": 34", StringComparison.Ordinal)
            .Replace("\"valley\": 8", "\"valley\": 36", StringComparison.Ordinal)
            .Replace("\"ceiling\": 20", "\"ceiling\": 48", StringComparison.Ordinal)
            .Replace("\"y\": 10, \"radius\": 46", "\"y\": 40, \"radius\": 46", StringComparison.Ordinal);
        var scene = SceneBuilder(SceneLayout.Parse(json));
        return AuthoredReliefRegion.Freeze("small", scene, 40, -30);
    });

    private static Scene SceneBuilder(SceneLayout layout) => Vocaterra.Game.Core.World.Generation.SceneBuilder.Build(layout, 42);
}
