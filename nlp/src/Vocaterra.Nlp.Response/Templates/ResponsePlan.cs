namespace Vocaterra.Nlp.Response.Templates;

/// <summary>The kinds of feedback a <see cref="FeedbackRequest"/> can carry; they select the <c>feedback.&lt;kind&gt;</c> dialogue pools.</summary>
public static class FeedbackKinds
{
    /// <summary>Repeat the corrected sentence naturally.</summary>
    public const string Recast = "recast";

    /// <summary>Point at the problem without giving the answer.</summary>
    public const string Hint = "hint";

    /// <summary>State the correct form and the rule.</summary>
    public const string ExplicitCorrection = "explicit_correction";

    /// <summary>Say the target word or sound for the learner to imitate.</summary>
    public const string ModelPronunciation = "model_pronunciation";

    /// <summary>Repeat the expected phrase slowly because the learner was not understood.</summary>
    public const string SlowRepeat = "slow_repeat";
}

/// <summary>One piece of feedback to voice, mapped by the pipeline from the decision layer's feedback plan; <c>Kind</c> is one of <see cref="FeedbackKinds"/>.</summary>
public sealed record FeedbackRequest(string Tag, string Expected, string Actual, string Kind);

/// <summary>One line of the NPC's response: the dialogue line id, the rendered text and whether it is a recast of the player's sentence.</summary>
public sealed record ResponseLine(string LineId, string Text, bool IsRecast);

/// <summary>The ordered lines an NPC says in response to one player utterance.</summary>
public sealed record ResponsePlan(IReadOnlyList<ResponseLine> Lines)
{
    /// <summary>Structural equality including the line list.</summary>
    public bool Equals(ResponsePlan? other)
    {
        return other is not null && ListEquality.Equal(Lines, other.Lines);
    }

    /// <summary>Hash consistent with <see cref="Equals(ResponsePlan)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Lines);
        return hash.ToHashCode();
    }
}
