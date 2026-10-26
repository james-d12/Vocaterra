namespace Vocaterra.Game.Core.World.Layout;

/// <summary>The lake: an irregular basin round a site, filled to the world's water level.</summary>
/// <param name="Site">The site at its middle.</param>
/// <param name="RadiusX">The half-width along x.</param>
/// <param name="RadiusZ">The half-depth along z.</param>
/// <param name="Depth">How far the middle of the bed lies under the water.</param>
public sealed record LakeSpec(string Site, int RadiusX, int RadiusZ, int Depth);

