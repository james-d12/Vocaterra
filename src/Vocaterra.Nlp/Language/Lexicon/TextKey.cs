using System.Text;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// The language-neutral lookup key: Unicode NFC and invariant lower case. It is what <see cref="Packs.ILexiconFolding.Key"/> returns for Spanish, and what the
/// parsers of slot grammars and intent catalogs use when no pack's folding is given (M34).
/// </summary>
public static class TextKey
{
    /// <summary>
    /// Returns the NFC form of the text, lowercased with the invariant culture. A lone surrogate reads as U+FFFD first, so the key of any string is
    /// computed (NFC throws on a lone surrogate; the pack members are total, as the O1 oracle requires; M35).
    /// </summary>
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

        return plain ? text : Utf16.ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
