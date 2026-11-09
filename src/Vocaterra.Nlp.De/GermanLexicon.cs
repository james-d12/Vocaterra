using Vocaterra.Nlp.De.Normalization;

namespace Vocaterra.Nlp.De;

/// <summary>
/// German entry points for <see cref="Lexicon"/> (M37): the lexicon built with the German folding and the German profile. German has no embedded seed lexicon;
/// the bundle's own <c>wiktionary-de.json</c> (M36) is the lexicon.
/// </summary>
public static class GermanLexicon
{
    /// <summary>The folding of the German pack: <c>Lexicon.Folding</c> of every German lexicon.</summary>
    public static ILexiconFolding Folding => GermanFolding.Instance;

    /// <summary>The German lexicon with no entries.</summary>
    public static Lexicon Empty { get; } = Lexicon.FromEntries([], GermanFolding.Instance, GermanLanguagePack.Profile);

    /// <summary>Parses and validates a German lexicon JSON document (schema version 1).</summary>
    /// <param name="json">The document.</param>
    public static Lexicon Load(string json) => Lexicon.Load(json, GermanFolding.Instance, GermanLanguagePack.Profile);

    /// <summary>Builds a German lexicon from entries (see <see cref="Lexicon.FromEntries(IEnumerable{LexiconEntry}, ILexiconFolding, LexiconProfile)"/> for the merge policy).</summary>
    /// <param name="entries">The entries of all lexicon files.</param>
    public static Lexicon FromEntries(IEnumerable<LexiconEntry> entries) => Lexicon.FromEntries(entries, GermanFolding.Instance, GermanLanguagePack.Profile);
}
