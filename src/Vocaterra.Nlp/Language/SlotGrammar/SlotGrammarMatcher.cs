namespace Vocaterra.Nlp.Language.SlotGrammar;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

/// <summary>A successful grammar match: the grammar, the extracted slots and how many tokens the explicit grammar elements consumed.</summary>
public sealed record SlotGrammarMatch(SlotGrammar Grammar, IReadOnlyList<SlotValue> Slots, int ExplicitTokenCount)
{
    /// <summary>
    /// What the grammar says about each token it consumed, in token order: the typed elements or literals that matched it in the parses that tie for the best score
    /// (usually one; two optional elements that compete for a word give two). The linguistic analyzer uses this to read a word that has several parts of speech as
    /// the one the grammar needs (<c>[NOUN:item]</c> binds <c>libro</c> as a noun, not as a form of <c>librar</c>).
    /// </summary>
    internal IReadOnlyList<TokenBinding> Bindings { get; init; } = [];

    /// <summary>
    /// The positions of the tokens that the best parses read in different ways, because two elements of the grammar compete for the word (<c>[NOUN:item]?</c> beside
    /// <c>[VERB]?</c> over a word that is both). Empty for a grammar that reads every word one way. The slots are those of the first of the tied parses.
    /// </summary>
    public IReadOnlyList<int> AmbiguousTokens => [.. Bindings.Where(static b => b.Nodes.Count > 1).Select(static b => b.TokenIndex)];

    /// <summary>
    /// The positions of the tokens that every best parse binds only to <c>[WORD]</c> elements: names. The analyzer does not call them unknown words and the
    /// reading resolver gives them no word class for the agreement checks.
    /// </summary>
    internal IReadOnlySet<int> WordTokens => new HashSet<int>(Bindings.Where(static b => b.Nodes.All(static n => n is WordNode)).Select(static b => b.TokenIndex));

    /// <summary>How many of the consumed tokens the best parses bind to a literal word (a phrase of the grammar, not a class of word).</summary>
    internal int LiteralTokenCount => Bindings.Count(static b => b.Nodes.Any(static n => n is LiteralNode));

    /// <summary>
    /// True when <paramref name="candidate"/> beats <paramref name="best"/> among grammars: more tokens consumed by explicit elements, or on a tie more tokens
    /// bound to literal words (a phrase such as <c>gracias</c> is more specific than a bare noun, which the spell grammar would also take), and otherwise not.
    /// </summary>
    internal static bool Beats(SlotGrammarMatch candidate, SlotGrammarMatch best)
    {
        return candidate.ExplicitTokenCount > best.ExplicitTokenCount
            || (candidate.ExplicitTokenCount == best.ExplicitTokenCount && candidate.LiteralTokenCount > best.LiteralTokenCount);
    }

    /// <summary>The slots as an ordinal-sorted map; the first occurrence of a slot name wins.</summary>
    public SlotMap ToSlotMap()
    {
        return SlotMap.From(Slots.Select(static slot => new KeyValuePair<string, string>(slot.Name, slot.Value)));
    }
}

/// <summary>The grammar elements that consumed one token in the best parses: a typed element (<c>[NOUN:item]</c>), a literal word, or the implicit subject pronoun or <c>por favor</c>.</summary>
internal sealed record TokenBinding(int TokenIndex, IReadOnlyList<GrammarNode> Nodes);

/// <summary>Deterministic backtracking matcher of slot grammars against analyzed tokens, using lemmas and ignoring agreement.</summary>
public static class SlotGrammarMatcher
{
    private const int MaxSteps = 200_000;

    /// <summary>
    /// Matches the whole token sequence against the grammar, allowing a leading subject pronoun (when the language allows one) and a trailing polite
    /// phrase (the language's <see cref="GrammarDefaults.TrailingPoliteWords"/>, for Spanish <c>por favor</c>).
    /// Among all complete parses the one with the most tokens consumed by explicit grammar elements wins; ties go to the parse with more literal words, then to the first found.
    /// </summary>
    /// <param name="tokens">The analyzed tokens.</param>
    /// <param name="grammar">The grammar to match.</param>
    /// <param name="language">The language whose grammar defaults apply.</param>
    /// <remarks>
    /// The typed elements (<c>[DET]</c>, the implicit subject pronoun, <c>[WORD]</c> names) use the word classes of <see cref="AnalysisRules.For"/> for the pack: the
    /// rules of a pack created over an empty lexicon. Use the overload with explicit rules when the bundle's own rules are at hand.
    /// </remarks>
    public static SlotGrammarMatch? Match(IReadOnlyList<AnalyzedToken> tokens, SlotGrammar grammar, ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Match(tokens, grammar, language, AnalysisRules.For(language));
    }

    /// <summary>The same as <see cref="Match(IReadOnlyList{AnalyzedToken}, SlotGrammar, ILanguagePack)"/> with the analysis rules given (M34, additive).</summary>
    /// <param name="tokens">The analyzed tokens.</param>
    /// <param name="grammar">The grammar to match.</param>
    /// <param name="language">The language whose grammar defaults and folding apply.</param>
    /// <param name="rules">The language's analysis rules, for the word classes of typed elements.</param>
    public static SlotGrammarMatch? Match(IReadOnlyList<AnalyzedToken> tokens, SlotGrammar grammar, ILanguagePack language, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(grammar);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(rules);
        var defaults = language.GrammarDefaults;
        var search = new Search(tokens, defaults.TrailingPoliteWords, new TypedMatching(language.Folding, rules));
        var nodes = new List<GrammarNode>(grammar.Nodes.Count + 2);
        if (defaults.OptionalSubjectPronoun)
        {
            nodes.Add(new OptionalNode(new DefaultSubjectNode()));
        }

        nodes.AddRange(grammar.Nodes);
        if (defaults.TrailingPoliteWords.Count > 0)
        {
            nodes.Add(new OptionalNode(new DefaultPoliteNode()));
        }

        var frame = Frame.Prepend(nodes, null);
        search.Step(frame, 0, null, 0);
        return search.Best is { } best ? new SlotGrammarMatch(grammar, best.Slots, best.Score) { Bindings = search.Bindings } : null;
    }

    private sealed record Frame(GrammarNode Node, Frame? Next)
    {
        public static Frame? Prepend(IReadOnlyList<GrammarNode> nodes, Frame? rest)
        {
            var frame = rest;
            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                frame = new Frame(nodes[i], frame);
            }

            return frame;
        }
    }

    // One consumed token: the element that matched it and, for a typed element with a slot name, the slot value it filled.
    private sealed record Capture(int Position, GrammarNode Node, SlotValue? Value, Capture? Previous);

    private sealed record ParseResult(IReadOnlyList<SlotValue> Slots, int Score);

    private sealed class Search
    {
        private readonly IReadOnlyList<AnalyzedToken> _tokens;

        // The trailing polite phrase, word by word, and the literal node of each word (the captures of a parse name them).
        private readonly string[] _politeWords;
        private readonly GrammarNode[] _politeNodes;
        private readonly Dictionary<int, List<GrammarNode>> _alternatives = [];
        private int _steps;

        private readonly TypedMatching _typed;

        public Search(IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<string> politeWords, TypedMatching typed)
        {
            _tokens = tokens;
            _typed = typed;
            _politeWords = [.. politeWords];
            _politeNodes = [.. _politeWords.Select(static word => (GrammarNode)new LiteralNode(word))];
        }

        public ParseResult? Best { get; private set; }

        // The elements that matched each token position, over every complete parse that ties for the best score.
        public IReadOnlyList<TokenBinding> Bindings => [.. _alternatives.OrderBy(static pair => pair.Key).Select(static pair => new TokenBinding(pair.Key, pair.Value))];

        // True when the tokens from the position on read the trailing polite phrase word by word (the caller checked that they are there).
        private bool MatchesPolite(int position)
        {
            for (var k = 0; k < _politeWords.Length; k++)
            {
                if (!_typed.MatchesLiteral(_tokens[position + k], _politeWords[k]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Step(Frame? frame, int position, Capture? captures, int score)
        {
            if (++_steps > MaxSteps)
            {
                return;
            }

            if (frame is null)
            {
                if (position == _tokens.Count)
                {
                    Offer(captures, score);
                }

                return;
            }

            var rest = frame.Next;
            switch (frame.Node)
            {
                case LiteralNode literal:
                    if (position < _tokens.Count && _typed.MatchesLiteral(_tokens[position], literal.Word))
                    {
                        Step(rest, position + 1, new Capture(position, literal, null, captures), score + 1);
                    }

                    break;
                case TypedNode typed:
                    if (position < _tokens.Count && _typed.TryMatch(_tokens[position], typed, out var value))
                    {
                        var slot = typed.Slot is null ? null : new SlotValue(typed.Slot, value, _tokens[position].Token.Span);
                        Step(rest, position + 1, new Capture(position, typed, slot, captures), score + 1);
                    }

                    break;
                case WordNode word:
                    if (position < _tokens.Count && _typed.CanBeName(_tokens[position]))
                    {
                        // A name binds the token as written in the normalized text, not a lemma: there is no lexicon reading to take one from.
                        var slot = word.Slot is null ? null : new SlotValue(word.Slot, _tokens[position].Token.Text, _tokens[position].Token.Span);
                        Step(rest, position + 1, new Capture(position, word, slot, captures), score + 1);
                    }

                    break;
                case OptionalNode optional:
                    Step(new Frame(optional.Inner, rest), position, captures, score);
                    Step(rest, position, captures, score);
                    break;
                case GroupNode group:
                    foreach (var alternative in group.Alternatives)
                    {
                        Step(Frame.Prepend(alternative, rest), position, captures, score);
                    }

                    break;
                case DefaultSubjectNode:
                    if (position < _tokens.Count && _typed.IsSubjectPronoun(_tokens[position]))
                    {
                        Step(rest, position + 1, new Capture(position, frame.Node, null, captures), score);
                    }

                    break;
                case DefaultPoliteNode:
                    if (_politeWords.Length > 0 && position + _politeWords.Length <= _tokens.Count && MatchesPolite(position))
                    {
                        var politeCaptures = captures;
                        for (var k = 0; k < _politeWords.Length; k++)
                        {
                            politeCaptures = new Capture(position + k, _politeNodes[k], null, politeCaptures);
                        }

                        Step(rest, position + _politeWords.Length, politeCaptures, score);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unexpected grammar node {frame.Node.GetType().Name}.");
            }
        }

        private void Offer(Capture? captures, int score)
        {
            if (Best is not null && Best.Score > score)
            {
                return;
            }

            if (Best is not null && Best.Score == score)
            {
                // A tie keeps the first parse's slots, and adds what this parse says about each token.
                AddAlternatives(captures);
                return;
            }

            var reversed = new List<Capture>();
            for (var capture = captures; capture is not null; capture = capture.Previous)
            {
                reversed.Add(capture);
            }

            reversed.Reverse();
            var slots = new List<SlotValue>(reversed.Count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capture in reversed)
            {
                if (capture.Value is { } slot && names.Add(slot.Name))
                {
                    slots.Add(slot);
                }
            }

            Best = new ParseResult(slots, score);
            _alternatives.Clear();
            AddAlternatives(captures);
        }

        private void AddAlternatives(Capture? captures)
        {
            for (var capture = captures; capture is not null; capture = capture.Previous)
            {
                if (!_alternatives.TryGetValue(capture.Position, out var nodes))
                {
                    nodes = [];
                    _alternatives.Add(capture.Position, nodes);
                }

                if (!nodes.Contains(capture.Node))
                {
                    nodes.Add(capture.Node);
                }
            }
        }
    }
}

/// <summary>Token-level matching rules shared by the grammar matcher.</summary>
internal sealed class TypedMatching
{
    private readonly ILexiconFolding _folding;
    private readonly IAnalysisRules _rules;

    public TypedMatching(ILexiconFolding folding, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(folding);
        ArgumentNullException.ThrowIfNull(rules);
        _folding = folding;
        _rules = rules;
    }

    /// <summary>Pseudo part of speech for <c>[DET]</c>: an article or a determiner adjective (possessive, demonstrative, quantifier).</summary>
    public const string DeterminerPos = "det";

    public bool MatchesLiteral(AnalyzedToken token, string literal)
    {
        var folded = _folding.LenientKey(literal);
        if (string.Equals(_folding.LenientKey(token.Token.Text), folded, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var reading in token.Readings)
        {
            if (string.Equals(_folding.LenientKey(reading.Lemma), folded, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the token is made of letters only (<c>ñ</c> and accented vowels included): not a number, not punctuation, not empty.</summary>
    public bool IsWord(AnalyzedToken token)
    {
        var text = token.Token.Text;
        return text.Length > 0 && text.All(static c => char.IsLetter(c));
    }

    /// <summary>
    /// True when the token can be a <c>[WORD]</c> name: a word of letters that has no reading at all (a word the lexicon lacks) or at least one reading that is not
    /// of a closed class. A name carries no reading, so a word whose every reading is closed (<c>ayer</c>, <c>yo</c>, <c>su</c>) cannot be one: an optional name in
    /// front of a sentence would otherwise absorb a tense marker or a possessive and hide the mistake that it carries.
    /// </summary>
    public bool CanBeName(AnalyzedToken token)
    {
        return IsWord(token) && (token.Readings.Count == 0 || token.Readings.Any(r => !ReadingClasses.IsClosedClassReading(r, _rules)));
    }

    public bool TryMatch(AnalyzedToken token, TypedNode node, out string value)
    {
        foreach (var reading in token.Readings)
        {
            if (MatchesTyped(reading, node))
            {
                value = reading.Lemma;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>True when the reading has the part of speech of the typed element and, for <c>[VERB:lemma]</c>, that lemma (accents ignored).</summary>
    public bool MatchesTyped(LexicalFeatures reading, TypedNode node)
    {
        if (!MatchesPos(reading, node.Pos))
        {
            return false;
        }

        return node.Lemma is null
            || string.Equals(_folding.LenientKey(reading.Lemma), _folding.LenientKey(node.Lemma), StringComparison.Ordinal);
    }

    /// <summary>True when the reading's lemma is the literal word, accents ignored.</summary>
    public bool HasLemma(LexicalFeatures reading, string literal)
    {
        return string.Equals(_folding.LenientKey(reading.Lemma), _folding.LenientKey(literal), StringComparison.Ordinal);
    }

    public bool IsSubjectPronoun(AnalyzedToken token)
    {
        foreach (var reading in token.Readings)
        {
            if (reading.Pos == LexiconSchema.Pronoun && _rules.IsSubjectPronoun(reading.Lemma))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesPos(LexicalFeatures reading, string pos)
    {
        if (pos == DeterminerPos)
        {
            return reading.Pos == LexiconSchema.Article
                || (reading.Pos == LexiconSchema.Adjective && _rules.IsDeterminerAdjective(reading.Lemma));
        }

        return string.Equals(reading.Pos, pos, StringComparison.Ordinal);
    }
}
