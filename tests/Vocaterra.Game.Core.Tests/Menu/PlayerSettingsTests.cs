using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
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
    [InlineData(SettingId.WeatherVolume, -40, 0)]
    [InlineData(SettingId.WeatherVolume, 400, 100)]
    [InlineData(SettingId.WeatherVolume, 62, 60)]
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
    [InlineData(SettingId.ViewDistance, 0, 32)]
    [InlineData(SettingId.ViewDistance, 47, 48)]
    [InlineData(SettingId.ViewDistance, 100, 96)]
    [InlineData(SettingId.ViewDistance, 9000, 576)]
    public void Given_AValueOutOfRange_When_Set_Then_ItIsLimitedAndSnappedToItsSteps(SettingId id, int asked, int expected)
    {
        var settings = new PlayerSettings();
        settings.Set(id, asked);
        Assert.Equal(expected, settings.Get(id));
    }

    [Fact]
    public void Given_TheWeatherSetting_When_ANewGameStarts_Then_ItIsSixtyLikeTheAmbientAndItsVolumeIsTheWeathers()
    {
        var settings = new PlayerSettings();
        var spec = PlayerSettings.Spec(SettingId.WeatherVolume);
        Assert.Equal("settingsWeather", spec.LabelKey);
        Assert.Equal((0, 100, 5, 60), (spec.Min, spec.Max, spec.Step, spec.Default));
        Assert.Equal(0.6, settings.WeatherVolume, 6);
        Assert.Equal(0.6, settings.Volumes.Weather, 6);
        Assert.Equal(settings.AmbientVolume, settings.Volumes.Ambient, 6); // a new game sounds as it did before E09
    }

    [Fact]
    public void Given_TheWeatherSetting_When_ChangedLive_Then_OnlyItMovesAndTheVolumesFollow()
    {
        var settings = new PlayerSettings();
        var seen = new List<SettingId>();
        settings.Changed += seen.Add;
        settings.Set(SettingId.WeatherVolume, 15);

        Assert.Equal([SettingId.WeatherVolume], seen);
        Assert.Equal(0.15, settings.Volumes.Weather, 6);
        Assert.Equal(0.6, settings.Volumes.Ambient, 6); // the ambient volume did not move
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
        settings.Set(SettingId.WeatherVolume, 35);
        settings.Set(SettingId.VoiceVolume, 35);
        settings.Set(SettingId.SfxVolume, 45);
        settings.Set(SettingId.UiVolume, 5);
        settings.Set(SettingId.Quality, (int)QualityLevel.Medium);
        settings.Set(SettingId.ViewDistance, 320);
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
        Assert.Equal(0, saved.ViewDistance);
        Assert.False(saved.InvertY);
        Assert.Equal(150, saved.TextScale);
    }

    [Fact]
    public void Given_TheDrawDistance_When_ItIsAutoOrChosen_Then_TheSaveHoldsZeroOrTheBlocks()
    {
        var settings = new PlayerSettings();
        Assert.Null(settings.ViewDistance); // Auto: the quality's own distance
        Assert.Equal(32, settings.Get(SettingId.ViewDistance));

        settings.Set(SettingId.ViewDistance, 48);
        Assert.Equal(48, settings.ViewDistance);
        Assert.Equal(48, settings.ToSave().ViewDistance);

        settings.Set(SettingId.ViewDistance, 0);
        Assert.Null(settings.ViewDistance);
        Assert.Equal(0, settings.ToSave().ViewDistance);
        Assert.Null(PlayerSettings.Resolve(settings.ToSave()).ViewDistance);
        Assert.Equal(512, PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default, 100, ViewDistance: 512)).ViewDistance);
        Assert.Null(PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default, 100, ViewDistance: 12)).ViewDistance); // below the lowest: Auto
    }

    [Fact]
    public void Given_TheDrawDistanceRow_When_ShownAndMoved_Then_AutoIsWrittenInWordsAndTheEndsStop()
    {
        var settings = new PlayerSettings();
        var row = MenuRow.Of(settings, SettingId.ViewDistance);
        Assert.Equal("Auto", row.ValueText(key => key == "settingsAuto" ? "Auto" : key));
        row.Change(1);
        Assert.Equal(48, settings.ViewDistance);
        Assert.Equal("48", row.ValueText(key => key));
        row.Change(-1);
        row.Change(-1);
        Assert.Null(settings.ViewDistance);
        row.SetFraction(1);
        Assert.Equal(576, settings.ViewDistance);
    }

    [Fact]
    public void Given_TheDrawDistanceAndTheQuality_When_Either_Changes_Then_TheyAreSeparateSettings()
    {
        var settings = new PlayerSettings();
        settings.Set(SettingId.ViewDistance, 160);
        settings.Set(SettingId.Quality, (int)QualityLevel.Low);
        Assert.Equal(160, settings.ViewDistance);
        settings.Set(SettingId.ViewDistance, 0);
        Assert.Equal(QualityLevel.Low, settings.Quality);
        Assert.Null(settings.ViewDistance);
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
    public void Given_AWeatherVolumeInTheSave_When_ResolvedWithAndWithoutTheFlag_Then_TheSaveHoldsUnlessTheFlagWins()
    {
        var saved = new SaveSettings(new SoundVolumes(1, 0.8, 0.8, 0.6, 0.9));
        Assert.Equal(90, PlayerSettings.Resolve(saved).Get(SettingId.WeatherVolume));
        Assert.Equal(20, PlayerSettings.Resolve(saved, new LaunchSettings(WeatherVolume: 0.2)).Get(SettingId.WeatherVolume));
        Assert.Equal(90, PlayerSettings.Resolve(saved, new LaunchSettings(AmbientVolume: 0.2)).Get(SettingId.WeatherVolume)); // the ambient flag leaves it alone
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
    public void Given_TheNewLaunchOptions_When_ResolvedOverTheSave_Then_EachWinsAndTheRestKeepTheSave()
    {
        var saved = new SaveSettings(new SoundVolumes(0.3, 0.8, 0.4, 0.6), 100, MouseSensitivity: 200, InvertY: true, FieldOfView: 90, ViewDistance: 160);
        var launch = new LaunchSettings(VoiceVolume: 0.1, UiVolume: 1, ViewDistance: 100, FieldOfView: 60, MouseSensitivity: 25, InvertY: false);
        var settings = PlayerSettings.Resolve(saved, launch);
        Assert.Equal(10, settings.Get(SettingId.VoiceVolume));
        Assert.Equal(100, settings.Get(SettingId.UiVolume));
        Assert.Equal(96, settings.ViewDistance); // kept to the menu's steps
        Assert.Equal(60, settings.FieldOfView);
        Assert.Equal(0.25, settings.MouseSensitivity);
        Assert.False(settings.InvertY);

        var kept = PlayerSettings.Resolve(saved, new LaunchSettings());
        Assert.Equal(30, kept.Get(SettingId.VoiceVolume));
        Assert.Equal(160, kept.ViewDistance);
        Assert.Equal(90, kept.FieldOfView);
        Assert.Equal(2.0, kept.MouseSensitivity);
        Assert.True(kept.InvertY);

        Assert.True(PlayerSettings.Resolve(null, new LaunchSettings(InvertY: true)).InvertY);
        Assert.Null(PlayerSettings.Resolve(null).ViewDistance);
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

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(34, 30)]
    [InlineData(35, 40)]
    [InlineData(100, 100)]
    [InlineData(900, 100)]
    public void Given_AGrassValue_When_Set_Then_ItIsLimitedToZeroToOneHundredInStepsOfTen(int asked, int expected)
    {
        var settings = new PlayerSettings();
        settings.Set(SettingId.Grass, asked);
        Assert.Equal(expected, settings.Get(SettingId.Grass));
        Assert.Equal(expected / 100.0, settings.Grass);
    }

    [Fact]
    public void Given_ANewGame_When_TheGrassIsRead_Then_ItIsTheLushDefaultAndMovingItIsTold()
    {
        var settings = new PlayerSettings();
        Assert.Equal(PlayerSettings.DefaultGrass, settings.Get(SettingId.Grass));
        Assert.Equal(SettingKind.Slider, PlayerSettings.Spec(SettingId.Grass).Kind);
        var changed = new List<SettingId>();
        settings.Changed += changed.Add;
        settings.Set(SettingId.Grass, 100);
        settings.Set(SettingId.Grass, 100);
        Assert.Equal([SettingId.Grass], changed);
        Assert.Contains(MenuRows.Settings(settings, null), static row => row.Id == nameof(SettingId.Grass));
        Assert.Equal("Grass", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.Grass).LabelKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PlayerSettings.DefaultGrass)]
    [InlineData(100)]
    [InlineData(10)]
    public void Given_AGrassSetting_When_SavedAndLoaded_Then_ItComesBack(int percent)
    {
        var settings = new PlayerSettings();
        settings.Set(SettingId.Grass, percent);
        var saved = settings.ToSave();
        Assert.Equal(percent == PlayerSettings.DefaultGrass ? null : percent, saved.Grass); // the default is left out, as the other settings' defaults are (zero is written: it is not the default)
        Assert.Equal(percent, PlayerSettings.Resolve(saved).Get(SettingId.Grass));
        var json = System.Text.Json.JsonSerializer.Serialize(saved);
        var again = System.Text.Json.JsonSerializer.Deserialize<SaveSettings>(json)!;
        Assert.Equal(percent, PlayerSettings.Resolve(again).Get(SettingId.Grass));
    }

    [Fact]
    public void Given_ASaveWithoutTheGrassField_When_Resolved_Then_TheDefaultHolds()
    {
        Assert.Equal(PlayerSettings.DefaultGrass, PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default)).Get(SettingId.Grass));
        Assert.Equal(100, PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default, Grass: 4000)).Get(SettingId.Grass));
    }

    [Fact]
    public void Given_AGrassLaunchOption_When_Resolved_Then_ItWinsOverTheSaveAndIsSnapped()
    {
        var saved = new SaveSettings(SoundVolumes.Default, Grass: 20);
        Assert.Equal(20, PlayerSettings.Resolve(saved).Get(SettingId.Grass));
        Assert.Equal(80, PlayerSettings.Resolve(saved, new LaunchSettings(Grass: 80)).Get(SettingId.Grass));
        Assert.Equal(0, PlayerSettings.Resolve(saved, new LaunchSettings(Grass: 0)).Get(SettingId.Grass));
        Assert.Equal(70, PlayerSettings.Resolve(null, new LaunchSettings(Grass: 68)).Get(SettingId.Grass));
    }

    [Fact]
    public void Given_ANewGame_When_TheShadersAreRead_Then_TheyAreOnAndTheRowIsInTheGraphicsGroup()
    {
        var settings = new PlayerSettings();
        Assert.True(settings.Shaders);
        Assert.Equal(SettingKind.Toggle, PlayerSettings.Spec(SettingId.Shaders).Kind);
        var rows = MenuRows.Settings(settings, null);
        var graphics = rows.ToList().FindIndex(static row => row.Id == "heading.headingGraphics");
        var interfaceGroup = rows.ToList().FindIndex(static row => row.Id == "heading.headingInterface");
        var shaders = rows.ToList().FindIndex(static row => row.Id == nameof(SettingId.Shaders));
        Assert.True(graphics >= 0 && shaders > graphics && shaders < interfaceGroup, "the Shaders row stands in the Graphics group");
        Assert.Equal("Shaders", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.Shaders).LabelKey));
        Assert.Equal("Shaders and effects", TestGame.Locale.Ui("subEffects"));
        var changed = new List<SettingId>();
        settings.Changed += changed.Add;
        settings.Set(SettingId.Shaders, 0);
        settings.Set(SettingId.Shaders, 0);
        Assert.Equal([SettingId.Shaders], changed);
        Assert.False(settings.Shaders);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_TheShadersSetting_When_SavedAndLoaded_Then_ItComesBackAndTheDefaultIsLeftOut(bool on)
    {
        var settings = new PlayerSettings();
        settings.Set(SettingId.Shaders, on ? 1 : 0);
        var saved = settings.ToSave();
        Assert.Equal(!on, saved.ShadersOff);
        Assert.Equal(on, PlayerSettings.Resolve(saved).Shaders);
        var json = System.Text.Json.JsonSerializer.Serialize(saved);
        Assert.Equal(!on, json.Contains("ShadersOff", StringComparison.Ordinal)); // written only when off: a save of the default is byte for byte what it was
        Assert.Equal(on, PlayerSettings.Resolve(System.Text.Json.JsonSerializer.Deserialize<SaveSettings>(json)!).Shaders);
    }

    [Fact]
    public void Given_ASaveWithoutTheShadersField_When_Resolved_Then_TheyAreOnAndTheLaunchOptionWins()
    {
        Assert.True(PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default)).Shaders);
        var off = new SaveSettings(SoundVolumes.Default, ShadersOff: true);
        Assert.False(PlayerSettings.Resolve(off).Shaders);
        Assert.True(PlayerSettings.Resolve(off, new LaunchSettings(Shaders: true)).Shaders);
        Assert.False(PlayerSettings.Resolve(null, new LaunchSettings(Shaders: false)).Shaders);
        Assert.True(PlayerSettings.Resolve(null, new LaunchSettings()).Shaders);
    }

    // B17c: the graphics effects of the settings page's Graphics group (D-game-34 item 43), each a toggle on by default, saved only when off, read back, and a launch option each.
    private static readonly (SettingId Id, string Json, string Label, Func<PlayerSettings, bool> Read, Func<SaveSettings, bool> Saved, Func<bool, LaunchSettings> Launch)[] Effects =
    [
        (SettingId.SunFollowsHour, "SunFollowsHourOff", "Sun follows the hour", static s => s.SunFollowsHour, static s => s.SunFollowsHourOff, static on => new LaunchSettings(SunFollowsHour: on)),
        (SettingId.ClimateColours, "ClimateColoursOff", "Climate colours", static s => s.ClimateColours, static s => s.ClimateColoursOff, static on => new LaunchSettings(ClimateColours: on)),
        (SettingId.WaterShader, "WaterShaderOff", "Water shader", static s => s.WaterShader, static s => s.WaterShaderOff, static on => new LaunchSettings(WaterShader: on)),
        (SettingId.HeightFog, "HeightFogOff", "Fog by height", static s => s.HeightFog, static s => s.HeightFogOff, static on => new LaunchSettings(HeightFog: on)),
        (SettingId.FarLight, "FarLightOff", "Far view light", static s => s.FarLight, static s => s.FarLightOff, static on => new LaunchSettings(FarLight: on)),
    ];

    [Fact]
    public void Given_TheGraphicsEffects_When_ANewGameStarts_Then_EachIsOnAndInTheGraphicsGroupWithTheShaders()
    {
        var settings = new PlayerSettings();
        var rows = MenuRows.Settings(settings, null).ToList();
        var graphics = rows.FindIndex(static row => row.Id == "heading.headingGraphics");
        var interfaceGroup = rows.FindIndex(static row => row.Id == "heading.headingInterface");
        var effects = rows.FindIndex(static row => row.LabelKey == "subEffects");
        foreach (var (id, _, label, read, _, _) in Effects)
        {
            Assert.True(read(settings), $"{id} is on in a new game");
            Assert.Equal(SettingKind.Toggle, PlayerSettings.Spec(id).Kind);
            Assert.Equal(label, TestGame.Locale.Ui(PlayerSettings.Spec(id).LabelKey));
            var row = rows.FindIndex(r => r.Id == id.ToString());
            Assert.True(row > effects && effects > graphics && row < interfaceGroup, $"{id}'s row is under the effects' heading in the Graphics group");
        }

        // B18's grass slider and the window's antialiasing moved into the group, beside the shaders (B17c).
        foreach (var moved in new[] { SettingId.Shaders, SettingId.Grass, SettingId.Msaa })
        {
            var row = rows.FindIndex(r => r.Id == moved.ToString());
            Assert.True(row > effects && row < interfaceGroup, $"{moved} is in the effects' group");
        }

        Assert.Equal(4, settings.Msaa);
        Assert.Equal(SettingKind.Choice, PlayerSettings.Spec(SettingId.Msaa).Kind);
        Assert.Equal("Antialiasing", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.Msaa).LabelKey));
    }

    [Fact]
    public void Given_AnEffectIsSwitched_When_SetTwice_Then_ChangedFiresOnceAndTheOthersStayOn()
    {
        foreach (var (id, _, _, read, _, _) in Effects)
        {
            var settings = new PlayerSettings();
            var changed = new List<SettingId>();
            settings.Changed += changed.Add;
            settings.Set(id, 0);
            settings.Set(id, 0);
            Assert.Equal([id], changed);
            Assert.False(read(settings));
            Assert.Equal(Effects.Length - 1, Effects.Count(e => e.Read(settings)));
        }
    }

    [Fact]
    public void Given_AnEffectOff_When_SavedAndLoaded_Then_ItComesBackAndAnEffectOnIsLeftOutOfTheFile()
    {
        var plain = System.Text.Json.JsonSerializer.Serialize(new PlayerSettings().ToSave());
        foreach (var (id, json, _, read, saved, _) in Effects)
        {
            Assert.DoesNotContain(json, plain, StringComparison.Ordinal); // a save of the default is byte for byte what it was
            var settings = new PlayerSettings();
            settings.Set(id, 0);
            var save = settings.ToSave();
            Assert.True(saved(save));
            var text = System.Text.Json.JsonSerializer.Serialize(save);
            Assert.Contains(json, text, StringComparison.Ordinal);
            var back = PlayerSettings.Resolve(System.Text.Json.JsonSerializer.Deserialize<SaveSettings>(text)!);
            Assert.False(read(back));
            Assert.Equal(Effects.Length - 1, Effects.Count(e => e.Read(back)));
        }

        Assert.DoesNotContain("Msaa", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ALaunchOptionForAnEffect_When_Resolved_Then_ItWinsOverTheSaveForTheRun()
    {
        foreach (var (_, _, _, read, _, launch) in Effects)
        {
            Assert.True(read(PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default, SunFollowsHourOff: true, ClimateColoursOff: true, WaterShaderOff: true, HeightFogOff: true, FarLightOff: true), launch(true))));
            Assert.False(read(PlayerSettings.Resolve(null, launch(false))));
            Assert.True(read(PlayerSettings.Resolve(null, new LaunchSettings())));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    public void Given_TheAntialiasingRow_When_SavedAndLoaded_Then_TheSamplesComeBackAndTheDefaultIsLeftOut(int samples, int expected)
    {
        var settings = PlayerSettings.Resolve(null, new LaunchSettings(Msaa: samples));
        Assert.Equal(expected, settings.Msaa);
        var save = settings.ToSave();
        Assert.Equal(samples == 4 ? null : samples, save.Msaa);
        Assert.Equal(expected, PlayerSettings.Resolve(save).Msaa);
        Assert.Equal(expected, PlayerSettings.Resolve(System.Text.Json.JsonSerializer.Deserialize<SaveSettings>(System.Text.Json.JsonSerializer.Serialize(save))!).Msaa);
        Assert.Equal(4, PlayerSettings.Resolve(new SaveSettings(SoundVolumes.Default, Msaa: 3)).Msaa); // not a count the window offers: the default
        Assert.Equal(4, PlayerSettings.Resolve(null, new LaunchSettings(Msaa: 3)).Msaa);
    }
}
