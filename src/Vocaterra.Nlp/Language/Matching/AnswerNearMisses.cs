using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>
/// Finds the near miss of a line that matched no answer (M75; the owner, 2026-11-02, D-game-22: "a near miss must name its word"): the line misses the answer phrase
/// nearest it by one word, and the two words are a slip pair, so the verdict's derived <c>vocab.wrong</c> can name the answer's word (<c>cuál es esto</c> for
/// <c>qué es esto</c>: <c>cuál</c>, expected <c>qué</c>).
/// </summary>
/// <remarks>
/// <para>
/// The nearest phrases are the canonical phrases and aliases of the set at the smallest distance from the line, measured as <see cref="AnswerMatcher"/> measures
/// it (the smaller of the spelling and the phonetic Damerau-Levenshtein distance of the matching keys of the whole texts). They are tried in the matcher's order
/// (ordinal answer id, the canonical phrase before the aliases, then ordinal phrase), and the first that the line misses by one slip names the word. The line
/// misses a phrase by one slip when the two have the same number of words, exactly one word differs (compared by matching key, so an accent alone is no
/// difference), and the two differing words are a <see cref="SlipPair"/>, tested in this order:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="SlipPair.Spelling"/>: close in spelling, the smaller of their spelling and phonetic distances within the answer word's own fuzzy
/// budget, <see cref="AnswerMatcher.MaxDistance"/> for its length, as the matcher takes a phrase's budget from the phrase;</description></item>
/// <item><description><see cref="SlipPair.QuestionWord"/>: both question words (<see cref="IAnalysisRules.IsQuestionWord"/>);</description></item>
/// <item><description><see cref="SlipPair.Article"/>, <see cref="SlipPair.Preposition"/> and <see cref="SlipPair.Pronoun"/>: both have a lexicon reading of that
/// part of speech (<see cref="LexiconSchema.Article"/>, <see cref="LexiconSchema.Preposition"/>, <see cref="LexiconSchema.Pronoun"/>); a pronoun pair needs a
/// reading with a person on each side (M97: a personal pronoun, <c>me</c> for <c>te</c>; <c>tal</c>, a pronoun with no person that stands in <c>qué tal</c> where
/// no personal pronoun does, is no slip of <c>me</c>: <c>que me</c> is no near miss of <c>qué tal</c>).</description></item>
/// </list>
/// <para>
/// So <c>quiero hacer un hacha</c> (an added word), <c>pasado mañana iré al río con Mar</c> and <c>rápido por favor</c> (content words far apart) are no near miss
/// of any answer, and a set with no answers has none (the PR #28 review's item 8: the matcher reports distance 0 when it has no phrase to measure). The near miss is
/// a fact about the words; whether the verdict tags it is <c>VerdictAssembler</c>'s rule. The arithmetic is in integers and every order is ordinal, so the result
/// is the same on every machine.
/// </para>
/// </remarks>
public static class AnswerNearMisses
{
    // The closed classes of the lexicon whose words are a slip pair for each other, in the order they are tested (after the question words).
    private static readonly (string Pos, SlipPair Pair)[] ClosedClasses =
    [
        (LexiconSchema.Article, SlipPair.Article),
        (LexiconSchema.Preposition, SlipPair.Preposition),
        (LexiconSchema.Pronoun, SlipPair.Pronoun),
    ];

    /// <summary>
    /// The near miss of the text against the answer set, or null when the nearest answer phrase is not one slip away. <paramref name="language"/> supplies the
    /// normalizer of the set's own phrases, the folding and the phonetic key; <paramref name="lexicon"/> the parts of speech of the two words and
    /// <paramref name="rules"/> the question words. <paramref name="options"/> normalizes the set's phrases and should equal the options used to normalize
    /// <paramref name="text"/> (default options when null). It does not look at whether the text matched: a caller asks only for a line that matched no answer.
    /// </summary>
    public static AnswerNearMiss? Find(
        NormalizedText text,
        AnswerSet set,
        Difficulty difficulty,
        ILanguagePack language,
        NlpLexicon lexicon,
        IAnalysisRules rules,
        NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(rules);

        var input = language.Folding.MatchingKey(text.Text);
        if (input.Length == 0 || text.Tokens.Count == 0)
        {
            return null;
        }

        var candidates = AnswerMatcher.BuildCandidates(set, language, options ?? NormalizationOptions.Default);
        var inputKey = language.PhoneticKey.Compute(input);
        var distances = new int[candidates.Count];
        var smallest = int.MaxValue;
        for (var i = 0; i < candidates.Count; i++)
        {
            var (spelling, phonetic) = AnswerMatcher.Distances(input, inputKey, candidates[i].Phrase, language.PhoneticKey);
            distances[i] = Math.Min(spelling, phonetic);
            smallest = Math.Min(smallest, distances[i]);
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (distances[i] == smallest && OneSlipAway(text, candidates[i], difficulty, language, lexicon, rules) is { } nearMiss)
            {
                return nearMiss;
            }
        }

        return null;
    }

    private static AnswerNearMiss? OneSlipAway(
        NormalizedText text,
        AnswerMatcher.Candidate candidate,
        Difficulty difficulty,
        ILanguagePack language,
        NlpLexicon lexicon,
        IAnalysisRules rules)
    {
        var phrase = candidate.Normalized;
        if (phrase.Tokens.Count != text.Tokens.Count)
        {
            return null;
        }

        var folding = language.Folding;
        var differing = -1;
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            if (string.Equals(folding.MatchingKey(text.Tokens[i].Text), folding.MatchingKey(phrase.Tokens[i].Text), StringComparison.Ordinal))
            {
                continue;
            }

            if (differing >= 0)
            {
                return null;
            }

            differing = i;
        }

        if (differing < 0)
        {
            return null;
        }

        var word = text.Tokens[differing];
        var answerWord = phrase.Tokens[differing];
        if (PairOf(word.Text, answerWord.Text, difficulty, language, lexicon, rules) is not { } pair)
        {
            return null;
        }

        return new AnswerNearMiss(candidate.AnswerId, phrase.Text, word.Span, word.Text, WordOf(answerWord, folding), pair);
    }

    private static SlipPair? PairOf(string word, string answerWord, Difficulty difficulty, ILanguagePack language, NlpLexicon lexicon, IAnalysisRules rules)
    {
        var folding = language.Folding;
        var key = folding.MatchingKey(word);
        var answerKey = folding.MatchingKey(answerWord);
        var (spelling, phonetic) = AnswerMatcher.Distances(key, language.PhoneticKey.Compute(key), answerKey, language.PhoneticKey);
        if (Math.Min(spelling, phonetic) <= AnswerMatcher.MaxDistance(answerKey.Length, difficulty))
        {
            return SlipPair.Spelling;
        }

        if (rules.IsQuestionWord(folding.Key(word)) && rules.IsQuestionWord(folding.Key(answerWord)))
        {
            return SlipPair.QuestionWord;
        }

        var readings = lexicon.Lookup(word);
        var answerReadings = lexicon.Lookup(answerWord);
        foreach (var (pos, pair) in ClosedClasses)
        {
            if (HasPartOfSpeech(readings, pos, pair) && HasPartOfSpeech(answerReadings, pos, pair))
            {
                return pair;
            }
        }

        return null;
    }

    private static bool HasPartOfSpeech(LexiconLookupResult readings, string pos, SlipPair pair)
    {
        foreach (var reading in readings)
        {
            if (string.Equals(reading.Pos, pos, StringComparison.Ordinal) && (pair != SlipPair.Pronoun || reading.Person is not null))
            {
                return true;
            }
        }

        return false;
    }

    // The word of a phrase token as it is said: a number the normalizer wrote in digits is the phrase's own word for it (tres, not 3), as AnswerCorrections says it.
    private static string WordOf(Token token, ILexiconFolding folding)
    {
        return token.Text.Length > 0 && token.Text.All(char.IsAsciiDigit) ? folding.Key(token.Original) : token.Text;
    }
}
