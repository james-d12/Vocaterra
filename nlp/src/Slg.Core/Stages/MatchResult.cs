namespace Slg.Core.Stages;

/// <summary>How an utterance matched an answer.</summary>
public enum MatchType
{
    /// <summary>Exact match after normalization.</summary>
    Exact,

    /// <summary>Match against a listed alias.</summary>
    Alias,

    /// <summary>Match within an edit-distance budget.</summary>
    Fuzzy,

    /// <summary>No match.</summary>
    None,
}

/// <summary>Result of matching normalized text against an answer set.</summary>
public sealed record MatchResult(string? AnswerId, MatchType Type, int Distance, bool LikelyPronunciation, double Confidence);
