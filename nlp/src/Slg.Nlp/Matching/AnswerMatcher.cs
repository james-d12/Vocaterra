using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Nlp.Normalization;

// System.IO.MatchType (implicit usings) would otherwise make the name ambiguous.
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Nlp.Matching;

/// <summary>
/// Matches normalized text against an <see cref="AnswerSet"/>: exact on a canonical phrase, then on an alias, then fuzzy.
/// </summary>
/// <remarks>
/// All comparisons ignore accents on both sides. The fuzzy stage compares against every canonical and alias phrase twice, once
/// as spelled and once as phonetic keys (see <see cref="SpanishPhoneticKey"/>), and takes the smaller Damerau-Levenshtein distance
/// <c>d</c>. The allowed distance is <c>floor(length / divisor)</c> with the phrase's length in characters and divisor 4, 5 or 6
/// for Beginner, Intermediate and Advanced. The decision runs through a <see cref="Margin"/>: <c>d</c> below the limit is a
/// confident fuzzy match (confidence <c>1 - d/length</c>), <c>d</c> exactly at the limit is uncertain (confidence 0.5), and
/// <c>d</c> above it is no match. Both fuzzy outcomes have type <see cref="MatchType.Fuzzy"/>; callers tell them apart by confidence.
/// The lowest distance wins; equal distances go to the ordinally smallest answer id.
/// </remarks>
public static class AnswerMatcher
{
    /// <summary>Confidence reported for a fuzzy match whose distance is exactly at the limit.</summary>
    public const double UncertainConfidence = 0.5;

    // Half a character: integer distances then land strictly Accept below the limit, Uncertain on it and Reject above it.
    private const double LimitEpsilon = 0.5;

    /// <summary>
    /// Matches the text against the answer set. <paramref name="options"/> normalizes the set's own phrases and should equal
    /// the options used to normalize <paramref name="text"/> (default options when null).
    /// </summary>
    /// <returns>
    /// The match. For <see cref="MatchType.None"/> the answer id is null, confidence is 0 and the distance is the smallest one found.
    /// For fuzzy matches the distance is the smaller of the spelling and phonetic distances, so a spelling variant that sounds
    /// identical (for example <c>baca</c> for <c>vaca</c>) has distance 0 and <c>LikelyPronunciation</c> set.
    /// </returns>
    public static MatchResult Match(
        NormalizedText text,
        AnswerSet set,
        Difficulty difficulty,
        NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(set);
        var divisor = Divisor(difficulty);

        var input = Accents.Strip(text.Text);
        var candidates = BuildCandidates(set, options ?? NormalizationOptions.Default);

        if (input.Length > 0)
        {
            foreach (var candidate in candidates)
            {
                if (!candidate.IsAlias && string.Equals(candidate.Phrase, input, StringComparison.Ordinal))
                {
                    return new MatchResult(candidate.AnswerId, MatchType.Exact, 0, false, 1.0);
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate.IsAlias && string.Equals(candidate.Phrase, input, StringComparison.Ordinal))
                {
                    return new MatchResult(candidate.AnswerId, MatchType.Alias, 0, false, 1.0);
                }
            }
        }

        return MatchFuzzy(input, candidates, divisor);
    }

    /// <summary>Returns the largest edit distance that still counts as a fuzzy match for a phrase of the given length.</summary>
    public static int MaxDistance(int length, Difficulty difficulty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return length / Divisor(difficulty);
    }

    private static MatchResult MatchFuzzy(string input, List<Candidate> candidates, int divisor)
    {
        var inputKey = SpanishPhoneticKey.Compute(input);
        Candidate? best = null;
        var bestDistance = 0;
        var bestPhonetic = 0;
        var bestSpelling = 0;
        var smallest = int.MaxValue;

        // Candidates are sorted by ordinal answer id, so keeping the first of equal distances breaks ties by id.
        foreach (var candidate in candidates)
        {
            var spelling = DamerauLevenshtein.Distance(input, candidate.Phrase);
            var phonetic = DamerauLevenshtein.Distance(inputKey, SpanishPhoneticKey.Compute(candidate.Phrase));
            var distance = Math.Min(spelling, phonetic);
            smallest = Math.Min(smallest, distance);

            var limit = candidate.Phrase.Length / divisor;
            if (Decide(distance, limit) == MarginDecision.Reject)
            {
                continue;
            }

            if (best is null || distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
                bestSpelling = spelling;
                bestPhonetic = phonetic;
            }
        }

        if (best is not { } winner)
        {
            return new MatchResult(null, MatchType.None, smallest == int.MaxValue ? 0 : smallest, false, 0.0);
        }

        var decision = Decide(bestDistance, winner.Phrase.Length / divisor);
        var confidence = decision == MarginDecision.Uncertain
            ? UncertainConfidence
            : 1.0 - ((double)bestDistance / winner.Phrase.Length);
        var likelyPronunciation = bestPhonetic == 0 && bestSpelling > 0;
        return new MatchResult(winner.AnswerId, MatchType.Fuzzy, bestDistance, likelyPronunciation, confidence);
    }

    // Lower distance is better, so the score is the negated distance and the threshold the negated limit.
    private static MarginDecision Decide(int distance, int limit)
    {
        return new Margin(-limit, LimitEpsilon).Evaluate(-distance);
    }

    private static int Divisor(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Beginner => 4,
            Difficulty.Intermediate => 5,
            Difficulty.Advanced => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty."),
        };
    }

    private static List<Candidate> BuildCandidates(AnswerSet set, NormalizationOptions options)
    {
        var candidates = new List<Candidate>();
        foreach (var answer in set.Answers)
        {
            AddCandidate(candidates, answer.AnswerId, answer.Canonical, isAlias: false, options);
            foreach (var alias in answer.Aliases)
            {
                AddCandidate(candidates, answer.AnswerId, alias, isAlias: true, options);
            }
        }

        // Stable sort: ordinal answer id, canonical before aliases, then ordinal phrase.
        return candidates
            .OrderBy(static c => c.AnswerId, StringComparer.Ordinal)
            .ThenBy(static c => c.IsAlias)
            .ThenBy(static c => c.Phrase, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddCandidate(List<Candidate> candidates, string answerId, string raw, bool isAlias, NormalizationOptions options)
    {
        var phrase = Accents.Strip(TextNormalizer.Normalize(raw, options).Text);
        if (phrase.Length > 0)
        {
            candidates.Add(new Candidate(answerId, phrase, isAlias));
        }
    }

    private readonly record struct Candidate(string AnswerId, string Phrase, bool IsAlias);
}
