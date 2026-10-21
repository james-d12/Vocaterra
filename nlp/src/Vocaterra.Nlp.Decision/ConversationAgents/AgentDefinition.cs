namespace Vocaterra.Nlp.Decision.ConversationAgents;

/// <summary>The JSON definition of one agent: its states, the initial state and the sizes of its variant pools.</summary>
public sealed record AgentDefinition(
    string AgentId,
    string InitialState,
    IReadOnlyList<AgentStateDefinition> States,
    IReadOnlyList<VariantPoolDefinition>? VariantPools = null);

/// <summary>One state of an agent with its ordered transitions and an optional fallback.</summary>
public sealed record AgentStateDefinition(
    string Id,
    IReadOnlyList<AgentTransition> Transitions,
    AgentFallback? Fallback = null);

/// <summary>
/// A guarded transition: when <see cref="On"/> matches the verdict, move to <see cref="To"/> and emit <see cref="Actions"/>.
/// With <see cref="OnHostEvent"/> set it is a host-event transition instead: it is taken only by <see cref="AgentStateMachine.StepHostEvent"/> when
/// the host raises that event name (a host-initiated turn, with no player utterance), and never by an utterance verdict. <see cref="On"/> may then be
/// omitted from the JSON; it reads as the empty condition and is ignored.
/// </summary>
/// <param name="On">The verdict condition of an utterance transition.</param>
/// <param name="To">The state to move to.</param>
/// <param name="Actions">What the agent does, in order.</param>
/// <param name="OnHostEvent">The host event name that triggers this transition, or null for an ordinary utterance transition (the default; existing agent files omit it).</param>
public sealed record AgentTransition(
    AgentCondition On,
    string To,
    IReadOnlyList<AgentActionDefinition> Actions,
    string? OnHostEvent = null)
{
    /// <summary>The verdict condition; a host-event transition that omits it gets the empty condition.</summary>
    public AgentCondition On { get; init; } = On ?? (OnHostEvent is not null ? new AgentCondition() : null!);
}

/// <summary>Conjunction of verdict predicates; an omitted predicate does not constrain the match, so <c>{}</c> matches every verdict.</summary>
public sealed record AgentCondition(
    string? Intent = null,
    IReadOnlyList<string>? AnyErrorTag = null,
    IReadOnlyList<string>? NoErrorTag = null,
    bool? Communicated = null);

/// <summary>What the agent does in a transition: a dialogue intent, an optional host event and an optional variant pool to draw from.</summary>
public sealed record AgentActionDefinition(
    string DialogueIntent,
    string? HostEvent = null,
    string? VariantPoolId = null);

/// <summary>The branch taken when no transition of the current state matches; a null target stays in the current state.</summary>
public sealed record AgentFallback(
    IReadOnlyList<AgentActionDefinition> Actions,
    string? To = null);

/// <summary>The number of interchangeable phrasings available under one variant pool id.</summary>
public sealed record VariantPoolDefinition(string Id, int Count);
