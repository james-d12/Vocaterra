using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Far;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's settings on the settings page (B07d) as they reach the preset: Auto and the defaults change nothing, the rest set the far view's numbers.</summary>
public sealed class FarChoicesTests
{
    [Theory]
    [InlineData(QualityLevel.Low)]
    [InlineData(QualityLevel.Medium)]
    [InlineData(QualityLevel.High)]
    public void Given_TheDefaultSettings_When_TheyAreApplied_Then_ThePresetAndSoTheFarViewIsUnchanged(QualityLevel level)
    {
        var preset = QualityPreset.For(level);
        var applied = FarChoices.Apply(preset, 0, 100, 0, 2, 0);

        Assert.Equal(preset, applied);
        Assert.Equal(preset.Far, applied.Far);
        var spec = (Distance: PlayerSettings.Spec(SettingId.FarDistance).Default, Detail: PlayerSettings.Spec(SettingId.FarDetail).Default, Trees: PlayerSettings.Spec(SettingId.FarTrees).Default, Textures: PlayerSettings.Spec(SettingId.FarTextures).Default, Exact: PlayerSettings.Spec(SettingId.FarExact).Default);
        Assert.Equal(preset, FarChoices.Apply(preset, spec.Distance, spec.Detail, spec.Trees, spec.Textures, spec.Exact)); // the settings' own defaults are exactly these
    }

    [Fact]
    public void Given_ChosenSettings_When_TheyAreApplied_Then_TheFarViewTakesThem()
    {
        var applied = FarChoices.Apply(QualityPreset.For(QualityLevel.High), 12, 150, 6, 0, 3);

        Assert.Equal(12 * 1024, applied.FarRange);
        Assert.Equal(1.5, applied.Far.BandScale);
        Assert.Equal(5, applied.Far.TreeLevels); // 4 km
        Assert.Equal(0, applied.Far.TexturedLevels);
        Assert.Equal(1000, applied.Far.RealRadius);
        Assert.Equal(0, FarChoices.Apply(QualityPreset.For(QualityLevel.High), 0, 100, 1, 2, 1).Far.TreeLevels); // none
        Assert.Equal(0, FarChoices.Apply(QualityPreset.For(QualityLevel.High), 0, 100, 0, 2, 1).Far.RealRadius); // the model alone
    }

    [Fact]
    public void Given_AFarViewThatIsOff_When_ADistanceIsChosen_Then_ItStaysOff()
    {
        var off = QualityPreset.For(QualityLevel.High).WithFarRange(0);

        Assert.Equal(0, FarChoices.Apply(off, 8, 100, 0, 2, 0).FarRange);
    }

    [Fact]
    public void Given_TheSettingsChoices_When_TheyAreMapped_Then_EveryChoiceHasAValueAndTheLaunchWordsMatch()
    {
        var trees = PlayerSettings.Spec(SettingId.FarTrees);
        Assert.Equal(trees.Max, FarChoices.TreeLevels.Count); // choice 0 is Auto, choices 1 to max the levels
        Assert.Equal(FarChoices.TreeLevels.Count + 1, trees.ChoiceKeys!.Count);
        var exact = PlayerSettings.Spec(SettingId.FarExact);
        Assert.Equal(exact.Max, FarChoices.ExactRadii.Count);
        Assert.Equal(FarChoices.ExactRadii.Count + 1, exact.ChoiceKeys!.Count);
        Assert.Equal(0, FarChoices.TreesOf("auto"));
        Assert.Equal(1, FarChoices.TreesOf("off"));
        Assert.Equal(2, FarChoices.TreesOf("256"));
        Assert.Equal(6, FarChoices.TreesOf("4096"));
        Assert.Null(FarChoices.TreesOf("300"));
        Assert.Null(FarChoices.TreesOf("-1"));
        Assert.Equal(0, FarChoices.ExactOf("auto"));
        Assert.Equal(1, FarChoices.ExactOf("off"));
        Assert.Equal(2, FarChoices.ExactOf("500"));
        Assert.Equal(5, FarChoices.ExactOf("2000"));
        Assert.Null(FarChoices.ExactOf("0"));
        Assert.Null(FarChoices.ExactOf("700"));
    }

    [Fact]
    public void Given_EveryChoiceOfTheTwoSettings_When_AppliedThroughTheLaunchWords_Then_ItGivesTheDistanceTheLabelSays()
    {
        var treeBlocks = new[] { 256, 512, 1024, 2048, 4096 };
        for (var i = 0; i < treeBlocks.Length; i++)
        {
            var choice = FarChoices.TreesOf(treeBlocks[i].ToString(System.Globalization.CultureInfo.InvariantCulture))!.Value;
            Assert.Equal(i + 1, FarChoices.Apply(QualityPreset.For(QualityLevel.High), 0, 100, choice, 2, 0).FarTreeLevels);
        }

        var radii = new[] { 500, 1000, 1500, 2000 };
        foreach (var radius in radii)
        {
            var choice = FarChoices.ExactOf(radius.ToString(System.Globalization.CultureInfo.InvariantCulture))!.Value;
            Assert.Equal(radius, FarChoices.Apply(QualityPreset.For(QualityLevel.High), 0, 100, 0, 2, choice).FarRealRadius);
        }
    }
}
