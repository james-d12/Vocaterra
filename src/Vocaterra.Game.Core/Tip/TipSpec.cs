namespace Vocaterra.Game.Core.Tip;

/// <summary>A first-time tip: the support-language text shown once, when something happens for the first time.</summary>
/// <param name="Id">A stable id (the save keeps which tips were shown).</param>
/// <param name="Trigger">What the tip waits for (C06): <c>start</c>, <c>aimed_npc</c> or a world event (<see cref="Event.WorldEvents"/>), with its filters.</param>
/// <param name="Text">The tip, in the support language (the locale's <c>tip.&lt;id&gt;</c>).</param>
/// <param name="DelaySeconds">How many seconds of game time after its trigger the tip waits before it may come up (S17: the opening tips are paced so they do not stack up); 0 for none.</param>
public sealed record TipSpec(string Id, Event.Trigger Trigger, string Text, int DelaySeconds = 0)
{
    /// <summary>The event the tip waits for (<see cref="Event.Trigger.Event"/>).</summary>
    public string When => Trigger.Event;
}
