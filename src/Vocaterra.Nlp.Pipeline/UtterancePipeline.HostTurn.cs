using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline;

public sealed partial class UtterancePipeline
{
    /// <summary>
    /// Advances the context's agent on a host event, with no player utterance (the guide opening a review with a question). The agent's current state
    /// must have a transition whose <see cref="AgentTransition.OnHostEvent"/> equals <paramref name="hostEvent"/>; its actions' dialogue pools are
    /// voiced (a fixed variant when the step drew one, otherwise a draw from the interaction's seeded stream, as in an utterance reply), rendered with
    /// <see cref="ConversationContext.HostSlots"/> only (there is no verdict). The interaction counter advances by one, so the random stream stays
    /// aligned with an utterance evaluated afterwards in <see cref="ConversationContext.Next(HostTurnResult, StoredTimestamp)"/>.
    /// The learner model and the repeat counts are not touched. Pure and deterministic; the result is recordable (<see cref="HostTurnEnvelope"/>).
    /// </summary>
    /// <returns>
    /// The turn, or null when nothing happened: the bundle has no state machine for the context's agent, or the agent's current state has no transition
    /// for the event. Null changes nothing (the counter is not advanced) and has nothing to record. A dialogue pool an action names that the bundle
    /// lacks is skipped, as for utterance actions.
    /// </returns>
    /// <exception cref="ArgumentException">The agent state belongs to another agent or names a state the machine does not have.</exception>
    /// <exception cref="TemplateException">A line needs a host slot the context does not have.</exception>
    public HostTurnResult? Advance(string hostEvent, ConversationContext context)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostEvent);
        ArgumentNullException.ThrowIfNull(context);
        var content = _services.Content;
        if (!content.Agents.TryGetValue(context.AgentId, out var machine))
        {
            return null;
        }

        var step = machine.StepHostEvent(context.AgentState, hostEvent, NewRng(context));
        if (step is null)
        {
            return null;
        }

        var rng = NewRng(context);
        var slots = context.HostSlots ?? SlotMap.Empty;
        var lines = new List<ResponseLine>(step.Actions.Count);
        foreach (var action in step.Actions)
        {
            if (!content.Dialogue.TryGetPool(action.DialogueIntent, out var pool))
            {
                continue;
            }

            var template = action.VariantIndex is { } index
                ? pool.Variants[index % pool.Variants.Count]
                : DialogueSelector.Select(pool, rng);
            lines.Add(new ResponseLine(template.LineId, content.TemplateEngine.Render(template, slots), false));
        }

        return new HostTurnResult(hostEvent, step, new ResponsePlan(lines), context.HostStateHash, _services.Versions.ContentHash);
    }
}
