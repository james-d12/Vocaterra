using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Menu;

/// <summary>The testing section's rows over their hooks: grouped under subheadings, each row a thin wrapper, and a row left out when its hook is not given.</summary>
public sealed class TestingRowsTests
{
    private static readonly string[] Points = ["bridge", "camp.guide", "camp.player", "stall"];

    private static readonly string[] Kinds = ["first", "second", "third"];

    private sealed class State
    {
        public Dictionary<NeedMeter, int> Needs { get; } = new() { [NeedMeter.Hunger] = 100, [NeedMeter.Thirst] = 80, [NeedMeter.Sleep] = 60, [NeedMeter.Health] = 100, [NeedMeter.Stamina] = 100 };

        public double? Hour { get; set; }

        public string? Weather { get; set; }

        public bool Greyscale { get; set; }

        public bool Assume { get; set; }

        public bool Tips { get; set; } = true;

        public string? Beacon { get; set; }

        public List<string> Teleports { get; } = [];

        public bool FullCredit { get; set; }

        public bool Autosave { get; set; } = true;

        public int Saves { get; set; }
    }

    private static TestingHooks Hooks(State state, bool everything = true) => new()
    {
        NeedsRunning = () => true,
        SetNeedsRunning = _ => { },
        RefillNeeds = () => { },
        SoundsOff = () => false,
        SetSoundsOff = _ => { },
        VoicesMuted = () => false,
        SetVoicesMuted = _ => { },
        ShoulderView = () => false,
        SetShoulderView = _ => { },
        SaveNow = () => state.Saves++,
        NeedValue = everything ? meter => state.Needs[meter] : null,
        SetNeed = everything ? (meter, value) => state.Needs[meter] = value : null,
        TimeHeld = everything ? () => state.Hour : null,
        SetTimeHeld = everything ? hour => state.Hour = hour : null,
        WeatherHeld = everything ? () => state.Weather : null,
        SetWeatherHeld = everything ? id => state.Weather = id : null,
        WeatherIds = everything ? Kinds : null,
        Greyscale = everything ? () => state.Greyscale : null,
        SetGreyscale = everything ? on => state.Greyscale = on : null,
        AssumeAllKnown = everything ? () => state.Assume : null,
        SetAssumeAllKnown = everything ? on => state.Assume = on : null,
        TipsShown = everything ? () => state.Tips : null,
        SetTipsShown = everything ? on => state.Tips = on : null,
        Places = everything ? Points : null,
        BeaconPlace = everything ? () => state.Beacon : null,
        SetBeacon = everything ? place => state.Beacon = place : null,
        TeleportTo = everything ? state.Teleports.Add : null,
        FullTypedCredit = everything ? () => state.FullCredit : null,
        SetFullTypedCredit = everything ? on => state.FullCredit = on : null,
        AutosaveOn = everything ? () => state.Autosave : null,
        SetAutosaveOn = everything ? on => state.Autosave = on : null,
    };

    private static IReadOnlyList<MenuRow> Rows(State state, bool everything = true) => MenuRows.Settings(new PlayerSettings(), Hooks(state, everything));

    private static MenuRow Row(IReadOnlyList<MenuRow> rows, string id) => rows.First(r => r.Id == id);

    [Fact]
    public void Given_EveryHook_When_TheRowsAreBuilt_Then_TheTestingSectionIsGroupedUnderSubheadingsInOrder()
    {
        var rows = Rows(new State());
        var headings = rows.SkipWhile(static r => r.LabelKey != "headingTesting").Where(static r => r.Kind == MenuRowKind.Subheading).Select(static r => r.LabelKey).ToList(); // the testing section's own (the Graphics group has its "Shaders and effects", B17b)
        Assert.Equal(["subNeeds", "subView", "subTeleport", "subSound", "subScoring", "subFiles"], headings);
        Assert.Equal(["subEffects"], rows.TakeWhile(static r => r.LabelKey != "headingTesting").Where(static r => r.Kind == MenuRowKind.Subheading).Select(static r => r.LabelKey));
        Assert.True(rows.ToList().FindIndex(static r => r.LabelKey == "headingTesting") > rows.ToList().FindIndex(static r => r.LabelKey == "headingInterface"));
        Assert.All(rows.Where(static r => r.Kind == MenuRowKind.Subheading), static r => Assert.False(r.Selectable));
        Assert.Equal(rows.Count, rows.Select(static r => r.Id).Distinct().Count()); // every row has a name of its own
        Assert.Equal("restartNote3", rows[^1].LabelKey); // the restart note stays last
    }

    [Fact]
    public void Given_OnlyTheRequiredHooks_When_TheRowsAreBuilt_Then_TheOptionalRowsAndTheirSubheadingsAreLeftOut()
    {
        var rows = Rows(new State(), everything: false);
        var ids = rows.Select(static r => r.Id).ToList();
        Assert.DoesNotContain("timeOfDay", ids);
        Assert.DoesNotContain("weather", ids);
        Assert.DoesNotContain("beacon", ids);
        Assert.DoesNotContain("teleportGo", ids);
        Assert.DoesNotContain("typedCredit", ids);
        Assert.DoesNotContain("autosave", ids);
        Assert.DoesNotContain(ids, static id => id.StartsWith("need.", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, static r => r.LabelKey is "subTeleport" or "subScoring");
        Assert.Contains("saveNow", ids);
        Assert.Contains("refillNeeds", ids);
    }

    [Fact]
    public void Given_TheNeedSliders_When_Moved_Then_TheHookGetsTheMeterAndAValueOnTheStep()
    {
        var state = new State();
        var rows = Rows(state);
        var thirst = Row(rows, "need.Thirst");
        Assert.Equal(MenuRowKind.Slider, thirst.Kind);
        Assert.Equal(80, thirst.Value);
        thirst.Change(-1);
        Assert.Equal(75, state.Needs[NeedMeter.Thirst]);
        thirst.SetFraction(0.123);
        Assert.Equal(10, state.Needs[NeedMeter.Thirst]); // 12.3 snaps to the step of 5
        thirst.SetFraction(5);
        Assert.Equal(100, state.Needs[NeedMeter.Thirst]);
        thirst.Change(1);
        Assert.Equal(100, state.Needs[NeedMeter.Thirst]); // stops at the end
        Row(rows, "need.Stamina").SetFraction(0);
        Assert.Equal(0, state.Needs[NeedMeter.Stamina]);
        Assert.Equal(5, rows.Count(static r => r.Id.StartsWith("need.", StringComparison.Ordinal)));
        Assert.Equal("60", Row(rows, "need.Sleep").ValueText(static key => key));
    }

    [Fact]
    public void Given_TheViewSwitches_When_Flipped_Then_EachHookRunsAndNothingElseMoves()
    {
        var state = new State();
        var rows = Rows(state);
        Row(rows, "greyscale").Activate();
        Row(rows, "assumeAllKnown").Activate();
        Row(rows, "tips").Activate();
        Assert.True(state.Greyscale);
        Assert.True(state.Assume);
        Assert.False(state.Tips);
        Assert.Equal("settingsOn", Row(rows, "greyscale").ValueText(static key => key));
        Assert.Null(state.Hour); // no other row moves the clock or the weather
        Assert.Null(state.Weather);
    }

    [Fact]
    public void Given_TheTimeOfDaySlider_When_Moved_Then_RunningIsTheLowestPositionAndEachHourHoldsTheClock()
    {
        var state = new State();
        var rows = Rows(state);
        var time = Row(rows, "timeOfDay");
        Assert.Equal(MenuRowKind.Slider, time.Kind);
        Assert.Equal("testTimeOfDay", time.LabelKey);
        Assert.Equal("testRunning", time.ValueText(static key => key)); // the clock runs: the first position
        Assert.Null(state.Hour);

        time.Change(1);
        Assert.Equal(0, state.Hour); // the hours 0 to 23 follow Running
        Assert.Equal("0:00", time.ValueText(static key => key));
        time.Change(-1);
        Assert.Null(state.Hour); // back to Running
        time.Change(-1);
        Assert.Null(state.Hour); // Running is the end of the track

        time.SetFraction(1);
        Assert.Equal(23, state.Hour);
        Assert.Equal("23:00", time.ValueText(static key => key));
    }

    [Fact]
    public void Given_AClockHeldElsewhere_When_TheRowIsShown_Then_ItShowsTheHeldHour()
    {
        var state = new State { Hour = 21 };
        var time = Row(Rows(state), "timeOfDay");
        Assert.Equal(21, time.Value);
        Assert.Equal("21:00", time.ValueText(static key => key));
        state.Hour = 7.5; // a fraction shows its whole hour
        Assert.Equal("7:00", time.ValueText(static key => key));
    }

    [Fact]
    public void Given_TheWeatherPicker_When_Cycled_Then_RunningIsFirstThenEachLoadedKindByItsId()
    {
        var state = new State();
        var rows = Rows(state);
        var weather = Row(rows, "weather");
        Assert.Equal("testWeather", weather.LabelKey);
        Assert.Equal("testRunning", weather.ValueText(static key => key));
        weather.Change(1);
        Assert.Equal("first", state.Weather);
        Assert.Equal("first", weather.ValueText(static key => key));
        weather.Change(1);
        weather.Change(1);
        Assert.Equal("third", state.Weather);
        weather.Change(1);
        Assert.Null(state.Weather); // Running, then the cycle goes on from the first
        weather.Change(-1);
        Assert.Equal("third", state.Weather);
    }

    [Fact]
    public void Given_AWeatherHeldElsewhere_When_TheRowIsShown_Then_ItShowsTheKind()
    {
        var state = new State { Weather = "second" };
        var weather = Row(Rows(state), "weather");
        Assert.Equal(2, weather.Value);
        Assert.Equal("second", weather.ValueText(static key => key));
    }

    [Fact]
    public void Given_NoWeatherKinds_When_TheRowsAreBuilt_Then_ThereIsNoWeatherRow()
    {
        var rows = MenuRows.Settings(new PlayerSettings(), Hooks(new State()) with { WeatherIds = [] });
        Assert.DoesNotContain(rows, static r => r.Id == "weather");
        Assert.Contains(rows, static r => r.Id == "timeOfDay");
    }

    [Fact]
    public void Given_TheBeaconRow_When_Cycled_Then_ItIsOffThenEachPointAndFollowsTheWorld()
    {
        var state = new State();
        var rows = Rows(state);
        var beacon = Row(rows, "beacon");
        Assert.Equal("settingsOff", beacon.ValueText(static key => key));
        beacon.Change(1);
        Assert.Equal("bridge", state.Beacon);
        Assert.Equal("bridge", beacon.ValueText(static key => key));
        beacon.Change(1);
        Assert.Equal("camp.guide", state.Beacon);
        beacon.Change(-1);
        beacon.Change(-1);
        Assert.Null(state.Beacon); // back to off
        beacon.Change(-1);
        Assert.Equal("stall", state.Beacon); // wraps to the last point

        state.Beacon = "elsewhere"; // the world's beacon is not one of the named points (or was reached and cleared)
        Assert.Equal(0, beacon.Value);
        state.Beacon = null;
        Assert.Equal(0, beacon.Value);
        state.Beacon = "camp.player";
        Assert.Equal(3, beacon.Value);
    }

    [Fact]
    public void Given_TheTeleportRows_When_ThePlaceIsChosenAndGoIsPressed_Then_OnlyGoMovesThePlayer()
    {
        var state = new State();
        var rows = Rows(state);
        var place = Row(rows, "teleportPlace");
        Assert.Equal("bridge", place.ValueText(static key => key));
        place.Change(1);
        place.Change(1);
        Assert.Empty(state.Teleports); // choosing a place goes nowhere
        Assert.Equal("camp.player", place.ValueText(static key => key));
        Row(rows, "teleportGo").Activate();
        Assert.Equal(["camp.player"], state.Teleports);
        place.Change(-1);
        place.Change(-1);
        place.Change(-1); // wraps round the other way
        Assert.Equal("stall", place.ValueText(static key => key));
        Row(rows, "teleportGo").Activate();
        Assert.Equal(["camp.player", "stall"], state.Teleports);
    }

    [Fact]
    public void Given_NoNamedPoints_When_TheRowsAreBuilt_Then_ThereIsNoBeaconOrTeleportRow()
    {
        var state = new State();
        var hooks = Hooks(state) with { Places = [] };
        var rows = MenuRows.Settings(new PlayerSettings(), hooks);
        Assert.DoesNotContain(rows, static r => r.Id is "beacon" or "teleportGo" or "teleportPlace");
        Assert.DoesNotContain(rows, static r => r.LabelKey == "subTeleport");
    }

    [Fact]
    public void Given_TheCreditAndAutosaveRows_When_Flipped_Then_TheHooksAndTheirWordsFollow()
    {
        var state = new State();
        var rows = Rows(state);
        var credit = Row(rows, "typedCredit");
        Assert.Equal("testCreditHalf", credit.ValueText(static key => key));
        credit.Activate();
        Assert.True(state.FullCredit);
        Assert.Equal("testCreditFull", credit.ValueText(static key => key));
        var autosave = Row(rows, "autosave");
        Assert.Equal("settingsOn", autosave.ValueText(static key => key));
        autosave.Activate();
        Assert.False(state.Autosave);
        Row(rows, "saveNow").Activate();
        Assert.Equal(1, state.Saves);
    }

    [Fact]
    public void Given_TheLongSettingsPage_When_TheCursorWalksIt_Then_ItNeverStopsOnAHeadingAndAlwaysKeepsTheRowOnTheScreen()
    {
        var rows = Rows(new State());
        var menu = new MenuScreen(rows, new FixedMetrics(9), () => { });
        menu.OpenSettings();
        for (var step = 0; step < rows.Count * 2; step++)
        {
            menu.HandleInput(TestInputFrame.Pressing(GameKey.Down));
            Assert.True(rows[menu.SettingsSelected].Selectable);
            Assert.InRange(menu.SettingsSelected - menu.SettingsScroll, 0, 8);
        }

        menu.ScrollTo(1000);
        Assert.Equal(rows.Count - 9, menu.SettingsScroll);
    }

    [Fact]
    public void Given_EveryRow_When_ItsWordsAreListed_Then_TheMenusLocaleKeysHoldThemAll()
    {
        var keys = MenuText.UiKeys.ToHashSet(StringComparer.Ordinal);
        var rows = Rows(new State());
        foreach (var row in rows.Where(static r => r.Id != "beacon" && r.Id != "teleportPlace"))
        {
            Assert.Contains(row.LabelKey, keys);
            foreach (var choice in row.ChoiceKeys)
            {
                Assert.Contains(choice, keys);
            }
        }

        Assert.Contains("settingsOff", keys); // the beacon's "off"
    }

    private sealed class FixedMetrics(int capacity) : IMenuMetrics
    {
        public int SettingsCapacity => capacity;

        public int ControlsMaxScroll => 0;

        public MenuHit? HitAt(MenuPage page, int settingsScroll, int x, int y) => null;

        public double TrackFraction(int settingsScroll, int row, int x) => 0;
    }
}
