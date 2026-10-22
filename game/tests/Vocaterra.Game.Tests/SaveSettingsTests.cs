namespace Vocaterra.Game.Tests;

/// <summary>The save's settings (S24): the sound volumes are kept in the save, a save without settings still loads, and nothing about them reaches the world.</summary>
public sealed class SaveSettingsTests
{
    private static string SaveOf(VoxelSession session, SaveSettings? settings) => SaveGame.Serialize(SaveGame.Capture(session, TestGame.Route(TestGame.Seed), settings));

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
    public void ASaveWithoutSettingsLoadsAndTakesTheDefaults()
    {
        var session = TestGame.NewSession();
        var text = SaveOf(session, null);
        var old = text.Replace(",\"settings\":null", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(text, old); // the field was there and is now gone, as in a save from before S24

        var document = SaveGame.Parse(old);

        Assert.Null(document.Settings);
        Assert.Equal(SoundVolumes.Default, document.Settings?.Sound ?? SoundVolumes.Default);
        var loaded = SaveGame.Load(old, TestGame.Bundle, static seed => TestGame.Route(seed), TestGame.Nouns, credit: CreditRules.FullTyped);
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
}
