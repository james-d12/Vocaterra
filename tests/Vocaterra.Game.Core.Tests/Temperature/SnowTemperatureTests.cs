using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>Snow (I06c): precipitation above the snow line (the generator's, through <c>IPlaceInfo.SnowBelowTemperature</c>) cools by the further coefficient; below it, rain cools as before.</summary>
public sealed class SnowTemperatureTests
{
    private const int SnowLine = -1640; // fixed point: -400 thousandths

    private static readonly WeatherTemperatureTerm Weather = new(20, 60, 50, snowTenths: 40);

    private static TemperatureContext Where(int climate, string kind) =>
        TemperatureRig.At(TemperatureRig.Sky(3, kind), new StubPlaces((_, _, _) => climate, snowBelow: SnowLine), 0, 41, 0, TemperatureEnvironment.Plain);

    [Fact]
    public void Given_PrecipitationWhereItSnows_When_TheWeatherTermIsRead_Then_ItCoolsByTheSnowCoefficientMore()
    {
        var rain = Weather.TenthsAt(Where(0, "rain"));
        var snow = Weather.TenthsAt(Where(SnowLine - 1, "rain"));

        Assert.True(Where(SnowLine - 1, "rain").Snows);
        Assert.False(Where(SnowLine, "rain").Snows);
        Assert.True(snow < rain, $"{snow} < {rain}");
    }

    [Fact]
    public void Given_ClearSkyWhereItWouldSnow_When_TheWeatherTermIsRead_Then_TheSnowCoefficientAddsNothing() =>
        Assert.Equal(Weather.TenthsAt(Where(0, "sunny")), Weather.TenthsAt(Where(SnowLine - 1, "sunny")));

    [Fact]
    public void Given_FullPrecipitation_When_ItSnows_Then_TheExtraIsExactlyTheCoefficientTimesTheIntensity()
    {
        var sky = TemperatureRig.Sky(3, "rain");
        var intensity = sky.Channels.PrecipitationIntensity;

        var extra = Weather.TenthsAt(Where(0, "rain")) - Weather.TenthsAt(Where(SnowLine - 1, "rain"));

        Assert.InRange(extra, (int)(40 * intensity) - 1, (int)(40 * intensity) + 1);
    }

    [Fact]
    public void Given_NoWorld_When_TheContextIsAskedIfItSnows_Then_NoAndTheWeatherIsThePlainRain() =>
        Assert.False(TemperatureRig.Open(TemperatureRig.Sky(3, "rain")).Snows);
}
