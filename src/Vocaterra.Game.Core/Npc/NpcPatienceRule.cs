namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// An NPC's fail-forward (W03; it was the stall's code): after so many lines at one prompt that asked for nothing, the NPC raises a host event to its own agent
/// (the shopkeeper hands the bottle over), so a misunderstanding never dead-ends.
/// </summary>
/// <param name="Misses">The lines in a row at the prompt that asked for nothing, after which the event is raised (the core's repair ladder also gives up at four).</param>
/// <param name="AfterHelp">The same count once the learner asked for help (the model answer was heard once); at most <paramref name="Misses"/>.</param>
/// <param name="Event">The host event the game raises to the agent, taken by one of its states (<c>shopkeeper.give</c>).</param>
/// <param name="Prompt">The prompt the rule applies at; a line off that prompt is not counted.</param>
/// <param name="Once">Whether the rule fires one time only (the NPC remembers it did); otherwise the counts start again after each firing.</param>
public sealed record NpcPatienceRule(int Misses, int AfterHelp, string Event, string Prompt, bool Once);
