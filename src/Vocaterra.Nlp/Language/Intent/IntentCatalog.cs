using System.Text;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// The set of intents the content defines, loaded from canonical JSON:
/// <c>{ "schemaVersion": 1, "intents": [ { "intentId", "requiredSlots", "slotGrammars", "keywordRules", "examples", "lint" } ] }</c>.
/// </summary>
/// <remarks>
/// A keyword rule is <c>{ "lemmas": [..], "requiresSlots": [..], "slotFromNoun": "item", "slotFromNumber": "qty" }</c>; when a rule lists
/// <c>qty</c> in <c>requiresSlots</c> without naming <c>slotFromNumber</c>, the numeral slot defaults to <c>qty</c>. Slot grammars are patterns
/// in the <see cref="SlotGrammar"/> DSL and are identified by their canonical pattern text. Intents are sorted by ordinal id.
/// <para>
/// An example is a string or an object <c>{ "text": "yo es Rosa", "errors": ["conj.person"], "prompt": "introduce-open" }</c> (see <see cref="IntentExample"/>);
/// <c>lint</c> optionally holds the waivers of the content lint (see <see cref="IntentLintWaivers"/>). Neither changes how an utterance is resolved, and a catalog
/// that uses neither serializes as before.
/// </para>
/// </remarks>
public sealed class IntentCatalog
{
    /// <summary>The only supported schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>By convention a slot with this name holds a quantity and is therefore filled from a numeral.</summary>
    public const string QuantitySlot = "qty";

    /// <summary>The value of an intent's <c>order</c> that asks for the verb-second expansion of its grammars (M88; the expansion is M40's).</summary>
    public const string V2Order = "v2";

    private static readonly IReadOnlyList<string> NoSlots = [];

    private readonly Dictionary<string, IntentDefinition> _byId;

    private IntentCatalog(IntentDefinition[] sortedIntents)
    {
        Intents = sortedIntents;
        _byId = new Dictionary<string, IntentDefinition>(StringComparer.Ordinal);
        var grammars = new List<SlotGrammar>();
        var seenGrammars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intent in sortedIntents)
        {
            _byId.Add(intent.Id.Value, intent);
            foreach (var grammar in intent.Grammars)
            {
                if (seenGrammars.Add(grammar.Id))
                {
                    grammars.Add(grammar);
                }
            }
        }

        Grammars = grammars;
    }

    /// <summary>The intents sorted by ordinal id.</summary>
    public IReadOnlyList<IntentDefinition> Intents { get; }

    /// <summary>Every distinct slot grammar of the catalog (intents in id order, grammars in authoring order), for linguistic analysis.</summary>
    public IReadOnlyList<SlotGrammar> Grammars { get; }

    /// <summary>Parses and validates a catalog JSON document. Throws <see cref="FormatException"/> for schema violations.</summary>
    /// <param name="json">The document.</param>
    /// <param name="folding">
    /// The language's folding, whose <see cref="ILexiconFolding.Key"/> lower-cases the keyword-rule lemmas and the grammar literals (M34, additive); null for the
    /// language-neutral key (Unicode NFC and invariant lower case, which is the Spanish key).
    /// </param>
    /// <param name="supportsV2Order">
    /// True when the pack declares <see cref="ILanguagePack.SupportsV2Order"/> (M88, additive). An intent that carries <c>"order": "v2"</c> is refused with a
    /// <see cref="FormatException"/> when this is false; any other <c>order</c> value is refused always.
    /// </param>
    public static IntentCatalog Load(string json, ILexiconFolding? folding = null, bool supportsV2Order = false)
    {
        ArgumentNullException.ThrowIfNull(json);
        var file = CanonicalJson.Deserialize<IntentCatalogFile>(json);
        if (file.SchemaVersion != CurrentSchemaVersion)
        {
            throw new FormatException($"Unsupported intent catalog schemaVersion {file.SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        // Missing members arrive as null even though the wire records are declared non-nullable, so check explicitly.
        if ((object?)file.Intents is null)
        {
            throw new FormatException("Intent catalog is missing 'intents'.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var intents = new List<IntentDefinition>(file.Intents.Count);
        foreach (var entry in file.Intents)
        {
            var definition = Build(entry, folding, supportsV2Order);
            if (!seen.Add(definition.Id.Value))
            {
                throw new FormatException($"Duplicate intentId '{definition.Id.Value}'.");
            }

            intents.Add(definition);
        }

        var sorted = intents.ToArray();
        Array.Sort(sorted, static (a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
        return new IntentCatalog(sorted);
    }

    /// <summary>Reads a catalog from a UTF-8 JSON file.</summary>
    public static IntentCatalog LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Load(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Returns the intent with the given id, or null when the catalog has none.</summary>
    public IntentDefinition? Find(string intentId)
    {
        ArgumentNullException.ThrowIfNull(intentId);
        return _byId.TryGetValue(intentId, out var intent) ? intent : null;
    }

    /// <summary>Returns the required slot names of an intent (ordinal-sorted); empty for an unknown intent.</summary>
    public IReadOnlyList<string> RequiredSlots(string intentId)
    {
        return Find(intentId)?.RequiredSlots ?? NoSlots;
    }

    /// <summary>Returns the required slot names of an intent (ordinal-sorted); empty for an unknown intent.</summary>
    public IReadOnlyList<string> RequiredSlots(IntentId intentId)
    {
        return intentId.IsUnknown ? NoSlots : RequiredSlots(intentId.Value);
    }

    /// <summary>Serializes the catalog as canonical JSON (intents sorted, grammars as canonical patterns); loading the result gives an equal catalog.</summary>
    public string ToJson()
    {
        var entries = new List<IntentEntry>(Intents.Count);
        foreach (var intent in Intents)
        {
            entries.Add(new IntentEntry(
                intent.Id.Value,
                intent.RequiredSlots,
                intent.Grammars.Select(static grammar => grammar.Pattern).ToArray(),
                intent.KeywordRules
                    .Select(static rule => new KeywordRuleEntry(rule.Lemmas, rule.RequiresSlots, rule.SlotFromNoun, rule.SlotFromNumber))
                    .ToArray(),
                intent.ExampleSpecs,
                intent.Lint.IsEmpty ? null : intent.Lint,
                intent.Order));
        }

        return CanonicalJson.Serialize(new IntentCatalogFile(CurrentSchemaVersion, entries));
    }

    /// <summary>The deterministic hash of the canonical serialization, for version stamps.</summary>
    public string ContentHash()
    {
        return DeterministicHash.OfText(ToJson());
    }

    private static IntentDefinition Build(IntentEntry? entry, ILexiconFolding? folding, bool supportsV2Order)
    {
        if ((object?)entry is null)
        {
            throw new FormatException("Intent catalog contains a null intent.");
        }

        if (string.IsNullOrWhiteSpace(entry.IntentId) || entry.IntentId.Any(char.IsWhiteSpace))
        {
            throw new FormatException($"Invalid intentId '{entry.IntentId}': it must be non-empty and contain no whitespace.");
        }

        if (string.Equals(entry.IntentId, IntentId.UnknownValue, StringComparison.Ordinal))
        {
            throw new FormatException($"The intentId '{IntentId.UnknownValue}' is reserved.");
        }

        var id = entry.IntentId;
        var order = BuildOrder(entry.Order, id, supportsV2Order);
        var required = SlotNames(entry.RequiredSlots, id, "requiredSlots");
        var grammars = BuildGrammars(entry.SlotGrammars, id, folding);
        var rules = BuildRules(entry.KeywordRules, id, folding);
        var examples = BuildExamples(entry.Examples, id);
        // M82: no grammar, rule or example can produce an intent that has none of them, so the loader refuses it rather than let it sit in the catalog.
        if (grammars.Count == 0 && rules.Count == 0 && examples.Count == 0)
        {
            throw new FormatException(
                $"Intent '{id}' has no slotGrammars, keywordRules or examples, so nothing can produce it; add at least one slot grammar, keyword rule or example.");
        }

        return new IntentDefinition(new IntentId(id), required, grammars, rules, examples, DeclareSlots(required, grammars, rules), BuildLint(entry.Lint, id), order);
    }

    private static string[] SlotNames(IReadOnlyList<string>? names, string intentId, string member)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in names ?? [])
        {
            set.Add(SlotName(name, intentId, member));
        }

        return [.. set];
    }

    private static string? BuildOrder(string? order, string intentId, bool supportsV2Order)
    {
        if (order is null)
        {
            return null;
        }

        if (!string.Equals(order, V2Order, StringComparison.Ordinal))
        {
            throw new FormatException($"Intent '{intentId}' has an unknown order '{order}'; the only value is \"{V2Order}\".");
        }

        return supportsV2Order
            ? order
            : throw new FormatException($"Intent '{intentId}' declares order \"{V2Order}\", but the language pack does not support V2 grammars (ILanguagePack.SupportsV2Order is false).");
    }

    private static string SlotName(string? name, string intentId, string member)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
        {
            throw new FormatException($"Intent '{intentId}' has an invalid slot name '{name}' in {member}.");
        }

        return name;
    }

    private static List<SlotGrammar> BuildGrammars(IReadOnlyList<string>? patterns, string intentId, ILexiconFolding? folding)
    {
        var grammars = new List<SlotGrammar>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pattern in patterns ?? [])
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new FormatException($"Intent '{intentId}' has an empty slot grammar.");
            }

            var grammar = SlotGrammar.Parse(pattern, null, folding);
            if (seen.Add(grammar.Id))
            {
                grammars.Add(grammar);
            }
        }

        return grammars;
    }

    private static List<KeywordRule> BuildRules(IReadOnlyList<KeywordRuleEntry>? entries, string intentId, ILexiconFolding? folding)
    {
        var rules = new List<KeywordRule>();
        foreach (var rule in entries ?? [])
        {
            if ((object?)rule is null || (object?)rule.Lemmas is null || rule.Lemmas.Count == 0)
            {
                throw new FormatException($"Intent '{intentId}' has a keyword rule without lemmas.");
            }

            var lemmas = new List<string>(rule.Lemmas.Count);
            foreach (var lemma in rule.Lemmas)
            {
                if (string.IsNullOrWhiteSpace(lemma))
                {
                    throw new FormatException($"Intent '{intentId}' has an empty keyword rule lemma.");
                }

                lemmas.Add(folding is null ? TextKey.Of(lemma.Trim()) : folding.Key(lemma.Trim()));
            }

            var requires = SlotNames(rule.RequiresSlots, intentId, "keywordRules.requiresSlots");
            var fromNoun = rule.SlotFromNoun is null ? null : SlotName(rule.SlotFromNoun, intentId, "keywordRules.slotFromNoun");
            var fromNumber = rule.SlotFromNumber is null ? null : SlotName(rule.SlotFromNumber, intentId, "keywordRules.slotFromNumber");
            if (fromNumber is null && Array.IndexOf(requires, QuantitySlot) >= 0 && !string.Equals(fromNoun, QuantitySlot, StringComparison.Ordinal))
            {
                fromNumber = QuantitySlot;
            }

            foreach (var name in requires)
            {
                if (!string.Equals(name, fromNoun, StringComparison.Ordinal) && !string.Equals(name, fromNumber, StringComparison.Ordinal))
                {
                    throw new FormatException(
                        $"Intent '{intentId}' keyword rule requires slot '{name}' but neither slotFromNoun nor slotFromNumber provides it.");
                }
            }

            rules.Add(new KeywordRule(lemmas, requires, fromNoun, fromNumber));
        }

        return rules;
    }

    private static List<IntentExample> BuildExamples(IReadOnlyList<IntentExample>? examples, string intentId)
    {
        var result = new List<IntentExample>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var example in examples ?? [])
        {
            if ((object?)example is null || string.IsNullOrWhiteSpace(example.Text))
            {
                throw new FormatException($"Intent '{intentId}' has an empty example.");
            }

            if (example.ExpectedErrors?.Any(string.IsNullOrWhiteSpace) == true)
            {
                throw new FormatException($"Intent '{intentId}' example \"{example.Text}\" has an empty error tag.");
            }

            if (example.Prompt is { } prompt && string.IsNullOrWhiteSpace(prompt))
            {
                throw new FormatException($"Intent '{intentId}' example \"{example.Text}\" has an empty prompt id.");
            }

            if (seen.Add(example.Text))
            {
                result.Add(example.ExpectedErrors is null
                    ? example
                    : example with { ExpectedErrors = [.. example.ExpectedErrors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] });
            }
        }

        return result;
    }

    private static IntentLintWaivers? BuildLint(IntentLintWaivers? lint, string intentId)
    {
        if ((object?)lint is null)
        {
            return null;
        }

        return new IntentLintWaivers(
            SortedWaivers(lint.AllowOverlap, intentId, "allowOverlap"),
            SortedWaivers(lint.AllowRuleOnly, intentId, "allowRuleOnly"),
            SortedWaivers(lint.AllowHomograph, intentId, "allowHomograph"));
    }

    private static string[]? SortedWaivers(IReadOnlyList<string>? values, string intentId, string member)
    {
        if (values is null)
        {
            return null;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new FormatException($"Intent '{intentId}' has an empty entry in lint.{member}.");
        }

        return [.. values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static DeclaredSlot[] DeclareSlots(string[] required, List<SlotGrammar> grammars, List<KeywordRule> rules)
    {
        var kinds = new SortedDictionary<string, SlotKind>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            AddKind(kinds, rule.SlotFromNoun, SlotKind.Noun);
            AddKind(kinds, rule.SlotFromNumber, SlotKind.Number);
        }

        foreach (var grammar in grammars)
        {
            CollectGrammarSlots(grammar.Nodes, kinds);
        }

        foreach (var name in required)
        {
            AddKind(kinds, name, string.Equals(name, QuantitySlot, StringComparison.Ordinal) ? SlotKind.Number : SlotKind.Noun);
        }

        return kinds.Select(static pair => new DeclaredSlot(pair.Key, pair.Value)).ToArray();
    }

    private static void AddKind(SortedDictionary<string, SlotKind> kinds, string? name, SlotKind kind)
    {
        if (name is not null)
        {
            kinds.TryAdd(name, kind);
        }
    }

    private static void CollectGrammarSlots(IReadOnlyList<GrammarNode> nodes, SortedDictionary<string, SlotKind> kinds)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TypedNode { Slot: not null } typed:
                    if (string.Equals(typed.Tag, "NOUN", StringComparison.Ordinal))
                    {
                        AddKind(kinds, typed.Slot, SlotKind.Noun);
                    }
                    else if (string.Equals(typed.Tag, "NUM", StringComparison.Ordinal))
                    {
                        AddKind(kinds, typed.Slot, SlotKind.Number);
                    }

                    break;
                case WordNode { Slot: not null } word:
                    // A name is filled by the embedding rung from the first noun, which is the name when the lexicon has it as one.
                    AddKind(kinds, word.Slot, SlotKind.Noun);
                    break;
                case OptionalNode optional:
                    CollectGrammarSlots([optional.Inner], kinds);
                    break;
                case GroupNode group:
                    // M74: a group's slot holds words, filled like a name by the embedding rung from the first noun.
                    AddKind(kinds, group.Slot, SlotKind.Noun);
                    foreach (var alternative in group.Alternatives)
                    {
                        CollectGrammarSlots(alternative, kinds);
                    }

                    break;
                default:
                    break;
            }
        }
    }
}
