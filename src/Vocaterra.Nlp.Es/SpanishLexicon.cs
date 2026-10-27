using System.Text;

namespace Vocaterra.Nlp.Es;

/// <summary>
/// Spanish entry points for <see cref="Lexicon"/> (moved out of the engine in M53): the lexicon built with the Spanish folding and the v1 profile, and the
/// seed lexicon embedded in this assembly (the same file as <c>content/game-v1/lexicon/seed.json</c>).
/// </summary>
public static class SpanishLexicon
{
    /// <summary>Manifest resource name under which the seed lexicon is embedded in this assembly.</summary>
    public const string SeedResourceName = "Vocaterra.Nlp.Language.Lexicon.seed.json";

    /// <summary>The folding of the Spanish pack: <c>Lexicon.Folding</c> of every Spanish lexicon.</summary>
    public static ILexiconFolding Folding => SpanishLanguagePack.Instance.Folding;

    /// <summary>The Spanish lexicon with no entries.</summary>
    public static Lexicon Empty { get; } = Lexicon.FromEntries([], SpanishLanguagePack.Instance.Folding, SpanishLanguagePack.Instance.LexiconProfile);

    /// <summary>The seed lexicon embedded in this assembly; loaded once on first use.</summary>
    public static Lexicon Seed => SeedHolder.Instance;

    /// <summary>Parses and validates a Spanish lexicon JSON document (schema version 1).</summary>
    /// <param name="json">The document.</param>
    public static Lexicon Load(string json)
    {
        return Lexicon.Load(json, SpanishLanguagePack.Instance.Folding, SpanishLanguagePack.Instance.LexiconProfile);
    }

    /// <summary>Builds a Spanish lexicon from entries (see <see cref="Lexicon.FromEntries(IEnumerable{LexiconEntry}, ILexiconFolding, LexiconProfile)"/> for the merge policy).</summary>
    /// <param name="entries">The entries of all lexicon files.</param>
    public static Lexicon FromEntries(IEnumerable<LexiconEntry> entries)
    {
        return Lexicon.FromEntries(entries, SpanishLanguagePack.Instance.Folding, SpanishLanguagePack.Instance.LexiconProfile);
    }

    /// <summary>Reads the embedded seed lexicon JSON text.</summary>
    public static string ReadSeedJson()
    {
        using var stream = typeof(SpanishLexicon).Assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{SeedResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Parses the embedded seed lexicon into a fresh instance.</summary>
    public static Lexicon LoadSeed()
    {
        return Load(ReadSeedJson());
    }

    private static class SeedHolder
    {
        internal static readonly Lexicon Instance = LoadSeed();
    }
}
