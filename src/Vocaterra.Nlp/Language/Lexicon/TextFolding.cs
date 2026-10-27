using System.Text;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>Culture-free text folding used for lexicon keys: NFC plus lowercase, and accent stripping that keeps the letter n-tilde.</summary>
internal static class TextFolding
{
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
