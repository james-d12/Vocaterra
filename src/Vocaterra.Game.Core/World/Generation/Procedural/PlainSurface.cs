namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>The default surface strategy: the ground is what the height model made it (B03).</summary>
internal sealed class PlainSurface : ISurfaceStrategy
{
    /// <inheritdoc />
    public string Name => StrategyRegistry.PlainSurface;

    /// <inheritdoc />
    public int FadeWidth => 0;

    /// <inheritdoc />
    public int HeightDelta(in SurfaceColumn column) => 0;
}
