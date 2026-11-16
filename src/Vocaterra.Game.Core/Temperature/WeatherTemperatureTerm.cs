namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The weather's cooling (I06): each channel of the sky takes its coefficient off the temperature, in proportion (cloud cover, the precipitation's intensity and the wind, each 0 to 1).
/// It reads the channels and never a weather kind's id (D-game-31), so a new kind in <c>game/weather.json</c> is felt with no edit here. From <c>game/temperature.json</c>.
/// </summary>
public sealed class WeatherTemperatureTerm : ITemperatureTerm
{
    private readonly int _cloud;
    private readonly int _precipitation;
    private readonly int _wind;

    /// <summary>A weather term.</summary>
    /// <param name="cloudTenths">The cooling of full cloud cover, in tenths of a degree (a positive number cools).</param>
    /// <param name="precipitationTenths">The cooling of precipitation at full intensity.</param>
    /// <param name="windTenths">The cooling of the strongest wind.</param>
    public WeatherTemperatureTerm(int cloudTenths, int precipitationTenths, int windTenths)
    {
        _cloud = cloudTenths;
        _precipitation = precipitationTenths;
        _wind = windTenths;
    }

    /// <inheritdoc />
    public string Name => "weather";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation)
    {
        var channels = situation.Atmosphere.Channels;
        return -(Permille.Times(_cloud, Permille.Of(channels.CloudCover))
            + Permille.Times(_precipitation, Permille.Of(channels.PrecipitationIntensity))
            + Permille.Times(_wind, Permille.Of(channels.Wind)));
    }
}
