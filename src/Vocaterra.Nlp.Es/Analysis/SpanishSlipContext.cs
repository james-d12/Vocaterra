namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// M69 (M58's left point): where a Spanish word of another class is the learner's slip for an irregular verb's form without a slot grammar that names the verb
/// (<see cref="IAnalysisRules.CallsForSlip"/>). M58 gave such a word (<c>podo</c>, <c>pode</c>, <c>podes</c>, forms of <c>podar</c>; the pronoun <c>esto</c>) the
/// reading of the slip only under a grammar that matches nothing without it, so <c>yo podo ir al río</c> and <c>yo esto cansado</c> were clean. Two contexts call for the
/// slip, each where no reading of the word as written fits:
/// <list type="bullet">
/// <item>a slip of a modal verb (<c>poder</c>: <c>podo</c> for <c>puedo</c>) right before an infinitive (<c>yo podo ir</c>, <c>no podo ir</c>, <c>tú podes venir</c>,
/// <c>él pode venir</c>): <c>podar</c> takes no infinitive, and <c>yo podo el árbol</c> stays the verb it is;</item>
/// <item><c>esto</c> for <c>estoy</c>: after <c>yo</c> (past <c>no</c> or an adverb), before an adjective, <c>bien</c>, <c>mal</c>, an adverb of place or <c>en</c>
/// (<c>yo esto cansado</c>, <c>yo esto bien</c>, <c>yo esto en casa</c>), or opening the utterance before an adjective of the class that takes <c>estar</c>, past
/// <c>muy</c> (<c>esto cansado</c>, <c>esto muy contento</c>); in both, no word after it may be only a finite verb, so the demonstrative keeps its reading wherever
/// it has one (<c>esto es pan</c>, <c>yo esto no lo sé</c>, <c>quiero esto</c>, <c>esto de aquí es pan</c>, <c>esto bien</c>).</item>
/// </list>
/// </summary>
internal static class SpanishSlipContext
{
    public static bool CallsForSlip(IReadOnlyList<AnalyzedToken> tokens, int index, IReadOnlyList<LexicalFeatures> slip)
    {
        var verb = slip.FirstOrDefault(static r => r.Pos == LexiconSchema.Verb && r.Person is not null);
        if (verb is null)
        {
            return false;
        }

        if (SpanishAnalysisTables.IsModalVerb(verb.Lemma))
        {
            return index + 1 < tokens.Count && tokens[index + 1].Readings.Any(IsInfinitive);
        }

        return verb is { Lemma: "estar", Person: "1", Number: LexiconSchema.Singular } && CallsForEstoy(tokens, index);
    }

    private static bool CallsForEstoy(IReadOnlyList<AnalyzedToken> tokens, int index)
    {
        // A word after it that can only be a finite verb makes esto that verb's subject or object (esto es pan, yo esto no lo sé).
        for (var k = index + 1; k < tokens.Count; k++)
        {
            if (tokens[k].Readings.Count > 0 && tokens[k].Readings.All(ReadingClasses.IsFiniteVerb))
            {
                return false;
            }
        }

        var before = index - 1;
        while (before >= 0 && IsSkippableAdverb(tokens[before]))
        {
            before--;
        }

        var next = index + 1;
        if (next >= tokens.Count)
        {
            return false;
        }

        var adjective = next;
        while (adjective < tokens.Count - 1 && tokens[adjective].Readings.Count > 0 && tokens[adjective].Readings.All(static r => r is { Pos: LexiconSchema.Adverb, Lemma: "muy" }))
        {
            adjective++;
        }

        if (before >= 0 && tokens[before].Readings.Any(static r => r is { Pos: LexiconSchema.Pronoun, Lemma: "yo" }) && !(before > 0 && IsPreposition(tokens[before - 1])))
        {
            return (before == 0 || IsClauseBoundary(tokens[before - 1]))
                && (IsDescriptiveAdjective(tokens[adjective])
                    || TakesEstar(tokens[adjective])
                    || tokens[adjective].Readings.Any(static r => r.Pos == LexiconSchema.Adverb && r.Lemma is "bien" or "mal" or "aquí" or "allí" or "ahí" or "cerca" or "lejos")
                    || tokens[next].Token.Text is "en" or "con");
        }

        return before < 0 && index == 0 && TakesEstar(tokens[adjective]);
    }

    // An adjective of the class that takes estar (cansado, contento, enfermo, lleno), whatever else the word is (enfermo is also enfermar's first person).
    private static bool TakesEstar(AnalyzedToken token)
    {
        return token.Readings.Any(static r => r.Pos == LexiconSchema.Adjective && SpanishAnalysisTables.TakesEstar(r.Lemma));
    }

    private static bool IsInfinitive(LexicalFeatures reading)
    {
        return reading is { Pos: LexiconSchema.Verb, Person: null, Tense: null, Mood: null };
    }

    // An adjective that is no determiner adjective, and no noun or finite verb besides.
    private static bool IsDescriptiveAdjective(AnalyzedToken token)
    {
        return token.Readings.Any(static r => r.Pos == LexiconSchema.Adjective && !WordClasses.IsDeterminerAdjective(r.Lemma))
            && !token.Readings.Any(static r => r.Pos == LexiconSchema.Noun || ReadingClasses.IsFiniteVerb(r));
    }

    private static bool IsAdverb(AnalyzedToken token)
    {
        return token.Readings.Count > 0 && token.Readings.All(static r => r.Pos == LexiconSchema.Adverb);
    }

    // An adverb between yo and esto (no, también, ya): a word that is an adverb and no pronoun or verb (no is also a noun).
    private static bool IsSkippableAdverb(AnalyzedToken token)
    {
        return token.Readings.Any(static r => r.Pos == LexiconSchema.Adverb) && !token.Readings.Any(static r => r.Pos is LexiconSchema.Pronoun or LexiconSchema.Verb);
    }

    private static bool IsPreposition(AnalyzedToken token)
    {
        return token.Readings.Any(static r => r.Pos == LexiconSchema.Preposition);
    }

    // The start of a clause before yo: a conjunction (y, pero, porque), a question word or relative, an interjection (hola) or an adverb (sí, también).
    private static bool IsClauseBoundary(AnalyzedToken token)
    {
        return token.Readings.Any(static r => r.Pos is LexiconSchema.Conjunction or LexiconSchema.Interjection)
            || IsAdverb(token)
            || SpanishAnalysisTables.IsClauseOpener(token.Token.Text);
    }
}
