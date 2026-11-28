using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The global settings file (Q01; before it the settings rode in the save, S24): the values survive a write and a read, a missing or foreign file is no file, and no save carries them.</summary>
public sealed class SettingsFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-settings-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheSoundVolumesSurviveAWriteAndARead()
    {
        var settings = new SaveSettings(new SoundVolumes(0.9, 0.25, 0.5, 0.0, 0.3));

        var text = SettingsFile.Serialize(settings);
        var read = SettingsFile.Parse(text);

        Assert.Equal(settings, read);
        Assert.Equal(text, SettingsFile.Serialize(read!)); // canonical: the same settings give the same bytes
        Assert.Contains("\"weather\":0.3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_APath_When_TheSettingsAreWrittenAndRead_Then_TheyComeBackAndTheFolderIsMade()
    {
        var path = Path.Combine(_folder, "deep", SettingsFile.FileName);
        var settings = PlayerSettings.Resolve(null);
        settings.Set(SettingId.Quality, (int)QualityLevel.Low);
        settings.Set(SettingId.AmbientVolume, 35);
        settings.Set(SettingId.TextSize, 1);

        SettingsFile.Write(path, settings.ToSave());
        var back = PlayerSettings.Resolve(SettingsFile.Read(path));

        Assert.Equal(QualityLevel.Low, back.Quality);
        Assert.Equal(35, back.Get(SettingId.AmbientVolume));
        Assert.Equal(125, back.TextPercent);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Given_NoFileOrAForeignOne_When_ItIsRead_Then_ThereAreNoSettings()
    {
        Directory.CreateDirectory(_folder);
        var missing = Path.Combine(_folder, "missing.json");
        var broken = Path.Combine(_folder, "broken.json");
        var foreign = Path.Combine(_folder, "foreign.json");
        var newer = Path.Combine(_folder, "newer.json");
        File.WriteAllText(broken, "{ not json");
        File.WriteAllText(foreign, "{\"format\":\"vocaterra-save\",\"version\":1,\"settings\":{\"sound\":{}}}");
        File.WriteAllText(newer, SettingsFile.Serialize(new SaveSettings(SoundVolumes.Default)).Replace("\"version\":1", "\"version\":9", StringComparison.Ordinal));

        Assert.Null(SettingsFile.Read(missing));
        Assert.Null(SettingsFile.Read(broken));
        Assert.Null(SettingsFile.Read(foreign));
        Assert.Null(SettingsFile.Read(newer));
        Assert.Equal(60, PlayerSettings.Resolve(SettingsFile.Read(broken)).Get(SettingId.AmbientVolume)); // the defaults apply
    }

    [Fact]
    public void VolumesAreClampedWhenWrittenAndWhenRead()
    {
        var read = SettingsFile.Parse(SettingsFile.Serialize(new SaveSettings(new SoundVolumes(7, -1, double.NaN, 0.5))));

        Assert.Equal(new SoundVolumes(1, 0, 0, 0.5), read!.Sound);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void TheTextScaleSurvivesAWriteAndARead(int percent)
    {
        var settings = new SaveSettings(new SoundVolumes(0.9, 0.25, 0.5, 0.0), percent);

        var text = SettingsFile.Serialize(settings);

        Assert.Equal(percent, SettingsFile.Parse(text)!.TextScale);
        Assert.Contains($"\"textScale\":{percent}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextScaleOffTheStepsIsSnappedToTheNearest()
    {
        Assert.Equal(125, SettingsFile.Parse(SettingsFile.Serialize(new SaveSettings(SoundVolumes.Default, 130)))!.TextScale);
        Assert.Equal(150, SettingsFile.Parse(SettingsFile.Serialize(new SaveSettings(SoundVolumes.Default, 400)))!.TextScale);
        Assert.Equal(100, SettingsFile.Parse(SettingsFile.Serialize(new SaveSettings(SoundVolumes.Default, -20)))!.TextScale);
        Assert.Equal([100, 125, 150], SaveSettings.TextScaleLevels);
    }

    [Fact]
    public void ASaveCarriesNoSettingsAndTheSameWorldSavesTheSameBytes()
    {
        var session = TestGame.NewSession();

        var text = SaveGame.Save(session);

        Assert.DoesNotContain("\"settings\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("textScale", text, StringComparison.Ordinal);
        Assert.Equal(text, SaveGame.Save(session));
        Assert.Equal(12, SaveGame.Parse(text).Version);
    }

    [Fact]
    public void Given_ASaveOfVersion11WithSettings_When_ItIsRead_Then_ItIsRefusedAsOlder()
    {
        var text = SaveGame.Save(TestGame.NewSession()).Replace("\"version\":12,", "\"version\":11,", StringComparison.Ordinal);

        var error = Assert.Throws<SaveException>(() => SaveGame.Parse(text));

        Assert.Contains("older saves are not migrated", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ChangedSettings_When_ThePersistencePumpsAfterItsDelay_Then_TheFileIsWrittenOnceAndFlushWritesAtOnce()
    {
        var path = Path.Combine(_folder, SettingsFile.FileName);
        var settings = PlayerSettings.Resolve(null);
        long now = 1000;
        var persistence = new SettingsPersistence(path, settings, () => now, static _ => { });

        persistence.Pump();
        Assert.False(File.Exists(path)); // nothing changed, nothing written

        settings.Set(SettingId.UiVolume, 10);
        persistence.Pump();
        Assert.False(File.Exists(path)); // the change has not rested

        now += SettingsPersistence.DelayMilliseconds;
        persistence.Pump();
        Assert.Equal(10, PlayerSettings.Resolve(SettingsFile.Read(path)).Get(SettingId.UiVolume));
        Assert.False(persistence.Dirty);

        settings.Set(SettingId.UiVolume, 20);
        persistence.Flush();
        Assert.Equal(20, PlayerSettings.Resolve(SettingsFile.Read(path)).Get(SettingId.UiVolume));

        settings.Set(SettingId.UiVolume, 30);
        persistence.Close(); // a waiting change is written when the owner is done
        Assert.Equal(30, PlayerSettings.Resolve(SettingsFile.Read(path)).Get(SettingId.UiVolume));
        settings.Set(SettingId.UiVolume, 40);
        persistence.Flush();
        Assert.Equal(30, PlayerSettings.Resolve(SettingsFile.Read(path)).Get(SettingId.UiVolume)); // closed: it no longer follows the settings
    }

    [Fact]
    public void Given_AFolderThatCannotBeWritten_When_TheSettingsAreFlushed_Then_TheFailureIsSaidAndTheChangeStaysWaiting()
    {
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "file");
        File.WriteAllText(blocker, "x");
        var lines = new List<string>();
        var settings = PlayerSettings.Resolve(null);
        var persistence = new SettingsPersistence(Path.Combine(blocker, SettingsFile.FileName), settings, () => 0, lines.Add);

        settings.Set(SettingId.UiVolume, 10);
        persistence.Flush();

        Assert.True(persistence.Dirty);
        Assert.Single(lines);
        Assert.StartsWith("settings: not written", lines[0], StringComparison.Ordinal);
    }
}
