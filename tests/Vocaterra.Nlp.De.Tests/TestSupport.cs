namespace Vocaterra.Nlp.De.Tests;

/// <summary>Helpers shared by the German pack's tests.</summary>
internal static class TestSupport
{
    /// <summary>The repository's German lexicon file, found by walking up from the test assembly.</summary>
    public static string LexiconPath { get; } = FindLexicon();

    /// <summary>The 108 533 entries of the Wiktionary German lexicon, loaded once with the German folding and profile.</summary>
    public static Lexicon FullLexicon => FullLexiconHolder.Instance;

    /// <summary>A lexicon of the given (form, lemma, pos, register) rows, with the German folding and profile; a register is only used on a pronoun.</summary>
    public static Lexicon Small(params (string Form, string Lemma, string Pos, string? Register)[] rows)
    {
        return GermanLexicon.FromEntries(rows.Select(static r => new LexiconEntry(r.Form, r.Lemma, r.Pos, new EntryFeatures(null, null, null, null, null, Register: r.Register))));
    }

    private static string FindLexicon()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content", "game-de-v1", "lexicon", "wiktionary-de.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("content/game-de-v1/lexicon/wiktionary-de.json was not found above the test assembly.");
    }

    private static class FullLexiconHolder
    {
        internal static readonly Lexicon Instance = GermanLexicon.Load(File.ReadAllText(LexiconPath));
    }
}
