namespace Vocaterra.Nlp.Language.SlotGrammar;

using System.Runtime.CompilerServices;
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
    /// True when the grammar matched only over the readings of a learner's slip besides the lexicon's (M58: <c>costa</c> as <c>costar</c>), because it matched nothing
    /// without them. The analyzer prefers a match without them (see <see cref="LinguisticAnalyzer"/>).
    /// </summary>
    internal bool ReadsSlips { get; init; }

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

    /// <summary>How many of the consumed tokens the best parses bind to a literal word (a phrase of the grammar, not a class of word); public since M74.</summary>
    public int LiteralTokenCount => Bindings.Count(static b => b.Nodes.Any(static n => n is LiteralNode));

    /// <summary>
    /// How many of the consumed tokens the best parses bind to an element that names the word: a literal word or a verb of a named lemma (<c>[VERB:hacer]</c>), not
    /// a class of word alone (<c>[NOUN:item]</c>) or a name. The intent resolver compares it across intents (M74).
    /// </summary>
    public int NamedTokenCount => Bindings.Count(static b => b.Nodes.Any(static n => n is LiteralNode or TypedNode { Lemma: not null }));

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
        return Match(TokenFacts.Of(tokens, language, rules), grammar);
    }

    /// <summary>The same match over tokens whose facts are known (M74): a caller that matches many grammars over one analysis computes them once.</summary>
    internal static SlotGrammarMatch? Match(TokenFacts facts, SlotGrammar grammar) => facts.Matched(grammar);

    /// <summary>The search for the best parse alone, without the check of <see cref="TokenFacts.CanMatch"/> first (a test compares the two).</summary>
    internal static SlotGrammarMatch? SearchOnly(TokenFacts facts, SlotGrammar grammar)
    {
        var tokens = facts.Tokens;
        var defaults = facts.Language.GrammarDefaults;
        var typed = facts.Typed;
        var search = new Search(tokens, defaults.TrailingPoliteWords, typed);
        var nodes = new List<GrammarNode>(grammar.Nodes.Count + 2);
        if (defaults.OptionalSubjectPronoun)
        {
            nodes.Add(new OptionalNode(new DefaultSubjectNode()));
        }

        var polite = defaults.TrailingPoliteWords.Count > 0 ? defaults.PolitePositions : PolitePositions.None;
        if (polite.HasFlag(PolitePositions.Leading))
        {
            nodes.Add(new OptionalNode(new DefaultPoliteNode()));
        }

        var afterVerb = polite.HasFlag(PolitePositions.AfterFiniteVerb);
        foreach (var node in grammar.Nodes)
        {
            nodes.Add(node);
            if (afterVerb && node is TypedNode { Pos: LexiconSchema.Verb })
            {
                nodes.Add(new OptionalNode(new DefaultPoliteNode()));
                afterVerb = false;
            }
        }

        if (polite.HasFlag(PolitePositions.Trailing))
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
                    // M74: a group with a slot name ends with a marker that fills the slot from the tokens the alternative consumed.
                    var after = group.Slot is null ? rest : new Frame(new GroupSlotEndNode(group.Slot, position), rest);
                    foreach (var alternative in group.Alternatives)
                    {
                        Step(Frame.Prepend(alternative, after), position, captures, score);
                    }

                    break;
                case GroupSlotEndNode end:
                    Step(rest, position, GroupSlot(end, position, captures), score);
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

        // The slot of a group that consumed the tokens from end.Start to position: a capture that consumes no token (Position -1), or the captures unchanged when the
        // alternative consumed nothing. A literal gives its word as the grammar writes it; any other element the token as written in the normalized text.
        private Capture? GroupSlot(GroupSlotEndNode end, int position, Capture? captures)
        {
            if (position == end.Start)
            {
                return captures;
            }

            var words = new List<string>(position - end.Start);
            for (var capture = captures; capture is not null && words.Count < position - end.Start; capture = capture.Previous)
            {
                if (capture.Position >= end.Start)
                {
                    words.Add(capture.Node is LiteralNode literal ? literal.Word : _tokens[capture.Position].Token.Text);
                }
            }

            words.Reverse();
            var first = _tokens[end.Start].Token.Span;
            var last = _tokens[position - 1].Token.Span;
            var span = new TextSpan(first.Start, last.Start + last.Length - first.Start);
            return new Capture(-1, end, new SlotValue(end.Slot, string.Join(' ', words), span), captures);
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
                if (capture.Position < 0)
                {
                    // A group's slot (M74) consumes no token.
                    continue;
                }

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

/// <summary>
/// What the grammar matcher asks of each token of one analysis, worked out once (M74): the folded keys of its text and of its readings' lemmas, its readings' parts
/// of speech with their folded lemmas, and whether it can be a name. With them the matcher checks, before it searches, what every match of a grammar needs: the
/// pattern's own elements consume between their fewest and most tokens (the implicit subject pronoun one more, the polite phrase its words), and every element that
/// no optional element or alternative skips (a literal word, a typed element, a name) can take one of the tokens. A grammar that fails it matches nothing, so no
/// result changes; the intent resolver reads every line over the whole catalog, and this keeps that cheap.
/// </summary>
internal sealed class TokenFacts
{
    // The facts of the token lists matched lately, by reference: the analyzer matches every grammar of a prompt over one list, and the resolver every grammar of the
    // catalog. An entry is used only while the list still holds the same tokens (a list changed in place is worked out again), and goes with its list.
    private static readonly ConditionalWeakTable<IReadOnlyList<AnalyzedToken>, TokenFacts> Known = new();

    private readonly IAnalysisRules _rules;
    private readonly AnalyzedToken[] _snapshot;

    // Over all the tokens: the folded keys of their texts and of their readings' lemmas, their readings' parts of speech with the folded lemmas, their readings'
    // parts of speech (with the determiner of [DET]), and whether one of them can be a name.
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly HashSet<(string Pos, string Lemma)> _readings = [];
    private readonly HashSet<string> _positions = new(StringComparer.Ordinal);
    private readonly bool _name;

    private TokenFacts(IReadOnlyList<AnalyzedToken> tokens, ILanguagePack language, IAnalysisRules rules, TypedMatching typed)
    {
        _rules = rules;
        _snapshot = [.. tokens];
        Tokens = _snapshot;
        Language = language;
        Typed = typed;
        var folding = language.Folding;
        foreach (var token in _snapshot)
        {
            _keys.Add(folding.LenientKey(token.Token.Text));
            foreach (var reading in token.Readings)
            {
                var lemma = folding.LenientKey(reading.Lemma);
                _keys.Add(lemma);
                _readings.Add((reading.Pos, lemma));
                _positions.Add(reading.Pos);
                if (typed.IsDeterminer(reading))
                {
                    _positions.Add(TypedMatching.DeterminerPos);
                }
            }

            _name |= typed.CanBeName(token);
        }
    }

    /// <summary>The tokens.</summary>
    public IReadOnlyList<AnalyzedToken> Tokens { get; }

    /// <summary>The language whose grammar defaults and folding apply.</summary>
    public ILanguagePack Language { get; }

    /// <summary>The token-level matching rules of the language.</summary>
    public TypedMatching Typed { get; }

    /// <summary>The facts of the tokens under the language and its analysis rules.</summary>
    public static TokenFacts Of(IReadOnlyList<AnalyzedToken> tokens, ILanguagePack language, IAnalysisRules rules)
    {
        if (Known.TryGetValue(tokens, out var known) && ReferenceEquals(known.Language, language) && ReferenceEquals(known._rules, rules) && known.Holds(tokens))
        {
            return known;
        }

        var facts = new TokenFacts(tokens, language, rules, new TypedMatching(language.Folding, rules));
        Known.AddOrUpdate(tokens, facts);
        return facts;
    }

    // The matches worked out over these tokens, by grammar object: the resolver asks about the same grammars again for every prompt it reads one analysis under.
    private readonly Dictionary<SlotGrammar, SlotGrammarMatch?> _matched = new(ReferenceEqualityComparer.Instance);

    /// <summary>The best match of the grammar over the tokens, or null; worked out once per grammar (the search is a function of the facts and the grammar).</summary>
    public SlotGrammarMatch? Matched(SlotGrammar grammar)
    {
        lock (_matched)
        {
            if (!_matched.TryGetValue(grammar, out var match))
            {
                match = CanMatch(grammar) ? SlotGrammarMatcher.SearchOnly(this, grammar) : null;
                _matched.Add(grammar, match);
            }

            return match;
        }
    }

    /// <summary>False when the grammar cannot match the tokens (see the summary); true does not mean it does.</summary>
    public bool CanMatch(SlotGrammar grammar)
    {
        var defaults = Language.GrammarDefaults;
        var extra = (defaults.OptionalSubjectPronoun ? 1 : 0) + defaults.MaxPoliteTokens;
        if (_snapshot.Length < grammar.MinTokens || _snapshot.Length > grammar.MaxTokens + extra)
        {
            return false;
        }

        var elements = grammar.RequiredElements;
        var keys = grammar.RequiredKeys(Language.Folding);
        for (var e = 0; e < elements.Count; e++)
        {
            // Whether one of the tokens can be consumed by the element, by the rules of TypedMatching (MatchesLiteral, MatchesTyped, CanBeName).
            var taken = elements[e] switch
            {
                LiteralNode => _keys.Contains(keys[e]!),
                TypedNode { Lemma: not null, Pos: TypedMatching.DeterminerPos } typed => _snapshot.Any(token => token.Readings.Any(r => Typed.MatchesTyped(r, typed))),
                TypedNode { Lemma: not null } typed => _readings.Contains((typed.Pos, keys[e]!)),
                TypedNode typed => _positions.Contains(typed.Pos),
                WordNode => _name,
                _ => true,
            };
            if (!taken)
            {
                return false;
            }
        }

        return true;
    }

    // True when the list holds the very tokens these facts were worked out from.
    private bool Holds(IReadOnlyList<AnalyzedToken> tokens)
    {
        if (tokens.Count != _snapshot.Length)
        {
            return false;
        }

        for (var i = 0; i < _snapshot.Length; i++)
        {
            if (!ReferenceEquals(tokens[i], _snapshot[i]))
            {
                return false;
            }
        }

        return true;
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
            return IsDeterminer(reading);
        }

        return string.Equals(reading.Pos, pos, StringComparison.Ordinal);
    }

    /// <summary>True when the reading is one a <c>[DET]</c> element takes: an article, or a determiner adjective of the language.</summary>
    public bool IsDeterminer(LexicalFeatures reading) =>
        reading.Pos == LexiconSchema.Article || (reading.Pos == LexiconSchema.Adjective && _rules.IsDeterminerAdjective(reading.Lemma));
}
