namespace Vocaterra.Nlp.Response.Templates;

/// <summary>The names of the dialogue pools the pipeline and the response planner use without any agent action naming them.</summary>
public static class ReservedPools
{
    /// <summary>The reply pool for an utterance that was not understood (<see cref="ResponsePlanner.NotUnderstoodIntent"/>).</summary>
    public const string NotUnderstood = "not_understood";

    /// <summary>The pool voiced when a prompt without an agent is answered acceptably (an answer-only prompt such as a quiz).</summary>
    public const string AnswerAccepted = "answer_accepted";

    /// <summary>The prefix of feedback pools: <c>feedback.&lt;kind&gt;</c> or <c>feedback.&lt;kind&gt;.&lt;tag&gt;</c>, where the kind is one of <see cref="FeedbackKindNames"/>.</summary>
    public const string FeedbackPrefix = "feedback.";

    /// <summary>The feedback kinds a feedback pool can be named after (see <see cref="FeedbackKinds"/>), in ordinal order.</summary>
    public static IReadOnlyList<string> FeedbackKindNames { get; } =
    [
        FeedbackKinds.ExplicitCorrection,
        FeedbackKinds.Hint,
        FeedbackKinds.ModelPronunciation,
        FeedbackKinds.Recast,
        FeedbackKinds.SlowRepeat,
    ];

    /// <summary>True for a pool the pipeline reaches by itself: <see cref="NotUnderstood"/>, <see cref="AnswerAccepted"/> and anything under <see cref="FeedbackPrefix"/>.</summary>
    public static bool IsReserved(string poolId)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        return string.Equals(poolId, NotUnderstood, StringComparison.Ordinal)
            || string.Equals(poolId, AnswerAccepted, StringComparison.Ordinal)
            || poolId.StartsWith(FeedbackPrefix, StringComparison.Ordinal);
    }

    /// <summary>True for a well-formed feedback pool id: the prefix, a feedback kind, and optionally a dot and an error tag.</summary>
    public static bool IsFeedbackPool(string poolId)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        if (!poolId.StartsWith(FeedbackPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = poolId[FeedbackPrefix.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        var kind = dot < 0 ? rest : rest[..dot];
        return FeedbackKindNames.Contains(kind, StringComparer.Ordinal) && (dot < 0 || dot + 1 < rest.Length);
    }
}
