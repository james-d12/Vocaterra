using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary><c>game/temperature.json</c> (I06): the file's parse, the shipped numbers and each validator rule.</summary>
public sealed class TemperatureDataTests
{
    private const string Json = """
        { "schemaVersion": 2,
          "terms": [ { "id": "climate", "curve": [ { "climate": -100, "tenths": 0 }, { "climate": 100, "tenths": 100 } ], "noWorldTenths": 100 }, { "id": "day", "amplitudeTenths": 40 },
                     { "id": "fire", "radiusBlocks": 4, "bonusTenths": 90 } ],
          "ground": { "tenths": 90, "shallowBlocks": 3, "fadeBlocks": 12 },
          "biomes": { "plains": { "daySwingPermille": 1300 }, "taiga": { "offsetTenths": -20, "weatherPermille": 1500 } },
          "lag": { "secondsToSettle": 9 },
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

        Assert.Equal(["climate", "day", "fire"], data.Terms.Select(static t => t.Id));
        Assert.Equal(new TemperatureEnvironment(90, 3, 12, new Dictionary<string, BiomeEffect>()).GroundTenths, data.Environment.GroundTenths);
        Assert.Equal((3, 12), (data.Environment.ShallowBlocks, data.Environment.FadeBlocks));
        Assert.Equal(new BiomeEffect(0, 1300, 1000), data.Environment.EffectOf("plains"));
        Assert.Equal(new BiomeEffect(-20, 1000, 1500), data.Environment.EffectOf("taiga"));
        Assert.Equal(BiomeEffect.Neutral, data.Environment.EffectOf("anything else"));
        Assert.Equal(9, data.Lag.SecondsToSettle);
        Assert.Equal([new TemperatureBand("mild", 50, 1.0, 0, null), new TemperatureBand("chilly", -1000, 0.5, 1, "temperature.cold")], data.Bands);
    }

    [Fact]
    public void TheShippedFileHoldsTogetherAndItsLineIsInTheWordsAndTheLocale()
    {
        Assert.Empty(Own(Shipped));
        Assert.Equal(["very-hot", "hot", "warm", "cool", "cold", "very-cold"], Shipped.Bands.Select(static b => b.Id));
        Assert.Equal(["temperature.hot", "temperature.cold"], Shipped.LineKeys);
        Assert.Equal("Tengo calor.", TestGame.Data.Words.HudLine("temperature.hot"));
        Assert.All(Shipped.LineKeys, key =>
        {
            Assert.False(string.IsNullOrEmpty(TestGame.Data.Words.HudLine(key)));
            Assert.True(TestGame.Data.Locale.Has(key), key);
            Assert.Contains(key, GameDataValidator.HudLineKeys(TestGame.Data));
        });
        Assert.Equal(2, Shipped.MaxChill);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 2, "terms": [] }""")]
    [InlineData("""{ "schemaVersion": 2, "terms": [], "ground": { "tenths": "warm", "shallowBlocks": 1, "fadeBlocks": 1 }, "biomes": {}, "lag": { "secondsToSettle": 1 }, "bands": [] }""")]
    [InlineData("not json")]
    public void AFileThatIsWrongInShapeIsRefused(string json) => Assert.Throws<InvalidDataException>(() => TemperatureData.Parse(json));

    [Theory]
    [InlineData(120, "warm")]
    [InlineData(279, "warm")]
    [InlineData(500, "very-hot")]
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
        Assert.Contains(Own(WithBands(new TemperatureBand("warm", 100, 1, 0, "temperature.cold"))), static p => p.Contains("has a line but no chill or heat", StringComparison.Ordinal));
        Assert.Contains(Problems(WithBands(new TemperatureBand("warm", 100, 1, 0, null), new TemperatureBand("cold", 0, 0.9, 1, "temperature.nowhere"))), static p => p.Contains("no Spanish for 'temperature.nowhere'", StringComparison.Ordinal));
    }

    [Fact]
    public void NumbersOutsideTheirLimitsAreRefusedAndNamed()
    {
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { GroundTenths = 99999 } }), static p => p.Contains("ground.tenths", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { ShallowBlocks = -1 } }), static p => p.Contains("ground.shallowBlocks", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { FadeBlocks = 0 } }), static p => p.Contains("ground.fadeBlocks", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { Lag = new TemperatureLag(-1) }), static p => p.Contains("lag.secondsToSettle", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { Biomes = new Dictionary<string, BiomeEffect> { ["plains"] = new(0, -1, 1000) } } }), static p => p.Contains("biomes.plains.daySwingPermille", StringComparison.Ordinal));
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { Biomes = new Dictionary<string, BiomeEffect> { ["plains"] = new(0, 1000, 9000) } } }), static p => p.Contains("biomes.plains.weatherPermille", StringComparison.Ordinal));
    }

    [Fact]
    public void ABiomeRowForABiomeThatDoesNotExistIsRefusedAndNamed() =>
        Assert.Contains(Own(Shipped with { Environment = Shipped.Environment with { Biomes = new Dictionary<string, BiomeEffect> { ["badlands"] = BiomeEffect.Neutral } } }), static p => p.Contains("'badlands'", StringComparison.Ordinal) && p.Contains("biomes.json", StringComparison.Ordinal));

    [Fact]
    public void TheShippedBiomeRowsAllNameRealBiomesAndTheValleysBiomeHasNone()
    {
        var ids = TestGame.Data.Biomes.Rows.Select(static r => r.Id).ToHashSet();

        Assert.All(Shipped.Environment.Biomes.Keys, id => Assert.Contains(id, ids));
        Assert.DoesNotContain("woodland", Shipped.Environment.Biomes.Keys);
    }

    [Fact]
    public void TheHeatBandsAreWarmerThanWarmAndTheirEffectsAreData()
    {
        var hot = Shipped.Bands.Single(static b => b.Id == "hot");
        var veryHot = Shipped.Bands.Single(static b => b.Id == "very-hot");

        Assert.Equal((280, 0.9, 1.5, 1), (hot.FromTenths, hot.WalkFactor, hot.ThirstFactor, hot.Heat));
        Assert.Equal((350, 0.75, 2.0, 2), (veryHot.FromTenths, veryHot.WalkFactor, veryHot.ThirstFactor, veryHot.Heat));
        Assert.Equal("temperature.hot", hot.Line);
        Assert.Equal("hot", Shipped.BandFor(280).Id);
        Assert.Equal("very-hot", Shipped.BandFor(350).Id);
        Assert.Equal("warm", Shipped.BandFor(279).Id);
    }

    [Fact]
    public void HeatThatRisesWithColdABandWithBothAndABadThirstFactorAreRefused()
    {
        Assert.Contains(Own(WithBands(new TemperatureBand("a", 100, 1, 0, null, Heat: 1), new TemperatureBand("b", 0, 1, 0, null, Heat: 2))), static p => p.Contains("a colder band cannot be hotter", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands(new TemperatureBand("a", 100, 1, 1, null, Heat: 1))), static p => p.Contains("both a chill and a heat", StringComparison.Ordinal));
        Assert.Contains(Own(WithBands(new TemperatureBand("a", 100, 1, 0, null, ThirstFactor: 0))), static p => p.Contains("thirstFactor", StringComparison.Ordinal));
        Assert.Empty(Own(WithBands(new TemperatureBand("a", 100, 1, 0, "temperature.hot", Heat: 1), new TemperatureBand("b", 0, 1, 0, null))));
    }
}
