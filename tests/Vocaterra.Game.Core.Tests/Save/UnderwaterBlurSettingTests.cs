using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The underwater blur setting (water-plan H11): saved and loaded, no field for the default, forced off by the view-effects switch, a row of the settings panel.</summary>
public sealed class UnderwaterBlurSettingTests
{
    private static string Text(UnderwaterBlur? blur) =>
        SettingsFile.Serialize(new SaveSettings(new SoundVolumes(), 100, UnderwaterBlur: blur));

    [Theory]
    [InlineData(UnderwaterBlur.Off)]
    [InlineData(UnderwaterBlur.Medium)]
    [InlineData(UnderwaterBlur.High)]
    public void Given_AChosenLevel_When_SavedAndParsed_Then_TheLevelComesBack(UnderwaterBlur level)
    {
        var parsed = SettingsFile.Parse(Text(level))!;

        Assert.Equal(level, parsed.EffectiveUnderwaterBlur);
        Assert.Equal(SettingsFile.Serialize(parsed), Text(level));
    }

    [Fact]
    public void Given_TheDefault_When_Saved_Then_NoFieldIsWrittenAndAnOldSaveReadsLow()
    {
        Assert.DoesNotContain("underwaterBlur", Text(null), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("underwaterBlur", Text(UnderwaterBlur.Low), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UnderwaterBlur.Low, SettingsFile.Parse(Text(null))!.EffectiveUnderwaterBlur);
        Assert.Equal(UnderwaterBlur.Low, new SaveSettings(new SoundVolumes()).EffectiveUnderwaterBlur);
    }

    [Fact]
    public void Given_AHandEditedLevel_When_Read_Then_ItFallsBackToTheDefault()
    {
        var edited = new SaveSettings(new SoundVolumes(), 100, UnderwaterBlur: (UnderwaterBlur)42);

        Assert.Equal(UnderwaterBlur.Low, edited.EffectiveUnderwaterBlur);
        Assert.Null(edited.Clamped().UnderwaterBlur);
    }

    [Theory]
    [InlineData(UnderwaterBlur.Low, true, UnderwaterBlur.Low)]
    [InlineData(UnderwaterBlur.High, true, UnderwaterBlur.High)]
    [InlineData(UnderwaterBlur.Low, false, UnderwaterBlur.Off)]
    [InlineData(UnderwaterBlur.High, false, UnderwaterBlur.Off)]
    public void Given_TheViewEffectsSwitch_When_Off_Then_TheBlurIsForcedOff(UnderwaterBlur chosen, bool effectsOn, UnderwaterBlur expected) =>
        Assert.Equal(expected, UnderwaterBlurSettings.Effective(chosen, effectsOn));
}
