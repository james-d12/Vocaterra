using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>Shared set-up of the streaming tests (B07): the valley frozen into a region and the world composed over it once (they are pure and immutable), and a streamed scene with no chunk resident for each test to read, install and edit on its own.</summary>
internal static class StreamedWorlds
{
    private static readonly Lazy<AuthoredReliefRegion> Region = new(static () => AuthoredReliefRegion.Freeze(WorldFactory.ValleyRegionId, RouteScene.Build(42), keepChunks: true));

    private static readonly Lazy<WorldComposition> Composed = new(static () => WorldFactory.Compose(TestGame.Data, 42, new RegionCatalog([Region.Value])));

    /// <summary>The composed world over the valley (seed 42).</summary>
    public static WorldComposition Composition => Composed.Value;

    /// <summary>A streamed scene of the valley with nothing resident and the valley's edge wall, on the shared composed world.</summary>
    public static Scene Scene()
    {
        var composition = Composition;
        var region = Region.Value;
        var grid = WorldFactory.StreamedGrid(TestGame.Data, composition.Source);
        grid.Barrier = new BodyBarrier(region.Bounds.MinX, region.Bounds.MinZ, region.Bounds.MaxX, region.Bounds.MaxZ);
        return RouteScene.Build(42).WithWorld(grid, composition.Places, region.TemplateDigest, composition.Source, composition.World);
    }
}
