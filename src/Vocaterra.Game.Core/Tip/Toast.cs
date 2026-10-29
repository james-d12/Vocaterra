namespace Vocaterra.Game.Core.Tip;

/// <summary>One line shown across the top of the screen for a while, for example <c>¡Nueva palabra: el árbol!</c>.</summary>
/// <param name="Text">The line.</param>
/// <param name="StartTick">The simulation tick it appeared at.</param>
public sealed record Toast(string Text, long StartTick);
