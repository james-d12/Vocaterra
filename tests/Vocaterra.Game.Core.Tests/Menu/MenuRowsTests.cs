using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.Core.Tests.Menu;

/// <summary>The settings page's rows: the sound rows in their order, the weather's slider right after the ambient one (E09).</summary>
public sealed class MenuRowsTests
{
    [Fact]
    public void Given_TheSoundRows_When_Listed_Then_WeatherFollowsAmbientAndComesBeforeTheVoices()
    {
        var rows = MenuRows.Settings(new PlayerSettings(), null);
        var ids = rows.Select(static row => row.Id).ToList();

        var ambient = ids.IndexOf(SettingId.AmbientVolume.ToString());
        Assert.True(ambient >= 0);
        Assert.Equal(SettingId.WeatherVolume.ToString(), ids[ambient + 1]);
        Assert.Equal(SettingId.VoiceVolume.ToString(), ids[ambient + 2]);
        Assert.Equal(1, ids.Count(static id => id == SettingId.WeatherVolume.ToString()));
    }
}
