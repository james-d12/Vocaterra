namespace Vocaterra.Game.Core.Npc;

/// <summary>How an NPC opens a talk (schema 2, village-plan V01; it was <c>NpcService.OpeningEvent</c>'s switch).</summary>
/// <param name="Event">The host event the game raises to the agent (<c>villager.greet</c>), or <see langword="null"/> with <see cref="OpeningWhen.Never"/>.</param>
/// <param name="When">When it is raised.</param>
public sealed record NpcOpeningRule(string? Event, OpeningWhen When)
{
    /// <summary>The rule of an NPC whose prompt's text opens every talk.</summary>
    public static NpcOpeningRule Never { get; } = new(null, OpeningWhen.Never);
}
