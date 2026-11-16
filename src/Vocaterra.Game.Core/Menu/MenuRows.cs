using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The rows of the settings page, in order: what a player changes at runtime (sound, graphics, the interface, the mouse, the needs), a note on what needs a restart, and, apart under its
/// own heading, the testing section. The testing section is left out of a playtest (its tools would change what the playtest measures); in every other run it is shown, labelled, and
/// nothing in it is kept in the save.
/// </summary>
public static class MenuRows
{
    /// <summary>Builds the rows.</summary>
    /// <param name="settings">The settings the rows change.</param>
    /// <param name="testing">The testing section's controls, or <see langword="null"/> for a run that has none (a playtest).</param>
    public static IReadOnlyList<MenuRow> Settings(PlayerSettings settings, TestingHooks? testing)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var rows = new List<MenuRow>
        {
            MenuRow.Heading("headingSound"),
            MenuRow.Of(settings, SettingId.AmbientVolume),
            MenuRow.Of(settings, SettingId.WeatherVolume),
            MenuRow.Of(settings, SettingId.VoiceVolume),
            MenuRow.Of(settings, SettingId.SfxVolume),
            MenuRow.Of(settings, SettingId.UiVolume),
            MenuRow.Heading("headingGraphics"),
            MenuRow.Of(settings, SettingId.Quality),
            MenuRow.Of(settings, SettingId.ViewDistance),
            MenuRow.Of(settings, SettingId.Grass),
            MenuRow.Of(settings, SettingId.FieldOfView),
            MenuRow.Of(settings, SettingId.Blur),
            MenuRow.Of(settings, SettingId.ViewEffects),
            MenuRow.Heading("headingInterface"),
            MenuRow.Of(settings, SettingId.TextSize),
            MenuRow.Heading("headingMouse"),
            MenuRow.Of(settings, SettingId.MouseSensitivity),
            MenuRow.Of(settings, SettingId.InvertY),
            MenuRow.Heading("headingNeeds"),
            MenuRow.Of(settings, SettingId.NeedsRate),
            MenuRow.Of(settings, SettingId.RelaxedNeeds),
            MenuRow.Heading("headingCheats"),
            MenuRow.Of(settings, SettingId.PassCheat),
        };
        if (testing is not null)
        {
            rows.Add(MenuRow.Heading("headingTesting"));
            rows.Add(MenuRow.Note("testingNote"));
            AddTesting(rows, testing);
        }

        rows.Add(MenuRow.Heading("headingRestart"));
        rows.Add(MenuRow.Note("restartNote"));
        rows.Add(MenuRow.Note("restartNote2"));
        rows.Add(MenuRow.Note("restartNote3"));
        return rows;
    }

    private static void AddTesting(List<MenuRow> rows, TestingHooks testing)
    {
        // The needs.
        rows.Add(MenuRow.Subheading("subNeeds"));
        rows.Add(MenuRow.Switch("needsRunning", "testNeedsRun", testing.NeedsRunning, testing.SetNeedsRunning));
        if (testing.NeedValue is { } needValue && testing.SetNeed is { } setNeed)
        {
            (NeedMeter Meter, string Label)[] needs =
            [
                (NeedMeter.Hunger, "testNeedHunger"),
                (NeedMeter.Thirst, "testNeedThirst"),
                (NeedMeter.Sleep, "testNeedSleep"),
                (NeedMeter.Health, "testNeedHealth"),
                (NeedMeter.Stamina, "testNeedStamina"),
            ];
            foreach (var (meter, label) in needs)
            {
                rows.Add(MenuRow.Slider("need." + meter, label, 0, 100, 5, () => needValue(meter), value => setNeed(meter, value)));
            }
        }

        rows.Add(MenuRow.Button("refillNeeds", "testRefill", testing.RefillNeeds));

        // The world and the view.
        rows.Add(MenuRow.Subheading("subView"));
        if (testing.DebugOverlay is { } overlay && testing.SetDebugOverlay is { } setOverlay)
        {
            rows.Add(MenuRow.Switch("debugOverlay", "testDebug", overlay, setOverlay));
        }

        rows.Add(MenuRow.Switch("shoulderView", "testShoulder", testing.ShoulderView, testing.SetShoulderView));
        AddTimeAndWeather(rows, testing);
        AddSwitch(rows, "greyscale", "testGreyscale", testing.Greyscale, testing.SetGreyscale);
        AddSwitch(rows, "assumeAllKnown", "testAssumeKnown", testing.AssumeAllKnown, testing.SetAssumeAllKnown);
        AddSwitch(rows, "tips", "testTips", testing.TipsShown, testing.SetTipsShown);
        var places = testing.Places ?? [];
        if (places.Count > 0 && testing.BeaconPlace is { } beaconPlace && testing.SetBeacon is { } setBeacon)
        {
            rows.Add(MenuRow.Picker(
                "beacon",
                "testBeacon",
                places,
                "settingsOff",
                () => beaconPlace() is { } name ? Math.Max(0, IndexOf(places, name) + 1) : 0,
                value => setBeacon(value <= 0 ? null : places[value - 1])));
        }

        // The teleport: a place chosen here (not kept anywhere), then the button.
        if (places.Count > 0 && testing.TeleportTo is { } teleport)
        {
            var chosen = 0;
            rows.Add(MenuRow.Subheading("subTeleport"));
            rows.Add(MenuRow.Picker("teleportPlace", "testPlace", places, null, () => chosen, value => chosen = Math.Clamp(value, 0, places.Count - 1)));
            rows.Add(MenuRow.Button("teleportGo", "testGo", () => teleport(places[chosen])));
        }

        // The sound.
        rows.Add(MenuRow.Subheading("subSound"));
        rows.Add(MenuRow.Switch("soundsOff", "testSoundsOff", testing.SoundsOff, testing.SetSoundsOff));
        rows.Add(MenuRow.Switch("voicesMuted", "testVoicesMuted", testing.VoicesMuted, testing.SetVoicesMuted));

        // The scoring rule.
        if (testing.FullTypedCredit is { } credit && testing.SetFullTypedCredit is { } setCredit)
        {
            rows.Add(MenuRow.Subheading("subScoring"));
            rows.Add(MenuRow.Switch("typedCredit", "testCredit", credit, setCredit, "testCreditHalf", "testCreditFull"));
        }

        // The files.
        rows.Add(MenuRow.Subheading("subFiles"));
        AddSwitch(rows, "autosave", "testAutosave", testing.AutosaveOn, testing.SetAutosaveOn);
        rows.Add(MenuRow.Button("saveNow", "testSave", testing.SaveNow));
    }

    // The time of day: "Running", then the hours 0 to 23 (a held hour holds the clock; Running lets it run on). The weather: "Running", then the loaded kinds by their ids.
    // Each shows what the simulation holds, so a clock held by --time shows its hour.
    private static void AddTimeAndWeather(List<MenuRow> rows, TestingHooks testing)
    {
        if (testing.TimeHeld is { } timeHeld && testing.SetTimeHeld is { } setTimeHeld)
        {
            rows.Add(MenuRow.Slider(
                "timeOfDay",
                "testTimeOfDay",
                -1,
                23,
                1,
                () => timeHeld() is { } hour ? (int)Math.Floor(hour) : -1,
                value => setTimeHeld(value < 0 ? null : value),
                ":00",
                "testRunning"));
        }

        var weather = testing.WeatherIds ?? [];
        if (weather.Count > 0 && testing.WeatherHeld is { } weatherHeld && testing.SetWeatherHeld is { } setWeatherHeld)
        {
            rows.Add(MenuRow.Picker(
                "weather",
                "testWeather",
                weather,
                "testRunning",
                () => weatherHeld() is { } id ? IndexOf(weather, id) + 1 : 0,
                value => setWeatherHeld(value <= 0 ? null : weather[value - 1])));
        }
    }

    private static void AddSwitch(List<MenuRow> rows, string id, string labelKey, Func<bool>? get, Action<bool>? set)
    {
        if (get is not null && set is not null)
        {
            rows.Add(MenuRow.Switch(id, labelKey, get, set));
        }
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
