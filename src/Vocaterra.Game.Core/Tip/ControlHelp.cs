namespace Vocaterra.Game.Core.Tip;

/// <summary>One line of the controls help: the keys and what they do (the order is <c>game/controls.json</c>, the text the locale's <c>control.&lt;id&gt;.*</c>).</summary>
/// <param name="Id">A stable id.</param>
/// <param name="Keys">The keys or buttons, as the screen shows them.</param>
/// <param name="Text">What they do, in the support language.</param>
public sealed record ControlHelp(string Id, string Keys, string Text);
