using System.Collections.Frozen;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>Small closed word classes that the lexicon schema cannot express: determiner adjectives, subject pronouns and similar.</summary>
internal static class WordClasses
{
    private static readonly FrozenSet<string> DeterminerAdjectives = new[]
    {
        "mucho", "poco", "otro", "varios", "todo", "cuánto", "mi", "tu", "su", "nuestro", "vuestro", "este", "ese", "aquel",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SubjectPronouns = new[]
    {
        "yo", "tú", "él", "ella", "usted", "nosotros", "nosotras", "vosotros", "vosotras", "ellos", "ellas", "ustedes",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> Clitics = new[] { "me", "te", "lo", "le", "nos", "les" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> FeminineNounsTakingEl = new[]
    {
        "agua", "arma", "hacha", "águila", "alma", "aula", "hada", "hambre", "área", "ala",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>True for adjectives that behave as determiners: quantifiers, possessives and demonstratives.</summary>
    public static bool IsDeterminerAdjective(string lemma)
    {
        return DeterminerAdjectives.Contains(lemma);
    }

    /// <summary>True for personal pronouns that can be the explicit subject of a verb.</summary>
    public static bool IsSubjectPronoun(string lemma)
    {
        return SubjectPronouns.Contains(lemma);
    }

    /// <summary>True for object pronouns that may sit between a subject pronoun and its verb.</summary>
    public static bool IsClitic(string lemma)
    {
        return Clitics.Contains(lemma);
    }

    /// <summary>True for feminine singular nouns that take <c>el</c>/<c>un</c> because they start with a stressed a sound.</summary>
    public static bool TakesElBeforeFeminine(string lemma)
    {
        return FeminineNounsTakingEl.Contains(lemma);
    }
}
