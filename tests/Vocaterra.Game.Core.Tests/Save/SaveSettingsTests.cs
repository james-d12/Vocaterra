using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The save's settings (S24): the sound volumes are kept in the save, a save without settings still loads, and nothing about them reaches the world.</summary>
public sealed class SaveSettingsTests
{
    private static string SaveOf(VoxelSession session, SaveSettings? settings) => SaveGame.Serialize(SaveGame.Capture(session, settings));

    [Fact]
    public void TheSoundVolumesSurviveASaveAndAParse()
    {
        var session = TestGame.NewSession();
        var settings = new SaveSettings(new SoundVolumes(0.9, 0.25, 0.5, 0.0));

        var text = SaveOf(session, settings);
        var document = SaveGame.Parse(text);

        Assert.Equal(settings, document.Settings);
        Assert.Equal(text, SaveGame.Serialize(document)); // canonical: the same document gives the same bytes
    }

    [Fact]
    public void Given_TheWeatherVolume_When_Saved_Then_ItSurvivesAParseAndAnOlderSaveWithoutItTakesTheDefault()
    {
        var session = TestGame.NewSession();
        var settings = new SaveSettings(new SoundVolumes(0.9, 0.25, 0.5, 0.0, 0.3));

        var text = SaveOf(session, settings);
        Assert.Contains("\"weather\":0.3", text, StringComparison.Ordinal); // kept under settings.sound (docs/game/saves.md)
        Assert.Equal(0.3, SaveGame.Parse(text).Settings!.Sound.Weather, 6);

        var older = System.Text.RegularExpressions.Regex.Replace(text, ",\"weather\":[^,}]*", string.Empty);
        Assert.NotEqual(text, older); // the field is gone, as in a save from before E09
        Assert.Equal(SoundVolumes.Default.Weather, SaveGame.Parse(older).Settings!.Sound.Weather, 6);
        Assert.Equal(0.0, SaveGame.Parse(older).Settings!.Sound.Ambient, 6); // the rest of the sound block is read as it was
    }

    [Fact]
    public void ASaveWithoutSettingsLoadsAndTakesTheDefaults()
    {
        var session = TestGame.NewSession();
        var text = SaveOf(session, null);
        var old = text.Replace(",\"settings\":null", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(text, old); // the field was there and is now gone, as in a save from before S24

        var document = SaveGame.Parse(old);

        Assert.Null(document.Settings);
        Assert.Equal(SoundVolumes.Default, document.Settings?.Sound ?? SoundVolumes.Default);
        var loaded = SaveGame.Load(old, TestGame.Data, static seed => TestGame.Route(seed), credit: CreditRules.FullTyped);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void VolumesAreClampedWhenSavedAndWhenRead()
    {
        var session = TestGame.NewSession();

        var document = SaveGame.Parse(SaveOf(session, new SaveSettings(new SoundVolumes(7, -1, double.NaN, 0.5))));

        Assert.Equal(new SoundVolumes(1, 0, 0, 0.5), document.Settings!.Sound);
        Assert.Equal(new SoundVolumes(1, 0, 0, 0.5), (new SoundVolumes(7, -3, double.NaN, 0.5)).Clamped());
    }

    [Fact]
    public void TheSettingsAreNotPartOfTheWorldsStateOrTheSavedGame()
    {
        var session = TestGame.NewSession();

        var quiet = SaveGame.Parse(SaveOf(session, new SaveSettings(new SoundVolumes(0, 0, 0, 0))));
        var loud = SaveGame.Parse(SaveOf(session, new SaveSettings(new SoundVolumes(1, 1, 1, 1))));

        Assert.Equal(SaveGame.Serialize(quiet with { Settings = null }), SaveGame.Serialize(loud with { Settings = null })); // only the settings differ
        Assert.Equal(quiet.StateDigest, loud.StateDigest);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void TheTextScaleSurvivesASaveAndAParseAndIsPartOfTheCanonicalBytes(int percent)
    {
        var session = TestGame.NewSession();
        var settings = new SaveSettings(new SoundVolumes(0.9, 0.25, 0.5, 0.0), percent);

        var text = SaveOf(session, settings);
        var document = SaveGame.Parse(text);

        Assert.Equal(percent, document.Settings!.TextScale);
        Assert.Equal(settings, document.Settings);
        Assert.Equal(text, SaveGame.Serialize(document));
        Assert.Contains($"\"textScale\":{percent}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveFromBeforeTheTextScaleLoadsAtOneHundredPercentAndAValueOffTheStepsIsSnappedToTheNearest()
    {
        var session = TestGame.NewSession();
        var text = SaveOf(session, new SaveSettings(SoundVolumes.Default, 125));
        var old = text.Replace(",\"textScale\":125", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(text, old); // the field was there and is now gone, as in a save from the time of S24

        var document = SaveGame.Parse(old);

        Assert.Equal(100, document.Settings!.TextScale);
        Assert.Equal(125, SaveGame.Parse(SaveOf(session, new SaveSettings(SoundVolumes.Default, 130))).Settings!.TextScale);
        Assert.Equal(150, SaveGame.Parse(SaveOf(session, new SaveSettings(SoundVolumes.Default, 400))).Settings!.TextScale);
        Assert.Equal(100, SaveGame.Parse(SaveOf(session, new SaveSettings(SoundVolumes.Default, -20))).Settings!.TextScale);
        Assert.Equal([100, 125, 150], SaveSettings.TextScaleLevels);
    }

    [Fact]
    public void TheTextScaleIsNotPartOfTheWorldsState()
    {
        var session = TestGame.NewSession();

        var small = SaveGame.Parse(SaveOf(session, new SaveSettings(SoundVolumes.Default, 100)));
        var big = SaveGame.Parse(SaveOf(session, new SaveSettings(SoundVolumes.Default, 150)));

        Assert.Equal(SaveGame.Serialize(small with { Settings = null }), SaveGame.Serialize(big with { Settings = null }));
        Assert.Equal(small.StateDigest, big.StateDigest);
    }
}
