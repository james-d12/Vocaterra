using System.Text;

namespace Slg.Nlp.Normalization;

/// <summary>
/// Collapses Spanish spellings that sound alike (seseo and yeísmo, the Latin American default) into one lowercase ASCII key.
/// </summary>
/// <remarks>
/// Rules: <c>b/v</c> map to <c>b</c>, <c>ll/y</c> to <c>y</c>,
/// <c>z</c>, <c>s</c> and <c>c</c> before <c>e/i</c> to <c>s</c>, <c>h</c> is dropped, <c>ch</c> becomes the single symbol <c>c</c>,
/// <c>qu</c>, <c>k</c> and <c>c</c> elsewhere to <c>k</c>, <c>gu</c> before <c>e/i</c> to <c>g</c> (<c>gü</c> keeps its sound as <c>gw</c>),
/// <c>j</c> and <c>g</c> before <c>e/i</c> to <c>x</c>, <c>x</c> to <c>ks</c>, <c>ñ</c> to <c>ny</c>, accents are stripped, and doubled
/// letters collapse except <c>rr</c>. Digits are kept; any other character separates words, which are joined by a single space.
/// </remarks>
public static class SpanishPhoneticKey
{
    /// <summary>Computes the phonetic key of a word or phrase.</summary>
    public static string Compute(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        // The diaeresis must be read before accents are stripped: "gü" sounds /gw/, plain "gu" before e/i does not.
        var lowered = word.Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace("gü", "gw", StringComparison.Ordinal);
        var plain = Accents.Strip(lowered);

        var key = new StringBuilder(plain.Length);
        var i = 0;
        while (i < plain.Length)
        {
            if (!IsKeyChar(plain[i]))
            {
                i++;
                continue;
            }

            var end = i;
            while (end < plain.Length && IsKeyChar(plain[end]))
            {
                end++;
            }

            if (key.Length > 0)
            {
                key.Append(' ');
            }

            AppendWord(key, plain.AsSpan(i, end - i));
            i = end;
        }

        return key.ToString();
    }

    private static bool IsKeyChar(char c)
    {
        return c is >= 'a' and <= 'z' or >= '0' and <= '9' or 'ñ';
    }

    private static void AppendWord(StringBuilder key, ReadOnlySpan<char> word)
    {
        var wordStart = key.Length;
        var i = 0;
        while (i < word.Length)
        {
            var c = word[i];
            var next = i + 1 < word.Length ? word[i + 1] : '\0';
            var afterNext = i + 2 < word.Length ? word[i + 2] : '\0';
            var softVowelNext = next is 'e' or 'i';
            switch (c)
            {
                case 'b' or 'v':
                    Emit(key, wordStart, 'b');
                    break;
                case 'c' when next == 'h':
                    Emit(key, wordStart, 'c');
                    i++;
                    break;
                case 'c':
                    Emit(key, wordStart, softVowelNext ? 's' : 'k');
                    break;
                case 'g' when softVowelNext:
                    Emit(key, wordStart, 'x');
                    break;
                case 'g' when next == 'u' && afterNext is 'e' or 'i':
                    Emit(key, wordStart, 'g');
                    i++;
                    break;
                case 'h':
                    break;
                case 'j':
                    Emit(key, wordStart, 'x');
                    break;
                case 'l' when next == 'l':
                    Emit(key, wordStart, 'y');
                    i++;
                    break;
                case 'q' when next == 'u' && afterNext is 'e' or 'i':
                    Emit(key, wordStart, 'k');
                    i++;
                    break;
                case 'q' or 'k':
                    Emit(key, wordStart, 'k');
                    break;
                case 'x':
                    Emit(key, wordStart, 'k');
                    Emit(key, wordStart, 's');
                    break;
                case 'z':
                    Emit(key, wordStart, 's');
                    break;
                case 'ñ':
                    Emit(key, wordStart, 'n');
                    Emit(key, wordStart, 'y');
                    break;
                default:
                    Emit(key, wordStart, c);
                    break;
            }

            i++;
        }
    }

    // Appends a symbol, collapsing doubled letters (but not digits) and allowing at most two r in a row.
    private static void Emit(StringBuilder key, int wordStart, char symbol)
    {
        var length = key.Length;
        if (symbol is >= 'a' and <= 'z' && length > wordStart && key[length - 1] == symbol)
        {
            var allowDouble = symbol == 'r' && (length - 1 == wordStart || key[length - 2] != 'r');
            if (!allowDouble)
            {
                return;
            }
        }

        key.Append(symbol);
    }
}
