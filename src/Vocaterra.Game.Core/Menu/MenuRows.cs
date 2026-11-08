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
            MenuRow.Of(settings, SettingId.VoiceVolume),
            MenuRow.Of(settings, SettingId.SfxVolume),
            MenuRow.Of(settings, SettingId.UiVolume),
            MenuRow.Heading("headingGraphics"),
            MenuRow.Of(settings, SettingId.Quality),
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
        };
        if (testing is not null)
        {
            rows.Add(MenuRow.Heading("headingTesting"));
            rows.Add(MenuRow.Note("testingNote"));
            if (testing.DebugOverlay is { } overlay && testing.SetDebugOverlay is { } setOverlay)
            {
                rows.Add(MenuRow.Switch("debugOverlay", "testDebug", overlay, setOverlay));
            }

            rows.Add(MenuRow.Switch("needsRunning", "testNeedsRun", testing.NeedsRunning, testing.SetNeedsRunning));
            rows.Add(MenuRow.Button("refillNeeds", "testRefill", testing.RefillNeeds));
            rows.Add(MenuRow.Switch("soundsOff", "testSoundsOff", testing.SoundsOff, testing.SetSoundsOff));
            rows.Add(MenuRow.Switch("voicesMuted", "testVoicesMuted", testing.VoicesMuted, testing.SetVoicesMuted));
            rows.Add(MenuRow.Switch("shoulderView", "testShoulder", testing.ShoulderView, testing.SetShoulderView));
            rows.Add(MenuRow.Button("saveNow", "testSave", testing.SaveNow));
        }

        rows.Add(MenuRow.Heading("headingRestart"));
        rows.Add(MenuRow.Note("restartNote"));
        return rows;
    }
}
