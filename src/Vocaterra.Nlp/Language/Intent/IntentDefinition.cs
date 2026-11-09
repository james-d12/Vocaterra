using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>How a declared slot is filled when no grammar captured it.</summary>
internal enum SlotKind
{
    /// <summary>Filled with the lemma of the first noun.</summary>
    Noun,

    /// <summary>Filled with the lemma (a digit string) of the first numeral.</summary>
    Number,
}

/// <summary>A slot an intent declares, with the kind of token that fills it.</summary>
internal readonly record struct DeclaredSlot(string Name, SlotKind Kind);

/// <summary>One intent of the catalog: its required slots, slot grammars, keyword rules and example utterances.</summary>
public sealed class IntentDefinition
{
    internal IntentDefinition(
        IntentId id,
        IReadOnlyList<string> requiredSlots,
        IReadOnlyList<SlotGrammar> grammars,
        IReadOnlyList<KeywordRule> keywordRules,
        IReadOnlyList<IntentExample> exampleSpecs,
        IReadOnlyList<DeclaredSlot> declaredSlots,
        IntentLintWaivers? lint = null,
        string? order = null)
    {
        Order = order;
        Id = id;
        RequiredSlots = requiredSlots;
        Grammars = grammars;
        KeywordRules = keywordRules;
        ExampleSpecs = exampleSpecs;
        Examples = [.. exampleSpecs.Select(static example => example.Text)];
        DeclaredSlots = declaredSlots;
        Lint = lint ?? IntentLintWaivers.None;
    }

    /// <summary>The intent identifier.</summary>
    public IntentId Id { get; }

    /// <summary>Slot names the intent needs before it counts as communicated; ordinal-sorted and distinct.</summary>
    public IReadOnlyList<string> RequiredSlots { get; }

    /// <summary>The slot grammars in authoring order; a grammar's id is its canonical pattern text.</summary>
    public IReadOnlyList<SlotGrammar> Grammars { get; }

    /// <summary>The lemma keyword rules in authoring order.</summary>
    public IReadOnlyList<KeywordRule> KeywordRules { get; }

    /// <summary>Example utterances used as embedding anchors, in authoring order.</summary>
    public IReadOnlyList<string> Examples { get; }

    /// <summary>The examples with their optional expectations, in authoring order; <see cref="Examples"/> holds the same texts.</summary>
    public IReadOnlyList<IntentExample> ExampleSpecs { get; }

    /// <summary>The lint waivers the intent declares; <see cref="IntentLintWaivers.None"/> when it has none. The resolver ignores them.</summary>
    public IntentLintWaivers Lint { get; }

    /// <summary>
    /// The word order the intent's grammar declares (<c>order: "v2"</c>, M88), or null for the order the patterns are written in. The loader only records it; the
    /// expansion of a V2 grammar into the orders it allows is M40's.
    /// </summary>
    public string? Order { get; }

    /// <summary>Every slot the intent declares (rules, grammars, required slots), ordinal-sorted, with the kind that fills it.</summary>
    internal IReadOnlyList<DeclaredSlot> DeclaredSlots { get; }
}
