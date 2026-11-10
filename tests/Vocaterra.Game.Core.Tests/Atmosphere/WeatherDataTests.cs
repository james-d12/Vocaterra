using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary><c>game/weather.json</c> (E04): the file's parse, the shipped values and each validator rule.</summary>
public sealed class WeatherDataTests
{
    private const string Json = """
        { "schemaVersion": 1, "start": "a", "transitionSeconds": 5, "kinds": [
          { "id": "a", "cloudCover": 0.1, "sunlight": 1, "fog": 0, "wind": 0.2, "minSeconds": 30, "maxSeconds": 60, "next": { "b": 2 }, "morningWeight": 1, "say": { "day": "a.day", "night": "a.night" } },
          { "id": "b", "cloudCover": 1, "sunlight": 0.4, "fog": 0.3, "wind": 0.9, "precipitation": { "kind": "snow", "intensity": 0.5 }, "minSeconds": 10, "maxSeconds": 10, "next": { "a": 1 }, "morningWeight": 0 }
        ] }
        """;

    private static WeatherData Shipped => WeatherTestData.Shipped;

    private static IReadOnlyList<string> Problems(WeatherData weather) => WeatherValidator.Validate(weather);

    private static WeatherData With(Func<WeatherKind, WeatherKind> change, string id = "cloudy") =>
        Shipped with { Kinds = Shipped.Kinds.Select(k => k.Id == id ? change(k) : k).ToList() };

    private static bool Names(IReadOnlyList<string> problems, string text) => problems.Any(p => p.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void TheFileParsesWithItsChannelsPrecipitationNextAndSay()
    {
        var weather = WeatherData.Parse(Json);

        Assert.Equal("a", weather.Start);
        Assert.Equal(5, weather.TransitionSeconds);
        var a = weather.Kind("a");
        Assert.Equal(WeatherChannels.Dry(0.1, 1, 0, 0.2), a.Channels);
        Assert.Equal([new WeightedKind("b", 2)], a.Next);
        Assert.Equal(new WeatherSay("a.day", "a.night"), a.Say);
        var b = weather.Kind("b");
        Assert.Equal(new WeatherChannels(1, 0.4, 0.3, 0.9, "snow", 0.5), b.Channels);
        Assert.Null(b.Say);
        Assert.Empty(Problems(weather));
    }

    [Fact]
    public void TheShippedFileIsTheOwnersFirstKindsAndHoldsTogether()
    {
        Assert.Empty(Problems(Shipped));
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.Contains("weather.json", StringComparison.Ordinal));
        Assert.Equal(["sunny", "cloudy", "rain"], Shipped.Kinds.Select(static k => k.Id));
        Assert.Equal("sunny", Shipped.Start);
        Assert.Equal(45, Shipped.TransitionSeconds);
        var sunny = Shipped.Kind("sunny");
        Assert.Equal(WeatherChannels.Dry(0.42, 1, 0, 0.2), sunny.Channels);
        Assert.Equal((240, 600, 6), (sunny.MinSeconds, sunny.MaxSeconds, sunny.MorningWeight));
        Assert.Equal([new WeightedKind("sunny", 3), new WeightedKind("rain", 2)], Shipped.Kind("cloudy").Next);
        Assert.Equal(new WeatherChannels(1, 0.5, 0.5, 0.6, "rain", 0.8), Shipped.Kind("rain").Channels);
        Assert.All(Shipped.Kinds, static k => Assert.Null(k.Say));
    }

    [Fact]
    public void TheDefaultDataIsOneNeutralKindThatHoldsTogether()
    {
        Assert.Single(WeatherData.Default.Kinds);
        Assert.Empty(Problems(WeatherData.Default));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "start": "a", "transitionSeconds": 0, "kinds": [] }""")]
    [InlineData("""{ "schemaVersion": 1, "start": "a", "kinds": [] }""")]
    [InlineData("""{ "schemaVersion": 1, "start": "a", "transitionSeconds": 0, "kinds": [ { "id": "a" } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "start": "a", "transitionSeconds": 0, "kinds": [ { "id": "a", "cloudCover": 0, "sunlight": 0, "fog": 0, "wind": 0, "minSeconds": 1, "maxSeconds": 1, "morningWeight": 1, "next": { "a": "heavy" } } ] }""")]
    [InlineData("not json")]
    public void AFileThatIsWrongInShapeIsRefused(string json) => Assert.Throws<InvalidDataException>(() => WeatherData.Parse(json));

    [Fact]
    public void ADuplicateOrBadIdIsRefused()
    {
        var twice = Shipped with { Kinds = [.. Shipped.Kinds, Shipped.Kind("rain")] };
        var bad = With(static k => k with { Id = "Cloudy_1" });

        Assert.True(Names(Problems(twice), "used twice"));
        Assert.True(Names(Problems(bad), "kebab-case"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void AChannelOutsideZeroToOneIsRefusedAndNamed(double value)
    {
        Assert.True(Names(Problems(With(k => k with { Channels = k.Channels with { CloudCover = value } })), "cloudCover"));
        Assert.True(Names(Problems(With(k => k with { Channels = k.Channels with { Sunlight = value } })), "sunlight"));
        Assert.True(Names(Problems(With(k => k with { Channels = k.Channels with { Fog = value } })), "fog"));
        Assert.True(Names(Problems(With(k => k with { Channels = k.Channels with { Wind = value } })), "wind"));
        Assert.True(Names(Problems(With(k => k with { Channels = k.Channels with { PrecipitationIntensity = value } })), "intensity"));
    }

    [Fact]
    public void APrecipitationNeedsItsKindAndAKindNeedsItsIntensity()
    {
        Assert.True(Names(Problems(With(static k => k with { Channels = k.Channels with { PrecipitationIntensity = 0.5 } })), "no kind of it"));
        Assert.True(Names(Problems(With(static k => k with { Channels = k.Channels with { PrecipitationKind = "rain" } })), "no intensity"));
        Assert.True(Names(Problems(With(static k => k with { Channels = k.Channels with { PrecipitationKind = string.Empty } }, "rain")), "no kind of it"));
    }

    [Fact]
    public void ASpellMustBePositiveAndOrdered()
    {
        Assert.True(Names(Problems(With(static k => k with { MinSeconds = 0 })), "positive spells"));
        Assert.True(Names(Problems(With(static k => k with { MinSeconds = 500, MaxSeconds = 400 })), "positive spells"));
        Assert.DoesNotContain(Problems(With(static k => k with { MinSeconds = 400, MaxSeconds = 400 })), static p => p.Contains("spells", StringComparison.Ordinal));
    }

    [Fact]
    public void NextMustBeKnownWeightedAndPresent()
    {
        Assert.True(Names(Problems(With(static k => k with { Next = [new WeightedKind("hail", 1)] })), "unknown next kind 'hail'"));
        Assert.True(Names(Problems(With(static k => k with { Next = [new WeightedKind("sunny", 0)] })), "must be positive"));
        Assert.True(Names(Problems(With(static k => k with { Next = [] })), "has no next kind"));
        var alone = new WeatherData([Shipped.Kind("sunny") with { Next = [] }], "sunny", 0);
        Assert.Empty(Problems(alone));
    }

    [Fact]
    public void TheStartMustBeAKindAndSomeKindMustHaveAMorningWeight()
    {
        Assert.True(Names(Problems(Shipped with { Start = "hail" }), "start 'hail'"));
        var none = Shipped with { Kinds = Shipped.Kinds.Select(static k => k with { MorningWeight = 0 }).ToList() };
        Assert.True(Names(Problems(none), "morningWeight"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    [InlineData(double.NaN)]
    public void TheTransitionMustBeNonNegativeAndNotLongerThanTheShortestSpell(double seconds) =>
        Assert.True(Names(Problems(Shipped with { TransitionSeconds = seconds }), "transitionSeconds"));

    [Fact]
    public void ATransitionAsLongAsTheShortestSpellIsAllowed() => Assert.Empty(Problems(Shipped with { TransitionSeconds = 120 }));
}
