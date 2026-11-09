using Vocaterra.Nlp.Language.Intent;

namespace Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// The expansion of a grammar that declares <c>order: "v2"</c> into the word orders a verb-second language allows (M88 stub; the expansion is M40's). Nothing
/// reaches it for a pack without <see cref="Packs.ILanguagePack.SupportsV2Order"/>, because the loader refuses the flag there.
/// </summary>
internal static class V2GrammarExpansion
{
    /// <summary>Expands the grammars of an intent that declares <see cref="IntentDefinition.Order"/> <c>v2</c>.</summary>
    /// <param name="intent">The intent.</param>
    /// <exception cref="NotSupportedException">Always, until M40.</exception>
    public static IReadOnlyList<SlotGrammar> Expand(IntentDefinition intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        throw new NotSupportedException($"The V2 expansion of intent '{intent.Id.Value}' is not implemented yet (M40).");
    }
}
