namespace Vocaterra.Game.Core.Session;

/// <summary>What the playtest needs from the game around it.</summary>
/// <param name="ScenarioIndex">The world script's step now (a scripted post-check answers from there).</param>
/// <param name="SkipScenarioTo">The world script goes on from this step (at the end: it is done with its steps).</param>
/// <param name="ClosePanels">Closes the dialogue, the book, the inventory and the crafting panel, whichever is open.</param>
/// <param name="DrainForSave">Applies the turns still on the worker before the save at the quit.</param>
public sealed record PlaytestHooks(Func<int> ScenarioIndex, Action<int> SkipScenarioTo, Action ClosePanels, Action DrainForSave);
