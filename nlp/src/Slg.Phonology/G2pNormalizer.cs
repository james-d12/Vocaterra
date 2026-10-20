using System.Globalization;
using System.Text;
using Slg.Core.Stages;
using Slg.Core.Verdicts;

namespace Slg.Phonology;

/// <summary>
/// Stage 1 of the G2P: Unicode NFC, invariant lowercase, accents/ñ/ü kept, everything that is not a
/// letter treated as a word boundary, words joined by single spaces.
/// </summary>
internal static class G2pNormalizer
{
    private const string SpanishLetters = "abcdefghijklmnopqrstuvwxyzáéíóúüñ";

    /// <summary>
    /// Normalizes text into words. Letters outside the Spanish alphabet are reduced to their base Latin letter
    /// when they have one (<c>ç</c> to <c>c</c>) and otherwise act as separators; digits and punctuation are separators.
    /// A lone surrogate (ill-formed UTF-16, which <c>string.Normalize</c> rejects) is a separator too.
    /// </summary>
    public static NormalizedText Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var composed = ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormC);

        var words = new List<(string Word, string Original)>();
        var word = new StringBuilder();
        var original = new StringBuilder();

        foreach (var character in composed)
        {
            var letter = Fold(character);
            if (letter is { } kept)
            {
                word.Append(kept);
                original.Append(character);
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            Flush(words, word, original);
        }

        Flush(words, word, original);

        var normalized = new StringBuilder();
        var tokens = new List<Token>(words.Count);
        foreach (var (value, surface) in words)
        {
            if (normalized.Length > 0)
            {
                normalized.Append(' ');
            }

            tokens.Add(new Token(value, new TextSpan(normalized.Length, value.Length), surface));
            normalized.Append(value);
        }

        return new NormalizedText(normalized.ToString(), tokens);
    }

    // Each unpaired surrogate becomes U+FFFD, which is not a letter and so separates words; valid pairs are left alone.
    private static string ReplaceLoneSurrogates(string text)
    {
        var chars = (char[]?)null;
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

            chars ??= text.ToCharArray();
            chars[i] = (char)0xFFFD;
        }

        return chars is null ? text : new string(chars);
    }

    private static void Flush(List<(string Word, string Original)> words, StringBuilder word, StringBuilder original)
    {
        if (word.Length == 0)
        {
            return;
        }

        words.Add((word.ToString(), original.ToString()));
        word.Clear();
        original.Clear();
    }

    private static char? Fold(char character)
    {
        if (!char.IsLetter(character))
        {
            return null;
        }

        var lower = char.ToLowerInvariant(character);
        if (SpanishLetters.Contains(lower, StringComparison.Ordinal))
        {
            return lower;
        }

        var decomposed = lower.ToString().Normalize(NormalizationForm.FormD);
        if (decomposed.Length > 0 && decomposed[0] is >= 'a' and <= 'z')
        {
            return decomposed[0];
        }

        return null;
    }
}
