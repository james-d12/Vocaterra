namespace Vocaterra.Game.Core.Menu;

/// <summary>The interface words the menu asks the locale for (<c>ui.&lt;key&gt;</c>), in one list so that the locale check (<c>LocaleText.UiKeys</c>) holds a locale to every one of them.</summary>
public static class MenuText
{
    /// <summary>Every <c>ui.</c> key of the menu: its buttons, the settings' labels, headings and values, the testing section, and the controls page.</summary>
    public static IReadOnlyList<string> UiKeys { get; } =
    [
        .. new[]
        {
            "menuTitle", "menuResume", "menuSettings", "menuControls", "menuExit", "menuBack", "menuHint",
            "headingSound", "headingGraphics", "headingInterface", "headingMouse", "headingNeeds", "headingCheats", "headingTesting", "headingRestart",
            "testingNote", "restartNote", "restartNote2", "restartNote3", "controlsRange", "controlsOr",
            "testDebug", "testNeedsRun", "testRefill", "testSoundsOff", "testVoicesMuted", "testShoulder", "testSave",
            "subNeeds", "subView", "subTeleport", "subSound", "subScoring", "subFiles",
            "testNeedHunger", "testNeedThirst", "testNeedSleep", "testNeedHealth", "testNeedStamina",
            "testTimeOfDay", "testWeather", "testRunning", "testGreyscale", "testAssumeKnown", "testTips", "testBeacon", "testPlace", "testGo", "testCredit", "testCreditHalf", "testCreditFull", "testAutosave",
            "settingsOn", "settingsOff",
        },
        .. PlayerSettings.All.SelectMany(static spec => (spec.ChoiceKeys ?? []).Prepend(spec.LabelKey).Concat(spec.MinKey is null ? [] : [spec.MinKey])),
        .. ControlsListing.UiKeys,
    ];
}
