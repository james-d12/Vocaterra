namespace Vocaterra.Game.Core.Tip;

/// <summary>A first-time tip: the support-language text shown once, when something happens for the first time.</summary>
/// <param name="Id">A stable id (the save keeps which tips were shown).</param>
/// <param name="When">The trigger: <c>start</c>, <c>aimed_npc</c> or the name of a world event (<see cref="Event.WorldEvents"/>).</param>
/// <param name="Text">The tip, in the support language (the locale's <c>tip.&lt;id&gt;</c>).</param>
/// <param name="DelaySeconds">How many seconds of game time after its trigger the tip waits before it may come up (S17: the opening tips are paced so they do not stack up); 0 for none.</param>
public sealed record TipSpec(string Id, string When, string Text, int DelaySeconds = 0);
