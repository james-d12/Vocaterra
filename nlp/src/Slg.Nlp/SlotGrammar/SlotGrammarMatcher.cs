namespace Slg.Nlp.SlotGrammar;

using Slg.Core.Stages;
using Slg.Core.Verdicts;
using Slg.Nlp.Lexicon;

/// <summary>A successful grammar match: the grammar, the extracted slots and how many tokens the explicit grammar elements consumed.</summary>
public sealed record SlotGrammarMatch(SlotGrammar Grammar, IReadOnlyList<SlotValue> Slots, int ExplicitTokenCount)
{
    /// <summary>The slots as an ordinal-sorted map; the first occurrence of a slot name wins.</summary>
    public SlotMap ToSlotMap()
    {
        return SlotMap.From(Slots.Select(static slot => new KeyValuePair<string, string>(slot.Name, slot.Value)));
    }
}

/// <summary>Deterministic backtracking matcher of slot grammars against analyzed tokens, using lemmas and ignoring agreement.</summary>
public static class SlotGrammarMatcher
{
    private const int MaxSteps = 200_000;

    /// <summary>
    /// Matches the whole token sequence against the grammar, allowing a leading subject pronoun and a trailing <c>por favor</c>.
    /// Among all complete parses the one with the most tokens consumed by explicit grammar elements wins; ties go to the first parse found.
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
        return search.Best is { } best ? new SlotGrammarMatch(grammar, best.Slots, best.Score) : null;
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

    private sealed record Capture(SlotValue Value, Capture? Previous);

    private sealed record ParseResult(IReadOnlyList<SlotValue> Slots, int Score);

    private sealed class Search
    {
        private readonly IReadOnlyList<AnalyzedToken> _tokens;
        private int _steps;

        public Search(IReadOnlyList<AnalyzedToken> tokens)
        {
            _tokens = tokens;
        }

        public ParseResult? Best { get; private set; }

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
                        Step(rest, position + 1, captures, score + 1);
                    }

                    break;
                case TypedNode typed:
                    if (position < _tokens.Count && TypedMatching.TryMatch(_tokens[position], typed, out var value))
                    {
                        var next = typed.Slot is null
                            ? captures
                            : new Capture(new SlotValue(typed.Slot, value, _tokens[position].Token.Span), captures);
                        Step(rest, position + 1, next, score + 1);
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
                        Step(rest, position + 1, captures, score);
                    }

                    break;
                case DefaultPoliteNode:
                    if (position + 1 < _tokens.Count
                        && TypedMatching.MatchesLiteral(_tokens[position], "por")
                        && TypedMatching.MatchesLiteral(_tokens[position + 1], "favor"))
                    {
                        Step(rest, position + 2, captures, score);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unexpected grammar node {frame.Node.GetType().Name}.");
            }
        }

        private void Offer(Capture? captures, int score)
        {
            if (Best is not null && Best.Score >= score)
            {
                return;
            }

            var reversed = new List<SlotValue>();
            for (var capture = captures; capture is not null; capture = capture.Previous)
            {
                reversed.Add(capture.Value);
            }

            reversed.Reverse();
            var slots = new List<SlotValue>(reversed.Count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in reversed)
            {
                if (names.Add(slot.Name))
                {
                    slots.Add(slot);
                }
            }

            Best = new ParseResult(slots, score);
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

    public static bool TryMatch(AnalyzedToken token, TypedNode node, out string value)
    {
        foreach (var reading in token.Readings)
        {
            if (!MatchesPos(reading, node.Pos))
            {
                continue;
            }

            if (node.Lemma is not null
                && !string.Equals(TextFolding.StripAccents(reading.Lemma), TextFolding.StripAccents(node.Lemma), StringComparison.Ordinal))
            {
                continue;
            }

            value = reading.Lemma;
            return true;
        }

        value = string.Empty;
        return false;
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
