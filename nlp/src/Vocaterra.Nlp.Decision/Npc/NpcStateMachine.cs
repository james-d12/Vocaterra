using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Decision.Npc;

/// <summary>A validated NPC state machine: first matching transition wins, variant choices come from the supplied <see cref="SeededRng"/>.</summary>
public sealed class NpcStateMachine
{
    // Lookup tables only: they are never enumerated, so hash order cannot influence any output.
    private readonly Dictionary<string, NpcStateDefinition> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _poolSizes = new(StringComparer.Ordinal);

    /// <summary>Validates the definition (unique states, known targets, known variant pools, well-formed predicates) and wraps it.</summary>
    /// <exception cref="InvalidDataException">The definition is structurally invalid.</exception>
    public NpcStateMachine(NpcDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        Validate();
    }

    /// <summary>The definition this machine runs.</summary>
    public NpcDefinition Definition { get; }

    /// <summary>The id of the NPC this machine drives.</summary>
    public string NpcId => Definition.NpcId;

    /// <summary>Loads a machine from canonical JSON.</summary>
    public static NpcStateMachine FromJson(string json)
    {
        return new NpcStateMachine(CanonicalJson.Deserialize<NpcDefinition>(json));
    }

    /// <summary>The state a new game starts this NPC in: the initial state with interaction counter 0.</summary>
    public NpcState InitialState()
    {
        return new NpcState(Definition.NpcId, Definition.InitialState, 0);
    }

    /// <summary>Steps with the generator for this interaction: <see cref="SeededRng.ForInteraction(ulong, string, long)"/> over the NPC id and current counter.</summary>
    public NpcStepResult Step(NpcState current, Verdict verdict, ulong worldSeed)
    {
        ArgumentNullException.ThrowIfNull(current);
        return Step(current, verdict, SeededRng.ForInteraction(worldSeed, current.NpcId, current.InteractionCounter));
    }

    /// <summary>
    /// Evaluates the current state's transitions in declaration order; the first whose condition matches wins,
    /// otherwise the fallback applies (and a fallback without a target stays in the current state).
    /// Every action with a variant pool draws <c>rng.NextInt(poolSize)</c> in action order. The interaction counter always advances by one.
    /// </summary>
    public NpcStepResult Step(NpcState current, Verdict verdict, SeededRng rng)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(rng);

        if (!string.Equals(current.NpcId, Definition.NpcId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"State belongs to NPC '{current.NpcId}', not '{Definition.NpcId}'.", nameof(current));
        }

        if (!_states.TryGetValue(current.StateId, out var state))
        {
            throw new ArgumentException($"NPC '{Definition.NpcId}' has no state '{current.StateId}'.", nameof(current));
        }

        string target = current.StateId;
        IReadOnlyList<NpcActionDefinition> chosen = [];
        var matched = false;
        foreach (var transition in state.Transitions)
        {
            if (Matches(transition.On, verdict))
            {
                target = transition.To;
                chosen = transition.Actions;
                matched = true;
                break;
            }
        }

        if (!matched && state.Fallback is { } fallback)
        {
            target = fallback.To ?? current.StateId;
            chosen = fallback.Actions;
        }

        var actions = new NpcAction[chosen.Count];
        for (var i = 0; i < chosen.Count; i++)
        {
            var action = chosen[i];
            int? variant = action.VariantPoolId is { } poolId ? rng.NextInt(_poolSizes[poolId]) : null;
            actions[i] = new NpcAction(action.DialogueIntent, action.GameEvent, variant);
        }

        var next = new NpcState(current.NpcId, target, checked(current.InteractionCounter + 1));
        return new NpcStepResult(next, actions);
    }

    private static bool Matches(NpcCondition condition, Verdict verdict)
    {
        if (condition.Intent is { } intent
            && !string.Equals(intent, verdict.Intent.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        if (condition.Communicated is { } communicated && communicated != verdict.Communicated)
        {
            return false;
        }

        if (condition.AnyErrorTag is { } any && !HasError(verdict, any))
        {
            return false;
        }

        if (condition.NoErrorTag is { } none && HasError(verdict, none))
        {
            return false;
        }

        return true;
    }

    private static bool HasError(Verdict verdict, IReadOnlyList<string> patterns)
    {
        foreach (var error in verdict.Errors)
        {
            if (TagPattern.MatchesAny(patterns, error.Tag))
            {
                return true;
            }
        }

        return false;
    }

    private static InvalidDataException Invalid(string message)
    {
        return new InvalidDataException($"Invalid NPC definition: {message}");
    }

    private void Validate()
    {
        if (string.IsNullOrEmpty(Definition.NpcId))
        {
            throw Invalid("npcId is required.");
        }

        if (Definition.States is null || Definition.States.Count == 0)
        {
            throw Invalid("at least one state is required.");
        }

        foreach (var pool in Definition.VariantPools ?? [])
        {
            if (string.IsNullOrEmpty(pool.Id) || pool.Count < 1)
            {
                throw Invalid("variant pools need a non-empty id and a count of at least 1.");
            }

            if (!_poolSizes.TryAdd(pool.Id, pool.Count))
            {
                throw Invalid($"duplicate variant pool '{pool.Id}'.");
            }
        }

        foreach (var state in Definition.States)
        {
            if (string.IsNullOrEmpty(state?.Id))
            {
                throw Invalid("every state needs an id.");
            }

            if (!_states.TryAdd(state.Id, state))
            {
                throw Invalid($"duplicate state '{state.Id}'.");
            }
        }

        if (!_states.ContainsKey(Definition.InitialState ?? string.Empty))
        {
            throw Invalid($"initial state '{Definition.InitialState}' is not defined.");
        }

        foreach (var state in Definition.States)
        {
            if (state.Transitions is null)
            {
                throw Invalid($"state '{state.Id}' needs a transitions array (it may be empty).");
            }

            foreach (var transition in state.Transitions)
            {
                if (transition?.On is null)
                {
                    throw Invalid($"a transition of state '{state.Id}' has no 'on' condition (use {{}} to match everything).");
                }

                ValidateCondition(state.Id, transition.On);
                ValidateTarget(state.Id, transition.To);
                ValidateActions(state.Id, transition.Actions);
            }

            if (state.Fallback is { } fallback)
            {
                if (fallback.To is not null)
                {
                    ValidateTarget(state.Id, fallback.To);
                }

                ValidateActions(state.Id, fallback.Actions);
            }
        }
    }

    private void ValidateTarget(string stateId, string? target)
    {
        if (target is null || !_states.ContainsKey(target))
        {
            throw Invalid($"state '{stateId}' moves to undefined state '{target}'.");
        }
    }

    private void ValidateActions(string stateId, IReadOnlyList<NpcActionDefinition>? actions)
    {
        if (actions is null)
        {
            throw Invalid($"state '{stateId}' has an action list that is missing (it may be empty).");
        }

        foreach (var action in actions)
        {
            if (string.IsNullOrEmpty(action?.DialogueIntent))
            {
                throw Invalid($"state '{stateId}' has an action without a dialogueIntent.");
            }

            if (action.VariantPoolId is { } poolId && !_poolSizes.ContainsKey(poolId))
            {
                throw Invalid($"state '{stateId}' uses undefined variant pool '{poolId}'.");
            }
        }
    }

    private static void ValidateCondition(string stateId, NpcCondition condition)
    {
        ValidatePatterns(stateId, "anyErrorTag", condition.AnyErrorTag);
        ValidatePatterns(stateId, "noErrorTag", condition.NoErrorTag);
    }

    private static void ValidatePatterns(string stateId, string field, IReadOnlyList<string>? patterns)
    {
        if (patterns is null)
        {
            return;
        }

        if (patterns.Count == 0)
        {
            throw Invalid($"state '{stateId}' has an empty {field} list; omit the field instead.");
        }

        foreach (var pattern in patterns)
        {
            if (!TagPattern.IsValid(pattern))
            {
                throw Invalid($"state '{stateId}' has a malformed {field} pattern '{pattern}'.");
            }
        }
    }
}
