using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.UI.Menu;

namespace Vocaterra.Game.Tests.UI.Menu;

/// <summary>The pointer's answers come from the same layout the menu is drawn with, so a click lands on what is drawn.</summary>
public sealed class MenuMetricsTests
{
    private static (MenuMetrics Metrics, IReadOnlyList<MenuRow> Rows, PlayerSettings Settings) Build(int width, int height)
    {
        var settings = new PlayerSettings();
        var rows = MenuRows.Settings(settings, null);
        return (new MenuMetrics(() => (width, height), () => rows, static _ => Enumerable.Range(0, 80).Select(static i => new ControlsPageLine(false, "K", "line " + i)).ToList()), rows, settings);
    }

    [Fact]
    public void Given_TheMainPage_When_AButtonIsPointedAt_Then_ItsIndexComesBack()
    {
        var (metrics, _, _) = Build(1280, 720);
        var layout = new MenuLayout(1280, 720, MenuPage.Main);
        for (var i = 0; i < 4; i++)
        {
            var button = layout.Button(i);
            Assert.Equal(i, metrics.HitAt(MenuPage.Main, 0, button.X + 5, button.Y + 5)?.Index);
        }

        Assert.Null(metrics.HitAt(MenuPage.Main, 0, 2, 2));
    }

    [Fact]
    public void Given_TheSettingsPage_When_ARowIsPointedAt_Then_TheScrolledRowComesBackAndHeadingsAreNotHit()
    {
        var (metrics, rows, _) = Build(1280, 720);
        var layout = new MenuLayout(1280, 720, MenuPage.Settings);
        var first = layout.Row(1); // the first slider, under the Sound heading
        var hit = metrics.HitAt(MenuPage.Settings, 0, first.X + 20, first.Y + 5);
        Assert.Equal(1, hit?.Index);
        Assert.False(hit?.OnTrack);
        Assert.Null(metrics.HitAt(MenuPage.Settings, 0, first.X + 20, layout.Row(0).Y + 5)); // the heading
        var scrolled = metrics.HitAt(MenuPage.Settings, 3, first.X + 20, first.Y + 5);
        Assert.Equal(4, scrolled?.Index);
        Assert.Equal(MenuRowKind.Slider, rows[1].Kind);
    }

    [Fact]
    public void Given_ASliderRow_When_ThePointerIsOnTheTrack_Then_TheHitIsOnTheTrackAndTheFractionFollowsX()
    {
        var (metrics, _, _) = Build(1280, 720);
        var layout = new MenuLayout(1280, 720, MenuPage.Settings);
        var row = layout.Row(1);
        var track = MenuLayout.Track(row);
        Assert.True(metrics.HitAt(MenuPage.Settings, 0, track.X + 10, row.Y + 5)?.OnTrack);
        Assert.Equal(0, metrics.TrackFraction(0, 1, track.X - 50));
        Assert.Equal(1, metrics.TrackFraction(0, 1, track.Right + 50));
        Assert.Equal(0.5, metrics.TrackFraction(0, 1, track.X + (track.Width / 2)), 2);
    }

    [Fact]
    public void Given_BackAndTheControlsPage_When_Used_Then_BackIsHitAndTheScrollEndsWhereTheLinesEnd()
    {
        var (metrics, _, _) = Build(800, 600);
        var layout = new MenuLayout(800, 600, MenuPage.Controls);
        Assert.True(metrics.HitAt(MenuPage.Controls, 0, layout.Back.X + 3, layout.Back.Y + 3)?.IsBack);
        Assert.Equal(80 - layout.LineCapacity, metrics.ControlsMaxScroll);
    }
}
