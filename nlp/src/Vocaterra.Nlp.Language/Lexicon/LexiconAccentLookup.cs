using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>One reading of an accented spelling that folds to a plain lexicon form: <c>está</c> and its reading for <c>esta</c>.</summary>
/// <param name="Form">The accented spelling the reading belongs to.</param>
/// <param name="Reading">The reading of that spelling.</param>
public sealed record AccentVariant(string Form, LexicalFeatures Reading);

/// <summary>
/// The result of <see cref="Lexicon.LookupWithAccentVariants"/>: the readings <see cref="Lexicon.Lookup"/> returns, and the readings of the accented
/// spellings that a plain lexicon form hides because the exact match wins.
/// </summary>
public sealed class LexiconAccentLookup
{
    internal LexiconAccentLookup(LexiconLookupResult exact, IReadOnlyList<AccentVariant> variants)
    {
        Exact = exact;
        Variants = variants;
    }

    /// <summary>What <see cref="Lexicon.Lookup"/> returns for the same form.</summary>
    public LexiconLookupResult Exact { get; }

    /// <summary>The readings of the accented spellings that fold to the form, in the lexicon's entry order; empty unless the form is plain and a lexicon form.</summary>
    public IReadOnlyList<AccentVariant> Variants { get; }
}
