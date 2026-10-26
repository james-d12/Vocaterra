namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// A named place at a fixed position (S13): <c>camp.player</c>, <c>camp.guide</c>, <c>village</c>, <c>bridge</c>, <c>lake</c> and so on. The position
/// never depends on the seed. A site with a <paramref name="Radius"/> flattens the terrain: the ground is level at <paramref name="Y"/> (or at the
/// height the terrain has there when none is given) out to the radius and blends into the surrounding terrain over <paramref name="Blend"/> more blocks.
/// </summary>
/// <param name="Name">The site's name; it is also the name of a scene point.</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Y">The y of the top block of the level ground, or <see langword="null"/> to take the terrain's own height at the centre.</param>
/// <param name="Radius">The radius of the level ground in blocks (0: the site only names a place).</param>
/// <param name="Blend">The width in blocks of the slope from the level ground into the terrain.</param>
public sealed record SiteSpec(string Name, int X, int Z, int? Y, int Radius, int Blend);

