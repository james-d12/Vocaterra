namespace Vocaterra.Nlp.Decision.Feedback;

/// <summary>One piece of feedback: the error tag it addresses, the chosen type and the expected versus actual text.</summary>
public sealed record FeedbackItem(string Tag, FeedbackType Type, string Expected, string Actual);

/// <summary>The feedback an NPC gives for one verdict: at most <see cref="FeedbackPolicy.MaxItems"/> items in error order.</summary>
public sealed record FeedbackPlan(IReadOnlyList<FeedbackItem> Items)
{
    /// <summary>Structural equality including the item list.</summary>
    public bool Equals(FeedbackPlan? other)
    {
        return other is not null && ListEquality.Equal(Items, other.Items);
    }

    /// <summary>Hash consistent with <see cref="Equals(FeedbackPlan)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Items);
        return hash.ToHashCode();
    }
}
