namespace Vocaterra.Nlp.Es.Text;

/// <summary>
/// The Spanish repair lines (moved out of the engine's <c>RepairStrategy</c> in M53): what the agent says after an utterance could not be resolved when the
/// bundle's reserved pools have no line. The Spanish pack's <see cref="ILanguagePack.RepairFallback"/>.
/// </summary>
public static class SpanishRepairLines
{
    /// <summary>The agent line for the first repair step (ask again).</summary>
    public const string AskAgainLine = "¿Cómo?";

    /// <summary>The agent line for the second repair step (repeat slowly).</summary>
    public const string SlowRepeatLine = "Perdón, te lo repito más despacio.";

    /// <summary>The agent line for giving up.</summary>
    public const string GiveUpLine = "Bueno, no importa.";

    /// <summary>The wording of the line that offers choices: «¿Quieres decir «a», «b» o «c»?».</summary>
    public static OfferLineFormat OfferFormat { get; } = new("¿Quieres decir ", "«", "»", ", ", " o ", "?");

    /// <summary>The four lines as the pack's <see cref="RepairLines"/>.</summary>
    public static RepairLines Lines { get; } = new(AskAgainLine, SlowRepeatLine, GiveUpLine, OfferFormat);
}
