namespace Vocaterra.Game.Core.Menu;

/// <summary>One row of the controls page: a group's heading, or a control with the keys that do it and what it does.</summary>
/// <param name="IsHeading">Whether this is a group's heading (<paramref name="Text"/> is its name and <paramref name="Keys"/> is empty).</param>
/// <param name="Keys">The keys and mouse controls, spelled by the locale (<c>W or Up</c>).</param>
/// <param name="Text">What the control does, or the group's name.</param>
public readonly record struct ControlsRow(bool IsHeading, string Keys, string Text);
