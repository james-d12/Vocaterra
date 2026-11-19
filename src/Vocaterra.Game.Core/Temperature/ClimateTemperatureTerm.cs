namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The place's baseline (I06c, which replaces I06's constant base and I06b's cooling-only term): the generator's climate temperature at the player's column and height
/// (<see cref="ITemperatureContext.ClimateTemperature"/>: the biomes' temperature field lowered by the lapse over height, the value that decides where snow lies) mapped to degrees by a
/// <see cref="ClimateCurve"/>, so warm places are warm and cold places cold; plus the biome's offset; and below the surface eased towards the steady ground temperature as the exposure falls.
/// The valley lies on the curve's flat stretch, so it keeps its 14 degrees. From <c>game/temperature.json</c>.
/// </summary>
public sealed class ClimateTemperatureTerm : ITemperatureTerm
{
    /// <summary>The term's id in the data and the registry.</summary>
    public const string Id = "climate";

    private readonly ClimateCurve _curve;
    private readonly int _noWorldTenths;

    /// <summary>A climate term.</summary>
    /// <param name="curve">Climate to degrees.</param>
    /// <param name="noWorldTenths">The baseline where the world's climate is not known.</param>
    public ClimateTemperatureTerm(ClimateCurve curve, int noWorldTenths)
    {
        ArgumentNullException.ThrowIfNull(curve);
        _curve = curve;
        _noWorldTenths = noWorldTenths;
    }

    /// <inheritdoc />
    public string Name => Id;

    /// <summary>Reads the term's row.</summary>
    /// <param name="parameters">The row's parameters.</param>
    public static ITemperatureTerm From(TermParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var points = parameters.Pairs("curve", "climate", "tenths", -4000, 4000);
        if (points.Count < 2)
        {
            throw parameters.Fault("curve needs at least two points");
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].First <= points[i - 1].First)
            {
                throw parameters.Fault("the climate of the curve's points must rise");
            }
        }

        return new ClimateTemperatureTerm(new ClimateCurve(points), parameters.Int("noWorldTenths", -2000, 2000));
    }

    /// <inheritdoc />
    public int TenthsAt(ITemperatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasWorld)
        {
            return _noWorldTenths;
        }

        var surface = _curve.TenthsAt(context.ClimateTemperature) + context.Biome.OffsetTenths;
        var depth = 1000 - context.Exposure;
        return depth == 0 ? surface : surface + (int)((long)(context.GroundTenths - surface) * depth / 1000);
    }
}
