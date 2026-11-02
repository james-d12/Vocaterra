namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// Decides which words spelled without an accent are read as the accented word they may stand for (<c>esta</c> as <c>está</c>, <c>el</c> as <c>él</c>, <c>tu</c> as <c>tú</c>,
/// <c>se</c> as <c>sé</c>), from the words around them. Pure and deterministic; the reading resolver calls it first of all (see <see cref="ReadingResolver"/>), and
/// <see cref="AccentRestoration"/> calls it to name the accented spelling for a recast, so both always agree.
/// </summary>
/// <remarks>
/// <para>
/// The lexicon holds both spellings as words, and the word as typed wins by default. The accented readings are chosen only when (a) the word is a determiner right after
/// <c>dónde</c>, <c>donde</c> or <c>cómo</c> and has a present-indicative verb reading (<c>dónde esta el árbol</c>), or (b) no reading of the word as typed fits the words around it.
/// Which readings fit depends on the class: an article needs a word that can go on a noun phrase after it (a noun, an adjective, a determiner, a name or an unknown word);
/// a demonstrative or possessive needs, after its further adjectives and determiners, a noun, and never an article right after it; a conjunction or preposition needs a word
/// after it; a pronoun that stands before a verb (<c>te</c>, <c>se</c>) needs a verb after it, skipping other such pronouns, or a word that is a verb once accented
/// (<c>te estas</c>: <c>estás</c>, M59). Every other class (nouns, verbs, adjectives,
/// adverbs, other pronouns) always fits.
/// </para>
/// <para>
/// A decision looks at the readings the neighbours have so far, so a word that was turned into a verb frees the word before it from having a noun phrase to head
/// (<c>el esta aquí</c>: <c>está</c> is a verb, so <c>el</c> is <c>él</c>). A word that has been given its accented readings stays so, which is what makes the passes end.
/// </para>
/// </remarks>
internal sealed class AccentVariantChoice
{
    private const int MaxPasses = 3;

    private readonly IAnalysisRules _rules;

    public AccentVariantChoice(IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    /// <summary>
    /// For each token, the accented readings it takes, or null to keep the readings as typed.
    /// </summary>
    /// <param name="tokens">The tokens with the readings as typed (a name has none).</param>
    /// <param name="variants">For each token, the readings of the accented spellings, or none.</param>
    /// <param name="isDecided">True for a token whose reading as typed is settled (a slot grammar matched it), which is then kept.</param>
    public IReadOnlyList<LexicalFeatures>?[] Choose(
        IReadOnlyList<AnalyzedToken> tokens,
        IReadOnlyList<IReadOnlyList<LexicalFeatures>> variants,
        Func<int, bool> isDecided)
    {
        var chosen = new IReadOnlyList<LexicalFeatures>?[tokens.Count];
        var current = new IReadOnlyList<LexicalFeatures>[tokens.Count];
        var any = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            current[i] = tokens[i].Readings;
            any |= i < variants.Count && variants[i].Count > 0 && tokens[i].Readings.Count > 0;
        }

        if (!any)
        {
            return chosen;
        }

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var changed = false;
            var decisions = new IReadOnlyList<LexicalFeatures>?[tokens.Count];
            for (var i = 0; i < tokens.Count; i++)
            {
                if (chosen[i] is not null || i >= variants.Count || variants[i].Count == 0 || tokens[i].Readings.Count == 0 || isDecided(i))
                {
                    continue;
                }

                decisions[i] = ChosenFor(tokens, current, variants, i);
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                if (decisions[i] is { Count: > 0 } decided)
                {
                    chosen[i] = decided;
                    current[i] = decided;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return chosen;
    }

    // The accented readings the word takes, or none to keep the readings as typed.
    private IReadOnlyList<LexicalFeatures>? ChosenFor(
        IReadOnlyList<AnalyzedToken> tokens,
        IReadOnlyList<LexicalFeatures>[] current,
        IReadOnlyList<IReadOnlyList<LexicalFeatures>> allVariants,
        int index)
    {
        var variants = allVariants[index];
        var typed = tokens[index].Readings;
        if (typed.All(r => ReadingClasses.IsDeterminerReading(r, _rules)) && index > 0 && _rules.IsVerbSeekingQuestionWord(tokens[index - 1].Token.Text))
        {
            var verbs = variants.Where(r => ReadingClasses.IsFiniteVerb(r) && r.Mood == LexiconSchema.Indicative).ToList();
            if (verbs.Count > 0)
            {
                return verbs;
            }
        }

        if (typed.Any(ReadingClasses.IsFiniteVerb) && variants.Any(r => r.Pos is LexiconSchema.Adverb or LexiconSchema.Pronoun) && AsksAQuestion(current, index))
        {
            return variants;
        }

        // M69 (V20): the interrogative quantifier typed without its accent (cuanto, cuantos) that opens the utterance (after a greeting at most), before a word that can
        // be a finite verb or a noun and no adverb, asks the question (cuanto son todo, cuanto cuesta el pan, cuantos panes): a relative cuanto follows a word (todo
        // cuanto tengo), and cuanto más and cuanto antes keep it. The checks then read it as cuánto, and a recast restores the accent.
        if (OpensTheUtterance(current, index)
            && !typed.Any(r => _rules.IsInterrogativeQuantifier(r.Lemma))
            && variants.Any(r => _rules.IsInterrogativeQuantifier(r.Lemma))
            && index + 1 < current.Length
            && current[index + 1].Any(r => ReadingClasses.IsFiniteVerb(r) || r.Pos == LexiconSchema.Noun)
            && !current[index + 1].Any(r => r.Pos == LexiconSchema.Adverb))
        {
            return [.. variants.Where(r => _rules.IsInterrogativeQuantifier(r.Lemma))];
        }

        return typed.Any(r => FitsContext(current, allVariants, index, r)) ? null : variants;
    }

    // M69: nothing but interjections (hola, buenas) stands before the word at index.
    private static bool OpensTheUtterance(IReadOnlyList<LexicalFeatures>[] current, int index)
    {
        for (var k = 0; k < index; k++)
        {
            if (current[k].Count == 0 || !current[k].All(static r => r.Pos == LexiconSchema.Interjection))
            {
                return false;
            }
        }

        return true;
    }

    // A word that is a finite verb form as typed and has an interrogative sibling (como, cómo) is the question word when the next word starts a clause of its own and nothing
    // makes it the verb: a pronoun that stands before a verb and then the verb (como te llamas), or a finite verb that cannot be an object (como estás). It stays the verb right
    // after yo (yo como pan) and before a noun (como manzanas).
    private bool AsksAQuestion(IReadOnlyList<LexicalFeatures>[] current, int index)
    {
        if (index > 0 && current[index - 1].Any(r => r.Pos == LexiconSchema.Pronoun && r.Person == "1" && r.Number == LexiconSchema.Singular && _rules.IsSubjectPronoun(r.Lemma)))
        {
            return false;
        }

        var next = index + 1;
        if (next >= current.Length || current[next].Count == 0)
        {
            return false;
        }

        if (current[next].All(r => r.Pos == LexiconSchema.Pronoun && _rules.IsPreverbalPronoun(r.Lemma)))
        {
            return VerbFollows(current, next);
        }

        return current[next].Any(ReadingClasses.IsFiniteVerb)
            && !current[next].Any(r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective || ReadingClasses.IsDeterminerReading(r, _rules));
    }

    // Whether a reading of the word as typed fits the words around it. Open classes always do; the closed classes below need a neighbour.
    private bool FitsContext(IReadOnlyList<LexicalFeatures>[] current, IReadOnlyList<IReadOnlyList<LexicalFeatures>> variants, int index, LexicalFeatures reading)
    {
        var next = index + 1;
        if (ReadingClasses.IsDeterminerReading(reading, _rules))
        {
            return reading.Pos == LexiconSchema.Adjective ? FormsNounPhrase(current, next) : ContinuesNounPhrase(current, next);
        }

        if (reading.Pos is LexiconSchema.Conjunction or LexiconSchema.Preposition)
        {
            return next < current.Length;
        }

        // M59: the verb may be the accented spelling of the next word (te estas relacionado: estás), which the same pass gives it, so the pronoun keeps its reading as
        // typed instead of becoming the noun té before a verb.
        if (reading.Pos == LexiconSchema.Pronoun && _rules.IsPreverbalPronoun(reading.Lemma))
        {
            return VerbFollows(current, next) || VerbFollows(current, next, variants);
        }

        return true;
    }

    // An article is followed by a word that can go on a noun phrase: a noun, an adjective (el rojo) or a determiner; a name or an unknown word may be any.
    private bool ContinuesNounPhrase(IReadOnlyList<LexicalFeatures>[] current, int next)
    {
        return next < current.Length
            && (current[next].Count == 0 || current[next].Any(r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective || ReadingClasses.IsDeterminerReading(r, _rules)));
    }

    // A demonstrative or a possessive is followed, after further adjectives and determiners, by a noun (or a name); an article right after it is no noun phrase.
    private bool FormsNounPhrase(IReadOnlyList<LexicalFeatures>[] current, int next)
    {
        if (next < current.Length && current[next].Count > 0 && current[next].All(r => r.Pos == LexiconSchema.Article))
        {
            return false;
        }

        var head = next;
        while (head < current.Length && current[head].Count > 0 && current[head].All(r => r.Pos == LexiconSchema.Adjective || ReadingClasses.IsDeterminerReading(r, _rules)))
        {
            head++;
        }

        return head < current.Length && (current[head].Count == 0 || current[head].Any(r => r.Pos == LexiconSchema.Noun));
    }

    // A verb comes after the pronouns that stand before a verb (te lo doy), at most MaxSkippedBetween of them; with variants, a word whose accented spelling is a finite
    // verb counts as the verb (M59).
    private bool VerbFollows(IReadOnlyList<LexicalFeatures>[] current, int from, IReadOnlyList<IReadOnlyList<LexicalFeatures>>? variants = null)
    {
        var index = from;
        var skipped = 0;
        while (index < current.Length && skipped < ReadingClasses.MaxSkippedBetween && current[index].Count > 0
            && current[index].All(r => r.Pos == LexiconSchema.Pronoun && _rules.IsPreverbalPronoun(r.Lemma)))
        {
            index++;
            skipped++;
        }

        if (variants is not null)
        {
            return index < current.Length && index < variants.Count && variants[index].Any(ReadingClasses.IsFiniteVerb);
        }

        return index < current.Length && current[index].Any(r => r.Pos == LexiconSchema.Verb);
    }
}
