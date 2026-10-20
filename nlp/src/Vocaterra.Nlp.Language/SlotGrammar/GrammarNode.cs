namespace Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>A node of a parsed slot grammar.</summary>
internal abstract record GrammarNode;

/// <summary>A literal word matched by surface form or lemma, ignoring accents.</summary>
internal sealed record LiteralNode(string Word) : GrammarNode;

/// <summary>A typed token such as <c>[NOUN:item]</c>: a part-of-speech tag, an optional verb lemma and an optional slot name.</summary>
internal sealed record TypedNode(string Tag, string Pos, string? Lemma, string? Slot) : GrammarNode;

/// <summary>A parenthesized choice between sequences, such as <c>(quiero|necesito)</c>.</summary>
internal sealed record GroupNode(IReadOnlyList<IReadOnlyList<GrammarNode>> Alternatives) : GrammarNode;

/// <summary>A node followed by <c>?</c>: it may match or be skipped.</summary>
internal sealed record OptionalNode(GrammarNode Inner) : GrammarNode;

/// <summary>The implicit optional leading subject pronoun every grammar allows.</summary>
internal sealed record DefaultSubjectNode : GrammarNode;

/// <summary>The implicit optional trailing <c>por favor</c> every grammar allows.</summary>
internal sealed record DefaultPoliteNode : GrammarNode;
