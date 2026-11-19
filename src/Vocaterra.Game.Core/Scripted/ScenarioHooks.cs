using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;
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
/// <param name="Sleep">Closes the panels and sleeps at the nearest usable sleep place (a bed or a lit fire), as the player's E there does (V12c, <c>--sleep</c>).</param>
/// <param name="Eat">Eats or drinks the item (given first if the bag has none), as a right click with it does (N02, <c>--eat</c>).</param>
/// <param name="Drink">Drinks by hand at the water the crosshair is on (N02, <c>--drink</c>).</param>
/// <param name="Station">Opens the station by its id and starts its procedure; returns whether it opened (W07, <c>--station</c>).</param>
/// <param name="Act">An act at the open station, <c>use,on[,verb]</c> (W07, <c>--act</c>).</param>
/// <param name="ProcHelp">Help at the open station (W07, <c>--proc-help</c>).</param>
/// <param name="StationSay">A typed line to the teacher from the open station; returns whether it was sent (W08, <c>--station-say</c>).</param>
/// <param name="PlaceCamp">Sets up the camp at a ground column facing a side (Y12, <c>--place-camp</c>).</param>
/// <param name="Equip">Gives the item if the bag has none and wears it in the first slot that takes it, saying the slot or the refusal (I03a, <c>--equip</c>).</param>
/// <param name="Wear">Sets the wear of an item, giving the item first when none is carried or worn (I07, <c>--wear</c>).</param>
/// <param name="WorldReady">Whether the world round the body is made (B07b): a teleport's next step waits until it is, so a shot, a walk or a look after it runs in the new place and not before the streamed world has come. Null: always.</param>
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
    Action? Sleep = null,
    Action<ItemId>? Eat = null,
    Action? Drink = null,
    Action? TakeBelongings = null,
    Action<string>? SetNeed = null,
    Action<ItemId>? Give = null,
    Action<BlockPos>? Plant = null,
    Func<string, bool>? Station = null,
    Action<string>? Act = null,
    Action? ProcHelp = null,
    Func<string, bool>? StationSay = null,
    Action<int, int, World.Layout.Direction>? PlaceCamp = null,
    Action<ItemId>? Equip = null,
    Func<bool>? WorldReady = null,
    Action<ItemId, int>? Wear = null);
