using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Decision.Feedback;

/// <summary>
/// The feedback rule table. Each error in a verdict gets the type of the most specific matching rule:
/// exact tag over family wildcard over <c>*</c>, then the narrower repeat range, then a rule that names the difficulty,
/// then the earlier rule in the table. Errors that no rule matches produce no item.
/// </summary>
public sealed record FeedbackPolicy(IReadOnlyList<FeedbackRule> Rules, FeedbackType NotCommunicatedType = FeedbackType.SlowRepeat)
{
    /// <summary>The most items a single plan contains, so the NPC never lectures.</summary>
    public const int MaxItems = 2;

    /// <summary>The tag of the single item produced when the player was not understood and the verdict carries no error to quote.</summary>
    public const string NotCommunicatedTag = "not_communicated";

    /// <summary>
    /// Default table: first offence is a recast, offences 2 to 3 a hint, 4 and more an explicit correction,
    /// any <c>pron.*</c> error from the second offence on a model pronunciation. A verdict that was not communicated gets a slow repeat.
    /// </summary>
    public static FeedbackPolicy Default { get; } = new(
        [
            new FeedbackRule("*", 1, 1, null, FeedbackType.Recast),
            new FeedbackRule("*", 2, 3, null, FeedbackType.Hint),
            new FeedbackRule("*", 4, int.MaxValue, null, FeedbackType.ExplicitCorrection),
            new FeedbackRule("pron.*", 2, int.MaxValue, null, FeedbackType.ModelPronunciation),
        ]);

    /// <summary>The rule table in declaration order; rules are validated (well-formed pattern, 1 &lt;= min &lt;= max).</summary>
    public IReadOnlyList<FeedbackRule> Rules { get; init; } = Validate(Rules);

    /// <summary>The feedback type used when the verdict was not communicated.</summary>
    public FeedbackType NotCommunicatedType { get; init; } = Enum.IsDefined(NotCommunicatedType)
        ? NotCommunicatedType
        : throw new ArgumentOutOfRangeException(nameof(NotCommunicatedType));

    /// <summary>Loads a policy from canonical JSON.</summary>
    public static FeedbackPolicy FromJson(string json)
    {
        return CanonicalJson.Deserialize<FeedbackPolicy>(json);
    }

    /// <summary>
    /// Plans the feedback for a verdict. A verdict that was not communicated yields exactly one item of <see cref="NotCommunicatedType"/>
    /// (quoting the first error when there is one, otherwise tagged <see cref="NotCommunicatedTag"/>). Otherwise there is one item per error
    /// in the verdict's error order, and only the first <see cref="MaxItems"/> are kept. <paramref name="repeatCountsByTag"/> holds how often
    /// the learner has made each tag, counting the current occurrence; a missing or non-positive count is treated as 1.
    /// </summary>
    public FeedbackPlan Plan(Verdict verdict, IReadOnlyDictionary<string, int> repeatCountsByTag, Difficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(repeatCountsByTag);

        if (!verdict.Communicated)
        {
            var first = verdict.Errors.Count > 0 ? verdict.Errors[0] : null;
            return new FeedbackPlan(
            [
                new FeedbackItem(first?.Tag ?? NotCommunicatedTag, NotCommunicatedType, first?.Expected ?? string.Empty, first?.Actual ?? string.Empty),
            ]);
        }

        var items = new List<FeedbackItem>(MaxItems);
        foreach (var error in verdict.Errors)
        {
            var count = repeatCountsByTag.TryGetValue(error.Tag, out var seen) && seen >= 1 ? seen : 1;
            if (Select(error.Tag, count, difficulty) is { } rule)
            {
                items.Add(new FeedbackItem(error.Tag, rule.Type, error.Expected, error.Actual));
                if (items.Count == MaxItems)
                {
                    break;
                }
            }
        }

        return new FeedbackPlan(items);
    }

    /// <summary>Returns the most specific rule for the tag, repeat count and difficulty, or null when none applies.</summary>
    public FeedbackRule? Select(string tag, int repeatCount, Difficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(tag);
        FeedbackRule? best = null;
        var bestPattern = -1;
        var bestWidth = long.MaxValue;
        var bestNamesDifficulty = false;
        foreach (var rule in Rules)
        {
            if (repeatCount < rule.MinRepeat
                || repeatCount > rule.MaxRepeat
                || (rule.Difficulty is { } needed && needed != difficulty)
                || !TagPattern.Matches(rule.TagPattern, tag))
            {
                continue;
            }

            var pattern = TagPattern.Specificity(rule.TagPattern);
            var width = (long)rule.MaxRepeat - rule.MinRepeat;
            var namesDifficulty = rule.Difficulty is not null;
            var better = best is null
                || pattern > bestPattern
                || (pattern == bestPattern && (width < bestWidth || (width == bestWidth && namesDifficulty && !bestNamesDifficulty)));
            if (better)
            {
                best = rule;
                bestPattern = pattern;
                bestWidth = width;
                bestNamesDifficulty = namesDifficulty;
            }
        }

        return best;
    }

    /// <summary>Structural equality including the rule list.</summary>
    public bool Equals(FeedbackPolicy? other)
    {
        return other is not null
            && NotCommunicatedType == other.NotCommunicatedType
            && ListEquality.Equal(Rules, other.Rules);
    }

    /// <summary>Hash consistent with <see cref="Equals(FeedbackPolicy)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(NotCommunicatedType);
        ListEquality.Add(ref hash, Rules);
        return hash.ToHashCode();
    }

    private static ImmutableArray<FeedbackRule> Validate(IReadOnlyList<FeedbackRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules, nameof(Rules));
        foreach (var rule in rules)
        {
            if (rule is null || !TagPattern.IsValid(rule.TagPattern))
            {
                throw new ArgumentException($"Malformed feedback tag pattern '{rule?.TagPattern}'.", nameof(rules));
            }

            if (rule.MinRepeat < 1 || rule.MaxRepeat < rule.MinRepeat)
            {
                throw new ArgumentException($"Rule '{rule.TagPattern}' needs 1 <= minRepeat <= maxRepeat.", nameof(rules));
            }

            if (rule.Difficulty is { } difficulty && !Enum.IsDefined(difficulty))
            {
                throw new ArgumentException($"Rule '{rule.TagPattern}' has an unknown difficulty.", nameof(rules));
            }

            if (!Enum.IsDefined(rule.Type))
            {
                throw new ArgumentException($"Rule '{rule.TagPattern}' has an unknown feedback type.", nameof(rules));
            }
        }

        return [.. rules];
    }
}
