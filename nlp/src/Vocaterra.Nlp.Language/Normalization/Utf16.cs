using System.Text;

namespace Vocaterra.Nlp.Language.Normalization;

/// <summary>
/// Repairs ill-formed UTF-16 at the entry of the normalization functions. <c>string.Normalize</c> throws <see cref="ArgumentException"/>
/// on a lone surrogate, and a recognizer or a player's keyboard can deliver one; the entry points therefore replace each lone surrogate
/// with U+FFFD (the Unicode replacement character, which separates words like any other symbol) before any BCL normalization sees the text.
/// </summary>
internal static class Utf16
{
    private const char Replacement = (char)0xFFFD;

    /// <summary>Returns the text with every unpaired high or low surrogate replaced by U+FFFD; well-formed text is returned as is.</summary>
    public static string ReplaceLoneSurrogates(string text)
    {
        var first = FirstLoneSurrogate(text);
        if (first < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, first);
        for (var i = first; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else
            {
                builder.Append(char.IsSurrogate(text[i]) ? Replacement : text[i]);
            }
        }

        return builder.ToString();
    }

    private static int FirstLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsSurrogate(text[i]))
            {
                continue;
            }

            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }

            return i;
        }

        return -1;
    }
}
