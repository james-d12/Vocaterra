using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

/// <summary>Finds the first noun or numeral of an analysis, the way keyword rules and the embedding rung fill slots.</summary>
internal static class AnalysisSlotFinder
{
    /// <summary>
    /// Returns the lemma of the first token with a noun reading, skipping excluded token indexes, the words of the language's trailing polite
    /// phrase (the <c>favor</c> of a <c>por favor</c>) and the noun of a phrase of elapsed time (M74: the <c>días</c> of <c>hace dos días</c>, whose
    /// <c>hace</c> the analysis read as the preposition); null when there is none.
    /// </summary>
    public static string? FirstNoun(AnalysisResult analysis, IReadOnlySet<int>? excluded, ILanguagePack language, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(rules);
        var tokens = analysis.Tokens;
        var elapsed = ElapsedTimeNouns(analysis, rules);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (excluded is not null && excluded.Contains(i))
            {
                continue;
            }

            if (IsPolitePhraseEnd(tokens, i, language) || elapsed.Contains(i))
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

    // The positions of the nouns of a phrase of elapsed time: after a word the analysis resolved as the preposition of elapsed time (hace, M59), the first noun of a
    // stretch of time that only numbers and words that can be numerals, articles or adjectives come before (hace dos días, hace un rato, hace mucho tiempo).
    private static HashSet<int> ElapsedTimeNouns(AnalysisResult analysis, IAnalysisRules rules)
    {
        var found = new HashSet<int>();
        var tokens = analysis.Tokens;
        if (analysis.Words is not { } words || words.Count != tokens.Count)
        {
            return found;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (words[i].Reading is not { Pos: LexiconSchema.Preposition } reading || !rules.IsElapsedTimePreposition(reading.Lemma))
            {
                continue;
            }

            for (var k = i + 1; k < tokens.Count; k++)
            {
                var next = tokens[k].Readings;
                if (next.Any(r => r.Pos == LexiconSchema.Noun && rules.IsElapsedTimeNoun(r.Lemma)))
                {
                    found.Add(k);
                    break;
                }

                var isDigits = tokens[k].Token.Text.Length > 0 && tokens[k].Token.Text.All(char.IsAsciiDigit);
                if (!isDigits && !next.Any(static r => r.Pos is LexiconSchema.Numeral or LexiconSchema.Article or LexiconSchema.Adjective))
                {
                    break;
                }
            }
        }

        return found;
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
