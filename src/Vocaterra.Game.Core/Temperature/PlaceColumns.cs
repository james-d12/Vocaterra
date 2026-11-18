using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>What the temperature needs of a column: its natural surface height and its biome's id.</summary>
/// <param name="Surface">The natural surface, in blocks.</param>
/// <param name="BiomeId">The biome row's id.</param>
public readonly record struct PlaceColumn(int Surface, string BiomeId);

/// <summary>
/// The last column read (I06c): the player stays in one column for many ticks, and the surface and the biome are a function of the world and the column only, so the answer is kept until the
/// column changes. One per place's temperature; a cache of a pure function, not state of the game.
/// </summary>
public sealed class PlaceColumns
{
    private IPlaceInfo? _places;
    private int _x;
    private int _z;
    private PlaceColumn _last;

    /// <summary>The column's surface and biome.</summary>
    /// <param name="places">The world.</param>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public PlaceColumn Read(IPlaceInfo places, int x, int z)
    {
        if (!ReferenceEquals(places, _places) || x != _x || z != _z)
        {
            _last = new PlaceColumn(places.NaturalSurfaceAt(x, z), places.BiomeAt(x, z).Id);
            (_places, _x, _z) = (places, x, z);
        }

        return _last;
    }
}
