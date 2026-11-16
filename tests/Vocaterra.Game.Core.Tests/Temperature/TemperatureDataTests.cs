using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary><c>game/temperature.json</c> (I06): the file's parse, the shipped numbers and each validator rule.</summary>
public sealed class TemperatureDataTests
{
    private const string Json = """
        { "schemaVersion": 1, "base": { "tenths": 100 }, "day": { "amplitudeTenths": 40 }, "weather": { "cloudTenths": 10, "precipitationTenths": 20, "windTenths": 30 },
          "shelter": { "bonusTenths": 5, "onlyInPrecipitation": false }, "fire": { "radiusBlocks": 4, "bonusTenths": 90 },
          "bands": [ { "id": "mild", "from": 50, "walkFactor": 1.0, "chill": 0 }, { "id": "chilly", "from": -1000, "walkFactor": 0.5, "chill": 1, "line": "temperature.cold" } ] }
        """;

    private static TemperatureData Shipped => TestGame.Data.Temperature;

    private static IReadOnlyList<string> Problems(TemperatureData data) => GameDataValidator.Validate(TestGame.Data.WithTemperature(data));

    private static IReadOnlyList<string> Own(TemperatureData data) => Problems(data).Where(static p => p.StartsWith("game/temperature.json", StringComparison.Ordinal)).ToList();

    private static TemperatureData WithBands(params TemperatureBand[] bands) => Shipped with { Bands = bands };

    [Fact]
    public void TheFileParses()
    {
        var data = TemperatureData.Parse(Json);

        Assert.Equal(100, data.BaseTenths);
        Assert.Equal(40, data.DayAmplitudeTenths);
        Assert.Equal((10, 20, 30), (data.CloudCoolingTenths, data.PrecipitationCoolingTenths, data.WindCoolingTenths));
        Assert.Equal((5, false), (data.ShelterBonusTenths, data.ShelterOnlyInPrecipitation));
        Assert.Equal((4, 90), (data.FireRadiusBlocks, data.FireBonusTenths));
        Assert.Equal([new TemperatureBand("mild", 50, 1.0, 0, null), new TemperatureBand("chilly", -1000, 0.5, 1, "temperature.cold")], data.Bands);
    }

    [Fact]
    public void TheShippedFileHoldsTogetherAndItsLineIsInTheWordsAndTheLocale()
    {
        Assert.Empty(Own(Shipped));
        Assert.Equal(["warm", "cool", "cold", "very-cold"], Shipped.Bands.Select(static b => b.Id));
        Assert.Equal([1.0, 1.0], Shipped.Bands.Take(2).Select(static b => b.WalkFactor));
        Assert.All(Shipped.LineKeys, key =>
        {
            Assert.Equal("Tengo frío.", TestGame.Data.Words.HudLine(key));
            Assert.True(TestGame.Data.Locale.Has(key), key);
            Assert.Contains(key, GameDataValidator.HudLineKeys(TestGame.Data));
        });
        Assert.Equal(2, Shipped.MaxChill);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 1, "base": { "tenths": 100 } }""")]
    [InlineData("""{ "schemaVersion": 1, "base": { "tenths": "warm" }, "day": { "amplitudeTenths": 1 }, "weather": { "cloudTenths": 1, "precipitationTenths": 1, "windTenths": 1 }, "shelter": { "bonusTenths": 1, "onlyInPrecipitation": true }, "fire": { "radiusBlocks": 1, "bonusTenths": 1 }, "bands": [] }""")]
    [InlineData("not json")]
    public void AFileThatIsWrongInShapeIsRefused(string json) => Assert.Throws<InvalidDataException>(() => TemperatureData.Parse(json));

    [Theory]
    [InlineData(120, "warm")]
    [InlineData(500, "warm")]
    [InlineData(119, "cool")]
    [InlineData(30, "cool")]
    [InlineData(29, "cold")]
    [InlineData(-50, "cold")]
    [InlineData(-51, "very-cold")]
    [InlineData(-5000, "very-cold")]
    public void AFeelingFallsInTheWarmestBandItReaches(int feeling, string band) => Assert.Equal(band, Shipped.BandFor(feeling).Id);

    [Fact]
    public void BandsOutOfOrderAreRefusedAndNamed()
    {
        var problems = Own(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("cold", 100, 0.8, 1, null)));

        Assert.Contains(problems, static p => p.Contains("the band 'cold' begins at 100", StringComparison.Ordinal) && p.Contains("warmest to the coldest", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands(new TemperatureBand("warm", 0, 1, 0, null), new TemperatureBand("cold", 50, 0.8, 1, null))), static p => p.Contains("the band 'cold' begins at 50", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void AWalkFactorOutsideZeroToOneIsRefused(double factor) =>
        Assert.Contains(Own(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("cold", 0, factor, 1, null))), static p => p.Contains("the band 'cold' has a walkFactor", StringComparison.Ordinal));

    [Fact]
    public void AFactorOfExactlyOneIsAllowed() => Assert.Empty(Own(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("cold", 0, 1, 1, null))));

    [Fact]
    public void ADuplicateIdAnEmptyListAndAChillThatFallsAreRefused()
    {
        Assert.Contains(Own(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("warm", 0, 1, 0, null))), static p => p.Contains("'warm' is used twice", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands([])), static p => p.Contains("at least one band", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands(new TemperatureBand("a", 100, 1, 2, null), new TemperatureBand("b", 0, 0.9, 1, null))), static p => p.Contains("the band 'b' has a chill of 1, less than", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands(new TemperatureBand("a", 100, 1, -1, null))), static p => p.Contains("chill of -1", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineNeedsAChillAndAKeyOfTheWordsAndTheLocale()
    {
        Assert.Contains(Own(WithBands(new TemperatureBand("warm", 100, 1, 0, "temperature.cold"))), static p => p.Contains("has a line but no chill", StringComparison.Ordinal));
        Assert.Contains(Problems(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("cold", 0, 0.9, 1, "temperature.nowhere"))), static p => p.Contains("no Spanish for 'temperature.nowhere'", StringComparison.Ordinal));
    }

    [Fact]
    public void NumbersOutsideTheirLimitsAreRefusedAndNamed()
    {
        Assert.Contains(Own(Shipped with { BaseTenths = 99999 }), static p => p.Contains("base.tenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { DayAmplitudeTenths = -1 }), static p => p.Contains("day.amplitudeTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { CloudCoolingTenths = -5 }), static p => p.Contains("weather.cloudTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { PrecipitationCoolingTenths = -5 }), static p => p.Contains("weather.precipitationTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { WindCoolingTenths = -5 }), static p => p.Contains("weather.windTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { ShelterBonusTenths = -5 }), static p => p.Contains("shelter.bonusTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { FireBonusTenths = -5 }), static p => p.Contains("fire.bonusTenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { FireRadiusBlocks = 0 }), static p => p.Contains("fire.radiusBlocks", StringComparison.Ordinal));
    }
}
