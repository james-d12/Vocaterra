using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// Underground (I06c): below the natural surface the temperature eases from the surface value to the steady ground temperature, the day and the weather and the shelter fade out, and the lapse
/// alone no longer makes a cave warmer; a dip of a few blocks is still the surface.
/// </summary>
public sealed class UndergroundTemperatureTests
{
    private const int Surface = 100;
    private static readonly TemperatureEnvironment Ground = new(100, 4, 20, new Dictionary<string, BiomeEffect>());
    private static readonly ClimateCurve Curve = new([(-1000, 0), (1000, 200)]);

    // A world whose climate falls with height (a lapse, 5 units a block) over a surface at y 100: the surface's own climate is -500 units.
    private static readonly StubPlaces Hill = new((_, y, _) => -(y * 5), surface: (_, _) => Surface);

    private static TemperatureContext At(int y, string kind = "sunny", int hour = 13, double covered = 0) =>
        new(TemperatureRig.Sky(hour, kind), new Vocaterra.Game.Core.Voxel.BlockPos(0, y, 0), covered, null, Hill, Ground);

    [Theory]
    [InlineData(Surface + 30, 1000)]
    [InlineData(Surface, 1000)]
    [InlineData(Surface - 4, 1000)] // the shallow dip
    [InlineData(Surface - 14, 500)] // halfway through the fade (4 + 10 of 20)
    [InlineData(Surface - 24, 0)]
    [InlineData(Surface - 80, 0)]
    public void Given_ADepthBelowTheSurface_When_TheExposureIsRead_Then_ItFallsFromFullToNothingOverTheFade(int y, int expected) => Assert.Equal(expected, At(y).Exposure);

    [Fact]
    public void Given_ALapseThatWarmsDeeperPlaces_When_ThePlayerIsBelowTheSurface_Then_TheBaselineIsNotWarmerForIt()
    {
        var term = new ClimateTemperatureTerm(Curve, 0);
        var onSurface = term.TenthsAt(At(Surface));
        var shallow = term.TenthsAt(At(Surface - 4)); // full exposure: the surface value and nothing else

        Assert.Equal(onSurface, shallow);
        Assert.Equal(At(Surface).ClimateTemperature, At(Surface - 60).ClimateTemperature); // the climate is read at the surface's height, not the feet's
    }

    [Fact]
    public void Given_DeepGround_When_TheBaselineIsRead_Then_ItIsTheSteadyGroundTemperatureWhateverTheSurface()
    {
        var term = new ClimateTemperatureTerm(Curve, 0);

        Assert.Equal(100, term.TenthsAt(At(Surface - 40)));
        Assert.Equal(100, new ClimateTemperatureTerm(new ClimateCurve([(-1000, -300), (1000, -100)]), 0).TenthsAt(At(Surface - 40))); // a frozen surface above a 10-degree cave
    }

    [Fact]
    public void Given_ADepthHalfwayThroughTheFade_When_TheBaselineIsRead_Then_ItIsHalfwayBetweenTheSurfaceAndTheGround()
    {
        var term = new ClimateTemperatureTerm(Curve, 0);
        var surface = term.TenthsAt(At(Surface));

        Assert.Equal(surface + ((100 - surface) / 2), term.TenthsAt(At(Surface - 14)));
    }

    [Fact]
    public void Given_ADeepPlace_When_TheDayTheWeatherAndTheShelterAreRead_Then_TheyAreNothing()
    {
        var day = new DayTemperatureTerm(60);
        var weather = new WeatherTemperatureTerm(20, 60, 50, 40);
        var shelter = new ShelterTemperatureTerm(50, onlyInPrecipitation: true);

        Assert.NotEqual(0, day.TenthsAt(At(Surface)));
        Assert.Equal(0, day.TenthsAt(At(Surface - 40)));
        Assert.NotEqual(0, weather.TenthsAt(At(Surface, "rain", 3)));
        Assert.Equal(0, weather.TenthsAt(At(Surface - 40, "rain", 3)));
        Assert.NotEqual(0, shelter.TenthsAt(At(Surface, "rain", 3, 1)));
        Assert.Equal(0, shelter.TenthsAt(At(Surface - 40, "rain", 3, 1)));
    }

    [Fact]
    public void Given_ACaveAtNoonAndAtMidnightInRainAndShine_When_TheFeelIsTaken_Then_ItIsTheSameSteadyValue()
    {
        var place = PlaceTemperature.From(TemperatureRig.Data);
        var feels = new[] { ("sunny", 13), ("sunny", 0), ("rain", 3), ("rain", 13) }
            .Select(k => place.TenthsAt(new TemperatureContext(TemperatureRig.Sky(k.Item2, k.Item1), new Vocaterra.Game.Core.Voxel.BlockPos(0, Surface - 60, 0), 0, null, Hill, TemperatureRig.Data.Environment)))
            .ToList();

        Assert.All(feels, f => Assert.Equal(TemperatureRig.Data.Environment.GroundTenths, f));
    }

    [Fact]
    public void Given_TheRealCaveOfSeed42_When_ItIsFeltAtNoonAndAtNight_Then_ItIsTheGroundTemperatureAndSteady()
    {
        // The deep end of a cave passage of seed 42 (the biomes series' entrances: the floor at y 22, 86 blocks under the surface at x -47, z 190).
        var places = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded).Places!;
        Assert.True(places.NaturalSurfaceAt(-47, 190) - 23 > TemperatureRig.Data.Environment.ShallowBlocks + TemperatureRig.Data.Environment.FadeBlocks);

        var noon = TemperatureRig.Place.TenthsAt(TemperatureRig.At(TemperatureRig.Sky(13, "sunny"), places, -47, 23, 190));
        var night = TemperatureRig.Place.TenthsAt(TemperatureRig.At(TemperatureRig.Sky(0, "rain"), places, -47, 23, 190));

        Assert.Equal(TemperatureRig.Data.Environment.GroundTenths, noon);
        Assert.Equal(noon, night);
    }
}
