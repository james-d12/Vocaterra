using System.Text;

namespace Vocaterra.Nlp.Es.Normalization;

/// <summary>
/// Culture-free text folding used for lexicon keys: NFC plus lowercase, and accent stripping that keeps the letter n-tilde; since M70 also the spelling with the
/// n-tilde typed as n, which the lexicon reads only for a word it lacks.
/// </summary>
internal static class TextFolding
{
    // The nouns and adjectives of IsWordSpelledWithN, singular and plural, in the folded spelling: año/ano, caña/cana, caño/cano, cañón/canon (cánones), coño/cono,
    // cuña/cuna, maña/mana, moña/mona, moño/mono, pañal/panal, peña/pena, piña/pina, piño/pino, seña/sena, tiña/tina, uña/una, and ceno (cenar) beside ceño.
    private static readonly HashSet<string> WordsSpelledWithN = new(StringComparer.Ordinal)
    {
        "ano", "anos", "cana", "canas", "cano", "canos", "canon", "canones", "ceno", "cono", "conos", "cuna", "cunas", "mana", "manas", "mona", "monas",
        "mono", "monos", "panal", "panales", "pena", "penas", "pina", "pinas", "pino", "pinos", "sena", "senas", "tina", "tinas", "una", "unas",
    };

    /// <summary>Returns the NFC form of the text, lowercased with the invariant culture.</summary>
    public static string Normalize(string text)
    {
        return TextKey.Of(text);
    }

    /// <summary>Normalizes and then maps acute-accented vowels and u-diaeresis to the plain vowel; n-tilde is kept.</summary>
    public static string StripAccents(string text)
    {
        var normalized = Normalize(text);
        var needsWork = false;
        foreach (var c in normalized)
        {
            if (Fold(c) != c)
            {
                needsWork = true;
                break;
            }
        }

        if (!needsWork)
        {
            return normalized;
        }

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            builder.Append(Fold(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The spelling of a word with each <c>ñ</c> typed as <c>n</c> and its accents stripped (M70, the owner's answer of 2026-11-01, D-game-22 item 2): <c>mañana</c>
    /// gives <c>manana</c>, <c>pequeño</c> <c>pequeno</c>, <c>español</c> <c>espanol</c>. Null for a word without <c>ñ</c>, and for one whose spelling with <c>n</c> is a
    /// Spanish word of its own (<see cref="IsWordSpelledWithN"/>: <c>año</c> and <c>ano</c>, <c>caña</c> and <c>cana</c>), which a reader cannot take for a slip.
    /// </summary>
    public static string? UnmarkedSpelling(string form)
    {
        var folded = StripAccents(form);
        if (!folded.Contains('ñ', StringComparison.Ordinal))
        {
            return null;
        }

        var plain = folded.Replace('ñ', 'n');
        return IsWordSpelledWithN(plain) ? null : plain;
    }

    /// <summary>
    /// True for a Spanish word that differs from another only in an <c>n</c> where the other has <c>ñ</c> (<c>ano</c> and <c>año</c>, <c>pena</c> and <c>peña</c>), from
    /// a closed list: the lexicon of a bundle may lack the word (the game's lacks <c>ano</c> and <c>cana</c>), and a spelling that is a word is never read as a slip.
    /// Forms of a verb whose paradigm the lexicon holds (<c>suena</c>, <c>sonar</c> beside <c>sueña</c>, <c>soñar</c>) are found in the lexicon instead.
    /// </summary>
    public static bool IsWordSpelledWithN(string folded)
    {
        return WordsSpelledWithN.Contains(folded);
    }

    private static char Fold(char c)
    {
        return c switch
        {
            'á' => 'a',
            'é' => 'e',
            'í' => 'i',
            'ó' => 'o',
            'ú' => 'u',
            'ü' => 'u',
            _ => c,
        };
    }
}
