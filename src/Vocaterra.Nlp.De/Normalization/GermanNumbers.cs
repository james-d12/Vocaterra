using System.Globalization;
using System.Text;

namespace Vocaterra.Nlp.De.Normalization;

/// <summary>
/// German number words (M37): 0 to 999 999 spelled out and read back. German writes a number below one million as one word
/// (<c>einundzwanzig</c>, <c>hundertvierzig</c>, <c>zweitausenddreihundert</c>), the units before the tens (<c>fünfundvierzig</c>).
/// </summary>
/// <remarks>
/// <see cref="ToWords"/> writes the standard form with <c>ß</c> and umlauts (<c>dreißig</c>, <c>fünf</c>, <c>zwölf</c>) and no leading <c>ein</c> before the first
/// <c>hundert</c> or <c>tausend</c> (<c>hundert</c>, <c>tausend</c>, <c>hunderttausend</c>; <c>eintausend</c> is not written, but read), and <c>ein</c> where the unit
/// stands before a scale word (<c>hunderteintausend</c>) and <c>eins</c> at the end (<c>hunderteins</c>, <c>zweihunderteins</c>). The parser reads those and every
/// spelling a learner or a recognizer may write: the umlauts and <c>ß</c> as <c>ae oe ue ss</c> (<c>fuenf</c>, <c>dreissig</c>) or the umlaut bare (<c>funf</c>, <c>zwolf</c>), <c>zwo</c> for <c>zwei</c>, <c>ein</c> and <c>eins</c> in either place, and a number written as
/// several tokens where the break is before or after a <c>hundert</c> or <c>tausend</c> (<c>zwei hundert vierzig</c>, <c>zweitausend dreihundert</c>); a break
/// elsewhere is two numbers (<c>drei zehn</c> is not thirteen). The article forms <c>ein eine einen einem einer eines</c> are one when they stand alone.
/// </remarks>
public static class GermanNumbers
{
    /// <summary>The largest value <see cref="ToWords"/> and the parsers handle.</summary>
    public const int MaxValue = 999_999;

    private const int MaxTokens = 5;

    private static readonly string[] Units = ["", "ein", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun"];

    private static readonly string[] Teens = ["zehn", "elf", "zwölf", "dreizehn", "vierzehn", "fünfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn"];

    private static readonly string[] Tens = ["", "", "zwanzig", "dreißig", "vierzig", "fünfzig", "sechzig", "siebzig", "achtzig", "neunzig"];

    private static readonly string[] ArticleForms = ["ein", "eine", "einen", "einem", "einer", "eines"];

    // The words below one hundred and their values, in the folded spelling (the umlauts as ae oe ue, the sharp s as ss); unit and tens words are one list.
    private static readonly Dictionary<string, int> Below100 = BuildBelow100();

    private static readonly Dictionary<string, int> UnitValues = BuildUnits();

    /// <summary>Spells an integer from 0 to <see cref="MaxValue"/> in words.</summary>
    /// <param name="value">The number.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is below 0 or above <see cref="MaxValue"/>.</exception>
    public static string ToWords(int value)
    {
        if (value < 0 || value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"German number words cover 0 to {MaxValue}.");
        }

        if (value == 0)
        {
            return "null";
        }

        var thousands = value / 1000;
        var rest = value % 1000;
        var builder = new StringBuilder();
        if (thousands > 0)
        {
            // "tausend" alone for one thousand; the group before it ends in "ein", never "eins".
            if (thousands > 1)
            {
                builder.Append(Group(thousands, final: false, atStart: true));
            }

            builder.Append("tausend");
        }

        if (rest > 0)
        {
            builder.Append(Group(rest, final: true, atStart: thousands == 0));
        }

        return builder.ToString();
    }

    /// <summary>Parses a whole token sequence as one number (the tokens are joined where a break is allowed, see the class remarks).</summary>
    /// <param name="tokens">The tokens, lower case or not.</param>
    /// <param name="value">The number, or 0.</param>
    public static bool TryParse(IReadOnlyList<string> tokens, out int value)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        value = 0;
        if (tokens.Count == 0 || tokens.Count > MaxTokens)
        {
            return false;
        }

        return TryParsePrefix(tokens, 0, out value) == tokens.Count;
    }

    /// <summary>Parses a text of number words (separated by white space) as one number.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number, or 0.</param>
    public static bool TryParse(string text, out int value)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), out value);
    }

    /// <summary>
    /// Parses the longest number that starts at <paramref name="start"/>; returns the number of tokens consumed (0 when none). A token that is an article form
    /// (<c>ein</c>, <c>eine</c>, <c>einen</c>, <c>einem</c>, <c>einer</c>, <c>eines</c>) alone is the number 1, as the Spanish <c>un</c> is: it is also an article, and
    /// the normalizer never turns it into a digit.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="start">The index of the first token.</param>
    /// <param name="value">The number, or 0.</param>
    public static int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        value = 0;
        if (start < 0 || start >= tokens.Count)
        {
            return 0;
        }

        var keys = new string[Math.Min(MaxTokens, tokens.Count - start)];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = KeyOf(tokens[start + i]);
        }

        return ParsePrefixOfKeys(keys, 0, out value);
    }

    /// <summary>Replaces stand-alone digit runs of up to six digits (no leading zero) with words, for example <c>2 Brote</c> becomes <c>zwei Brote</c>.</summary>
    /// <param name="text">The text.</param>
    public static string DigitsToWords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + 16);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var startsRun = c is >= '0' and <= '9' && (i == 0 || !char.IsLetterOrDigit(text[i - 1]));
            if (!startsRun)
            {
                builder.Append(c);
                i++;
                continue;
            }

            var end = i;
            while (end < text.Length && text[end] is >= '0' and <= '9')
            {
                end++;
            }

            var length = end - i;
            var boundary = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (boundary && length <= 6 && (length == 1 || c != '0'))
            {
                builder.Append(ToWords(int.Parse(text.AsSpan(i, length), NumberStyles.None, CultureInfo.InvariantCulture)));
            }
            else
            {
                builder.Append(text, i, length);
            }

            i = end;
        }

        return builder.ToString();
    }

    /// <summary>True for <c>ein</c>, <c>eine</c>, <c>einen</c>, <c>einem</c>, <c>einer</c> and <c>eines</c>: the indefinite article, which is also the number one.</summary>
    /// <param name="token">The token, lower case.</param>
    public static bool IsArticleForm(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Array.IndexOf(ArticleForms, token) >= 0;
    }

    /// <summary>The folded spelling of a token for number parsing: lower case, the umlauts as ae oe ue and the sharp s as ss; lone surrogates read as U+FFFD.</summary>
    internal static string KeyOf(string token)
    {
        return GermanFolding.Instance.LenientKey(token);
    }

    // The longest number among the keys from start on (each key in KeyOf form); the break rule joins consecutive keys.
    internal static int ParsePrefixOfKeys(string[] keys, int start, out int value)
    {
        value = 0;
        var longest = 0;
        var joined = new StringBuilder();
        for (var count = 1; start + count <= keys.Length; count++)
        {
            var index = start + count - 1;
            if (count > 1 && !MayJoin(keys[index - 1], keys[index]))
            {
                break;
            }

            joined.Append(keys[index]);
            if (TryParseWord(joined.ToString(), out var parsed))
            {
                longest = count;
                value = parsed;
            }
            else if (count == 1 && IsArticleForm(keys[index]))
            {
                longest = 1;
                value = 1;
            }
        }

        return longest;
    }

    private static bool MayJoin(string before, string after)
    {
        return before.EndsWith("hundert", StringComparison.Ordinal)
            || before.EndsWith("tausend", StringComparison.Ordinal)
            || after.StartsWith("hundert", StringComparison.Ordinal)
            || after.StartsWith("tausend", StringComparison.Ordinal);
    }

    private static string Group(int n, bool final, bool atStart)
    {
        var builder = new StringBuilder();
        var hundreds = n / 100;
        var rest = n % 100;
        if (hundreds > 0)
        {
            if (hundreds > 1 || !atStart)
            {
                builder.Append(Units[hundreds]);
            }

            builder.Append("hundert");
        }

        builder.Append(Below(rest, final));
        return builder.ToString();
    }

    private static string Below(int n, bool final)
    {
        if (n == 0)
        {
            return string.Empty;
        }

        if (n < 10)
        {
            return n == 1 && final ? "eins" : Units[n];
        }

        if (n < 20)
        {
            return Teens[n - 10];
        }

        var unit = n % 10;
        var ten = Tens[n / 10];
        return unit == 0 ? ten : Units[unit] + "und" + ten;
    }

    // Reads one word (one folded token or several joined at allowed breaks) as a number from 0 to 999 999.
    private static bool TryParseWord(string word, out int value)
    {
        value = 0;
        if (word.Length == 0)
        {
            return false;
        }

        if (word == "null")
        {
            return true;
        }

        var scale = word.IndexOf("tausend", StringComparison.Ordinal);
        if (scale < 0)
        {
            return TryParseBelow1000(word, out value) && value > 0;
        }

        var thousands = 1;
        if (scale > 0 && (!TryParseBelow1000(word[..scale], out thousands) || thousands == 0))
        {
            return false;
        }

        var rest = 0;
        var tail = word[(scale + 7)..];
        if (tail.Length > 0 && (!TryParseBelow1000(tail, out rest) || rest == 0))
        {
            return false;
        }

        value = (thousands * 1000) + rest;
        return true;
    }

    private static bool TryParseBelow1000(string word, out int value)
    {
        value = 0;
        var scale = word.IndexOf("hundert", StringComparison.Ordinal);
        if (scale < 0)
        {
            return TryParseBelow100(word, out value);
        }

        var hundreds = 1;
        if (scale > 0 && (!UnitValues.TryGetValue(word[..scale], out hundreds) || hundreds == 0))
        {
            return false;
        }

        var rest = 0;
        var tail = word[(scale + 7)..];
        if (tail.Length > 0 && (!TryParseBelow100(tail, out rest) || rest == 0))
        {
            return false;
        }

        value = (hundreds * 100) + rest;
        return true;
    }

    private static bool TryParseBelow100(string word, out int value)
    {
        if (Below100.TryGetValue(word, out value))
        {
            return true;
        }

        // unit + "und" + tens: einundzwanzig, fuenfundvierzig.
        var and = word.IndexOf("und", StringComparison.Ordinal);
        if (and > 0
            && UnitValues.TryGetValue(word[..and], out var unit)
            && unit > 0
            && Below100.TryGetValue(word[(and + 3)..], out var ten)
            && ten >= 20
            && ten % 10 == 0)
        {
            value = ten + unit;
            return true;
        }

        value = 0;
        return false;
    }

    private static Dictionary<string, int> BuildUnits()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 1; i < Units.Length; i++)
        {
            map[GermanFolding.Instance.LenientKey(Units[i])] = i;
        }

        map["funf"] = 5;

        map["eins"] = 1;
        map["zwo"] = 2;
        return map;
    }

    private static Dictionary<string, int> BuildBelow100()
    {
        var map = BuildUnits();
        for (var i = 0; i < Teens.Length; i++)
        {
            map[GermanFolding.Instance.LenientKey(Teens[i])] = 10 + i;
        }

        for (var i = 2; i < Tens.Length; i++)
        {
            map[GermanFolding.Instance.LenientKey(Tens[i])] = i * 10;
        }

        // The umlaut typed as its bare vowel: funf, zwolf, funfzehn, funfzig.
        foreach (var word in new[] { "fünf", "zwölf", "fünfzehn", "fünfzig" })
        {
            map[word.Replace('ü', 'u').Replace('ö', 'o')] = map[GermanFolding.Instance.LenientKey(word)];
        }

        return map;
    }
}
