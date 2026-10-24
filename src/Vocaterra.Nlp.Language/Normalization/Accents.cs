using System.Globalization;
using System.Text;

namespace Vocaterra.Nlp.Language.Normalization;

/// <summary>Accent removal for matching only; it is never applied to <c>NormalizedText.Text</c>.</summary>
public static class Accents
{
    private const char CombiningTilde = '̃';

    /// <summary>
    /// Removes combining accents (acute, grave, diaeresis, cedilla and so on) and returns the result in NFC.
    /// The tilde of <c>ñ</c> is kept because <c>ñ</c> is a letter of its own: <c>año</c> and <c>ano</c> are different words.
    /// A lone surrogate in the text is replaced by U+FFFD first instead of making the normalization throw.
    /// </summary>
    public static string Strip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = Utf16.ReplaceLoneSurrogates(text);
        if (IsPlainAscii(text))
        {
            return text;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                var keepTilde = c == CombiningTilde && builder.Length > 0 && builder[^1] is 'n' or 'N';
                if (!keepTilde)
                {
                    continue;
                }
            }

            builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsPlainAscii(string text)
    {
        foreach (var c in text)
        {
            if (c > '\u007f')
            {
                return false;
            }
        }

        return true;
    }
}
