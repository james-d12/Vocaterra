using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>The valley as the game builds it (B13): the scene builder with the shipped bundle's underground laid into the valley's buried stone, and the same valley without it.</summary>
internal static class ValleyBuild
{
    /// <summary>Builds the layout as the world factory does: with the shipped underground in the valley's stone.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="seed">The world seed.</param>
    public static Scene Build(SceneLayout layout, ulong seed) => SceneBuilder.Build(layout, seed, null, For(seed));

    /// <summary>Builds the layout with plain stone under the valley (what the valley was until B13).</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="seed">The world seed.</param>
    public static Scene Bare(SceneLayout layout, ulong seed) => SceneBuilder.Build(layout, seed);

    /// <summary>The valley's underground of the shipped bundle.</summary>
    /// <param name="seed">The world seed.</param>
    public static ValleyUnderground For(ulong seed) => ValleyUnderground.Create(RouteScene.Shipped.Underground, seed) ?? throw new InvalidOperationException("The shipped bundle has no valley underground.");
}
