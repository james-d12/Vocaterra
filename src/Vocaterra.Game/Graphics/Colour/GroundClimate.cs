using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Graphics.Colour;

/// <summary>The ground climate the colour map is read at (B22), from a world's place query: the temperature at the block (lowered with height) and the column's humidity. Safe from any thread (the place query is).</summary>
public static class GroundClimate
{
    /// <summary>The function of a world's places: (x, y, z) to the temperature at that height and the column's humidity.</summary>
    /// <param name="places">The place query.</param>
    public static Func<int, int, int, (int Temperature, int Humidity)> Of(IPlaceInfo places)
    {
        ArgumentNullException.ThrowIfNull(places);
        return (x, y, z) => (places.TemperatureAt(x, y, z), places.ClimateAt(x, z).Humidity);
    }
}
