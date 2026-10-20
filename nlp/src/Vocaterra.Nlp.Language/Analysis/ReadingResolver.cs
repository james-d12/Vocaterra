namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// Chooses one word class for every token that the lexicon reads as several (a part-of-speech homograph such as <c>libro</c>, a noun and a form of
/// <c>librar</c>), so that the agreement checks run on one reading of each word instead of on all of them. Pure and deterministic.
/// </summary>
/// <remarks>
/// <para>
/// A word class is the part of speech of a reading, with finite verbs apart from infinitives and participles (see <see cref="WordClass"/>). The resolver removes
/// readings from each token in three steps. A step never removes the last reading of a token, so a rule that does not fit a word leaves it alone.
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Local syntax.</b> Rules over neighbouring words, repeated until nothing changes. They fire on a word only when every reading it still has is of the kind that
/// triggers them (an article, not <c>la</c> the clitic), and they narrow the neighbour only when one of its readings fits. After an article, numeral or possessive
/// come further determiners and adjectives and then the head of the phrase, which is a noun when the word can be one (<c>una buena lanza</c>); an adjective that does not
/// follow a noun is prenominal, so the word after it is a noun (<c>correcto paseo</c>); a quantifier that is also an adverb (<c>mucho</c>) before a noun is the
/// determiner, and so is the adjective-and-adverb or adjective-and-pronoun word before a noun that cannot be a verb (<c>temprano pasillo</c>), unless it is an adverb
/// that goes before nouns (<c>solo agua</c>); the word after a subject pronoun or a clitic, skipping adverbs and clitics, is a verb; the word after a preposition
/// and the complement of a copula are not finite verbs; an interjection reading is the last resort; and a determiner adjective beats an adjective of its own
/// spelling (<c>sus</c> beside <c>su</c>). These rules are what keeps the third step from hiding a real mistake: <c>un lanza</c> and <c>dos libro</c> are mistakes
/// because the word after the determiner can only be the noun, and <c>yo lanza</c> is a person mistake because the word after the pronoun can only be the verb.
/// </description></item>
/// <item><description>
/// <b>Grammar bindings.</b> When a slot grammar matched the whole utterance, every token was consumed by a grammar element, and that element names the reading:
/// <c>[NOUN:item]</c> keeps the noun readings, <c>[VERB:comprar]</c> the readings of that verb, the implicit leading subject its pronoun readings, a literal that matched
/// through a lemma (<c>querer</c> for <c>quiero</c>) the readings of that lemma, and a literal spelled like the word (<c>lanza</c>, <c>tardes</c>) nothing, because the
/// author wrote a form and not a reading. A word bound only to <c>[WORD]</c> elements is a name: it is a proper noun, which is no class the checks know, so it keeps
/// no reading at all and takes part in no agreement check, whatever the lexicon says about its spelling. A word that tied parses read in different ways (two optional elements that compete for it) keeps what any of them accepts.
/// The bindings come after the local rules, which then run once more: a slot grammar ignores agreement and accepts a modifier where a noun is needed, so where the
/// two disagree the neighbouring words win (<c>yo lanza</c> matches <c>[VERB:lanzar]? [ART]? [NOUN:spell]</c> with the noun, and is still a mistake of person).
/// </description></item>
/// <item><description>
/// <b>Charitable fallback.</b> The words that are still ambiguous are assigned the class combination that makes the checks report the fewest errors; the caller
/// supplies the cost of a reading, which is the number of errors and, for the analyzer, a penalty for a past-time adverb with no verb at all, so that a verb form
/// beside <c>ayer</c> is not read as a noun just to make its tense mistake disappear. Ties go to the combination that takes the earlier class in reading order. Up to <see cref="MaxExhaustiveCombinations"/> combinations are tried exhaustively in
/// lexicographic order; a longer sentence is settled by coordinate descent (word by word, left to right, at most <see cref="MaxDescentPasses"/> passes).
/// </description></item>
/// </list>
/// <para>
/// What the resolver cannot do: two verbs that share a form (<c>fue</c> is <c>ir</c> and <c>ser</c>) are one class, so a copula check still sees the copula; and a
/// sentence that is a mistake under one reading and correct under another, with nothing in the neighbouring words or in a grammar to prefer either
/// (<c>ayer … intenciones parte</c>, where <c>parte</c> is a noun or a verb), is read the charitable way.
/// </para>
/// </remarks>
internal static class ReadingResolver
{
    /// <summary>The most class combinations tried exhaustively for one sentence.</summary>
    public const int MaxExhaustiveCombinations = 256;

    /// <summary>The most passes of coordinate descent for a sentence with more combinations than <see cref="MaxExhaustiveCombinations"/>.</summary>
    public const int MaxDescentPasses = 4;

    private const int MaxRulePasses = 4;

    /// <summary>
    /// Returns the tokens with the readings narrowed to one word class each. A token that no step narrows keeps all its readings, and an unknown token (no readings)
    /// is returned as it is.
    /// </summary>
    /// <param name="tokens">The tokens with every lexicon reading.</param>
    /// <param name="match">The best slot grammar match over <paramref name="tokens"/>, or null when no grammar matched.</param>
    /// <param name="cost">The cost of a resolved reading of the whole sentence: the number of distinct errors the checks report for it, plus whatever the caller charges for a reading it finds implausible. The fallback takes the cheapest reading.</param>
    public static IReadOnlyList<AnalyzedToken> Resolve(
        IReadOnlyList<AnalyzedToken> tokens,
        SlotGrammarMatch? match,
        Func<IReadOnlyList<AnalyzedToken>, int> cost)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(cost);
        var state = new State(tokens);
        if (match is not null)
        {
            // Names keep no reading before any rule looks at them, so a name's lexicon readings cannot narrow its neighbours.
            state.ClearNames(match);
        }

        state.ApplyLocalSyntax();
        if (match is not null)
        {
            state.ApplyGrammar(match);
            state.ApplyLocalSyntax();
        }

        return state.ChooseCharitably(cost);
    }

    private sealed class State
    {
        private readonly IReadOnlyList<AnalyzedToken> _tokens;
        private readonly List<LexicalFeatures>[] _candidates;

        public State(IReadOnlyList<AnalyzedToken> tokens)
        {
            _tokens = tokens;
            _candidates = new List<LexicalFeatures>[tokens.Count];
            for (var i = 0; i < tokens.Count; i++)
            {
                _candidates[i] = [.. tokens[i].Readings];
            }
        }

        private int Count => _candidates.Length;

        // ---- Step 1: grammar bindings ----------------------------------------------------------------------------------------------

        public void ClearNames(SlotGrammarMatch match)
        {
            foreach (var index in match.WordTokens)
            {
                if (index >= 0 && index < Count)
                {
                    _candidates[index].Clear();
                }
            }
        }

        public void ApplyGrammar(SlotGrammarMatch match)
        {
            foreach (var binding in match.Bindings)
            {
                if (binding.TokenIndex < 0 || binding.TokenIndex >= Count)
                {
                    continue;
                }

                // A word the best parses read in different ways (two optional elements that compete for it) keeps every reading that one of them accepts.
                var nodes = binding.Nodes;
                if (nodes.All(static node => node is WordNode))
                {
                    continue;
                }

                var surface = TextFolding.StripAccents(_tokens[binding.TokenIndex].Token.Text);
                Narrow(binding.TokenIndex, r => nodes.Any(node => Accepts(node, r, surface)));
            }
        }

        private static bool Accepts(GrammarNode node, LexicalFeatures reading, string surface)
        {
            return node switch
            {
                TypedNode typed => TypedMatching.MatchesTyped(reading, typed),

                // A name in a tied parse: the word may be the name whatever its readings are, so the other tied elements decide.
                WordNode => true,

                // A literal spelled like the word (lanza, muestra, tardes) says nothing about its part of speech: the author wrote a form, not a reading. A literal that
                // matched through a lemma (querer for quiero) keeps the readings of that lemma.
                LiteralNode literal => string.Equals(surface, TextFolding.StripAccents(literal.Word), StringComparison.Ordinal) || TypedMatching.HasLemma(reading, literal.Word),
                DefaultSubjectNode => reading.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(reading.Lemma),
                _ => true,
            };
        }

        // ---- Step 2: local syntax --------------------------------------------------------------------------------------------------

        public void ApplyLocalSyntax()
        {
            for (var pass = 0; pass < MaxRulePasses; pass++)
            {
                var changed = false;
                for (var i = 0; i < Count; i++)
                {
                    changed |= ApplyRulesAt(i);
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        private bool ApplyRulesAt(int i)
        {
            var changed = false;
            var next = i + 1;

            // An interjection reading is the last resort: an imported lexicon lists sus and sé as interjections as well, and no check needs them.
            changed |= Narrow(i, static r => r.Pos != LexiconSchema.Interjection);

            // A determiner adjective that an imported lexicon also lists as an adjective of its own (sus beside su) is the determiner.
            if (All(i, static r => ReadingClasses.IsDeterminerReading(r) || r.Pos == LexiconSchema.Adjective))
            {
                changed |= Narrow(i, ReadingClasses.IsDeterminerReading);
            }

            // A noun phrase that starts with a determiner, or with an adjective that starts the utterance (buenos días), has a noun as its head.
            if (IsCertainDeterminer(i) || (i == 0 && IsCertainAdjective(i)))
            {
                changed |= ResolveNounPhraseHead(i);
            }

            // An adjective that does not follow a word that can be a noun (looking back over the adjectives before it) is prenominal, so a word after it that can be a
            // noun is one (correcto paseo, not correcto and a verb); behind a noun it is one of the adjectives of that noun (guapos prohibidos) and the next word is free.
            if (next < Count && IsCertainAdjective(i) && !FollowsNoun(i) && HasReading(next, static r => r.Pos == LexiconSchema.Noun))
            {
                changed |= Narrow(next, static r => r.Pos == LexiconSchema.Noun);
            }

            // A quantifier that is also an adverb (mucho, poco) before a noun, or before adjectives and a noun, is the determiner, because an adverb does not modify a
            // noun. The interrogative cuánto is left to the fallback when the next word can be a finite verb as well (cuánto cuesta: the adverb, then the verb;
            // cuánto dinero: the determiner).
            if (next < Count
                && HasReading(i, static r => r.Pos == LexiconSchema.Adjective && WordClasses.IsDeterminerAdjective(r.Lemma))
                && HasReading(i, static r => r.Pos == LexiconSchema.Adverb))
            {
                var head = next;
                while (head < Count && IsCertainAdjective(head))
                {
                    head++;
                }

                if (head < Count
                    && HasReading(head, static r => r.Pos == LexiconSchema.Noun)
                    && !(HasReading(i, static r => AnalysisTables.IsInterrogativeQuantifier(r.Lemma)) && HasReading(head, ReadingClasses.IsFiniteVerb)))
                {
                    changed |= Narrow(i, static r => r.Pos == LexiconSchema.Adjective);
                }
            }

            // A modifier before a noun: a word that is an adjective and also an adverb or a pronoun (temprano, ninguno), and never a noun or a verb, is the adjective
            // before a noun that cannot be a verb. An adverb does not modify a noun and a pronoun does not take one, except the adverbs of IsNounModifyingAdverb
            // (solo agua, más agua).
            if (next < Count
                && HasReading(i, static r => r.Pos == LexiconSchema.Adjective)
                && !HasReading(i, static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Verb)
                && !HasReading(i, static r => r.Pos == LexiconSchema.Adverb && AnalysisTables.IsNounModifyingAdverb(r.Lemma))
                && HasReading(next, static r => r.Pos == LexiconSchema.Noun)
                && !HasReading(next, ReadingClasses.IsFiniteVerb))
            {
                changed |= Narrow(i, static r => r.Pos == LexiconSchema.Adjective);
            }

            // After a subject pronoun or a clitic comes the verb (skipping adverbs and clitics).
            if (IsCertainSubjectPronoun(i) || IsCertainClitic(i))
            {
                var verb = NextContent(next);
                if (verb >= 0)
                {
                    changed |= Narrow(verb, static r => r.Pos == LexiconSchema.Verb);
                }
            }

            // A preposition does not govern a finite verb (a casa, de lanza).
            if (next < Count && IsCertainPreposition(i))
            {
                changed |= Narrow(next, static r => !ReadingClasses.IsFiniteVerb(r));
            }

            // A copula is followed by an adjective, a noun or an adverb, not by another finite verb.
            if (IsCertainCopula(i))
            {
                var complement = NextContent(next);
                if (complement >= 0)
                {
                    changed |= Narrow(complement, static r => !ReadingClasses.IsFiniteVerb(r));
                }
            }

            return changed;
        }

        // The words after a determiner: any further determiners and adjectives that cannot be anything else, and then the head. The head is the first word that is
        // not one of those, and it is a noun when it can be one (una buena lanza, los 18 arrestados puestos). A word that is a noun and an adjective (rojo, puestos)
        // is the head, as the checks have always taken it. A word that follows the determiner directly is at least a noun, an adjective or a determiner.
        private bool ResolveNounPhraseHead(int determiner)
        {
            var head = determiner + 1;
            while (head < Count && (IsCertainDeterminer(head) || IsCertainAdjective(head)))
            {
                head++;
            }

            if (head >= Count)
            {
                return false;
            }

            var changed = false;
            if (head == determiner + 1)
            {
                changed |= Narrow(head, static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective || ReadingClasses.IsDeterminerReading(r));
            }

            if (HasReading(head, static r => r.Pos == LexiconSchema.Noun))
            {
                changed |= Narrow(head, static r => r.Pos == LexiconSchema.Noun);
            }

            return changed;
        }

        // True when the word before the adjectives that end at index, or before the one at index, can be a noun.
        private bool FollowsNoun(int index)
        {
            var before = index - 1;
            while (before >= 0 && IsCertainAdjective(before))
            {
                before--;
            }

            return before >= 0 && HasReading(before, static r => r.Pos == LexiconSchema.Noun);
        }

        // An article, numeral or determiner adjective: the word opens a noun phrase.
        private bool IsCertainDeterminer(int index)
        {
            return All(index, ReadingClasses.IsDeterminerReading);
        }

        private bool IsCertainAdjective(int index)
        {
            return All(index, static r => r.Pos == LexiconSchema.Adjective);
        }

        private bool IsCertainSubjectPronoun(int index)
        {
            return All(index, static r => r.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(r.Lemma));
        }

        private bool IsCertainClitic(int index)
        {
            return All(index, static r => r.Pos == LexiconSchema.Pronoun && WordClasses.IsClitic(r.Lemma));
        }

        private bool IsCertainPreposition(int index)
        {
            return All(index, static r => r.Pos == LexiconSchema.Preposition);
        }

        private bool IsCertainCopula(int index)
        {
            return All(index, static r => ReadingClasses.IsFiniteVerb(r) && r.Lemma is "ser" or "estar")
                && HasReading(index, static r => r.Mood != LexiconSchema.Imperative);
        }

        private bool All(int index, Func<LexicalFeatures, bool> predicate)
        {
            var readings = _candidates[index];
            return readings.Count > 0 && readings.TrueForAll(r => predicate(r));
        }

        private bool HasReading(int index, Func<LexicalFeatures, bool> predicate)
        {
            return _candidates[index].Exists(r => predicate(r));
        }

        // The word the checks would take for the verb or complement after index - 1: the first one that is not an adverb, interjection or clitic.
        private int NextContent(int from)
        {
            var index = from;
            var skipped = 0;
            while (index < Count && skipped < ReadingClasses.MaxSkippedBetween && ReadingClasses.IsSkippable(_candidates[index]))
            {
                index++;
                skipped++;
            }

            return index < Count ? index : -1;
        }

        // Keeps the readings that satisfy the predicate when some do and some do not; returns true when it removed anything.
        private bool Narrow(int index, Func<LexicalFeatures, bool> keep)
        {
            var readings = _candidates[index];
            var kept = 0;
            foreach (var reading in readings)
            {
                if (keep(reading))
                {
                    kept++;
                }
            }

            if (kept == 0 || kept == readings.Count)
            {
                return false;
            }

            readings.RemoveAll(r => !keep(r));
            return true;
        }

        // ---- Step 3: charitable fallback -------------------------------------------------------------------------------------------

        public IReadOnlyList<AnalyzedToken> ChooseCharitably(Func<IReadOnlyList<AnalyzedToken>, int> cost)
        {
            var ambiguous = new List<int>();
            var options = new List<WordClass[]>();
            for (var i = 0; i < Count; i++)
            {
                var classes = DistinctClasses(_candidates[i]);
                if (classes.Length > 1)
                {
                    ambiguous.Add(i);
                    options.Add(classes);
                }
            }

            var fixedTokens = new AnalyzedToken[Count];
            for (var i = 0; i < Count; i++)
            {
                fixedTokens[i] = _candidates[i].Count == _tokens[i].Readings.Count ? _tokens[i] : new AnalyzedToken(_tokens[i].Token, [.. _candidates[i]]);
            }

            if (ambiguous.Count == 0)
            {
                return fixedTokens;
            }

            var search = new Search(this, fixedTokens, ambiguous, options, cost);
            return search.Run();
        }

        private static WordClass[] DistinctClasses(List<LexicalFeatures> readings)
        {
            var classes = new List<WordClass>(3);
            foreach (var reading in readings)
            {
                var wordClass = ReadingClasses.ClassOf(reading);
                if (!classes.Contains(wordClass))
                {
                    classes.Add(wordClass);
                }
            }

            return [.. classes];
        }

        public AnalyzedToken ReadingsOfClass(int index, WordClass wordClass)
        {
            var readings = _candidates[index].FindAll(r => ReadingClasses.ClassOf(r) == wordClass);
            return new AnalyzedToken(_tokens[index].Token, [.. readings]);
        }
    }

    private sealed class Search
    {
        private readonly State _state;
        private readonly AnalyzedToken[] _fixed;
        private readonly List<int> _ambiguous;
        private readonly List<WordClass[]> _options;
        private readonly Func<IReadOnlyList<AnalyzedToken>, int> _cost;

        public Search(State state, AnalyzedToken[] fixedTokens, List<int> ambiguous, List<WordClass[]> options, Func<IReadOnlyList<AnalyzedToken>, int> cost)
        {
            _state = state;
            _fixed = fixedTokens;
            _ambiguous = ambiguous;
            _options = options;
            _cost = cost;
        }

        public IReadOnlyList<AnalyzedToken> Run()
        {
            long combinations = 1;
            foreach (var option in _options)
            {
                combinations *= option.Length;
                if (combinations > MaxExhaustiveCombinations)
                {
                    break;
                }
            }

            var choice = combinations <= MaxExhaustiveCombinations ? Exhaustive() : Descent();
            return Build(choice);
        }

        private int[] Exhaustive()
        {
            var choice = new int[_ambiguous.Count];
            var best = (int[])choice.Clone();
            var bestCost = Cost(choice);
            while (Advance(choice))
            {
                var cost = Cost(choice);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = (int[])choice.Clone();
                }
            }

            return best;
        }

        // Odometer in lexicographic order: the last ambiguous token changes fastest. Returns false after the last combination.
        private bool Advance(int[] choice)
        {
            for (var k = choice.Length - 1; k >= 0; k--)
            {
                if (++choice[k] < _options[k].Length)
                {
                    return true;
                }

                choice[k] = 0;
            }

            return false;
        }

        private int[] Descent()
        {
            var choice = new int[_ambiguous.Count];
            var current = Cost(choice);
            for (var pass = 0; pass < MaxDescentPasses; pass++)
            {
                var changed = false;
                for (var k = 0; k < choice.Length; k++)
                {
                    var original = choice[k];
                    var bestAlternative = original;
                    var bestCost = current;
                    for (var alternative = 0; alternative < _options[k].Length; alternative++)
                    {
                        if (alternative == original)
                        {
                            continue;
                        }

                        choice[k] = alternative;
                        var cost = Cost(choice);
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestAlternative = alternative;
                        }
                    }

                    choice[k] = bestAlternative;
                    if (bestAlternative != original)
                    {
                        current = bestCost;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            return choice;
        }

        private int Cost(int[] choice)
        {
            return _cost(Build(choice));
        }

        private AnalyzedToken[] Build(int[] choice)
        {
            var tokens = (AnalyzedToken[])_fixed.Clone();
            for (var k = 0; k < _ambiguous.Count; k++)
            {
                tokens[_ambiguous[k]] = _state.ReadingsOfClass(_ambiguous[k], _options[k][choice[k]]);
            }

            return tokens;
        }
    }
}
