using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>Gives the unknown words of a fuzzy answer match the canonical spelling of the answer, so a near miss is corrected to the answer.</summary>
/// <remarks>
/// After a <see cref="MatchType.Fuzzy"/> match to an answer, every <c>vocab.unknown</c> error that has no expected text gets a canonical word
/// of that answer as its expected text (<c>ezpada</c> on <c>espada</c>, <c>yelo</c> on <c>hielo</c>), so the recast says the answer. The
/// canonical phrase is the answer's own spelling, not an alias. Rule: a player's input of one word takes the whole canonical phrase
/// (<c>buenosdias</c> gives <c>buenos días</c>); otherwise each unknown word takes the canonical word it is nearest to, by accent-insensitive
/// Damerau-Levenshtein distance, and the earlier canonical word wins a tie (<c>una ezpada</c> gives <c>espada</c>, so the recast is
/// <c>una espada</c>). Words the lexicon knows are never changed, and exact and alias matches change nothing.
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

        var canonical = language.Normalizer.Normalize(answer.Canonical, options ?? NormalizationOptions.Default).Text;
        var words = canonical.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return analysis;
        }

        var changed = false;
        var errors = new List<ErrorTag>(analysis.Errors.Count);
        foreach (var error in analysis.Errors)
        {
            var replacement = error.Tag == ErrorTags.VocabUnknown && error.Expected.Length == 0
                ? ReplacementFor(error, text, words, canonical, language.Folding)
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

    private static string? ReplacementFor(ErrorTag error, NormalizedText text, string[] words, string canonical, ILexiconFolding folding)
    {
        if (!IsToken(text, error.Span))
        {
            return null;
        }

        var replacement = text.Tokens.Count == 1 ? canonical : NearestWord(error.Actual, words, folding);

        // A word the player already spelled as the answer is not a vocabulary error to correct.
        return string.Equals(replacement, error.Actual, StringComparison.Ordinal) ? null : replacement;
    }

    // The canonical word the spelling is nearest to, comparing accent-stripped spellings; the earlier word wins a tie.
    private static string NearestWord(string spelling, string[] words, ILexiconFolding folding)
    {
        var plain = folding.MatchingKey(spelling);
        var nearest = words[0];
        var nearestDistance = int.MaxValue;
        foreach (var word in words)
        {
            var distance = DamerauLevenshtein.Distance(plain, folding.MatchingKey(word));
            if (distance < nearestDistance)
            {
                nearest = word;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static bool IsToken(NormalizedText text, TextSpan span)
    {
        foreach (var token in text.Tokens)
        {
            if (token.Span == span)
            {
                return true;
            }
        }

        return false;
    }
}
