using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// The shipped numbers (I06): a fair noon is warm, a clear night cool, a rainy windy night cold and a fire warm again; night is colder than noon, rain and wind colder than fair, a roof
/// warmer in the rain, a fire warmer. The kinds are named here, in the test, as the data names them; the code under test names none.
/// </summary>
public sealed class TemperatureFeelTests
{
    private static int Place(TemperatureSituation situation) => TemperatureRig.Place.TenthsAt(situation);

    private static string Band(int tenths) => TemperatureRig.Data.BandFor(tenths).Id;

    [Fact]
    public void TheShippedNumbersGiveTheFeelsOfTheBriefInTheValley()
    {
        var noon = Place(TemperatureRig.Open(TemperatureRig.Sky(13, "sunny")));
        var clearNight = Place(TemperatureRig.Open(TemperatureRig.Sky(0, "sunny")));
        var rainyNight = Place(TemperatureRig.Open(TemperatureRig.Sky(0, "rain")));
        var fires = TemperatureRig.Ring(lit: true);
        var rainyNightByAFire = Place(TemperatureRig.Beside(TemperatureRig.Sky(0, "rain"), fires, 2));

        Assert.Equal((182, "warm"), (noon, Band(noon)));
        Assert.Equal((62, "cool"), (clearNight, Band(clearNight)));
        Assert.Equal((-18, "cold"), (rainyNight, Band(rainyNight)));
        Assert.Equal((132, "warm"), (rainyNightByAFire, Band(rainyNightByAFire)));
        Assert.Equal("warm", Band(Place(TemperatureRig.Beside(TemperatureRig.Sky(0, "sunny"), fires, 2))));
    }

    [Fact]
    public void NightIsColderThanNoonInAnyWeather()
    {
        foreach (var kind in new[] { "sunny", "cloudy", "rain" })
        {
            Assert.True(Place(TemperatureRig.Open(TemperatureRig.Sky(0, kind))) < Place(TemperatureRig.Open(TemperatureRig.Sky(13, kind))), kind);
        }
    }

    [Fact]
    public void DawnAndDuskEaseBetweenTheNightAndTheDay()
    {
        var night = Place(TemperatureRig.Open(TemperatureRig.Sky(0, "sunny")));
        var dawn = Place(TemperatureRig.Open(TemperatureRig.Sky(5, "sunny")));
        var noon = Place(TemperatureRig.Open(TemperatureRig.Sky(13, "sunny")));

        Assert.InRange(dawn, night + 1, noon - 1);
    }

    [Fact]
    public void RainAndWindAreColderThanFairWeather()
    {
        var fair = Place(TemperatureRig.Open(TemperatureRig.Sky(13, "sunny")));

        Assert.True(Place(TemperatureRig.Open(TemperatureRig.Sky(13, "cloudy"))) < fair);
        Assert.True(Place(TemperatureRig.Open(TemperatureRig.Sky(13, "rain"))) < Place(TemperatureRig.Open(TemperatureRig.Sky(13, "cloudy"))));
        var windy = TemperatureRig.Sky(13, "sunny") with { Channels = TemperatureRig.Sky(13, "sunny").Channels with { Wind = 1 } };
        Assert.True(Place(TemperatureRig.Open(windy)) < fair);
    }

    [Fact]
    public void ACoverIsWarmerInTheRainAndNoWarmerInFairWeather()
    {
        var rain = TemperatureRig.Sky(13, "rain");
        var fair = TemperatureRig.Sky(13, "sunny");

        Assert.True(Place(TemperatureRig.Covered(rain)) > Place(TemperatureRig.Open(rain)));
        Assert.Equal(Place(TemperatureRig.Open(fair)), Place(TemperatureRig.Covered(fair))); // the shipped shelter counts only in precipitation
    }

    [Fact]
    public void AFireIsWarmerAndOnlyWhenLitAndNear()
    {
        var sky = TemperatureRig.Sky(0, "sunny");
        var open = Place(TemperatureRig.Open(sky));

        Assert.True(Place(TemperatureRig.Beside(sky, TemperatureRig.Ring(lit: true), 3)) > open);
        Assert.Equal(open, Place(TemperatureRig.Beside(sky, TemperatureRig.Ring(lit: false), 3)));
        Assert.Equal(open, Place(TemperatureRig.Beside(sky, TemperatureRig.Ring(lit: true), 9)));
    }

    [Fact]
    public void TheSameSituationGivesTheSameNumberEveryTime()
    {
        var situation = TemperatureRig.Open(TemperatureRig.Sky(5.3, "cloudy"));

        Assert.Equal(Place(situation), Place(situation));
        Assert.Equal(Place(situation), new PlaceTemperature(TemperatureTerms.Standard(TemperatureRig.Data)).TenthsAt(situation));
    }

    [Fact]
    public void TheUnitsAreTenthsOfADegreeWrittenWithOneDecimal()
    {
        Assert.Equal("12.5", TemperatureUnits.Format(125));
        Assert.Equal("-5.0", TemperatureUnits.Format(-50));
        Assert.Equal("0.0", TemperatureUnits.Format(0));
        Assert.Equal(125, TemperatureUnits.FromCelsius(12.5));
        Assert.Equal(-50, TemperatureUnits.FromCelsius(-5));
        Assert.Equal(126, TemperatureUnits.FromCelsius(12.55));
    }
}
