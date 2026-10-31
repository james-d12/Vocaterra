using Vocaterra.Game.Core.Language.Book;

namespace Vocaterra.Game.Core.Scripted;

/// <summary>
/// What the scripted run asks of the game around it: open and close the dialogue panel on an NPC, and save a screenshot. The game supplies
/// these (the panel and the back buffer are its), so the script itself stays free of the engine and is tested without a window.
/// </summary>
/// <param name="OpenTalk">Opens the panel on an NPC (the same call the E key makes).</param>
/// <param name="CloseTalk">Closes the panel (the same call the Esc key makes).</param>
/// <param name="TakeShot">Asks for a screenshot of the next drawn frame.</param>
/// <param name="ShotPending">Whether the last requested screenshot has not been saved yet.</param>
/// <param name="Lesson">Carries out a lesson step of the script (S16): the kind and its value (the lesson's id, or the option). The game supplies the lesson front-end's call.</param>
/// <param name="Report">Opens the report panel (S21) and types the text in it; the flag says whether the panel stays open (<c>--report-open</c>) or the text is saved (<c>--report</c>).</param>
/// <param name="ReportBusy">Whether the report panel is up or on its way (S21): a saved report closes it, and the script waits for that.</param>
/// <param name="BookPlay">Opens the book on the target's page and plays its recording, normal or slow (S20b, <c>--book-play</c>).</param>
/// <param name="BookPractise">Opens the book on its Lessons page and presses "Practise again" on the lesson (S26, <c>--book-practise</c>).</param>
/// <param name="Sleep">Closes the panels and sleeps at the player's own fire, as the player's E there does (V12c, <c>--sleep</c>).</param>
public sealed record ScenarioHooks(
    Action<string> OpenTalk,
    Action CloseTalk,
    Action<string> TakeShot,
    Func<bool> ShotPending,
    Action<ScriptStepKind, string>? Lesson = null,
    Action<string, bool>? Report = null,
    Func<bool>? ReportBusy = null,
    Action<string, bool>? BookPlay = null,
    Action<string>? BookPractise = null,
    Action? Sleep = null);
