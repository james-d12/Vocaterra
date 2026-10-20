using Slg.Core.Verdicts;
using Slg.Response.Templates;

namespace Slg.Fuzzing.Tests.Oracles;

/// <summary>
/// The only place an exception may be waved through. Every entry names the oracle that relies on it and says why the rejection is the
/// designed outcome rather than a bug; any other exception from the stage under test is a finding. Stage 2 adds the WAV reader and
/// content-loader entries next to the template entry.
/// </summary>
public static class KnownTolerances
{
    /// <summary>
    /// O12: a <see cref="TemplateException"/> is expected when the slot map handed to a template lacks a placeholder the template reads.
    /// The engine reports a missing slot with the slot name in <see cref="TemplateException.Placeholder"/> (a malformed template or an
    /// invalid filter reports the raw placeholder text including its braces instead), so the entry only covers the exception whose
    /// placeholder really is absent from the slot map. A <see cref="TemplateException"/> for a slot that is present means the slot's
    /// value did not suit its filter, which the generators avoid by construction and which therefore counts as a finding.
    /// </summary>
    public static bool IsMissingSlot(TemplateException exception, SlotMap slots)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(slots);
        return exception.Placeholder.Length > 0
            && exception.Placeholder[0] != '{'
            && !slots.ContainsKey(exception.Placeholder);
    }
}
