using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.Feedback;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// The "in a row" repeat counts of error tags, which a host carries from one utterance to the next and the feedback policy reads.
/// </summary>
public static class RepeatCounts
{
    /// <summary>
    /// The counts after one verdict. Each error tag in the verdict, and <see cref="FeedbackPolicy.NotCommunicatedTag"/> when the player was not
    /// understood, counts one more than before (a tag seen for the first time counts 1); every other tag is absent from the result, because the
    /// counts are "in a row". A count that would pass <see cref="int.MaxValue"/> stays there.
    /// </summary>
    /// <param name="before">The counts before the utterance, in any order; a count below 1 is treated as absent.</param>
    /// <param name="verdict">The verdict of the utterance.</param>
    /// <returns>The counts in ordinal key order. The dictionary is immutable and does not depend on the order of <paramref name="before"/>.</returns>
    public static IReadOnlyDictionary<string, int> Next(IReadOnlyDictionary<string, int> before, Verdict verdict)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(verdict);

        var tags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var error in verdict.Errors)
        {
            tags.Add(error.Tag);
        }

        if (!verdict.Communicated)
        {
            tags.Add(FeedbackPolicy.NotCommunicatedTag);
        }

        var counts = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            var seen = before.TryGetValue(tag, out var count) && count >= 1 ? count : 0;
            counts[tag] = seen == int.MaxValue ? seen : seen + 1;
        }

        return counts.ToImmutable();
    }
}
