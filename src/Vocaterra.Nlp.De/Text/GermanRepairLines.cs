namespace Vocaterra.Nlp.De.Text;

/// <summary>
/// The German repair lines (M37): what the agent says after an utterance could not be resolved when the bundle's reserved pools have no line. The German pack's
/// <see cref="ILanguagePack.RepairFallback"/>. They address the learner as <c>du</c>.
/// </summary>
public static class GermanRepairLines
{
    /// <summary>The agent line for the first repair step (ask again).</summary>
    public const string AskAgainLine = "Wie bitte?";

    /// <summary>The agent line for the second repair step (repeat slowly).</summary>
    public const string SlowRepeatLine = "Entschuldigung, ich sage es noch einmal langsamer.";

    /// <summary>The agent line for giving up.</summary>
    public const string GiveUpLine = "Gut, das macht nichts.";

    /// <summary>The wording of the line that offers choices: „Meinst du „a“, „b“ oder „c“?“ (without the outer quotes).</summary>
    public static OfferLineFormat OfferFormat { get; } = new("Meinst du ", "„", "“", ", ", " oder ", "?");

    /// <summary>The four lines as the pack's <see cref="RepairLines"/>.</summary>
    public static RepairLines Lines { get; } = new(AskAgainLine, SlowRepeatLine, GiveUpLine, OfferFormat);
}
