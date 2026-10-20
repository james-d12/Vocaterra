namespace Vocaterra.Nlp.Language.SlotGrammar;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Lexicon;

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

    private static readonly LiteralNode PorLiteral = new("por");
    private static readonly LiteralNode FavorLiteral = new("favor");

    /// <summary>
    /// Matches the whole token sequence against the grammar, allowing a leading subject pronoun and a trailing <c>por favor</c>.
    /// Among all complete parses the one with the most tokens consumed by explicit grammar elements wins; ties go to the parse with more literal words, then to the first found.
    /// </summary>
    public static SlotGrammarMatch? Match(IReadOnlyList<AnalyzedToken> tokens, SlotGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(grammar);
        var search = new Search(tokens);
        var frame = Frame.Prepend(
            [new OptionalNode(new DefaultSubjectNode()), .. grammar.Nodes, new OptionalNode(new DefaultPoliteNode())],
            null);
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
        private readonly Dictionary<int, List<GrammarNode>> _alternatives = [];
        private int _steps;

        public Search(IReadOnlyList<AnalyzedToken> tokens)
        {
            _tokens = tokens;
        }

        public ParseResult? Best { get; private set; }

        // The elements that matched each token position, over every complete parse that ties for the best score.
        public IReadOnlyList<TokenBinding> Bindings => [.. _alternatives.OrderBy(static pair => pair.Key).Select(static pair => new TokenBinding(pair.Key, pair.Value))];

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
                    if (position < _tokens.Count && TypedMatching.MatchesLiteral(_tokens[position], literal.Word))
                    {
                        Step(rest, position + 1, new Capture(position, literal, null, captures), score + 1);
                    }

                    break;
                case TypedNode typed:
                    if (position < _tokens.Count && TypedMatching.TryMatch(_tokens[position], typed, out var value))
                    {
                        var slot = typed.Slot is null ? null : new SlotValue(typed.Slot, value, _tokens[position].Token.Span);
                        Step(rest, position + 1, new Capture(position, typed, slot, captures), score + 1);
                    }

                    break;
                case WordNode word:
                    if (position < _tokens.Count && TypedMatching.IsWord(_tokens[position]))
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
                    if (position < _tokens.Count && TypedMatching.IsSubjectPronoun(_tokens[position]))
                    {
                        Step(rest, position + 1, new Capture(position, frame.Node, null, captures), score);
                    }

                    break;
                case DefaultPoliteNode:
                    if (position + 1 < _tokens.Count
                        && TypedMatching.MatchesLiteral(_tokens[position], "por")
                        && TypedMatching.MatchesLiteral(_tokens[position + 1], "favor"))
                    {
                        Step(rest, position + 2, new Capture(position + 1, FavorLiteral, null, new Capture(position, PorLiteral, null, captures)), score);
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
internal static class TypedMatching
{
    /// <summary>Pseudo part of speech for <c>[DET]</c>: an article or a determiner adjective (possessive, demonstrative, quantifier).</summary>
    public const string DeterminerPos = "det";

    public static bool MatchesLiteral(AnalyzedToken token, string literal)
    {
        var folded = TextFolding.StripAccents(literal);
        if (string.Equals(TextFolding.StripAccents(token.Token.Text), folded, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var reading in token.Readings)
        {
            if (string.Equals(TextFolding.StripAccents(reading.Lemma), folded, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the token is made of letters only (<c>ñ</c> and accented vowels included): not a number, not punctuation, not empty.</summary>
    public static bool IsWord(AnalyzedToken token)
    {
        var text = token.Token.Text;
        return text.Length > 0 && text.All(static c => char.IsLetter(c));
    }

    public static bool TryMatch(AnalyzedToken token, TypedNode node, out string value)
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
    public static bool MatchesTyped(LexicalFeatures reading, TypedNode node)
    {
        if (!MatchesPos(reading, node.Pos))
        {
            return false;
        }

        return node.Lemma is null
            || string.Equals(TextFolding.StripAccents(reading.Lemma), TextFolding.StripAccents(node.Lemma), StringComparison.Ordinal);
    }

    /// <summary>True when the reading's lemma is the literal word, accents ignored.</summary>
    public static bool HasLemma(LexicalFeatures reading, string literal)
    {
        return string.Equals(TextFolding.StripAccents(reading.Lemma), TextFolding.StripAccents(literal), StringComparison.Ordinal);
    }

    public static bool IsSubjectPronoun(AnalyzedToken token)
    {
        foreach (var reading in token.Readings)
        {
            if (reading.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(reading.Lemma))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPos(LexicalFeatures reading, string pos)
    {
        if (pos == DeterminerPos)
        {
            return reading.Pos == LexiconSchema.Article
                || (reading.Pos == LexiconSchema.Adjective && WordClasses.IsDeterminerAdjective(reading.Lemma));
        }

        return string.Equals(reading.Pos, pos, StringComparison.Ordinal);
    }
}
