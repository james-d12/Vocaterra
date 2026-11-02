using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>Gives the unknown words of a fuzzy answer match the spelling of the answer's word at their place, so a near miss is corrected to the answer.</summary>
/// <remarks>
/// After a <see cref="MatchType.Fuzzy"/> match to an answer, every <c>vocab.unknown</c> error that has no expected text gets the word of that answer at its
/// place as its expected text (<c>ezpada</c> on <c>espada</c>, <c>yelo</c> on <c>hielo</c>), so the recast says the answer. Rule: a player's input of one word
/// takes the whole canonical phrase (<c>buenosdias</c> gives <c>buenos días</c>); otherwise the line is aligned word by word with the answer's phrase nearest it,
/// its canonical phrase or an alias (<see cref="AnswerAlignment"/>), and each unknown word takes the word it is aligned with, in its spelling with accents and
/// a number in words (<c>una ezpada</c> gives <c>espada</c>, <c>compré three manzanas</c> gives <c>tres</c>, <c>buen dia</c> gives <c>día</c> from the alias
/// <c>buen día</c>). An unknown word that the phrase has no word for (an added word), or whose word is already the word before or after it, gets none, so a
/// recast never says a word twice (M70, QA-language-6: before M70 each unknown word took the canonical word nearest its spelling wherever it stood, and
/// <c>compré three manzanas</c> was recast as <c>compré compré manzanas</c>). Words the lexicon knows are never changed, and exact and alias matches change nothing.
/// </remarks>
public static class AnswerCorrections
{
    /// <summary>Returns the analysis with the expected text of its unknown words set from the matched answer; the input is returned unchanged when nothing applies.</summary>
    public static AnalysisResult WithCanonicalWords(
        AnalysisResult analysis,
        NormalizedText text,
        MatchResult match,
        AnswerSet set,
        ILanguagePack language,
        NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(language);
        if (match.Type != MatchType.Fuzzy || match.AnswerId is null)
        {
            return analysis;
        }

        var answer = set.Answers.FirstOrDefault(a => string.Equals(a.AnswerId, match.AnswerId, StringComparison.Ordinal));
        if (answer is null)
        {
            return analysis;
        }

        var normalization = options ?? NormalizationOptions.Default;
        var canonical = language.Normalizer.Normalize(answer.Canonical, normalization).Text;
        var phrase = AnswerAlignment.NearestPhrase(text, match, set, language, normalization);
        if (canonical.Length == 0 || phrase is null)
        {
            return analysis;
        }

        var aligned = AnswerAlignment.Align(text.Tokens, phrase.Tokens, language.Folding);
        var changed = false;
        var errors = new List<ErrorTag>(analysis.Errors.Count);
        foreach (var error in analysis.Errors)
        {
            var replacement = error.Tag == ErrorTags.VocabUnknown && error.Expected.Length == 0
                ? ReplacementFor(error, text, canonical, phrase, aligned, language.Folding)
                : null;
            if (replacement is null)
            {
                errors.Add(error);
                continue;
            }

            errors.Add(error with { Expected = replacement });
            changed = true;
        }

        return changed ? analysis with { Errors = errors } : analysis;
    }

    private static string? ReplacementFor(ErrorTag error, NormalizedText text, string canonical, NormalizedText phrase, int[] aligned, ILexiconFolding folding)
    {
        var index = TokenIndex(text, error.Span);
        if (index < 0)
        {
            return null;
        }

        if (text.Tokens.Count == 1)
        {
            return string.Equals(canonical, error.Actual, StringComparison.Ordinal) ? null : canonical;
        }

        if (aligned[index] < 0)
        {
            return null;
        }

        var replacement = WordOf(phrase.Tokens[aligned[index]], folding);

        // A word the player already spelled as the answer is not a vocabulary error to correct, and a word that stands next to it already would be said twice.
        if (string.Equals(replacement, error.Actual, StringComparison.Ordinal)
            || (index > 0 && string.Equals(text.Tokens[index - 1].Text, replacement, StringComparison.Ordinal))
            || (index + 1 < text.Tokens.Count && string.Equals(text.Tokens[index + 1].Text, replacement, StringComparison.Ordinal)))
        {
            return null;
        }

        return replacement;
    }

    // The word of a phrase token as it is said: a number the normalizer wrote in digits is the phrase's own word for it (tres, not 3).
    private static string WordOf(Token token, ILexiconFolding folding)
    {
        return token.Text.Length > 0 && token.Text.All(char.IsAsciiDigit) ? folding.Key(token.Original) : token.Text;
    }

    private static int TokenIndex(NormalizedText text, TextSpan span)
    {
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            if (text.Tokens[i].Span == span)
            {
                return i;
            }
        }

        return -1;
    }
}
