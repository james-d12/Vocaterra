using System.Globalization;
using System.Text;

namespace Vocaterra.Nlp.Es.Normalization;

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
///
/// Spanish compatibility entry point (M33): the Spanish normalizer behind <c>SpanishLanguagePack.Normalizer</c>. The pipeline, the grader and the content tools call <c>bundle.Language.Normalizer</c>; this static keeps its Spanish meaning for the game and the tools.
/// </remarks>
public static class SpanishTextNormalizer
{
    /// <summary>Normalizes text with the given options (default options when <paramref name="options"/> is null).</summary>
    public static NormalizedText Normalize(string input, NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= NormalizationOptions.Default;

        var clean = TextTokenizer.Clean(input);
        var words = TextTokenizer.Split(clean);
        if (options.NumbersToDigits)
        {
            words = ConvertNumbers(words, options);
        }

        if (options.ExpandContractions)
        {
            words = ExpandContractions(words);
        }

        var built = TextTokenizer.Build(words);
        return options.CaptureFinalMark ? built with { FinalMark = TextTokenizer.FinalMarkOf(clean) } : built;
    }

    /// <summary>Replaces stand-alone digit runs (up to 999 999) with Spanish words, for example <c>2 espadas</c> becomes <c>dos espadas</c>.</summary>
    public static string DigitsToWords(string text)
    {
        return SpanishNumbers.DigitsToWords(text);
    }

    private static List<TokenizedWord> ConvertNumbers(List<TokenizedWord> words, NormalizationOptions options)
    {
        var keys = new string[words.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = SpanishNumbers.KeyOf(words[i].Text);
        }

        var result = new List<TokenizedWord>(words.Count);
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
            result.Add(new TokenizedWord(value.ToString(CultureInfo.InvariantCulture), original));
            index += used;
        }

        return result;
    }

    private static string JoinOriginals(List<TokenizedWord> words, int index, int count)
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

    private static List<TokenizedWord> ExpandContractions(List<TokenizedWord> words)
    {
        var result = new List<TokenizedWord>(words.Count + 2);
        foreach (var word in words)
        {
            switch (word.Text)
            {
                case "al":
                    result.Add(new TokenizedWord("a", word.Original));
                    result.Add(new TokenizedWord("el", word.Original));
                    break;
                case "del":
                    result.Add(new TokenizedWord("de", word.Original));
                    result.Add(new TokenizedWord("el", word.Original));
                    break;
                default:
                    result.Add(word);
                    break;
            }
        }

        return result;
    }

}
