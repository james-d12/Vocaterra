namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The weather's cooling (I06): each channel of the sky (0 to 1) times its coefficient, taken off; never a weather kind's id. I06c: precipitation that falls as snow (the climate at the
/// feet below the generator's snow line) cools by a further coefficient; the biome's share scales the whole, and the exposure fades it out underground. From <c>game/temperature.json</c>.
/// </summary>
public sealed class WeatherTemperatureTerm : ITemperatureTerm
{
    /// <summary>The term's id in the data and the registry.</summary>
    public const string Id = "weather";

    private readonly int _cloud;
    private readonly int _precipitation;
    private readonly int _wind;
    private readonly int _snow;

    /// <summary>A weather term.</summary>
    /// <param name="cloudTenths">The cooling of full cloud.</param>
    /// <param name="precipitationTenths">The cooling of precipitation at full intensity.</param>
    /// <param name="windTenths">The cooling of the strongest wind.</param>
    /// <param name="snowTenths">The further cooling of precipitation at full intensity where it snows.</param>
    public WeatherTemperatureTerm(int cloudTenths, int precipitationTenths, int windTenths, int snowTenths = 0)
    {
        _cloud = cloudTenths;
        _precipitation = precipitationTenths;
        _wind = windTenths;
        _snow = snowTenths;
    }

    /// <inheritdoc />
    public string Name => Id;

    /// <summary>Reads the term's row.</summary>
    /// <param name="parameters">The row's parameters.</param>
    public static ITemperatureTerm From(TermParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new WeatherTemperatureTerm(parameters.Int("cloudTenths", 0, 2000), parameters.Int("precipitationTenths", 0, 2000), parameters.Int("windTenths", 0, 2000), parameters.Int("snowPrecipitationTenths", 0, 2000));
    }

    /// <inheritdoc />
    public int TenthsAt(ITemperatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var channels = context.Atmosphere.Channels;
        var precipitation = _precipitation + (context.Snows ? _snow : 0);
        var cooling = Permille.Times(_cloud, Permille.Of(channels.CloudCover))
            + Permille.Times(precipitation, Permille.Of(channels.PrecipitationIntensity))
            + Permille.Times(_wind, Permille.Of(channels.Wind));
        return -Permille.Times(Permille.Times(cooling, context.Biome.WeatherPermille), context.Exposure);
    }
}
