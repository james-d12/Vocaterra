using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// A biome's row of the data (I06c): its offset moves the baseline, its shares scale the day's swing and the weather's cooling, a biome with no row changes nothing; the rows are keyed by id
/// in <c>game/temperature.json</c> and the code names none (<c>NoBiomeIdsInSourceTests</c> reads every source file).
/// </summary>
public sealed class BiomeAdjustmentTests
{
    private static readonly ClimateCurve Flat = new([(-1000, 100), (1000, 100)]);

    private static TemperatureEnvironment Rows(string id, BiomeEffect effect) =>
        TemperatureEnvironment.Plain with { Biomes = new Dictionary<string, BiomeEffect> { [id] = effect } };

    private static TemperatureContext In(string biome, TemperatureEnvironment environment, string kind = "sunny", int hour = 13) =>
        TemperatureRig.At(TemperatureRig.Sky(hour, kind), new StubPlaces((_, _, _) => 0, biome: (_, _) => biome), 0, 41, 0, environment);

    [Fact]
    public void Given_ABiomeWithAnOffset_When_TheBaselineIsRead_Then_TheOffsetIsAddedAndOtherBiomesAreNot()
    {
        var environment = Rows("sand", new BiomeEffect(25, 1000, 1000));
        var term = new ClimateTemperatureTerm(Flat, 0);

        Assert.Equal(125, term.TenthsAt(In("sand", environment)));
        Assert.Equal(100, term.TenthsAt(In("loam", environment)));
    }

    [Fact]
    public void Given_ABiomeThatSwingsMore_When_TheDayTermIsRead_Then_NoonIsWarmerAndMidnightColderThanElsewhere()
    {
        var environment = Rows("open", new BiomeEffect(0, 1500, 1000));
        var day = new DayTemperatureTerm(60);

        var noonHere = day.TenthsAt(In("open", environment, hour: 13));
        var noonThere = day.TenthsAt(In("shut", environment, hour: 13));
        var nightHere = day.TenthsAt(In("open", environment, hour: 0));
        var nightThere = day.TenthsAt(In("shut", environment, hour: 0));

        Assert.Equal(noonThere * 3 / 2, noonHere);
        Assert.Equal(nightThere * 3 / 2, nightHere);
        Assert.True(noonHere > noonThere && nightHere < nightThere);
    }

    [Fact]
    public void Given_ABiomeThatTakesTheWeatherHarder_When_TheWeatherTermIsRead_Then_TheCoolingIsScaled()
    {
        var environment = Rows("ridge", new BiomeEffect(0, 1000, 1500));
        var weather = new WeatherTemperatureTerm(20, 60, 50);

        var here = weather.TenthsAt(In("ridge", environment, "rain", 3));
        var there = weather.TenthsAt(In("valley", environment, "rain", 3));

        Assert.True(there < 0);
        Assert.Equal(there * 3 / 2, here);
    }

    [Fact]
    public void Given_NoWorld_When_TheBiomeIsAsked_Then_ItIsTheNeutralOne() =>
        Assert.Equal(BiomeEffect.Neutral, TemperatureRig.Open(TemperatureRig.Sky(13, "sunny")).Biome);

    [Fact]
    public void Given_ABiomeRowChangedInData_When_ThePlaceIsRead_Then_OnlyThatBiomesFeelMoves()
    {
        var shipped = TemperatureRig.Data.Environment;
        var sky = TemperatureRig.Sky(13, "sunny");
        var places = new StubPlaces((_, _, _) => 0, biome: (x, _) => x < 0 ? "meadow" : "stub");
        var tuned = shipped with { Biomes = new Dictionary<string, BiomeEffect>(shipped.Biomes) { ["meadow"] = new BiomeEffect(30, 1000, 1000) } };
        var terms = TemperatureTermRegistry.Standard.Build(TemperatureRig.Data.Terms);

        int Feel(TemperatureEnvironment e, int x) => new PlaceTemperature(terms, e).TenthsAt(new TemperatureContext(sky, new Vocaterra.Game.Core.Voxel.BlockPos(x, 41, 0), 0, null, places, e));

        Assert.Equal(Feel(shipped, 5), Feel(tuned, 5));
        Assert.Equal(Feel(shipped, -5) + 30, Feel(tuned, -5));
    }
}
