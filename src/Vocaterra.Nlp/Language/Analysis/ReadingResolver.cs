namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// Chooses one word class for every token that the lexicon reads as several (a part-of-speech homograph such as <c>libro</c>, a noun and a form of
/// <c>librar</c>), so that the agreement checks run on one reading of each word instead of on all of them. Pure and deterministic.
/// </summary>
/// <remarks>
/// <para>
/// A word class is the part of speech of a reading, with finite verbs apart from infinitives and participles (see <see cref="WordClass"/>). Before those steps, a
/// word that is spelled without an accent and is a lexicon form (<c>esta</c>) may be read as an accented spelling of the same letters (<c>está</c>); see below.
/// The resolver removes readings from each token in three steps. A step never removes the last reading of a token, so a rule that does not fit a word leaves it alone.
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Local syntax.</b> Rules over neighbouring words, repeated until nothing changes. They fire on a word only when every reading it still has is of the kind that
/// triggers them (an article, not <c>la</c> the clitic), and they narrow the neighbour only when one of its readings fits. After an article, numeral or possessive
/// come further determiners and adjectives and then the head of the phrase, which is a noun when the word can be one (<c>una buena lanza</c>); an adjective that does not
/// follow a noun is prenominal, so the word after it is a noun (<c>correcto paseo</c>); a quantifier that is also an adverb (<c>mucho</c>) before a noun is the
/// determiner, and so is the adjective-and-adverb or adjective-and-pronoun word before a noun that cannot be a verb (<c>temprano pasillo</c>), unless it is an adverb
/// that goes before nouns (<c>solo agua</c>); the word after a subject pronoun or a clitic, skipping adverbs and clitics, is a verb, and so is the word after
/// <c>dónde</c> or <c>cómo</c> when it can be one (<c>¿dónde son las casas?</c>, M56); the word after a preposition
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
/// <b>Accent variants (step 0).</b> A learner who types without accents writes <c>esta</c> for <c>está</c>, <c>el</c> for <c>él</c>, <c>tu</c> for <c>tú</c>, <c>se</c>
/// for <c>sé</c>, and the lexicon has both spellings as words. The analyzer hands the resolver the readings of the accented spellings next to those of the word as
/// typed, and <see cref="AccentVariantChoice"/> decides first, from the neighbouring words, which of the two sets the word has; the word as typed is the default, so a word
/// that the context does not contradict is read as spelled. A word that a slot grammar binds, and a name, keep the reading the grammar matched. Nothing is reported to the
/// learner for the choice: there is no accent error tag, and a recast restores the accent (<see cref="AccentRestoration"/>) as it does for a word the lexicon lacks as spelled (<c>arbol</c>).
/// </para>
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
    /// <param name="accentVariants">For each token, the readings of the accented spellings of a plain lexicon form, or none; the resolver replaces the readings of the token by them when the context calls for it (see the remarks).</param>
    /// <param name="rules">The language's analysis rules: its closed word classes and word lists (M34).</param>
    /// <param name="folding">The language's folding, which the resolver compares literals and surfaces with (M34).</param>
    public static IReadOnlyList<AnalyzedToken> Resolve(
        IReadOnlyList<AnalyzedToken> tokens,
        IReadOnlyList<IReadOnlyList<LexicalFeatures>> accentVariants,
        SlotGrammarMatch? match,
        Func<IReadOnlyList<AnalyzedToken>, int> cost,
        IAnalysisRules rules,
        ILexiconFolding folding)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(accentVariants);
        ArgumentNullException.ThrowIfNull(cost);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(folding);
        var state = new State(tokens, accentVariants, rules, folding);
        if (match is not null)
        {
            // Names keep no reading before any rule looks at them, so a name's lexicon readings cannot narrow its neighbours.
            state.ClearNames(match);
        }

        state.ChooseAccentVariants(match);

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
        private readonly IReadOnlyList<IReadOnlyList<LexicalFeatures>> _variants;
        private readonly List<LexicalFeatures>[] _candidates;
        private readonly IAnalysisRules _rules;
        private readonly ILexiconFolding _folding;
        private readonly TypedMatching _typed;

        public State(IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<IReadOnlyList<LexicalFeatures>> variants, IAnalysisRules rules, ILexiconFolding folding)
        {
            _tokens = tokens;
            _variants = variants;
            _rules = rules;
            _folding = folding;
            _typed = new TypedMatching(folding, rules);
            _candidates = new List<LexicalFeatures>[tokens.Count];
            for (var i = 0; i < tokens.Count; i++)
            {
                _candidates[i] = [.. tokens[i].Readings];
            }
        }

        private int Count => _candidates.Length;

        // ---- Step 0: accent variants -----------------------------------------------------------------------------------------------

        // Decides every token that has accent variants (see AccentVariantChoice) and gives it their readings in place of those as typed.
        public void ChooseAccentVariants(SlotGrammarMatch? match)
        {
            var chosen = new AccentVariantChoice(_rules).Choose(_tokens, _variants, index => GrammarBinds(match, index));
            for (var i = 0; i < Count; i++)
            {
                if (chosen[i] is { Count: > 0 } readings)
                {
                    _candidates[i].Clear();
                    _candidates[i].AddRange(readings);
                }
            }
        }

        // The slot grammar matched the word through one of its readings as typed, so its reading is decided.
        private static bool GrammarBinds(SlotGrammarMatch? match, int index)
        {
            return match is not null && match.Bindings.Any(b => b.TokenIndex == index && !b.Nodes.All(static node => node is WordNode));
        }

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

                var surface = _folding.LenientKey(_tokens[binding.TokenIndex].Token.Text);
                Narrow(binding.TokenIndex, r => nodes.Any(node => Accepts(node, r, surface)));
            }
        }

        private bool Accepts(GrammarNode node, LexicalFeatures reading, string surface)
        {
            return node switch
            {
                TypedNode typed => _typed.MatchesTyped(reading, typed),

                // A name in a tied parse: the word may be the name whatever its readings are, so the other tied elements decide.
                WordNode => true,

                // A literal spelled like the word (lanza, muestra, tardes) says nothing about its part of speech: the author wrote a form, not a reading. A literal that
                // matched through a lemma (querer for quiero) keeps the readings of that lemma.
                LiteralNode literal => string.Equals(surface, _folding.LenientKey(literal.Word), StringComparison.Ordinal) || _typed.HasLemma(reading, literal.Word),
                DefaultSubjectNode => reading.Pos == LexiconSchema.Pronoun && _rules.IsSubjectPronoun(reading.Lemma),
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
            changed |= Narrow(i, r => r.Pos != LexiconSchema.Interjection);

            // A determiner adjective that an imported lexicon also lists as an adjective of its own (sus beside su) is the determiner.
            if (All(i, r => ReadingClasses.IsDeterminerReading(r, _rules) || r.Pos == LexiconSchema.Adjective))
            {
                changed |= Narrow(i, IsDeterminer);
            }

            // A noun phrase that starts with a determiner, or with an adjective that starts the utterance (buenos días), has a noun as its head.
            if (IsCertainDeterminer(i) || (i == 0 && IsCertainAdjective(i)))
            {
                changed |= ResolveNounPhraseHead(i);
            }

            // An adjective that does not follow a word that can be a noun (looking back over the adjectives before it) is prenominal, so a word after it that can be a
            // noun is one (correcto paseo, not correcto and a verb); behind a noun it is one of the adjectives of that noun (guapos prohibidos) and the next word is free.
            if (next < Count && IsCertainAdjective(i) && !FollowsNoun(i) && HasReading(next, r => r.Pos == LexiconSchema.Noun))
            {
                changed |= Narrow(next, r => r.Pos == LexiconSchema.Noun);
            }

            // A quantifier that is also an adverb (mucho, poco) before a noun, or before adjectives and a noun, is the determiner, because an adverb does not modify a
            // noun. The interrogative cuánto is left to the fallback when the next word can be a finite verb as well (cuánto cuesta: the adverb, then the verb;
            // cuánto dinero: the determiner).
            if (next < Count
                && HasReading(i, r => r.Pos == LexiconSchema.Adjective && _rules.IsDeterminerAdjective(r.Lemma))
                && HasReading(i, r => r.Pos == LexiconSchema.Adverb))
            {
                var head = next;
                while (head < Count && IsCertainAdjective(head))
                {
                    head++;
                }

                if (head < Count
                    && HasReading(head, r => r.Pos == LexiconSchema.Noun)
                    && !(HasReading(i, r => _rules.IsInterrogativeQuantifier(r.Lemma)) && HasReading(head, ReadingClasses.IsFiniteVerb)))
                {
                    changed |= Narrow(i, r => r.Pos == LexiconSchema.Adjective);
                }
            }

            // A modifier before a noun: a word that is an adjective and also an adverb or a pronoun (temprano, ninguno), and never a noun or a verb, is the adjective
            // before a noun that cannot be a verb. An adverb does not modify a noun and a pronoun does not take one, except the adverbs of IsNounModifyingAdverb
            // (solo agua, más agua).
            if (next < Count
                && HasReading(i, r => r.Pos == LexiconSchema.Adjective)
                && !HasReading(i, r => r.Pos is LexiconSchema.Noun or LexiconSchema.Verb)
                && !HasReading(i, r => r.Pos == LexiconSchema.Adverb && _rules.IsNounModifyingAdverb(r.Lemma))
                && HasReading(next, r => r.Pos == LexiconSchema.Noun)
                && !HasReading(next, ReadingClasses.IsFiniteVerb))
            {
                changed |= Narrow(i, r => r.Pos == LexiconSchema.Adjective);
            }

            // After a subject pronoun or a clitic comes the verb (skipping adverbs and clitics).
            if (IsCertainSubjectPronoun(i) || IsCertainClitic(i))
            {
                var verb = NextContent(next);
                if (verb >= 0)
                {
                    changed |= Narrow(verb, r => r.Pos == LexiconSchema.Verb);
                }
            }

            // M56: a question word that takes a verb (dónde, cómo) is followed by the verb when the next word can be one: ¿dónde son las casas? is ser, not the noun son,
            // so that the ser/estar check sees it.
            if (next < Count && _rules.IsVerbSeekingQuestionWord(_tokens[i].Token.Text) && HasReading(next, ReadingClasses.IsFiniteVerb))
            {
                changed |= Narrow(next, ReadingClasses.IsFiniteVerb);
            }

            // A preposition does not govern a finite verb (a casa, de lanza).
            if (next < Count && IsCertainPreposition(i))
            {
                // M49: after "ir a" a word that is a finite verb and nothing but a conjunction or adverb besides (voy a como) keeps the verb, so that the near-future check
                // sees it; a word that can also be a noun, adjective or pronoun is still the noun phrase (voy a casa).
                if (IsIrA(i)
                    && HasReading(next, ReadingClasses.IsFiniteVerb)
                    && All(next, r => ReadingClasses.IsFiniteVerb(r) || r.Pos is LexiconSchema.Conjunction or LexiconSchema.Adverb))
                {
                    changed |= Narrow(next, ReadingClasses.IsFiniteVerb);
                }
                else
                {
                    changed |= Narrow(next, r => !ReadingClasses.IsFiniteVerb(r));
                }
            }

            // M51: a form of ir that is also a noun (van) right before a word that can only be an infinitive (van tener) is the verb, so that the missing-a check sees it.
            // After a determiner (la van) the noun phrase rules above have already decided.
            if (next < Count
                && !(i > 0 && IsCertainDeterminer(i - 1))
                && HasReading(i, r => ReadingClasses.IsFiniteVerb(r) && _rules.IsNearFutureAuxiliary(r.Lemma) && r.Mood != LexiconSchema.Imperative)
                && All(next, r => r.Pos == LexiconSchema.Verb && r.Person is null))
            {
                changed |= Narrow(i, r => ReadingClasses.IsFiniteVerb(r) && _rules.IsNearFutureAuxiliary(r.Lemma));
            }

            // A copula is followed by an adjective, a noun or an adverb, not by another finite verb.
            if (IsCertainCopula(i))
            {
                var complement = NextContent(next);
                if (complement >= 0)
                {
                    changed |= Narrow(complement, r => !ReadingClasses.IsFiniteVerb(r));
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
                changed |= Narrow(head, r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective || ReadingClasses.IsDeterminerReading(r, _rules));
            }

            if (HasReading(head, r => r.Pos == LexiconSchema.Noun))
            {
                changed |= Narrow(head, r => r.Pos == LexiconSchema.Noun);
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

            return before >= 0 && HasReading(before, r => r.Pos == LexiconSchema.Noun);
        }

        // An article, numeral or determiner adjective: the word opens a noun phrase.
        private bool IsCertainDeterminer(int index)
        {
            return All(index, IsDeterminer);
        }

        private bool IsDeterminer(LexicalFeatures reading)
        {
            return ReadingClasses.IsDeterminerReading(reading, _rules);
        }

        private bool IsCertainAdjective(int index)
        {
            return All(index, r => r.Pos == LexiconSchema.Adjective);
        }

        private bool IsCertainSubjectPronoun(int index)
        {
            return All(index, r => r.Pos == LexiconSchema.Pronoun && _rules.IsSubjectPronoun(r.Lemma));
        }

        private bool IsCertainClitic(int index)
        {
            return All(index, r => r.Pos == LexiconSchema.Pronoun && _rules.IsClitic(r.Lemma));
        }

        private bool IsCertainPreposition(int index)
        {
            return All(index, r => r.Pos == LexiconSchema.Preposition);
        }

        // The preposition at index is the a of "ir a": the word before it is a finite form of ir (voy, vamos, iba).
        private bool IsIrA(int index)
        {
            return index > 0
                && _rules.IsNearFutureLinker(_candidates[index][0].Lemma)
                && All(index - 1, r => ReadingClasses.IsFiniteVerb(r) && _rules.IsNearFutureAuxiliary(r.Lemma))
                && HasReading(index - 1, r => r.Mood != LexiconSchema.Imperative);
        }

        private bool IsCertainCopula(int index)
        {
            return All(index, r => ReadingClasses.IsFiniteVerb(r) && _rules.IsCopula(r.Lemma))
                && HasReading(index, r => r.Mood != LexiconSchema.Imperative);
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
            while (index < Count && skipped < ReadingClasses.MaxSkippedBetween && ReadingClasses.IsSkippable(_candidates[index], _rules))
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
                fixedTokens[i] = _candidates[i].SequenceEqual(_tokens[i].Readings) ? _tokens[i] : new AnalyzedToken(_tokens[i].Token, [.. _candidates[i]]);
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
