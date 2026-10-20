namespace Slg.Nlp.Intent;

/// <summary>Wire format of the intent catalog JSON document.</summary>
internal sealed record IntentCatalogFile(int SchemaVersion, IReadOnlyList<IntentEntry> Intents);

/// <summary>Wire format of one intent; list members may be omitted in authored files.</summary>
internal sealed record IntentEntry(
    string IntentId,
    IReadOnlyList<string>? RequiredSlots,
    IReadOnlyList<string>? SlotGrammars,
    IReadOnlyList<KeywordRuleEntry>? KeywordRules,
    IReadOnlyList<string>? Examples);

/// <summary>Wire format of one keyword rule.</summary>
internal sealed record KeywordRuleEntry(
    IReadOnlyList<string> Lemmas,
    IReadOnlyList<string>? RequiresSlots,
    string? SlotFromNoun,
    string? SlotFromNumber);
