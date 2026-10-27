using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

/// <summary>Finds the first noun or numeral of an analysis, the way keyword rules and the embedding rung fill slots.</summary>
internal static class AnalysisSlotFinder
{
    /// <summary>
    /// Returns the lemma of the first token with a noun reading, skipping excluded token indexes and the words of the language's trailing polite
    /// phrase (the <c>favor</c> of a <c>por favor</c>); null when there is none.
    /// </summary>
    public static string? FirstNoun(AnalysisResult analysis, IReadOnlySet<int>? excluded, ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var tokens = analysis.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (excluded is not null && excluded.Contains(i))
            {
                continue;
            }

            if (IsPolitePhraseEnd(tokens, i, language))
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

    // True when the words ending at the index are the language's trailing polite phrase (its last word is the token at the index).
    private static bool IsPolitePhraseEnd(IReadOnlyList<AnalyzedToken> tokens, int index, ILanguagePack language)
    {
        var phrase = language.GrammarDefaults.TrailingPoliteWords;
        if (phrase.Count == 0 || index < phrase.Count - 1)
        {
            return false;
        }

        var start = index - (phrase.Count - 1);
        for (var k = 0; k < phrase.Count; k++)
        {
            if (!string.Equals(language.Folding.LenientKey(tokens[start + k].Token.Text), phrase[k], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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
