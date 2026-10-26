namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// The waterfall: a cliff with a sheet of water falling from its lip into a pool, where the river begins. The site is the lip (the first column
/// beyond the cliff's edge, facing south); the plateau behind it is raised to <paramref name="Top"/>.
/// </summary>
/// <param name="Site">The site at the foot of the lip: the water column that falls is at this x, in the row of the site.</param>
/// <param name="Top">The height of the plateau and the lip.</param>
/// <param name="Width">The width of the falling water in blocks.</param>
/// <param name="Plateau">The half-width in blocks of the raised plateau along the cliff.</param>
/// <param name="PoolRadius">The radius of the pool at the foot.</param>
public sealed record WaterfallSpec(string Site, int Top, int Width, int Plateau, int PoolRadius);

