using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// The terms as data and the registry that builds them (I06c): every shipped row builds; an unknown id, a missing or out-of-range or unknown parameter and an id listed twice are validation errors;
/// and a fourth system is one class, one registry line and a row of data, with no edit of the place, the data type or the other terms.
/// </summary>
public sealed class TemperatureTermRegistryTests
{
    private static TemperatureData Shipped => TestGame.Data.Temperature;

    private static TemperatureTermSpec Spec(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new TemperatureTermSpec(document.RootElement.GetProperty("id").GetString()!, document.RootElement.Clone());
    }

    private static IReadOnlyList<string> Problems(params string[] rows) => TemperatureTermRegistry.Standard.Problems(rows.Select(Spec));

    [Fact]
    public void Given_TheShippedRows_When_TheStandardRegistryBuildsThem_Then_EachTermIsNamedByItsId()
    {
        var terms = TemperatureTermRegistry.Standard.Build(Shipped.Terms);

        Assert.Equal(Shipped.Terms.Select(static t => t.Id), terms.Select(static t => t.Name));
        Assert.Equal(["climate", "day", "fire", "shelter", "weather"], TemperatureTermRegistry.Standard.Ids.Order(StringComparer.Ordinal));
        Assert.Empty(TemperatureTermRegistry.Standard.Problems(Shipped.Terms));
    }

    [Fact]
    public void Given_AnUnknownId_When_TheRowsAreChecked_Then_TheProblemNamesTheIdAndTheKnownOnes()
    {
        var problems = Problems("""{ "id": "tide", "tenths": 5 }""");

        var problem = Assert.Single(problems);
        Assert.Contains("'tide'", problem, StringComparison.Ordinal);
        Assert.Contains("not one the game knows", problem, StringComparison.Ordinal);
        Assert.Contains("weather", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AMissingParameter_When_TheRowIsChecked_Then_TheProblemNamesTheTermAndTheParameter() =>
        Assert.Contains(Problems("""{ "id": "day" }"""), static p => p.Contains("the term 'day': amplitudeTenths is missing", StringComparison.Ordinal));

    [Fact]
    public void Given_AParameterOutOfRangeOrOfTheWrongKind_When_TheRowIsChecked_Then_TheProblemSaysSo()
    {
        Assert.Contains(Problems("""{ "id": "day", "amplitudeTenths": 99999 }"""), static p => p.Contains("amplitudeTenths is 99999; it must be from 0 to 2000", StringComparison.Ordinal));
        Assert.Contains(Problems("""{ "id": "day", "amplitudeTenths": "big" }"""), static p => p.Contains("amplitudeTenths must be a whole number", StringComparison.Ordinal));
        Assert.Contains(Problems("""{ "id": "shelter", "bonusTenths": 5, "onlyInPrecipitation": 1 }"""), static p => p.Contains("onlyInPrecipitation must be true or false", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AParameterNoTermReads_When_TheRowIsChecked_Then_ItIsRefusedAsAMisspelling() =>
        Assert.Contains(Problems("""{ "id": "day", "amplitudeTenths": 60, "amplitudeTenth": 5 }"""), static p => p.Contains("'amplitudeTenth' the term does not know", StringComparison.Ordinal));

    [Fact]
    public void Given_AClimateCurveThatDoesNotRise_When_TheRowIsChecked_Then_ItIsRefused()
    {
        Assert.Contains(Problems("""{ "id": "climate", "noWorldTenths": 0, "curve": [ { "climate": 5, "tenths": 0 }, { "climate": 5, "tenths": 9 } ] }"""), static p => p.Contains("must rise", StringComparison.Ordinal));
        Assert.Contains(Problems("""{ "id": "climate", "noWorldTenths": 0, "curve": [ { "climate": 5, "tenths": 0 } ] }"""), static p => p.Contains("at least two points", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnIdListedTwice_When_TheRowsAreChecked_Then_TheSecondIsRefused() =>
        Assert.Contains(Problems("""{ "id": "day", "amplitudeTenths": 60 }""", """{ "id": "day", "amplitudeTenths": 10 }"""), static p => p.Contains("'day' is listed twice", StringComparison.Ordinal));

    [Fact]
    public void Given_ADataFileWithAnUnknownTerm_When_TheGameDataIsValidated_Then_TheProblemIsReported()
    {
        var data = Shipped with { Terms = [.. Shipped.Terms, Spec("""{ "id": "tide", "tenths": 5 }""")] };

        Assert.Contains(GameDataValidator.Validate(TestGame.Data.WithTemperature(data)), static p => p.StartsWith("game/temperature.json", StringComparison.Ordinal) && p.Contains("'tide'", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AFourthSystemAsOneClassOneRegistryLineAndARow_When_ThePlaceIsBuiltFromData_Then_ItsTermSumsInAndNothingElseChanged()
    {
        var registry = TemperatureTermRegistry.Standard.With(LapseTerm.Id, LapseTerm.From);
        var data = Shipped with { Terms = [.. Shipped.Terms, Spec("""{ "id": "lapse", "tenthsPerBlock": 2 }""")] };
        var context = TemperatureRig.Open(TemperatureRig.Sky(13, "sunny")); // feet at y 10

        var standard = PlaceTemperature.From(Shipped);
        var extended = PlaceTemperature.From(data, registry);

        Assert.Equal(standard.Terms.Count + 1, extended.Terms.Count);
        Assert.Equal(standard.TenthsAt(context) - 20, extended.TenthsAt(context));
        Assert.Equal("lapse", extended.Breakdown(context)[^1].Term);
        Assert.Empty(TemperatureValidator.Validate(data, registry: registry));
        Assert.Contains(TemperatureValidator.Validate(data), static p => p.Contains("'lapse'", StringComparison.Ordinal)); // the standard registry does not know it
    }

    // A system a later series would add: a degree or so lost for each block of height.
    private sealed class LapseTerm(int tenthsPerBlock) : ITemperatureTerm
    {
        public const string Id = "lapse";

        public string Name => Id;

        public static ITemperatureTerm From(TermParameters parameters) => new LapseTerm(parameters.Int("tenthsPerBlock", 0, 100));

        public int TenthsAt(ITemperatureContext context) => -context.Feet.Y * tenthsPerBlock;
    }
}
