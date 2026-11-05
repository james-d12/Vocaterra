namespace Vocaterra.Game.Core.Input;

/// <summary>The commands the keys are bound to (<c>KeyBindings</c>, in the game); one object per thing the player can ask for.</summary>
/// <param name="ToggleView">F5: first person and over the shoulder (S15).</param>
/// <param name="Save">F6: saves the game (S10).</param>
/// <param name="CycleTextSize">F9: the interface text size, 100, 125 and 150 percent (X08).</param>
/// <param name="EndPlaytest">F10: the playtest's post-check at once (S12).</param>
/// <param name="ToggleDebug">F3: the debug overlay (not in a playtest).</param>
/// <param name="Listen">V on an NPC: opens the panel and starts listening.</param>
/// <param name="TypeTo">T on an NPC: opens the panel with the text box.</param>
/// <param name="TalkTo">E on an NPC: opens the panel.</param>
/// <param name="OpenControls">F1: the controls help (S16).</param>
/// <param name="NextTip">Enter: the next tip (S16).</param>
/// <param name="OpenBook">J, or the book item: the book (S20).</param>
/// <param name="OpenCrafting">C, or E at the workbench: the crafting panel.</param>
/// <param name="OpenInventory">I, or E: the inventory.</param>
/// <param name="ReleaseMouse">Esc in the world: the mouse is let go, and the closing card goes (S17).</param>
/// <param name="Sleep">Enter on the closing card, or E at the player's own lit fire: sleep until the next play session (V12c), or <see langword="null"/> for none.</param>
/// <param name="DrinkByHand">E while aiming at water within reach: a drink by hand, with nothing held and without Spanish (N02), or <see langword="null"/> for none.</param>
/// <param name="ToggleViewEffects">F7: the needs' effects on the view (dimmed edges, a blink, red edges) on or off (N05); the icons and the lines stay. <see langword="null"/> for none.</param>
/// <param name="TakeBelongings">E while aiming at the chest a death left, within reach: takes back what the bag can hold (N04), or <see langword="null"/> for none.</param>
public sealed record GameCommands(
    IGameCommand ToggleView,
    IGameCommand Save,
    IGameCommand CycleTextSize,
    IGameCommand EndPlaytest,
    IGameCommand ToggleDebug,
    IGameCommand Listen,
    IGameCommand TypeTo,
    IGameCommand TalkTo,
    IGameCommand OpenControls,
    IGameCommand NextTip,
    IGameCommand OpenBook,
    IGameCommand OpenCrafting,
    IGameCommand OpenInventory,
    IGameCommand ReleaseMouse,
    IGameCommand? Sleep = null,
    IGameCommand? DrinkByHand = null,
    IGameCommand? TakeBelongings = null,
    IGameCommand? ToggleViewEffects = null);
