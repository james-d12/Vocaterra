using System.Text;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// The language-neutral lookup key: Unicode NFC and invariant lower case. It is what <see cref="Packs.ILexiconFolding.Key"/> returns for Spanish, and what the
/// parsers of slot grammars and intent catalogs use when no pack's folding is given (M34).
/// </summary>
internal static class TextKey
{
    /// <summary>Returns the NFC form of the text, lowercased with the invariant culture.</summary>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Most lexicon words are plain lower-case ASCII letters; they are already normalized, so the common case allocates nothing.
        var plain = true;
        foreach (var c in text)
        {
            if (c is < 'a' or > 'z')
            {
                plain = false;
                break;
            }
        }

        return plain ? text : text.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
