using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Nlp.Decision;

/// <summary>Error tag patterns shared by the agent conditions and the feedback rules: an exact tag, a family wildcard such as <c>pron.*</c>, or <c>*</c>.</summary>
internal static class TagPattern
{
    /// <summary>The pattern that matches every tag.</summary>
    public const string Any = "*";

    /// <summary>Specificity of <see cref="Any"/>.</summary>
    public const int AnySpecificity = 0;

    /// <summary>Specificity of a family wildcard.</summary>
    public const int FamilySpecificity = 1;

    /// <summary>Specificity of an exact tag.</summary>
    public const int ExactSpecificity = 2;

    private const string FamilySuffix = ".*";

    /// <summary>Returns true when the pattern is well formed.</summary>
    public static bool IsValid(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        if (string.Equals(pattern, Any, StringComparison.Ordinal))
        {
            return true;
        }

        if (pattern.EndsWith(FamilySuffix, StringComparison.Ordinal))
        {
            var family = pattern[..^FamilySuffix.Length];
            return family.Length > 0 && !family.Contains('*', StringComparison.Ordinal);
        }

        return !pattern.Contains('*', StringComparison.Ordinal);
    }

    /// <summary>Returns 2 for an exact tag, 1 for a family wildcard and 0 for <c>*</c>.</summary>
    public static int Specificity(string pattern)
    {
        if (string.Equals(pattern, Any, StringComparison.Ordinal))
        {
            return AnySpecificity;
        }

        return pattern.EndsWith(FamilySuffix, StringComparison.Ordinal) ? FamilySpecificity : ExactSpecificity;
    }

    /// <summary>Returns true when the tag matches the pattern; a family wildcard compares against <see cref="ErrorTags.Family(string)"/>.</summary>
    public static bool Matches(string pattern, string tag)
    {
        switch (Specificity(pattern))
        {
            case AnySpecificity:
                return true;
            case FamilySpecificity:
                return string.Equals(ErrorTags.Family(tag), pattern[..^FamilySuffix.Length], StringComparison.Ordinal);
            default:
                return string.Equals(pattern, tag, StringComparison.Ordinal);
        }
    }

    /// <summary>Returns true when the tag matches at least one of the patterns.</summary>
    public static bool MatchesAny(IReadOnlyList<string> patterns, string tag)
    {
        for (var i = 0; i < patterns.Count; i++)
        {
            if (Matches(patterns[i], tag))
            {
                return true;
            }
        }

        return false;
    }
}
