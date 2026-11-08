using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Menu;

/// <summary>The player's settings: the defaults the issue names, the limits of each value, the save round trip and the command line over the save.</summary>
public sealed class PlayerSettingsTests
{
    [Fact]
    public void Given_ANewGame_When_TheSettingsAreRead_Then_QualityIsHighAndTheTextIsAtItsLargest()
    {
        var settings = new PlayerSettings();
        Assert.Equal(QualityLevel.High, settings.Quality);
        Assert.Equal(SaveSettings.TextScaleLevels.Max(), settings.TextPercent);
        Assert.Equal(SoundVolumes.Default, settings.Volumes);
        Assert.Equal(1.0, settings.MouseSensitivity);
        Assert.False(settings.InvertY);
        Assert.Equal(70, settings.FieldOfView);
        Assert.Equal(1.0, settings.NeedsRate);
        Assert.Equal(UnderwaterBlurSettings.Default, settings.Blur);
        Assert.True(settings.ViewEffects);
    }

    [Theory]
    [InlineData(SettingId.AmbientVolume, -40, 0)]
    [InlineData(SettingId.AmbientVolume, 400, 100)]
    [InlineData(SettingId.VoiceVolume, 62, 60)]
    [InlineData(SettingId.VoiceVolume, 63, 65)]
    [InlineData(SettingId.Quality, 9, 2)]
    [InlineData(SettingId.Quality, -1, 0)]
    [InlineData(SettingId.TextSize, 7, 2)]
    [InlineData(SettingId.MouseSensitivity, 1, 25)]
    [InlineData(SettingId.MouseSensitivity, 1000, 300)]
    [InlineData(SettingId.FieldOfView, 30, 60)]
    [InlineData(SettingId.FieldOfView, 200, 110)]
    [InlineData(SettingId.NeedsRate, 10, 50)]
    [InlineData(SettingId.InvertY, 5, 1)]
    public void Given_AValueOutOfRange_When_Set_Then_ItIsLimitedAndSnappedToItsSteps(SettingId id, int asked, int expected)
    {
        var settings = new PlayerSettings();
        settings.Set(id, asked);
        Assert.Equal(expected, settings.Get(id));
    }

    [Fact]
    public void Given_ASetting_When_ItMoves_Then_ChangedFiresOnceAndNotForTheSameValue()
    {
        var settings = new PlayerSettings();
        var seen = new List<SettingId>();
        settings.Changed += seen.Add;
        settings.Set(SettingId.Quality, 0);
        settings.Set(SettingId.Quality, 0);
        settings.Set(SettingId.AmbientVolume, 60);
        Assert.Equal([SettingId.Quality], seen);
        Assert.Equal(QualityLevel.Low, settings.Quality);
    }

    [Fact]
    public void Given_EverySetting_When_ChangedAndSavedAndLoaded_Then_TheSameValuesComeBack()
    {
        var settings = new PlayerSettings();
        settings.Set(SettingId.AmbientVolume, 15);
        settings.Set(SettingId.VoiceVolume, 35);
        settings.Set(SettingId.SfxVolume, 45);
        settings.Set(SettingId.UiVolume, 5);
        settings.Set(SettingId.Quality, (int)QualityLevel.Medium);
        settings.Set(SettingId.TextSize, 1);
        settings.Set(SettingId.Blur, (int)UnderwaterBlur.High);
        settings.Set(SettingId.ViewEffects, 0);
        settings.Set(SettingId.MouseSensitivity, 175);
        settings.Set(SettingId.InvertY, 1);
        settings.Set(SettingId.FieldOfView, 95);
        settings.Set(SettingId.NeedsRate, 250);
        settings.Set(SettingId.RelaxedNeeds, 1);

        var loaded = PlayerSettings.Resolve(settings.ToSave());
        foreach (var spec in PlayerSettings.All)
        {
            Assert.Equal(settings.Get(spec.Id), loaded.Get(spec.Id));
        }
    }

    [Fact]
    public void Given_EverySettingAtItsDefault_When_Saved_Then_TheSaveHoldsOnlyTheVolumesAndTheText()
    {
        var saved = new PlayerSettings().ToSave();
        Assert.Null(saved.Quality);
        Assert.Null(saved.UnderwaterBlur);
        Assert.Equal(0, saved.MouseSensitivity);
        Assert.Equal(0, saved.FieldOfView);
        Assert.False(saved.InvertY);
        Assert.Equal(150, saved.TextScale);
    }

    [Fact]
    public void Given_ASaveFromBeforeTheMenu_When_Resolved_Then_TheNewSettingsTakeTheirDefaults()
    {
        var settings = PlayerSettings.Resolve(new SaveSettings(new SoundVolumes(0.5, 0.5, 0.5, 0.5), 100));
        Assert.Equal(QualityLevel.High, settings.Quality);
        Assert.Equal(100, settings.MouseSensitivity * 100);
        Assert.Equal(70, settings.FieldOfView);
        Assert.Equal(100, settings.TextPercent);
        Assert.Equal(50, settings.Get(SettingId.VoiceVolume));
    }

    [Fact]
    public void Given_ALaunchOption_When_ResolvedOverTheSave_Then_TheFlagWinsForTheRun()
    {
        var saved = new SaveSettings(new SoundVolumes(1, 0.2, 0.8, 0.9), 125, Quality: QualityLevel.Medium, UnderwaterBlur: UnderwaterBlur.High);
        var settings = PlayerSettings.Resolve(saved, new LaunchSettings(SfxVolume: 0.7, AmbientVolume: 0, TextPercent: 100, Quality: QualityLevel.Low, Blur: UnderwaterBlur.Off, NeedsRate: 2));
        Assert.Equal(70, settings.Get(SettingId.SfxVolume));
        Assert.Equal(0, settings.Get(SettingId.AmbientVolume));
        Assert.Equal(100, settings.TextPercent);
        Assert.Equal(QualityLevel.Low, settings.Quality);
        Assert.Equal(UnderwaterBlur.Off, settings.Blur);
        Assert.Equal(2.0, settings.NeedsRate);
        Assert.Equal(80, settings.Get(SettingId.UiVolume)); // not given: the save's
    }

    [Fact]
    public void Given_AScriptedRun_When_NothingSaysTheTextSize_Then_ItStaysAtOneHundredPercent()
    {
        Assert.Equal(100, PlayerSettings.Resolve(null, new LaunchSettings(Scripted: true)).TextPercent);
        Assert.Equal(125, PlayerSettings.Resolve(null, new LaunchSettings(Scripted: true, TextPercent: 125)).TextPercent);
        Assert.Equal(150, PlayerSettings.Resolve(null, new LaunchSettings()).TextPercent);
    }

    [Fact]
    public void Given_ARateOrAGarbledSave_When_Resolved_Then_NothingEscapesItsRange()
    {
        var settings = PlayerSettings.Resolve(new SaveSettings(new SoundVolumes(9, double.NaN, -3, 2), 77, MouseSensitivity: 9999, FieldOfView: 3, NeedsRate: 77));
        Assert.All(PlayerSettings.All, spec => Assert.InRange(settings.Get(spec.Id), spec.Min, spec.Max));
        Assert.Equal("×0.5", PlayerSettings.RateText(0.5));
        Assert.Equal("×1.25", PlayerSettings.RateText(1.25));
    }

    [Fact]
    public void Given_TheSpecs_When_Listed_Then_EverySettingHasOneAndItsDefaultIsInRange()
    {
        Assert.Equal(Enum.GetValues<SettingId>().Length, PlayerSettings.All.Count);
        Assert.All(PlayerSettings.All, spec =>
        {
            Assert.Equal(spec.Default, spec.Snap(spec.Default));
            Assert.True(spec.Kind != SettingKind.Slider || (spec.Max - spec.Min) % spec.Step == 0, spec.Id.ToString());
        });
    }
}
