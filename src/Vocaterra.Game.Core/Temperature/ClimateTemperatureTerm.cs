using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The world's climate in what the player feels (I06b): colder where the climate temperature at the player's column and height (<see cref="World.IPlaceInfo.TemperatureAt"/>: the biomes'
/// temperature field lowered by the lapse over every block above the sea, the very value that decides where snow lies) falls below a reference, in proportion, down to a floor. High ground
/// and cold biomes both lower that value, so one term makes the peaks and the snowfields cold and leaves the valley as it was. It never warms (a hot biome is not hotter), reads nothing but
/// the world's pure model (a function of the seed, the column and the height, so no state and no save), and is 0 where the situation has no world. From <c>game/temperature.json</c>.
/// </summary>
public sealed class ClimateTemperatureTerm : ITemperatureTerm
{
    private readonly int _reference;
    private readonly int _perRange;
    private readonly int _max;

    /// <summary>A climate term.</summary>
    /// <param name="cooling">Its numbers.</param>
    public ClimateTemperatureTerm(ClimateCooling cooling)
    {
        _reference = FieldScale.FromThousandths(cooling.ReferenceThousandths);
        _perRange = cooling.CoolingTenthsPerRange;
        _max = cooling.MaxCoolingTenths;
    }

    /// <inheritdoc />
    public string Name => "climate";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation)
    {
        if (situation.Places is not { } places)
        {
            return 0;
        }

        var feet = situation.Feet;
        var below = _reference - places.TemperatureAt(feet.X, feet.Y, feet.Z);
        if (below <= 0)
        {
            return 0;
        }

        var cooling = (int)Math.Min(_max, (long)below * _perRange / Mathematics.IntMath.One);
        return -cooling;
    }
}
