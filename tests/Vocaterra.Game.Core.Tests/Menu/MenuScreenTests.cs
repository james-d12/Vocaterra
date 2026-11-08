using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Menu;

/// <summary>The menu's state machine: its pages, the keys, the mouse, Resume, Exit and the rows' effects.</summary>
public sealed class MenuScreenTests
{
    private sealed class Metrics : IMenuMetrics
    {
        public int SettingsCapacity { get; set; } = 8;

        public int ControlsMaxScroll { get; set; } = 20;

        public Func<MenuPage, int, int, int, MenuHit?> Hit { get; set; } = static (_, _, _, _) => null;

        public MenuHit? HitAt(MenuPage page, int settingsScroll, int x, int y) => Hit(page, settingsScroll, x, y);

        public double TrackFraction(int settingsScroll, int row, int x) => x / 100.0;
    }

    private sealed record Rig(MenuScreen Menu, PlayerSettings Settings, Metrics Metrics, TestingState Testing, Func<int> Exits);

    private sealed class TestingState
    {
        public bool Needs { get; set; } = true;

        public bool Sounds { get; set; }

        public int Refills { get; set; }
    }

    private static Rig Build(bool withTesting = true)
    {
        var settings = new PlayerSettings();
        var testing = new TestingState();
        var exits = 0;
        var hooks = withTesting
            ? new TestingHooks(null, null, () => testing.Needs, on => testing.Needs = on, () => testing.Refills++, () => testing.Sounds, on => testing.Sounds = on, () => false, _ => { }, () => false, _ => { }, () => { })
            : null;
        var metrics = new Metrics();
        return new Rig(new MenuScreen(MenuRows.Settings(settings, hooks), metrics, () => exits++), settings, metrics, testing, () => exits);
    }

    private static TestInputFrame Press(params GameKey[] keys) => TestInputFrame.Pressing(keys);

    [Fact]
    public void Given_TheMenu_When_Opened_Then_ItIsOnResumeAndFreesTheMouse()
    {
        var rig = Build();
        Assert.False(rig.Menu.IsOpen);
        rig.Menu.Open();
        Assert.True(rig.Menu.IsOpen);
        Assert.True(rig.Menu.FreesMouse);
        Assert.Equal(MenuPage.Main, rig.Menu.Page);
        Assert.Equal(MenuItem.Resume, rig.Menu.MainSelected);
    }

    [Fact]
    public void Given_TheMainPage_When_EscOrResumeIsPressed_Then_TheMenuCloses()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Escape));
        Assert.False(rig.Menu.IsOpen);

        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Enter));
        Assert.False(rig.Menu.IsOpen);
        Assert.Equal(0, rig.Exits());
    }

    [Fact]
    public void Given_TheMainPage_When_DownAndEnterAreUsed_Then_SettingsOpenAndEscGoesBackOnePage()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Down));
        Assert.Equal(MenuItem.Settings, rig.Menu.MainSelected);
        rig.Menu.HandleInput(Press(GameKey.Enter));
        Assert.Equal(MenuPage.Settings, rig.Menu.Page);
        rig.Menu.HandleInput(Press(GameKey.Escape));
        Assert.Equal(MenuPage.Main, rig.Menu.Page);
        Assert.True(rig.Menu.IsOpen);
    }

    [Fact]
    public void Given_TheMainPage_When_UpIsPressedOnResume_Then_ItWrapsToExit()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Up));
        Assert.Equal(MenuItem.Exit, rig.Menu.MainSelected);
    }

    [Fact]
    public void Given_ExitIsChosen_When_Entered_Then_TheHostIsAskedToSaveAndQuitOnce()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Up));
        rig.Menu.HandleInput(Press(GameKey.Enter));
        Assert.Equal(1, rig.Exits());
    }

    [Fact]
    public void Given_TheControlsPage_When_ScrolledPastItsEnds_Then_TheScrollStaysInside()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Down));
        rig.Menu.HandleInput(Press(GameKey.Down));
        rig.Menu.HandleInput(Press(GameKey.Enter));
        Assert.Equal(MenuPage.Controls, rig.Menu.Page);
        for (var i = 0; i < 10; i++)
        {
            rig.Menu.HandleInput(Press(GameKey.PageDown));
        }

        Assert.Equal(20, rig.Menu.ControlsScroll);
        rig.Menu.HandleInput(new TestInputFrame { Wheel = 120 * 40 });
        Assert.Equal(0, rig.Menu.ControlsScroll);
    }

    [Fact]
    public void Given_F1_When_PressedInTheMenu_Then_ControlsOpenAndF1AgainClosesTheMenu()
    {
        var rig = Build();
        rig.Menu.OpenControls();
        Assert.Equal(MenuPage.Controls, rig.Menu.Page);
        rig.Menu.HandleInput(Press(GameKey.Escape)); // opened directly: Esc closes the menu, not back to the main page
        Assert.False(rig.Menu.IsOpen);

        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.F1));
        Assert.Equal(MenuPage.Controls, rig.Menu.Page);
        rig.Menu.HandleInput(Press(GameKey.F1));
        Assert.False(rig.Menu.IsOpen);
    }

    [Fact]
    public void Given_TheSettingsPage_When_LeftAndRightAreUsed_Then_TheChosenRowChanges()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        Assert.Equal(SettingId.AmbientVolume.ToString(), rig.Menu.Rows[rig.Menu.SettingsSelected].Id);
        rig.Menu.HandleInput(Press(GameKey.Right));
        Assert.Equal(65, rig.Settings.Get(SettingId.AmbientVolume));
        rig.Menu.HandleInput(Press(GameKey.Left));
        rig.Menu.HandleInput(Press(GameKey.Left));
        Assert.Equal(55, rig.Settings.Get(SettingId.AmbientVolume));
        for (var i = 0; i < 30; i++)
        {
            rig.Menu.HandleInput(Press(GameKey.Left));
        }

        Assert.Equal(0, rig.Settings.Get(SettingId.AmbientVolume));
    }

    [Fact]
    public void Given_TheSettingsPage_When_DownIsPressed_Then_HeadingsAreSkippedAndTheCursorWraps()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        for (var i = 0; i < 4; i++)
        {
            rig.Menu.HandleInput(Press(GameKey.Down));
        }

        Assert.Equal(SettingId.Quality.ToString(), rig.Menu.Rows[rig.Menu.SettingsSelected].Id); // the Graphics heading is passed over
        for (var i = 0; i < 200; i++)
        {
            rig.Menu.HandleInput(Press(GameKey.Down));
            Assert.True(rig.Menu.Rows[rig.Menu.SettingsSelected].Selectable);
        }
    }

    [Fact]
    public void Given_AChoiceRow_When_EnterIsPressed_Then_ItCyclesAndWrapsRound()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        for (var i = 0; i < 4; i++)
        {
            rig.Menu.HandleInput(Press(GameKey.Down));
        }

        rig.Menu.HandleInput(Press(GameKey.Enter)); // High + 1 wraps to Low
        Assert.Equal(QualityLevel.Low, rig.Settings.Quality);
        rig.Menu.HandleInput(Press(GameKey.Left)); // and back down wraps to High
        Assert.Equal(QualityLevel.High, rig.Settings.Quality);
    }

    [Fact]
    public void Given_ASliderRow_When_DraggedWithTheMouse_Then_TheValueFollowsThePointerAndStopsOnRelease()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        rig.Metrics.Hit = (_, _, _, _) => new MenuHit(1, OnTrack: true);
        rig.Menu.HandleInput(new TestInputFrame { LeftDown = true, PointerX = 40 });
        Assert.Equal(40, rig.Settings.Get(SettingId.AmbientVolume));
        rig.Menu.HandleInput(new TestInputFrame { LeftDown = true, PreviousLeftDown = true, PointerX = 80 });
        Assert.Equal(80, rig.Settings.Get(SettingId.AmbientVolume));
        rig.Menu.HandleInput(new TestInputFrame { PointerX = 10 });
        Assert.Equal(80, rig.Settings.Get(SettingId.AmbientVolume));
    }

    [Fact]
    public void Given_TheMainPage_When_AButtonIsClicked_Then_ItIsChosen()
    {
        var rig = Build();
        rig.Menu.Open();
        rig.Metrics.Hit = (page, _, _, _) => page == MenuPage.Main ? new MenuHit((int)MenuItem.Exit) : null;
        rig.Menu.HandleInput(new TestInputFrame { LeftDown = true });
        Assert.Equal(1, rig.Exits());
    }

    [Fact]
    public void Given_TheBackButton_When_Clicked_Then_ThePageGoesBack()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        rig.Menu.HandleInput(Press(GameKey.Escape));
        Assert.False(rig.Menu.IsOpen);
        rig.Menu.Open();
        rig.Menu.HandleInput(Press(GameKey.Down));
        rig.Menu.HandleInput(Press(GameKey.Enter));
        rig.Metrics.Hit = (_, _, _, _) => MenuHit.Back;
        rig.Menu.HandleInput(new TestInputFrame { LeftDown = true });
        Assert.Equal(MenuPage.Main, rig.Menu.Page);
    }

    [Fact]
    public void Given_TheTestingSection_When_ItsRowsAreUsed_Then_TheHooksRunAndNothingIsSavedForThem()
    {
        var rig = Build();
        var ids = rig.Menu.Rows.Select(static r => r.Id).ToList();
        Assert.Contains("needsRunning", ids);
        var refill = rig.Menu.Rows.First(static r => r.Id == "refillNeeds");
        refill.Activate();
        Assert.Equal(1, rig.Testing.Refills);
        var needs = rig.Menu.Rows.First(static r => r.Id == "needsRunning");
        Assert.Equal(1, needs.Value);
        needs.Change(1);
        Assert.False(rig.Testing.Needs);
    }

    [Fact]
    public void Given_APlaytest_When_TheRowsAreBuilt_Then_NoTestingSectionIsOffered()
    {
        var rig = Build(withTesting: false);
        Assert.DoesNotContain(rig.Menu.Rows, static r => r.LabelKey == "headingTesting");
        Assert.DoesNotContain(rig.Menu.Rows, static r => r.Id == "refillNeeds");
        Assert.Contains(rig.Menu.Rows, static r => r.LabelKey == "headingSound");
    }

    [Fact]
    public void Given_EverySetting_When_TheRowsAreBuilt_Then_EachHasARowInTheSettingsPage()
    {
        var rig = Build();
        foreach (var id in Enum.GetValues<SettingId>())
        {
            Assert.Contains(rig.Menu.Rows, r => r.Id == id.ToString());
        }
    }

    [Fact]
    public void Given_ClosedMenu_When_Closed_Then_TheStateIsReset()
    {
        var rig = Build();
        rig.Menu.OpenSettings();
        rig.Menu.Close();
        Assert.False(rig.Menu.IsOpen);
        Assert.Null(rig.Menu.Hover);
    }
}
