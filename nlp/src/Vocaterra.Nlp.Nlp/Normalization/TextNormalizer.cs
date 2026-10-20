using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Nlp.Normalization;

/// <summary>
/// Turns raw learner or recognizer text into <see cref="NormalizedText"/>: NFC, invariant lowercase, punctuation removed,
/// whitespace collapsed, optional number-word conversion and optional contraction expansion.
/// </summary>
/// <remarks>
/// Letters (including <c>ñ</c>, accented vowels and <c>ü</c>) and digits are kept; hyphens between two word characters are kept;
/// every other character, including <c>¿ ¡ ? ! , . ; : " ' ( ) …</c>, separates tokens. Decimal and thousands separators are not
/// interpreted, so <c>1.000</c> becomes the two tokens <c>1</c> and <c>000</c>. Accents are never removed here; use <see cref="Accents.Strip"/> for matching.
/// Ill-formed UTF-16 never throws: each lone surrogate is read as U+FFFD, which separates tokens.
/// <see cref="Token.Original"/> is the NFC form of the source substring the token came from, so canonically equivalent inputs give equal results.
/// </remarks>
public static class TextNormalizer
{
    /// <summary>Normalizes text with the given options (default options when <paramref name="options"/> is null).</summary>
    public static NormalizedText Normalize(string input, NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= NormalizationOptions.Default;

        var words = Split(Utf16.ReplaceLoneSurrogates(input));
        if (options.NumbersToDigits)
        {
            words = ConvertNumbers(words, options);
        }

        if (options.ExpandContractions)
        {
            words = ExpandContractions(words);
        }

        return Build(words);
    }

    /// <summary>Replaces stand-alone digit runs (up to 999 999) with Spanish words, for example <c>2 espadas</c> becomes <c>dos espadas</c>.</summary>
    public static string DigitsToWords(string text)
    {
        return SpanishNumbers.DigitsToWords(text);
    }

    private static List<Word> Split(string input)
    {
        var words = new List<Word>();
        var i = 0;
        while (i < input.Length)
        {
            if (!char.IsLetterOrDigit(input[i]))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < input.Length)
            {
                var c = input[i];
                if (char.IsLetterOrDigit(c) || IsCombiningMark(c))
                {
                    i++;
                }
                else if (IsHyphen(c) && i + 1 < input.Length && char.IsLetterOrDigit(input[i + 1]))
                {
                    i++;
                }
                else
                {
                    break;
                }
            }

            var original = input.Substring(start, i - start).Normalize(NormalizationForm.FormC);
            var text = Lower(original);
            words.Add(new Word(text, original));
        }

        return words;
    }

    private static string Lower(string nfc)
    {
        var builder = new StringBuilder(nfc.Length);
        foreach (var c in nfc)
        {
            builder.Append(IsHyphen(c) ? '-' : c);
        }

        return builder.ToString().ToLowerInvariant().Normalize(NormalizationForm.FormC);
    }

    private static bool IsHyphen(char c)
    {
        return c is '-' or '‐' or '‑';
    }

    private static bool IsCombiningMark(char c)
    {
        return CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
    }

    private static List<Word> ConvertNumbers(List<Word> words, NormalizationOptions options)
    {
        var keys = new string[words.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = SpanishNumbers.KeyOf(words[i].Text);
        }

        var result = new List<Word>(words.Count);
        var index = 0;
        while (index < words.Count)
        {
            var used = SpanishNumbers.ParsePrefixOfKeys(keys, index, out var value);
            var keepArticle = used == 1
                && !options.ConvertIndefiniteArticles
                && keys[index] is "un" or "una";
            if (used == 0 || keepArticle)
            {
                result.Add(words[index]);
                index++;
                continue;
            }

            var original = used == 1 ? words[index].Original : JoinOriginals(words, index, used);
            result.Add(new Word(value.ToString(CultureInfo.InvariantCulture), original));
            index += used;
        }

        return result;
    }

    private static string JoinOriginals(List<Word> words, int index, int count)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(words[index + i].Original);
        }

        return builder.ToString();
    }

    private static List<Word> ExpandContractions(List<Word> words)
    {
        var result = new List<Word>(words.Count + 2);
        foreach (var word in words)
        {
            switch (word.Text)
            {
                case "al":
                    result.Add(new Word("a", word.Original));
                    result.Add(new Word("el", word.Original));
                    break;
                case "del":
                    result.Add(new Word("de", word.Original));
                    result.Add(new Word("el", word.Original));
                    break;
                default:
                    result.Add(word);
                    break;
            }
        }

        return result;
    }

    private static NormalizedText Build(List<Word> words)
    {
        var text = new StringBuilder();
        var tokens = new Token[words.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (i > 0)
            {
                text.Append(' ');
            }

            var word = words[i];
            tokens[i] = new Token(word.Text, new TextSpan(text.Length, word.Text.Length), word.Original);
            text.Append(word.Text);
        }

        return new NormalizedText(text.ToString(), tokens);
    }

    private readonly record struct Word(string Text, string Original);
}
