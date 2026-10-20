using Slg.Core.Stages;

namespace Slg.Nlp.Intent;

using Slg.Nlp.Lexicon;

/// <summary>Finds the first noun or numeral of an analysis, the way keyword rules and the embedding rung fill slots.</summary>
internal static class AnalysisSlotFinder
{
    /// <summary>
    /// Returns the lemma of the first token with a noun reading, skipping excluded token indexes and the <c>favor</c> of a
    /// <c>por favor</c>; null when there is none.
    /// </summary>
    public static string? FirstNoun(AnalysisResult analysis, IReadOnlySet<int>? excluded)
    {
        var tokens = analysis.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (excluded is not null && excluded.Contains(i))
            {
                continue;
            }

            if (IsPoliteFavor(tokens, i))
            {
                continue;
            }

            if (ReadingLemma(tokens[i], LexiconSchema.Noun) is { } lemma)
            {
                return lemma;
            }
        }

        return null;
    }

    /// <summary>Returns the lemma (a digit string) of the first token with a numeral reading, skipping excluded token indexes; null when there is none.</summary>
    public static string? FirstNumber(AnalysisResult analysis, IReadOnlySet<int>? excluded)
    {
        var tokens = analysis.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (excluded is not null && excluded.Contains(i))
            {
                continue;
            }

            if (ReadingLemma(tokens[i], LexiconSchema.Numeral) is { } lemma)
            {
                return lemma;
            }
        }

        return null;
    }

    private static bool IsPoliteFavor(IReadOnlyList<AnalyzedToken> tokens, int index)
    {
        return index > 0
            && string.Equals(TextFolding.StripAccents(tokens[index].Token.Text), "favor", StringComparison.Ordinal)
            && string.Equals(TextFolding.StripAccents(tokens[index - 1].Token.Text), "por", StringComparison.Ordinal);
    }

    private static string? ReadingLemma(AnalyzedToken token, string pos)
    {
        foreach (var reading in token.Readings)
        {
            if (string.Equals(reading.Pos, pos, StringComparison.Ordinal))
            {
                return reading.Lemma;
            }
        }

        return null;
    }
}
