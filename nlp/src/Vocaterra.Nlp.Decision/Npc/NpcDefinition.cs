namespace Vocaterra.Nlp.Decision.Npc;

/// <summary>The JSON definition of one NPC: its states, the initial state and the sizes of its variant pools.</summary>
public sealed record NpcDefinition(
    string NpcId,
    string InitialState,
    IReadOnlyList<NpcStateDefinition> States,
    IReadOnlyList<VariantPoolDefinition>? VariantPools = null);

/// <summary>One state of an NPC with its ordered transitions and an optional fallback.</summary>
public sealed record NpcStateDefinition(
    string Id,
    IReadOnlyList<NpcTransition> Transitions,
    NpcFallback? Fallback = null);

/// <summary>A guarded transition: when <see cref="On"/> matches the verdict, move to <see cref="To"/> and emit <see cref="Actions"/>.</summary>
public sealed record NpcTransition(
    NpcCondition On,
    string To,
    IReadOnlyList<NpcActionDefinition> Actions);

/// <summary>Conjunction of verdict predicates; an omitted predicate does not constrain the match, so <c>{}</c> matches every verdict.</summary>
public sealed record NpcCondition(
    string? Intent = null,
    IReadOnlyList<string>? AnyErrorTag = null,
    IReadOnlyList<string>? NoErrorTag = null,
    bool? Communicated = null);

/// <summary>What the NPC does in a transition: a dialogue intent, an optional game event and an optional variant pool to draw from.</summary>
public sealed record NpcActionDefinition(
    string DialogueIntent,
    string? GameEvent = null,
    string? VariantPoolId = null);

/// <summary>The branch taken when no transition of the current state matches; a null target stays in the current state.</summary>
public sealed record NpcFallback(
    IReadOnlyList<NpcActionDefinition> Actions,
    string? To = null);

/// <summary>The number of interchangeable phrasings available under one variant pool id.</summary>
public sealed record VariantPoolDefinition(string Id, int Count);
