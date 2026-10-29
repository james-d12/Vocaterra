namespace Vocaterra.Game.Core.Language.Checks;

/// <summary>What the check battery needs from the game around it.</summary>
/// <param name="TakeShot">A <c>--shot</c> step of the battery's script: the screen at this point.</param>
/// <param name="ShotPending">Whether a <c>--shot</c> is waiting for its frame.</param>
/// <param name="MicCheckActive">Whether the mic check is on screen: it comes first.</param>
/// <param name="SkipScenarioTo">A pre-check hands over to the world's script, which goes on from this step.</param>
/// <param name="Exit">A post- or recall-check ends the run after its thank-you screen.</param>
/// <param name="PlaytestEnded">A playtest's post- or recall-check is over: its thank-you screen holds until Esc.</param>
public sealed record CheckBatteryHooks(
    Action<string> TakeShot,
    Func<bool> ShotPending,
    Func<bool> MicCheckActive,
    Action<int> SkipScenarioTo,
    Action Exit,
    Action<CheckScreen> PlaytestEnded);
