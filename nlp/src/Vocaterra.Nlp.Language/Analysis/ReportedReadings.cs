namespace Vocaterra.Nlp.Language.Analysis;

using System.Collections.Frozen;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// Chooses the one reading of each word that <c>result.Words</c> reports (M29j). The resolver (see <see cref="ReadingResolver"/>) narrows a word to a word class
/// for the agreement checks, and the checks stay as they are; a host that shows the learner what a word is wants a more precise answer in three cases. Pure and
/// deterministic, and it changes no error.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><description>
/// <b>Mood.</b> A form that is both the imperative and the indicative (<c>está</c>, <c>habla</c>, <c>abre</c>) is reported as the imperative only when it opens its
/// clause: nothing precedes it but interjections (<c>¡Está quieto!</c>, <c>por favor abre la puerta</c>). Anywhere else, after a question word, a subject or an adverb, it is
/// the indicative (<c>¿dónde está el río?</c>). Even a clause-initial copula or auxiliary (<c>ser</c>, <c>estar</c>, <c>haber</c>) is the indicative when a noun phrase, a
/// participle or a gerund follows (<c>está el río</c>), because their imperative takes an adjective, an adverb or a preposition; and any clause-initial
/// verb is the indicative before a subject pronoun. The normalized text keeps no punctuation, so a question is recognized by its question word, which makes the verb non-initial.
/// </description></item>
/// <item><description>
/// <b>Interjection.</b> A word of <see cref="AnalysisTables.IsInterjectionFirst"/> that the lexicon lists as an interjection (<c>gracias</c>) is reported as the
/// interjection, also after a quantifier (<c>muchas gracias</c>), unless an article or a numeral precedes it (<c>las gracias</c>, <c>dos gracias</c>), where it is
/// the plural noun. The agreement checks still read the noun there (<c>mucho gracias</c> is corrected to <c>muchas</c>).
/// </description></item>
/// <item><description>
/// <b>Fixed expression.</b> Two or three adjacent words that the lexicon lists together as one form (<c>por favor</c>, <c>de nada</c>) get that entry's reading on every
/// token, with the expression as the lemma, so a host sees one unit; each token keeps its own span and text.
/// </description></item>
/// </list>
/// </remarks>
internal static class ReportedReadings
{
    private const int LongestFixedExpression = 3;

    private static readonly FrozenSet<string> CopulaLemmas = new[] { "ser", "estar", "haber" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The reading to report for every token; null for a name or an unknown word.</summary>
    public static LexicalFeatures?[] Choose(
        IReadOnlyList<Token> tokens,
        IReadOnlyList<AnalyzedToken> analyzed,
        IReadOnlyList<AnalyzedToken> resolved,
        IReadOnlySet<int> names,
        Lexicon lexicon)
    {
        var reported = new LexicalFeatures?[tokens.Count];
        var expression = new bool[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            if (names.Contains(i) || analyzed[i].Readings.Count == 0)
            {
                continue;
            }

            if (TryFixedExpression(tokens, analyzed, names, lexicon, i, out var length, out var reading))
            {
                for (var k = i; k < i + length; k++)
                {
                    reported[k] = reading;
                    expression[k] = true;
                }

                i += length - 1;
            }
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (reported[i] is null && !names.Contains(i) && analyzed[i].Readings.Count > 0)
            {
                reported[i] = ReadingOf(tokens, analyzed, resolved, expression, i);
            }
        }

        return reported;
    }

    private static bool TryFixedExpression(
        IReadOnlyList<Token> tokens,
        IReadOnlyList<AnalyzedToken> analyzed,
        IReadOnlySet<int> names,
        Lexicon lexicon,
        int start,
        out int length,
        out LexicalFeatures reading)
    {
        for (var count = Math.Min(LongestFixedExpression, tokens.Count - start); count >= 2; count--)
        {
            var covered = true;
            for (var k = start; k < start + count; k++)
            {
                covered &= !names.Contains(k) && analyzed[k].Readings.Count > 0;
            }

            if (!covered)
            {
                continue;
            }

            var form = string.Join(' ', Enumerable.Range(start, count).Select(k => tokens[k].Text));
            var found = lexicon.Lookup(form);
            if (found.Count > 0 && !found.IsAccentInsensitive)
            {
                length = count;
                reading = found[0];
                return true;
            }
        }

        length = 0;
        reading = null!;
        return false;
    }

    private static LexicalFeatures ReadingOf(
        IReadOnlyList<Token> tokens,
        IReadOnlyList<AnalyzedToken> analyzed,
        IReadOnlyList<AnalyzedToken> resolved,
        bool[] expression,
        int i)
    {
        var readings = resolved[i].Readings.Count > 0 ? resolved[i].Readings : analyzed[i].Readings;
        if (AnalysisTables.IsInterjectionFirst(tokens[i].Text)
            && !FollowsArticleOrNumeral(resolved, i)
            && analyzed[i].Readings.FirstOrDefault(static r => r.Pos == LexiconSchema.Interjection) is { } interjection)
        {
            return interjection;
        }

        var imperative = readings.Where(static r => IsImperative(r)).ToList();
        var indicative = readings.Where(static r => ReadingClasses.IsFiniteVerb(r) && !IsImperative(r)).ToList();
        if (imperative.Count > 0 && indicative.Count > 0 && indicative.Any(r => string.Equals(r.Lemma, imperative[0].Lemma, StringComparison.Ordinal)))
        {
            return OpensClause(analyzed, expression, i) && ImperativeFits(resolved, i, imperative[0].Lemma) ? imperative[0] : indicative[0];
        }

        return readings[0];
    }

    private static bool IsImperative(LexicalFeatures reading)
    {
        return ReadingClasses.IsFiniteVerb(reading) && string.Equals(reading.Mood, LexiconSchema.Imperative, StringComparison.Ordinal);
    }

    private static bool FollowsArticleOrNumeral(IReadOnlyList<AnalyzedToken> resolved, int i)
    {
        return i > 0 && resolved[i - 1].Readings.Count > 0 && resolved[i - 1].Readings.All(static r => r.Pos is LexiconSchema.Article or LexiconSchema.Numeral);
    }

    // Only interjections (or a fixed expression such as por favor) may come before an imperative.
    private static bool OpensClause(IReadOnlyList<AnalyzedToken> analyzed, bool[] expression, int i)
    {
        for (var k = 0; k < i; k++)
        {
            if (!expression[k] && !analyzed[k].Readings.All(static r => r.Pos == LexiconSchema.Interjection))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ImperativeFits(IReadOnlyList<AnalyzedToken> resolved, int i, string lemma)
    {
        var next = i + 1;
        if (next >= resolved.Count || resolved[next].Readings.Count == 0)
        {
            return true;
        }

        var after = resolved[next].Readings;
        if (after.All(static r => r.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(r.Lemma)))
        {
            return false;
        }

        if (!CopulaLemmas.Contains(lemma))
        {
            return true;
        }

        return !after.All(static r => ReadingClasses.IsDeterminerReading(r) || r.Pos == LexiconSchema.Noun || (r.Pos == LexiconSchema.Verb && r.Person is null));
    }
}
