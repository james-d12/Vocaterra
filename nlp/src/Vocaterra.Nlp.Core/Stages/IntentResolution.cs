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
public sealed record IntentResolution(IntentId Intent, SlotMap Slots, IntentRung Rung, double Margin, double Confidence);
