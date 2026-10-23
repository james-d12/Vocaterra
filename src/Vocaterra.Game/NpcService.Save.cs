using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game;

// The save (S10) of the conversations: each NPC's context pieces the host owns (its agent state and interaction counter, its learner model with the
// phrase skills, its repeat counts, the stored time and digest of its last turn) and the host decisions NpcService remembers (visits, the bottle, the
// offer, the patience, the uncertain lines). The replay records are not saved: they are the session log's concern (S11).
public sealed partial class NpcService
{
    /// <summary>
    /// The state of one NPC's conversation as a save holds it. The context's world seed is the session's and is not written: the restore keeps it.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    internal NpcSave CaptureAgent(string npc)
    {
        var agent = AgentOf(npc);
        var context = agent.Context;
        var skills = context.Learner.Skills
            .Select(static skill => new SkillSave(skill.SkillId, skill.PKnown, skill.Attempts, skill.Errors))
            .ToArray();
        var parameters = context.Learner.Parameters;
        var counts = context.RepeatCountsByTag
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new RepeatSave(pair.Key, pair.Value))
            .ToArray();
        return new NpcSave(
            npc,
            context.AgentState.StateId,
            context.AgentState.InteractionCounter,
            context.Now.UnixSeconds,
            context.HostStateHash,
            new LearnerSave(parameters.PriorKnown, parameters.Learn, parameters.Slip, parameters.Guess, skills),
            counts,
            agent.Visits,
            agent.Unproductive,
            agent.Uncertain,
            agent.Helped,
            agent.BottleGiven,
            agent.Offered,
            agent.LastRepair);
    }

    /// <summary>
    /// Puts an NPC's conversation back as a save recorded it. The save must be for this NPC; its learner skills and repeat counts are checked by the
    /// core's own types (a duplicate skill or an invalid id throws).
    /// </summary>
    /// <param name="save">The NPC's saved state.</param>
    /// <exception cref="ArgumentException">The save is for another NPC, or its values are not valid.</exception>
    internal void RestoreAgent(NpcSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var agent = AgentOf(save.Agent);
        var learner = new LearnerModel(
            new BktParameters(save.Learner.PriorKnown, save.Learner.Learn, save.Learner.Slip, save.Learner.Guess),
            save.Learner.Skills.Select(static skill => new SkillMastery(skill.SkillId, skill.PKnown, skill.Attempts, skill.Errors)).ToArray());
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var repeat in save.RepeatCounts)
        {
            if (!counts.TryAdd(repeat.Tag, repeat.Count))
            {
                throw new ArgumentException($"The repeat count '{repeat.Tag}' is given twice.", nameof(save));
            }
        }

        agent.Context = new ConversationContext(
            agent.Context.WorldSeed,
            save.Agent,
            new AgentState(save.Agent, save.StateId, save.InteractionCounter),
            new StoredTimestamp(save.Now),
            learner,
            counts,
            save.HostStateHash);
        agent.Visits = save.Visits;
        agent.Unproductive = save.Unproductive;
        agent.Uncertain = save.Uncertain;
        agent.Helped = save.Helped;
        agent.BottleGiven = save.BottleGiven;
        agent.Offered = save.Offered;
        agent.LastRepair = save.LastRepair;
    }
}
