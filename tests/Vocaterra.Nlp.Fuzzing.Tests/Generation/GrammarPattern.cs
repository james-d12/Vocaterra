using System.Text;
using Vocaterra.Nlp.Language.Intent;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>A node of a slot grammar pattern as the generators see it; the compiled form in <c>Vocaterra.Nlp.Language</c> is internal.</summary>
public abstract record PatternNode;

/// <summary>A literal word.</summary>
/// <param name="Word">The lower-case word, with no whitespace or grammar metacharacters.</param>
public sealed record LiteralPattern(string Word) : PatternNode;

/// <summary>A typed token such as <c>[NOUN:item]</c> or <c>[VERB:tener:verb]</c>.</summary>
/// <param name="Tag">The upper-case tag (<c>ART</c>, <c>DET</c>, <c>PRON</c>, <c>NUM</c>, <c>NOUN</c>, <c>ADJ</c>, <c>VERB</c>, <c>ADV</c>, <c>PREP</c>, <c>CONJ</c>, <c>INTERJ</c>).</param>
/// <param name="Lemma">The verb lemma for <c>VERB</c> tags, else null.</param>
/// <param name="Slot">The slot the token fills, or null.</param>
public sealed record TypedPattern(string Tag, string? Lemma, string? Slot) : PatternNode;

/// <summary>A name: <c>[WORD]</c> or <c>[WORD:slot]</c>, one token of letters whatever its lexicon readings (M17b).</summary>
/// <param name="Slot">The slot the name fills, or null.</param>
public sealed record WordPattern(string? Slot) : PatternNode;

/// <summary>A node followed by <c>?</c>.</summary>
/// <param name="Inner">The optional node.</param>
public sealed record OptionalPattern(PatternNode Inner) : PatternNode;

/// <summary>A parenthesized choice between sequences.</summary>
/// <param name="Alternatives">The alternatives; each is a non-empty sequence.</param>
/// <param name="Slot">The slot the group fills with the words of the chosen alternative (M74, <c>(frío|buen tiempo):weather</c>), or null.</param>
public sealed record GroupPattern(IReadOnlyList<IReadOnlyList<PatternNode>> Alternatives, string? Slot = null) : PatternNode;

/// <summary>
/// A slot grammar pattern as a small syntax tree that can be rendered to the DSL text, parsed back from canonical pattern text, and walked
/// by the sentence generator. Rendering followed by <c>SlotGrammar.Parse</c> must give the same canonical pattern; the O3 oracle checks that.
/// </summary>
/// <param name="Nodes">The top-level sequence.</param>
public sealed record GrammarPattern(IReadOnlyList<PatternNode> Nodes)
{
    /// <summary>Renders the DSL text: single spaces between elements, upper-case tags, <c>?</c> directly after its element.</summary>
    public string Render()
    {
        var builder = new StringBuilder();
        RenderSequence(builder, Nodes);
        return builder.ToString();
    }

    /// <summary>Parses canonical pattern text (the form <c>SlotGrammar.Pattern</c> returns).</summary>
    public static GrammarPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(text);
        return new GrammarPattern(parser.Sequence(inGroup: false));
    }

    /// <summary>
    /// True when two typed tokens that can match the same word exist (same tag, or <c>[DET]</c> next to <c>[ART]</c> or <c>[ADJ]</c>), at least one
    /// of them fills a slot and at least one is optional or inside a group. The matcher may then legitimately bind one sentence's words to the
    /// tokens in more than one way (<c>[NOUN:a]? [NOUN:b]?</c> over a single noun, <c>[ADJ]? [ADJ:x]?</c> over a single adjective), so only the
    /// multiset of slot values, not the slot names, is a property of the sentence.
    /// </summary>
    /// <param name="literalClasses">The pattern tags a literal word can also match, from its lexicon readings (for example <c>NOUN</c> for <c>isla</c>).</param>
    public bool HasAmbiguousSlots(Func<string, IEnumerable<string>> literalClasses)
    {
        ArgumentNullException.ThrowIfNull(literalClasses);
        var found = new List<(HashSet<string> Classes, bool HasSlot, bool Flexible)>();
        Collect(Nodes, flexible: false, literalClasses, found);
        for (var i = 0; i < found.Count; i++)
        {
            for (var j = i + 1; j < found.Count; j++)
            {
                if (found[i].Classes.Overlaps(found[j].Classes)
                    && (found[i].HasSlot || found[j].HasSlot)
                    && (found[i].Flexible || found[j].Flexible))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>True when a typed token with this tag (<c>VERB</c>, <c>NOUN</c>, ...) is optional or inside a group, so the matcher may or may not give it a word.</summary>
    public bool HasFlexibleTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return ContainsFlexibleTag(Nodes, tag, flexible: false);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Render();
    }

    private static bool ContainsFlexibleTag(IReadOnlyList<PatternNode> nodes, string tag, bool flexible)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TypedPattern typed when flexible && string.Equals(typed.Tag, tag, StringComparison.Ordinal):
                    return true;
                case OptionalPattern optional when ContainsFlexibleTag([optional.Inner], tag, flexible: true):
                    return true;
                case GroupPattern group when group.Alternatives.Any(alternative => ContainsFlexibleTag(alternative, tag, flexible: true)):
                    return true;
                default:
                    break;
            }
        }

        return false;
    }

    // Every class a typed token or a name can take a word of: a name (WORD) matches any token of letters.
    private static readonly string[] AllClasses = ["ART", "ADJ", "NUM", "NOUN", "PRON", "VERB", "ADV", "PREP", "CONJ", "INTERJ", "WORD"];

    private static void Collect(
        IReadOnlyList<PatternNode> nodes,
        bool flexible,
        Func<string, IEnumerable<string>> literalClasses,
        List<(HashSet<string> Classes, bool HasSlot, bool Flexible)> found)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TypedPattern typed:
                    // Words can belong to more than one class: un and una are articles and numerals, and a determiner adjective is an adjective.
                    var classes = typed.Tag switch
                    {
                        "DET" => new HashSet<string> { "ART", "ADJ", "NUM" },
                        "ART" or "NUM" => new HashSet<string> { "ART", "NUM" },
                        _ => new HashSet<string> { typed.Tag },
                    };
                    found.Add((classes, typed.Slot is not null, flexible));
                    break;
                case WordPattern word:
                    // A name is any word of letters, so it can compete with every class.
                    found.Add(([.. AllClasses], word.Slot is not null, flexible));
                    break;
                case LiteralPattern literal:
                    // A literal word also matches the typed tokens of the parts of speech its readings have, and a name.
                    found.Add(([.. literalClasses(literal.Word).Append("WORD")], false, flexible));
                    break;
                case OptionalPattern optional:
                    Collect([optional.Inner], flexible: true, literalClasses, found);
                    break;
                case GroupPattern group:
                    // M74: a group with a slot name fills it with the words of the alternative that matched, which no other element competes for in the catalog's
                    // grammars; its words are collected as any group's.
                    foreach (var alternative in group.Alternatives)
                    {
                        Collect(alternative, flexible: true, literalClasses, found);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static void RenderSequence(StringBuilder builder, IReadOnlyList<PatternNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            RenderNode(builder, nodes[i]);
        }
    }

    private static void RenderNode(StringBuilder builder, PatternNode node)
    {
        switch (node)
        {
            case LiteralPattern literal:
                builder.Append(literal.Word);
                break;
            case TypedPattern typed:
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
            case WordPattern word:
                builder.Append("[WORD");
                if (word.Slot is not null)
                {
                    builder.Append(':').Append(word.Slot);
                }

                builder.Append(']');
                break;
            case OptionalPattern optional:
                RenderNode(builder, optional.Inner);
                builder.Append('?');
                break;
            case GroupPattern group:
                builder.Append('(');
                for (var i = 0; i < group.Alternatives.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append('|');
                    }

                    RenderSequence(builder, group.Alternatives[i]);
                }

                builder.Append(')');
                if (group.Slot is not null)
                {
                    builder.Append(':').Append(group.Slot);
                }

                break;
            default:
                throw new InvalidOperationException($"Unexpected pattern node {node.GetType().Name}.");
        }
    }

    private sealed class Parser(string text)
    {
        private int _index;

        public List<PatternNode> Sequence(bool inGroup)
        {
            var nodes = new List<PatternNode>();
            while (true)
            {
                SkipWhitespace();
                if (_index >= text.Length || text[_index] is ')' or '|')
                {
                    if (!inGroup && _index < text.Length)
                    {
                        throw new FormatException($"Unexpected '{text[_index]}' in '{text}'.");
                    }

                    return nodes;
                }

                nodes.Add(Item());
            }
        }

        private static bool IsLiteralChar(char c)
        {
            return !char.IsWhiteSpace(c) && c is not ('(' or ')' or '[' or ']' or '|' or '?');
        }

        private PatternNode Item()
        {
            PatternNode node;
            var c = text[_index];
            if (c == '(')
            {
                _index++;
                var alternatives = new List<IReadOnlyList<PatternNode>>();
                while (true)
                {
                    alternatives.Add(Sequence(inGroup: true));
                    if (_index < text.Length && text[_index] == '|')
                    {
                        _index++;
                        continue;
                    }

                    _index++;
                    break;
                }

                // M74: a slot name right after the closing parenthesis.
                string? slot = null;
                if (_index < text.Length && text[_index] == ':')
                {
                    var start = ++_index;
                    while (_index < text.Length && (char.IsAsciiLetterOrDigit(text[_index]) || text[_index] == '_'))
                    {
                        _index++;
                    }

                    slot = text[start.._index];
                }

                node = new GroupPattern(alternatives, slot);
            }
            else if (c == '[')
            {
                var close = text.IndexOf(']', _index);
                var parts = text[(_index + 1)..close].Split(':');
                _index = close + 1;
                var tag = parts[0];
                node = tag switch
                {
                    "WORD" => new WordPattern(parts.Length > 1 ? parts[1] : null),
                    "VERB" => new TypedPattern(tag, parts.Length > 1 ? parts[1] : null, parts.Length > 2 ? parts[2] : null),
                    _ => new TypedPattern(tag, null, parts.Length > 1 ? parts[1] : null),
                };
            }
            else
            {
                var start = _index;
                while (_index < text.Length && IsLiteralChar(text[_index]))
                {
                    _index++;
                }

                node = new LiteralPattern(text[start.._index]);
            }

            if (_index < text.Length && text[_index] == '?')
            {
                _index++;
                node = new OptionalPattern(node);
            }

            return node;
        }

        private void SkipWhitespace()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }
        }
    }
}

/// <summary>One grammar of the intent catalog: its canonical text, its tree, the compiled grammar and the intents that own it.</summary>
/// <param name="Id">The grammar id (its canonical pattern text).</param>
/// <param name="Pattern">The pattern tree.</param>
/// <param name="Compiled">The compiled grammar the matcher uses.</param>
/// <param name="IntentIds">The ids of the intents that list this grammar, in ordinal order.</param>
public sealed record CatalogGrammar(string Id, GrammarPattern Pattern, SlotGrammarType Compiled, IReadOnlyList<string> IntentIds);

/// <summary>Every distinct grammar of an <see cref="IntentCatalog"/> as a <see cref="CatalogGrammar"/>, in catalog order.</summary>
public sealed class PatternCatalog
{
    private PatternCatalog(IReadOnlyList<CatalogGrammar> grammars)
    {
        Grammars = grammars;
    }

    /// <summary>The distinct grammars in catalog order (intents by id, grammars in authoring order).</summary>
    public IReadOnlyList<CatalogGrammar> Grammars { get; }

    /// <summary>The intent id that the fixture grammars of <see cref="FromBundle"/> are listed under; no bundle has an intent of this name.</summary>
    public const string FixtureIntent = "fixture";

    /// <summary>The intent id that the patterns of <see cref="FromPatterns"/> are listed under; no bundle has an intent of this name.</summary>
    public const string FrozenIntent = "frozen";

    /// <summary>
    /// Builds the catalog from the grammars of an intent catalog and appends the fixture grammars: grammar shapes that the game's bundle does not use
    /// (a name, <c>[WORD]</c>; an adjective, <c>[ADJ]</c>; a verb bound to a slot, <c>[VERB:lemma:slot]</c>) and that the sentence generators, the analyzer and the
    /// serialization oracles must keep covering. The fixtures are only patterns: no intent of the bundle owns them, no prompt allows them.
    /// </summary>
    public static PatternCatalog FromBundle(IntentCatalog catalog, IEnumerable<string>? fixtures = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var intent in catalog.Intents)
        {
            foreach (var grammar in intent.Grammars)
            {
                if (!owners.TryGetValue(grammar.Id, out var list))
                {
                    list = [];
                    owners.Add(grammar.Id, list);
                }

                list.Add(intent.Id.Value);
            }
        }

        var grammars = catalog.Grammars
            .Select(grammar => new CatalogGrammar(grammar.Id, GrammarPattern.Parse(grammar.Pattern), grammar, owners[grammar.Id]))
            .ToList();
        return WithFixtures(grammars, fixtures);
    }

    /// <summary>
    /// Builds the catalog from pattern texts listed in catalog order (a frozen copy of a bundle's grammars, <c>FrozenGeneratorInputs</c>) and appends the fixture
    /// grammars as <see cref="FromBundle"/> does. The patterns are owned by no intent; they are listed under <see cref="FrozenIntent"/>.
    /// </summary>
    public static PatternCatalog FromPatterns(IEnumerable<string> patterns, IEnumerable<string>? fixtures = null)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return WithFixtures(Distinct(patterns, FrozenIntent, []), fixtures);
    }

    private static PatternCatalog WithFixtures(List<CatalogGrammar> grammars, IEnumerable<string>? fixtures)
    {
        return new PatternCatalog(Distinct(fixtures ?? [], FixtureIntent, grammars));
    }

    // Appends each pattern that is not yet in the list (by the id of its compiled grammar), owned by the one intent given.
    private static List<CatalogGrammar> Distinct(IEnumerable<string> patterns, string intent, List<CatalogGrammar> grammars)
    {
        var known = grammars.Select(static grammar => grammar.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var text in patterns)
        {
            var parsed = GrammarPattern.Parse(text);
            var compiled = SlotGrammarType.Parse(parsed.Render());
            if (known.Add(compiled.Id))
            {
                grammars.Add(new CatalogGrammar(compiled.Id, parsed, compiled, [intent]));
            }
        }

        return grammars;
    }
}
