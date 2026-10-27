using System.Runtime.CompilerServices;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Language.Analysis;

/// <summary>
/// The analysis rules of a pack for callers that have no lexicon at hand (the typed matching of a slot grammar, the intent resolver). The word classes of
/// <see cref="IAnalysisRules"/> must not depend on the lexicon, so a pack's rules over an empty lexicon answer them as its rules over the bundle's lexicon do.
/// M34, additive.
/// </summary>
public static class AnalysisRules
{
    private static readonly ConditionalWeakTable<ILanguagePack, IAnalysisRules> Cache = new();

    /// <summary>The rules of the pack over an empty lexicon, created once per pack instance.</summary>
    /// <param name="language">The pack.</param>
    public static IAnalysisRules For(ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Cache.GetValue(
            language,
            static pack =>
            {
                var empty = Lexicon.Lexicon.FromEntries([], pack.Folding, pack.LexiconProfile);
                return pack.CreateAnalysisRules(empty, pack.CreateMorphology(empty));
            });
    }
}
