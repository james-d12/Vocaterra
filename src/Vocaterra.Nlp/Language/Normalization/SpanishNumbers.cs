using System.Globalization;
using System.Text;

namespace Vocaterra.Nlp.Language.Normalization;

/// <summary>Conversion between Spanish cardinal number words and integers in the range 0 to 999 999.</summary>
public static class SpanishNumbers
{
    /// <summary>Largest value that can be written or parsed.</summary>
    public const int MaxValue = 999_999;

    // Lookup keys are lowercase and accent-free so that "veintidós" and "veintidos" are both accepted.
    private static readonly Dictionary<string, int> UnitsAndTeens = new(StringComparer.Ordinal)
    {
        ["un"] = 1,
        ["uno"] = 1,
        ["una"] = 1,
        ["dos"] = 2,
        ["tres"] = 3,
        ["cuatro"] = 4,
        ["cinco"] = 5,
        ["seis"] = 6,
        ["siete"] = 7,
        ["ocho"] = 8,
        ["nueve"] = 9,
        ["diez"] = 10,
        ["once"] = 11,
        ["doce"] = 12,
        ["trece"] = 13,
        ["catorce"] = 14,
        ["quince"] = 15,
        ["dieciseis"] = 16,
        ["diecisiete"] = 17,
        ["dieciocho"] = 18,
        ["diecinueve"] = 19,
        ["veinte"] = 20,
        ["veintiun"] = 21,
        ["veintiuno"] = 21,
        ["veintiuna"] = 21,
        ["veintidos"] = 22,
        ["veintitres"] = 23,
        ["veinticuatro"] = 24,
        ["veinticinco"] = 25,
        ["veintiseis"] = 26,
        ["veintisiete"] = 27,
        ["veintiocho"] = 28,
        ["veintinueve"] = 29,
    };

    private static readonly Dictionary<string, int> Tens = new(StringComparer.Ordinal)
    {
        ["treinta"] = 30,
        ["cuarenta"] = 40,
        ["cincuenta"] = 50,
        ["sesenta"] = 60,
        ["setenta"] = 70,
        ["ochenta"] = 80,
        ["noventa"] = 90,
    };

    private static readonly Dictionary<string, int> Hundreds = new(StringComparer.Ordinal)
    {
        ["cien"] = 100,
        ["ciento"] = 100,
        ["doscientos"] = 200,
        ["doscientas"] = 200,
        ["trescientos"] = 300,
        ["trescientas"] = 300,
        ["cuatrocientos"] = 400,
        ["cuatrocientas"] = 400,
        ["quinientos"] = 500,
        ["quinientas"] = 500,
        ["seiscientos"] = 600,
        ["seiscientas"] = 600,
        ["setecientos"] = 700,
        ["setecientas"] = 700,
        ["ochocientos"] = 800,
        ["ochocientas"] = 800,
        ["novecientos"] = 900,
        ["novecientas"] = 900,
    };

    private static readonly string[] UnitWords =
    [
        "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez",
        "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve",
        "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis",
        "veintisiete", "veintiocho", "veintinueve",
    ];

    private static readonly string[] TensWords =
        ["", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];

    private static readonly string[] HundredWords =
    [
        "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos",
        "ochocientos", "novecientos",
    ];

    /// <summary>
    /// Writes a value as Spanish words with written accents, for example 22 as <c>veintidós</c> and 21000 as <c>veintiún mil</c>.
    /// One is written <c>uno</c>, or <c>un</c> before <c>mil</c>.
    /// </summary>
    public static string ToWords(int value)
    {
        if (value is < 0 or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between 0 and {MaxValue}.");
        }

        if (value == 0)
        {
            return UnitWords[0];
        }

        var builder = new StringBuilder();
        var thousands = value / 1000;
        var rest = value % 1000;
        if (thousands == 1)
        {
            builder.Append("mil");
        }
        else if (thousands > 1)
        {
            AppendBelowThousand(builder, thousands, apocopated: true);
            builder.Append(" mil");
        }

        if (rest > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            AppendBelowThousand(builder, rest, apocopated: false);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a whole sequence of lowercase tokens as one number. Accents are optional; the sequence must be consumed completely.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> tokens, out int value)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var keys = ToKeys(tokens);
        var consumed = ParseNumber(keys, 0, out value);
        if (consumed > 0 && consumed == keys.Length)
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Parses whitespace-separated words as one number; see <see cref="TryParse(IReadOnlyList{string}, out int)"/>.</summary>
    public static bool TryParse(string text, out int value)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), out value);
    }

    /// <summary>
    /// Parses the longest number that starts at <paramref name="start"/> and returns how many tokens it used (0 when there is none).
    /// </summary>
    public static int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (start < 0 || start > tokens.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        var keys = ToKeys(tokens);
        return ParseNumber(keys, start, out value);
    }

    /// <summary>
    /// Replaces every stand-alone run of up to six ASCII digits in the text with its Spanish words; runs with a leading zero are kept.
    /// </summary>
    public static string DigitsToWords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + 16);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var startsRun = IsAsciiDigit(c) && (i == 0 || !char.IsLetterOrDigit(text[i - 1]));
            if (!startsRun)
            {
                builder.Append(c);
                i++;
                continue;
            }

            var end = i;
            while (end < text.Length && IsAsciiDigit(text[end]))
            {
                end++;
            }

            var length = end - i;
            var boundary = end == text.Length || !char.IsLetterOrDigit(text[end]);
            var convertible = boundary && length <= 6 && (length == 1 || c != '0');
            if (convertible)
            {
                var number = int.Parse(text.AsSpan(i, length), NumberStyles.None, CultureInfo.InvariantCulture);
                builder.Append(ToWords(number));
            }
            else
            {
                builder.Append(text, i, length);
            }

            i = end;
        }

        return builder.ToString();
    }

    internal static string KeyOf(string token)
    {
        return Accents.Strip(token.ToLowerInvariant());
    }

    internal static string[] ToKeys(IReadOnlyList<string> tokens)
    {
        var keys = new string[tokens.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = KeyOf(tokens[i]);
        }

        return keys;
    }

    internal static int ParsePrefixOfKeys(string[] keys, int start, out int value)
    {
        return ParseNumber(keys, start, out value);
    }

    private static bool IsAsciiDigit(char c)
    {
        return c is >= '0' and <= '9';
    }

    private static void AppendBelowThousand(StringBuilder builder, int value, bool apocopated)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        if (hundreds > 0)
        {
            builder.Append(hundreds == 1 && rest == 0 ? "cien" : HundredWords[hundreds]);
            if (rest > 0)
            {
                builder.Append(' ');
            }
        }

        if (rest == 0)
        {
            return;
        }

        if (rest < 30)
        {
            builder.Append(Unit(rest, apocopated));
            return;
        }

        builder.Append(TensWords[rest / 10]);
        if (rest % 10 > 0)
        {
            builder.Append(" y ").Append(Unit(rest % 10, apocopated));
        }
    }

    private static string Unit(int value, bool apocopated)
    {
        if (apocopated && value == 1)
        {
            return "un";
        }

        return apocopated && value == 21 ? "veintiún" : UnitWords[value];
    }

    // Parses a number starting at keys[start]; returns the token count, or 0 when no number starts there.
    private static int ParseNumber(string[] keys, int start, out int value)
    {
        value = 0;
        if (start >= keys.Length)
        {
            return 0;
        }

        if (keys[start] == "cero")
        {
            return 1;
        }

        var next = ParseBelowThousand(keys, start, out var below);
        int thousands;
        int afterMil;
        if (next > start && next < keys.Length && keys[next] == "mil")
        {
            thousands = below;
            afterMil = next + 1;
        }
        else if (next == start && keys[start] == "mil")
        {
            thousands = 1;
            afterMil = start + 1;
        }
        else if (next > start)
        {
            value = below;
            return next - start;
        }
        else
        {
            return 0;
        }

        var restEnd = ParseBelowThousand(keys, afterMil, out var rest);
        if (restEnd > afterMil)
        {
            value = (thousands * 1000) + rest;
            return restEnd - start;
        }

        value = thousands * 1000;
        return afterMil - start;
    }

    // Parses 1..999 at keys[start]; returns the index after the number, or start when nothing parses.
    private static int ParseBelowThousand(string[] keys, int start, out int value)
    {
        value = 0;
        var index = start;
        if (index >= keys.Length)
        {
            return start;
        }

        if (Hundreds.TryGetValue(keys[index], out var hundreds))
        {
            value = hundreds;
            index++;
            if (keys[index - 1] == "cien")
            {
                return index;
            }
        }

        if (index < keys.Length)
        {
            if (UnitsAndTeens.TryGetValue(keys[index], out var unit))
            {
                value += unit;
                index++;
            }
            else if (Tens.TryGetValue(keys[index], out var ten))
            {
                value += ten;
                index++;
                if (index + 1 < keys.Length
                    && keys[index] == "y"
                    && UnitsAndTeens.TryGetValue(keys[index + 1], out var digit)
                    && digit < 10)
                {
                    value += digit;
                    index += 2;
                }
            }
        }

        return index;
    }
}
