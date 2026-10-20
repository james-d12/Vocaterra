namespace Slg.Decision.Npc;

/// <summary>The persisted position of one NPC: its current state and how many interactions it has had.</summary>
public sealed record NpcState(string NpcId, string StateId, long InteractionCounter);

/// <summary>One thing an NPC does in a step; <see cref="VariantIndex"/> is set only when the action drew from a variant pool.</summary>
public sealed record NpcAction(string DialogueIntent, string? GameEvent, int? VariantIndex);

/// <summary>The outcome of one NPC step: the next state (counter advanced by one) and the actions to perform in order.</summary>
public sealed record NpcStepResult(NpcState Next, IReadOnlyList<NpcAction> Actions)
{
    /// <summary>Structural equality including the action list.</summary>
    public bool Equals(NpcStepResult? other)
    {
        return other is not null
            && Next.Equals(other.Next)
            && ListEquality.Equal(Actions, other.Actions);
    }

    /// <summary>Hash consistent with <see cref="Equals(NpcStepResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Next);
        ListEquality.Add(ref hash, Actions);
        return hash.ToHashCode();
    }
}
