using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>The rung of the resolution ladder that decided the intent.</summary>
public enum IntentRung
{
    /// <summary>Slot grammar match.</summary>
    Grammar,

    /// <summary>Keyword and rule match.</summary>
    Rules,

    /// <summary>Embedding similarity.</summary>
    Embedding,

    /// <summary>Nothing resolved.</summary>
    Unresolved,
}

/// <summary>Resolved intent with slots, deciding rung, the margin over the runner-up and a confidence.</summary>
/// <param name="Intent">The intent among the prompt's allowed intents, or <see cref="IntentId.Unknown"/>.</param>
/// <param name="Slots">The slots of that intent.</param>
/// <param name="Rung">The rung that decided it.</param>
/// <param name="Margin">The margin over the runner-up.</param>
/// <param name="Confidence">The confidence of the deciding rung.</param>
/// <param name="OffPromptIntent">
/// M72: when the allowed intents gave <see cref="IntentId.Unknown"/> at a prompt that lists allowed intents, the intent the line has over the whole catalog
/// (the grammar and keyword rungs only, no embedding), which the prompt does not allow: <c>me gusta el río</c> at a prompt that does not take
/// <c>state_like</c>; since M74 also when a grammar of an intent the prompt does not allow takes the line from the best allowed grammar (<c>hace calor</c> at a
/// prompt whose <c>[VERB:hacer] [NOUN:item]</c> would read it, off the prompt as <c>state_weather</c>). Null otherwise, and left out of the canonical JSON while null.
/// </param>
public sealed record IntentResolution(
    IntentId Intent,
    SlotMap Slots,
    IntentRung Rung,
    double Margin,
    double Confidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IntentId? OffPromptIntent = null);
