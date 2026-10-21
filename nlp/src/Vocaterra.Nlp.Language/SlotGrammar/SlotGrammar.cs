using System.Text;

namespace Vocaterra.Nlp.Language.SlotGrammar;

using Vocaterra.Nlp.Language.Lexicon;

/// <summary>A parsed slot grammar pattern such as <c>[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]</c>.</summary>
public sealed class SlotGrammar
{
    private SlotGrammar(string id, string pattern, IReadOnlyList<GrammarNode> nodes)
    {
        Id = id;
        Pattern = pattern;
        Nodes = nodes;
    }

    /// <summary>The grammar identifier reported as <c>MatchedGrammarId</c>; defaults to the canonical pattern text.</summary>
    public string Id { get; }

    /// <summary>The canonical pattern text (single spaces, upper-case tags, lower-case literals).</summary>
    public string Pattern { get; }

    internal IReadOnlyList<GrammarNode> Nodes { get; }

    /// <summary>
    /// Parses a pattern. Tokens: literal words; <c>[ART]</c>, <c>[PRON]</c>, <c>[DET]</c>, <c>[NUM:slot]</c>, <c>[NOUN:slot]</c>,
    /// <c>[ADJ:slot]</c>, <c>[ADV]</c>, <c>[PREP]</c>, <c>[CONJ]</c>, <c>[INTERJ]</c> (each with an optional slot), <c>[VERB]</c>,
    /// <c>[VERB:lemma]</c> and <c>[VERB:lemma:slot]</c>, <c>[WORD]</c> and <c>[WORD:slot]</c> (one token of letters that is not only closed-class words; a name); a <c>?</c>
    /// suffix makes an element optional; <c>(a|b c)</c> lists alternatives.
    /// </summary>
    public static SlotGrammar Parse(string pattern, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var nodes = new SlotGrammarParser(pattern).Parse();
        var canonical = Render(nodes);
        return new SlotGrammar(id ?? canonical, canonical, nodes);
    }

    /// <summary>The literal words of the pattern (including those inside alternatives), accent-folded and lower-case, distinct, in ordinal order.</summary>
    public IReadOnlyList<string> LiteralWords()
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        Walk(Nodes, node =>
        {
            if (node is LiteralNode literal)
            {
                words.Add(literal.Word);
            }
        });
        return [.. words];
    }

    /// <summary>The lemmas named by <c>[VERB:lemma]</c> elements, distinct, in ordinal order.</summary>
    public IReadOnlyList<string> VerbLemmas()
    {
        var lemmas = new SortedSet<string>(StringComparer.Ordinal);
        Walk(Nodes, node =>
        {
            if (node is TypedNode { Lemma: { } lemma })
            {
                lemmas.Add(lemma);
            }
        });
        return [.. lemmas];
    }

    /// <summary>
    /// Describes every optional <c>[WORD]</c> element that sits next to another optional element in the same sequence (before or after it), as text such as
    /// <c>[WORD:item]? next to [ART]?</c>; empty when there is none. An optional name beside an optional word can absorb a word the author meant for the
    /// other element or for no element at all (<c>ayer</c> read as the item), so the content lint reports it.
    /// </summary>
    public IReadOnlyList<string> OptionalWordNeighbours()
    {
        var found = new List<string>();
        FindOptionalWordNeighbours(Nodes, found);
        return found;
    }

    private static void FindOptionalWordNeighbours(IReadOnlyList<GrammarNode> sequence, List<string> found)
    {
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] is OptionalNode { Inner: WordNode })
            {
                foreach (var j in new[] { i - 1, i + 1 })
                {
                    if (j >= 0 && j < sequence.Count && sequence[j] is OptionalNode)
                    {
                        found.Add($"{Render([sequence[i]])} next to {Render([sequence[j]])}");
                    }
                }
            }

            switch (sequence[i])
            {
                case OptionalNode { Inner: GroupNode inner }:
                    foreach (var alternative in inner.Alternatives)
                    {
                        FindOptionalWordNeighbours(alternative, found);
                    }

                    break;
                case GroupNode group:
                    foreach (var alternative in group.Alternatives)
                    {
                        FindOptionalWordNeighbours(alternative, found);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static void Walk(IReadOnlyList<GrammarNode> nodes, Action<GrammarNode> visit)
    {
        foreach (var node in nodes)
        {
            visit(node);
            switch (node)
            {
                case OptionalNode optional:
                    Walk([optional.Inner], visit);
                    break;
                case GroupNode group:
                    foreach (var alternative in group.Alternatives)
                    {
                        Walk(alternative, visit);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Returns the canonical pattern text.</summary>
    public override string ToString()
    {
        return Pattern;
    }

    private static string Render(IReadOnlyList<GrammarNode> nodes)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            Render(builder, nodes[i]);
        }

        return builder.ToString();
    }

    private static void Render(StringBuilder builder, GrammarNode node)
    {
        switch (node)
        {
            case LiteralNode literal:
                builder.Append(literal.Word);
                break;
            case TypedNode typed:
                builder.Append('[').Append(typed.Tag);
                if (typed.Lemma is not null)
                {
                    builder.Append(':').Append(typed.Lemma);
                }

                if (typed.Slot is not null)
                {
                    builder.Append(':').Append(typed.Slot);
                }

                builder.Append(']');
                break;
            case WordNode word:
                builder.Append("[WORD");
                if (word.Slot is not null)
                {
                    builder.Append(':').Append(word.Slot);
                }

                builder.Append(']');
                break;
            case OptionalNode optional:
                Render(builder, optional.Inner);
                builder.Append('?');
                break;
            case GroupNode group:
                builder.Append('(');
                for (var i = 0; i < group.Alternatives.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append('|');
                    }

                    builder.Append(Render(group.Alternatives[i]));
                }

                builder.Append(')');
                break;
            default:
                throw new InvalidOperationException($"Unexpected grammar node {node.GetType().Name}.");
        }
    }
}

/// <summary>Recursive-descent parser for the slot grammar DSL.</summary>
internal sealed class SlotGrammarParser
{
    private static readonly Dictionary<string, string> TagToPos = new(StringComparer.Ordinal)
    {
        ["ART"] = LexiconSchema.Article,
        ["PRON"] = LexiconSchema.Pronoun,
        ["NUM"] = LexiconSchema.Numeral,
        ["NOUN"] = LexiconSchema.Noun,
        ["ADJ"] = LexiconSchema.Adjective,
        ["VERB"] = LexiconSchema.Verb,
        ["ADV"] = LexiconSchema.Adverb,
        ["PREP"] = LexiconSchema.Preposition,
        ["CONJ"] = LexiconSchema.Conjunction,
        ["INTERJ"] = LexiconSchema.Interjection,
        ["DET"] = TypedMatching.DeterminerPos,
    };

    private readonly string _text;
    private int _index;

    public SlotGrammarParser(string text)
    {
        _text = text;
    }

    public IReadOnlyList<GrammarNode> Parse()
    {
        var nodes = ParseSequence(inGroup: false);
        if (nodes.Count == 0)
        {
            throw Error("pattern is empty");
        }

        return nodes;
    }

    private static bool IsLiteralChar(char c)
    {
        return !char.IsWhiteSpace(c) && c is not ('(' or ')' or '[' or ']' or '|' or '?');
    }

    private static bool IsSlotName(string name)
    {
        if (name.Length == 0 || char.IsDigit(name[0]))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private List<GrammarNode> ParseSequence(bool inGroup)
    {
        var nodes = new List<GrammarNode>();
        while (true)
        {
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                if (inGroup)
                {
                    throw Error("unterminated '('");
                }

                return nodes;
            }

            var c = _text[_index];
            if (c is ')' or '|')
            {
                if (inGroup)
                {
                    return nodes;
                }

                throw Error($"unexpected '{c}'");
            }

            nodes.Add(ParseItem());
        }
    }

    private GrammarNode ParseItem()
    {
        var c = _text[_index];
        GrammarNode node;
        if (c == '(')
        {
            node = ParseGroup();
        }
        else if (c == '[')
        {
            node = ParseTyped();
        }
        else if (IsLiteralChar(c))
        {
            var start = _index;
            while (_index < _text.Length && IsLiteralChar(_text[_index]))
            {
                _index++;
            }

            node = new LiteralNode(TextFolding.Normalize(_text[start.._index]));
        }
        else
        {
            throw Error($"unexpected '{c}'");
        }

        if (_index < _text.Length && _text[_index] == '?')
        {
            _index++;
            node = new OptionalNode(node);
        }

        return node;
    }

    private GroupNode ParseGroup()
    {
        _index++;
        var alternatives = new List<IReadOnlyList<GrammarNode>>();
        while (true)
        {
            alternatives.Add(ParseSequence(inGroup: true));
            if (_text[_index] == '|')
            {
                _index++;
                continue;
            }

            _index++;
            return new GroupNode(alternatives);
        }
    }

    private GrammarNode ParseTyped()
    {
        var close = _text.IndexOf(']', _index);
        if (close < 0)
        {
            throw Error("unterminated '['");
        }

        var content = _text[(_index + 1)..close];
        _index = close + 1;
        var parts = content.Split(':');
        var tag = parts[0].Trim().ToUpperInvariant();
        var args = parts.Skip(1).Select(static part => part.Trim()).ToArray();
        if (tag == "WORD")
        {
            if (args.Length > 1)
            {
                throw Error($"too many arguments in '[{content}]'");
            }

            var wordSlot = args.Length > 0 ? args[0] : null;
            if (wordSlot is not null && !IsSlotName(wordSlot))
            {
                throw Error($"invalid slot name '{wordSlot}' in '[{content}]'");
            }

            return new WordNode(wordSlot);
        }

        if (!TagToPos.TryGetValue(tag, out var pos))
        {
            throw Error($"unknown tag '[{content}]'");
        }

        string? lemma = null;
        string? slot = null;
        if (tag == "VERB")
        {
            if (args.Length > 2)
            {
                throw Error($"too many arguments in '[{content}]'");
            }

            lemma = args.Length > 0 ? TextFolding.Normalize(args[0]) : null;
            slot = args.Length > 1 ? args[1] : null;
            if (lemma is { Length: 0 })
            {
                throw Error($"empty lemma in '[{content}]'");
            }
        }
        else
        {
            if (args.Length > 1)
            {
                throw Error($"too many arguments in '[{content}]'");
            }

            slot = args.Length > 0 ? args[0] : null;
        }

        if (slot is not null && !IsSlotName(slot))
        {
            throw Error($"invalid slot name '{slot}' in '[{content}]'");
        }

        return new TypedNode(tag, pos, lemma, slot);
    }

    private void SkipWhitespace()
    {
        while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
        {
            _index++;
        }
    }

    private FormatException Error(string message)
    {
        return new FormatException($"Invalid slot grammar '{_text}' at position {_index}: {message}.");
    }
}
