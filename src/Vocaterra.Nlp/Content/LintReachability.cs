using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Content;

/// <summary>Check 4: content that nothing reaches, measured from the prompts (the host starts a conversation with a prompt, and a prompt names its agent).</summary>
internal static class LintReachability
{
    public static void Run(LintContext context)
    {
        var bundle = context.Bundle;
        var none = context.Options.RootPrompts is null ? "no prompt" : "none of the selected prompts";
        var allowedIntents = new HashSet<string>(StringComparer.Ordinal);
        var answerSets = new HashSet<string>(StringComparer.Ordinal);
        var agents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prompt in context.RootPrompts)
        {
            allowedIntents.UnionWith(prompt.AllowedIntentIds);
            answerSets.Add(prompt.AnswerSetId);
            if (prompt.AgentId is { } agentId)
            {
                agents.Add(agentId);
            }
        }

        foreach (var intent in bundle.Intents.Intents.Where(intent => !allowedIntents.Contains(intent.Id.Value)))
        {
            // M74: since M72 an intent with a grammar or a keyword rule is read off the prompt at every prompt that lists its intents (the verdict's off-prompt
            // intent, which a host answers), so no prompt allowing it is a fact, not a hazard: the weather intents before a host allows them anywhere.
            if (intent.Grammars.Count > 0 || intent.KeywordRules.Count > 0)
            {
                context.Add(LintSeverity.Info, "reach.intent", ContentPaths.Intents, [intent.Id.Value], $"{none} allows intent '{intent.Id.Value}'; it is read only off the prompt.");
            }
            else
            {
                context.Add(LintSeverity.Warning, "reach.intent", ContentPaths.Intents, [intent.Id.Value], $"{none} allows intent '{intent.Id.Value}'.");
            }
        }

        foreach (var id in bundle.AnswerSets.Keys.Where(id => !answerSets.Contains(id)))
        {
            context.Add(LintSeverity.Warning, "reach.answer-set", ContentPaths.AnswerSetFolder, [id], $"{none} names answer set '{id}'.");
        }

        foreach (var agentId in bundle.Agents.Keys.Where(id => !agents.Contains(id)))
        {
            context.Add(LintSeverity.Warning, "reach.agent", $"{ContentPaths.AgentFolder}{agentId}.json", [agentId], $"{none} names agent '{agentId}'.");
        }

        foreach (var machine in bundle.Agents.Values)
        {
            ReportUnreachableStates(context, machine);
        }

        ReportPools(context, agents, none);
    }

    private static bool IsHostPool(LintContext context, string poolId)
    {
        foreach (var entry in context.Options.HostPools ?? [])
        {
            if (entry.EndsWith('*') ? poolId.StartsWith(entry[..^1], StringComparison.Ordinal) : string.Equals(poolId, entry, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportUnreachableStates(LintContext context, AgentStateMachine machine)
    {
        var definition = machine.Definition;
        var byId = definition.States.ToDictionary(static state => state.Id, StringComparer.Ordinal);
        var reached = new HashSet<string>(StringComparer.Ordinal) { definition.InitialState };
        var queue = new Queue<string>();
        queue.Enqueue(definition.InitialState);
        while (queue.Count > 0)
        {
            if (!byId.TryGetValue(queue.Dequeue(), out var state))
            {
                continue;
            }

            var targets = state.Transitions.Select(static transition => transition.To);
            if (state.Fallback?.To is { } fallbackTarget)
            {
                targets = targets.Append(fallbackTarget);
            }

            foreach (var target in targets)
            {
                if (reached.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        foreach (var state in definition.States.Where(state => !reached.Contains(state.Id)))
        {
            context.Add(
                LintSeverity.Warning,
                "reach.state",
                $"{ContentPaths.AgentFolder}{definition.AgentId}.json",
                [definition.AgentId, state.Id],
                $"state '{state.Id}' of agent '{definition.AgentId}' cannot be reached from the initial state '{definition.InitialState}'.");
        }
    }

    private static void ReportPools(LintContext context, HashSet<string> promptAgents, string none)
    {
        var bundle = context.Bundle;
        var usedBy = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var machine in bundle.Agents.Values)
        {
            foreach (var state in machine.Definition.States)
            {
                var actions = state.Transitions.SelectMany(static transition => transition.Actions).Concat(state.Fallback?.Actions ?? []);
                foreach (var action in actions)
                {
                    if (!usedBy.TryGetValue(action.DialogueIntent, out var agents))
                    {
                        agents = new SortedSet<string>(StringComparer.Ordinal);
                        usedBy.Add(action.DialogueIntent, agents);
                    }

                    agents.Add(machine.AgentId);
                }
            }
        }

        foreach (var poolId in bundle.Dialogue.Pools.Keys)
        {
            if (IsHostPool(context, poolId))
            {
                continue;
            }

            if (ReservedPools.IsReserved(poolId))
            {
                if (poolId.StartsWith(ReservedPools.FeedbackPrefix, StringComparison.Ordinal) && !ReservedPools.IsFeedbackPool(poolId))
                {
                    context.Add(
                        LintSeverity.Warning,
                        "reach.pool",
                        ContentPaths.Dialogue,
                        [poolId],
                        $"pool '{poolId}' starts with 'feedback.' but is not 'feedback.<kind>' or 'feedback.<kind>.<tag>' for a feedback kind ({string.Join(", ", ReservedPools.FeedbackKindNames)}).");
                }

                continue;
            }

            if (!usedBy.TryGetValue(poolId, out var agents))
            {
                context.Add(
                    LintSeverity.Warning,
                    "reach.pool",
                    ContentPaths.Dialogue,
                    [poolId],
                    $"pool '{poolId}' is used by no agent action and is not a pipeline pool ({ReservedPools.NotUnderstood}, {ReservedPools.AnswerAccepted}, feedback.*).");
            }
            else if (!agents.Any(promptAgents.Contains))
            {
                var which = string.Join(", ", agents.Select(static id => $"'{id}'"));
                context.Add(
                    LintSeverity.Warning,
                    "reach.pool",
                    ContentPaths.Dialogue,
                    [poolId, .. agents],
                    $"pool '{poolId}' is used only by agent{(agents.Count > 1 ? "s" : string.Empty)} {which}, which {none} names, so no prompt reaches it.");
            }
        }
    }
}
