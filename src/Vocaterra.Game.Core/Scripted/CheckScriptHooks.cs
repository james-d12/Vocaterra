namespace Vocaterra.Game.Core.Scripted;

/// <summary>
/// What the scripted check asks of the game around it: pick an option, send a typed or a spoken answer, take a screenshot. The game supplies
/// these (the check screen, the worker and the back buffer are its), so the script stays free of the engine.
/// </summary>
/// <param name="Pick">Picks the option at the position given (1 to 3, as shown).</param>
/// <param name="Typed">Sends the line as a typed production answer.</param>
/// <param name="Speak">Synthesizes the line and sends it as a spoken production answer.</param>
/// <param name="TakeShot">Asks for a screenshot of the next drawn frame.</param>
/// <param name="ShotPending">Whether the last requested screenshot has not been saved yet.</param>
public sealed record CheckScriptHooks(Action<int> Pick, Action<string> Typed, Action<string> Speak, Action<string> TakeShot, Func<bool> ShotPending);
