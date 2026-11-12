using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary><c>game/day.json</c> (E01): the file's parse and the validator's rules.</summary>
public sealed class DayDataTests
{
    private const string Json = """
        { "schemaVersion": 1, "cycleSeconds": 600, "dawnHour": 5, "dayHour": 7.5, "duskHour": 19, "nightHour": 21, "startHour": 9, "wakeHour": 6.5 }
        """;

    private static DayData Day => TestGame.Data.Day;

    private static IReadOnlyList<string> Problems(DayData day) => GameDataValidator.Validate(TestGame.Data.WithDay(day));

    [Fact]
    public void TheFileParsesIncludingFractionalHours()
    {
        var day = DayData.Parse(Json);

        Assert.Equal(new DayData(600, 5, 7.5, 19, 21, 9, 6.5), day);
    }

    [Fact]
    public void TheShippedFileIsTheOwnersFirstValuesAndHoldsTogether()
    {
        Assert.Equal(new DayData(2400, 4, 6, 20, 22, 8, 7), Day);
        Assert.Equal(DayData.Default, Day);
        Assert.Empty(DayValidator.Validate(Day));
        Assert.DoesNotContain(Problems(Day), static p => p.Contains("day.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "cycleSeconds": 1, "dawnHour": 1, "dayHour": 2, "duskHour": 3, "nightHour": 4, "startHour": 0, "wakeHour": 0 }""")]
    [InlineData("""{ "schemaVersion": 1, "cycleSeconds": 1, "dawnHour": 1, "dayHour": 2, "duskHour": 3, "nightHour": 4, "startHour": 0 }""")]
    [InlineData("""{ "schemaVersion": 1, "cycleSeconds": "long", "dawnHour": 1, "dayHour": 2, "duskHour": 3, "nightHour": 4, "startHour": 0, "wakeHour": 0 }""")]
    [InlineData("not json")]
    public void AFileThatIsWrongInShapeIsRefused(string json) => Assert.Throws<InvalidDataException>(() => DayData.Parse(json));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void ACycleThatIsNotPositiveIsRefused(double seconds) =>
        Assert.Contains(Problems(Day with { CycleSeconds = seconds }), static p => p.Contains("cycleSeconds", StringComparison.Ordinal));

    [Fact]
    public void AnHourOffTheDialIsRefusedAndNamed()
    {
        Assert.Contains(Problems(Day with { DawnHour = -1 }), static p => p.Contains("dawnHour", StringComparison.Ordinal));
        Assert.Contains(Problems(Day with { DayHour = 25 }), static p => p.Contains("dayHour", StringComparison.Ordinal));
        Assert.Contains(Problems(Day with { DuskHour = double.NaN }), static p => p.Contains("duskHour", StringComparison.Ordinal));
        Assert.Contains(Problems(Day with { NightHour = 24.5 }), static p => p.Contains("nightHour", StringComparison.Ordinal));
        Assert.Contains(Problems(Day with { StartHour = 24 }), static p => p.Contains("startHour", StringComparison.Ordinal));
        Assert.Contains(Problems(Day with { WakeHour = -0.5 }), static p => p.Contains("wakeHour", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(6, 6, 20, 22)]
    [InlineData(7, 6, 20, 22)]
    [InlineData(4, 6, 22, 20)]
    [InlineData(4, 6, 20, 20)]
    [InlineData(4, 21, 20, 22)]
    public void PhasesThatDoNotBeginInOrderAreRefused(double dawn, double day, double dusk, double night) =>
        Assert.Contains(Problems(Day with { DawnHour = dawn, DayHour = day, DuskHour = dusk, NightHour = night }), static p => p.Contains("in order", StringComparison.Ordinal));

    [Fact]
    public void AnEdgeValueThatIsValidIsNotRefused()
    {
        Assert.Empty(DayValidator.Validate(Day with { DawnHour = 0, NightHour = 24, StartHour = 0, WakeHour = 23.99 }));
    }
}
