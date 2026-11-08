using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The underwater blur setting (water-plan H11): saved and loaded, no field for the default, forced off by the view-effects switch, a row of the settings panel.</summary>
public sealed class UnderwaterBlurSettingTests
{
    private static string Text(UnderwaterBlur? blur) =>
        SaveGame.Serialize(SaveGame.Capture(TestGame.NewSession(), TestGame.Route(TestGame.Seed), new SaveSettings(new SoundVolumes(), 100, UnderwaterBlur: blur)));

    [Theory]
    [InlineData(UnderwaterBlur.Off)]
    [InlineData(UnderwaterBlur.Medium)]
    [InlineData(UnderwaterBlur.High)]
    public void Given_AChosenLevel_When_SavedAndParsed_Then_TheLevelComesBack(UnderwaterBlur level)
    {
        var parsed = SaveGame.Parse(Text(level));

        Assert.Equal(level, parsed.Settings!.EffectiveUnderwaterBlur);
        Assert.Equal(SaveGame.Serialize(parsed), Text(level));
    }

    [Fact]
    public void Given_TheDefault_When_Saved_Then_NoFieldIsWrittenAndAnOldSaveReadsLow()
    {
        Assert.DoesNotContain("underwaterBlur", Text(null), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("underwaterBlur", Text(UnderwaterBlur.Low), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UnderwaterBlur.Low, SaveGame.Parse(Text(null)).Settings!.EffectiveUnderwaterBlur);
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

    [Fact]
    public void Given_ThePanel_When_TheBlurRowIsChosen_Then_LeftRightAndEnterCycleTheLevels()
    {
        var blur = UnderwaterBlur.Low;
        var panel = new SettingsPanel(() => 1.0, _ => { }, () => true, _ => { }, () => false, _ => { }, () => blur, b => blur = b);
        panel.Open();
        for (var i = 0; i < SettingsPanel.RowCount - 1; i++)
        {
            panel.HandleInput(TestInputFrame.Pressing(GameKey.Down));
        }

        Assert.Equal(3, panel.Selected);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Right));
        Assert.Equal(UnderwaterBlur.Medium, blur);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Enter));
        Assert.Equal(UnderwaterBlur.High, blur);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Right));
        Assert.Equal(UnderwaterBlur.Off, blur);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Left));
        Assert.Equal(UnderwaterBlur.High, blur);
    }
}
