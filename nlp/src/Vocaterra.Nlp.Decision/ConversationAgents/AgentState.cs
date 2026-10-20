namespace Vocaterra.Nlp.Decision.ConversationAgents;

/// <summary>The persisted position of one agent: its current state and how many interactions it has had.</summary>
public sealed record AgentState(string AgentId, string StateId, long InteractionCounter);

/// <summary>One thing an agent does in a step; <see cref="VariantIndex"/> is set only when the action drew from a variant pool.</summary>
public sealed record AgentAction(string DialogueIntent, string? HostEvent, int? VariantIndex);

/// <summary>The outcome of one agent step: the next state (counter advanced by one) and the actions to perform in order.</summary>
public sealed record AgentStepResult(AgentState Next, IReadOnlyList<AgentAction> Actions)
{
    /// <summary>Structural equality including the action list.</summary>
    public bool Equals(AgentStepResult? other)
    {
        return other is not null
            && Next.Equals(other.Next)
            && ListEquality.Equal(Actions, other.Actions);
    }

    /// <summary>Hash consistent with <see cref="Equals(AgentStepResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Next);
        ListEquality.Add(ref hash, Actions);
        return hash.ToHashCode();
    }
}
