using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Normalization;

/// <summary>One word of a text as the tokenizer splits it: the lower-cased <paramref name="Text"/> and the NFC <paramref name="Original"/> spelling it came from.</summary>
/// <param name="Text">The word as the token text: NFC, invariant lower case, hyphen variants unified to <c>-</c>.</param>
/// <param name="Original">The NFC form of the source substring.</param>
public readonly record struct TokenizedWord(string Text, string Original);

/// <summary>
/// The language-neutral part of text normalization (M53): NFC, invariant lower case, punctuation dropped, white space collapsed, and the final
/// question or exclamation mark. It converts no numbers and expands no contractions: those are a language's rules, and a language pack builds its
/// <see cref="Packs.ITextNormalizer"/> from <see cref="Clean"/>, <see cref="Split"/>, its own word rules and <see cref="Build"/>. The Spanish pack's
/// normalizer is the Spanish composition of these pieces.
/// </summary>
/// <remarks>
/// Letters and digits are kept, together with the combining marks that follow them; hyphens between two word characters are kept; every other character
/// separates tokens. Ill-formed UTF-16 never throws: each lone surrogate is read as U+FFFD, which separates tokens.
/// <see cref="Token.Original"/> is the NFC form of the source substring the token came from.
/// </remarks>
public static class TextTokenizer
{
    /// <summary>Normalizes text without any language rule: split into words, no number conversion, no contraction expansion.</summary>
    /// <param name="input">The raw text.</param>
    /// <param name="options">Only <see cref="NormalizationOptions.CaptureFinalMark"/> is read; the other switches are language rules and are ignored here.</param>
    public static NormalizedText Tokenize(string input, NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= NormalizationOptions.Default;
        var clean = Clean(input);
        var built = Build(Split(clean));
        return options.CaptureFinalMark ? built with { FinalMark = FinalMarkOf(clean) } : built;
    }

    /// <summary>Replaces each lone surrogate by U+FFFD, so no later normalization throws.</summary>
    /// <param name="input">The raw text.</param>
    public static string Clean(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Utf16.ReplaceLoneSurrogates(input);
    }

    /// <summary>The last character that is neither white space nor a closing quote or bracket, when it is a question or exclamation mark (also the full-width forms); otherwise null.</summary>
    /// <param name="input">The cleaned text (see <see cref="Clean"/>).</param>
    public static string? FinalMarkOf(string input)
    {
        for (var i = input.Length - 1; i >= 0; i--)
        {
            var c = input[i];
            if (char.IsWhiteSpace(c) || c is '"' or '\'' or ')' or ']' or '\u00BB' or '\u201D' or '\u2019')
            {
                continue;
            }

            return c switch
            {
                '?' or '\uFF1F' => "?",
                '!' or '\uFF01' => "!",
                _ => null,
            };
        }

        return null;
    }

    /// <summary>Splits cleaned text into words (see <see cref="Clean"/>).</summary>
    /// <param name="input">The cleaned text.</param>
    public static List<TokenizedWord> Split(string input)
    {
        var words = new List<TokenizedWord>();
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
            words.Add(new TokenizedWord(text, original));
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

    /// <summary>Joins words with single spaces into <see cref="NormalizedText"/>, with one token per word.</summary>
    /// <param name="words">The words, in order.</param>
    public static NormalizedText Build(IReadOnlyList<TokenizedWord> words)
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
}
