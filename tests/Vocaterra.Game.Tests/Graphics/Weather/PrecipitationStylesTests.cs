using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Weather;

namespace Vocaterra.Game.Tests.Graphics.Weather;

/// <summary>The style table (E06): a row for every precipitation kind of the bundle's weather data, sensible numbers, and a drop budget in every quality preset.</summary>
public sealed class PrecipitationStylesTests
{
    [Fact]
    public void Given_TheBundlesWeather_When_EveryKindIsRead_Then_EveryPrecipitationKindHasAStyleRow()
    {
        var kinds = TestGame.Data.Weather.Kinds.Select(static k => k.Channels.PrecipitationKind).Where(static p => p.Length > 0).Distinct().ToList();

        Assert.NotEmpty(kinds); // the first data has rain
        foreach (var kind in kinds)
        {
            Assert.True(PrecipitationStyles.TryFor(kind, out _), $"the weather data names the precipitation '{kind}' and the style table has no row for it: add one to PrecipitationStyles.Table");
        }
    }

    [Fact]
    public void Given_TheTable_When_ItsRowsAreRead_Then_EachIdIsUniqueAndEachNumberIsInRange()
    {
        Assert.Equal(PrecipitationStyles.Table.Count, PrecipitationStyles.Table.Select(static r => r.Id).Distinct().Count());
        foreach (var row in PrecipitationStyles.Table)
        {
            Assert.False(string.IsNullOrEmpty(row.Id));
            Assert.InRange(row.Length, 0.1f, 4f);
            Assert.InRange(row.Width, 0.005f, 0.2f);
            Assert.InRange(row.FallSpeed, 0.5f, 40f);
            Assert.InRange(row.Opacity, 0.05f, 1f);
            Assert.InRange(row.WindSlant, 0f, 2f);
            Assert.InRange(row.CountShare, 0.05f, 1f);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("drizzle")]
    [InlineData("RAIN ")]
    public void Given_AKindTheTableLacks_When_LookedUp_Then_ThereIsNoRow(string kind)
    {
        Assert.False(PrecipitationStyles.TryFor(kind, out _));
    }

    [Fact]
    public void Given_EveryQualityLevel_When_ThePresetIsRead_Then_TheDropBudgetRisesWithTheLevelAndFitsTheField()
    {
        var low = QualityPreset.For(QualityLevel.Low).RainDrops;
        var medium = QualityPreset.For(QualityLevel.Medium).RainDrops;
        var high = QualityPreset.For(QualityLevel.High).RainDrops;

        Assert.InRange(low, 1, PrecipitationField.MaxDrops);
        Assert.True(low < medium && medium < high);
        Assert.InRange(high, 1, PrecipitationField.MaxDrops);
    }
}
