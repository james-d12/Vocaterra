using System.Globalization;
using System.Text;

namespace Vocaterra.Nlp.De.Normalization;

/// <summary>
/// The German pack's <see cref="IPhoneticKey"/> (M37): collapses German spellings that sound alike into one lower-case ASCII key, as the Spanish key does for
/// Spanish. Answer matching takes the smaller of the spelling distance and the distance between two keys, so a word a recognizer heard right and spelled wrong
/// (<c>Fater</c> for <c>Vater</c>, <c>Schtrasse</c> for <c>Straße</c>) is a sound-alike and not a mistake of the learner.
/// </summary>
/// <remarks>
/// The key works on the letters as the matching key delivers them (<c>ä ö ü ß</c> as <c>ae oe ue ss</c>, see <see cref="GermanFolding.MatchingKey"/>) and on the
/// umlaut letters themselves. Rules, in order of reading: <c>ä</c> and <c>ae</c> are <c>e</c> (they sound alike), <c>ö</c> and <c>oe</c> stay <c>oe</c>, <c>ü</c> and
/// <c>ue</c> stay <c>ue</c> (a vowel of their own: <c>schön</c> and <c>schon</c> differ); <c>eu</c> and <c>äu</c> are <c>oy</c>, <c>ei ai ey ay</c> are <c>ai</c>,
/// <c>ie</c> is <c>i</c>, a doubled vowel is one (<c>Haar</c>, <c>See</c>), an <c>h</c> after a vowel is not sounded (<c>sehen</c>, <c>Bahn</c>); <c>sch</c> and <c>tsch</c>
/// are <c>c</c>, <c>ch</c> is <c>x</c>, <c>chs</c> is <c>ks</c>, <c>ck</c> is <c>k</c>, <c>qu</c> is <c>kv</c>, <c>ph</c> and <c>v</c> are <c>f</c>, <c>w</c> is <c>v</c>, <c>z</c>, <c>tz</c>, <c>ts</c> and <c>c</c> before
/// a front vowel are <c>z</c>, <c>c</c> elsewhere <c>k</c>, <c>j</c> and <c>y</c> are <c>y</c>, <c>x</c> is <c>ks</c>, <c>dt</c> and <c>th</c> are <c>t</c>, <c>s</c>, <c>ss</c> and <c>ß</c> are <c>s</c>, an initial
/// <c>st</c> and <c>sp</c> are <c>ct</c> and <c>cp</c>; a final <c>b d g</c> is <c>p t k</c> (the final devoicing); doubled consonants and vowels collapse (doubled digits do not); accents of other letters
/// are stripped. Digits are kept; any other character separates words, which are joined by a single space. A lone surrogate counts as U+FFFD.
/// </remarks>
public sealed class GermanPhoneticKey : IPhoneticKey
{
    private GermanPhoneticKey()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanPhoneticKey Instance { get; } = new();

    /// <summary>Computes the phonetic key of a word or phrase.</summary>
    /// <param name="word">The word or phrase, in any case.</param>
    public static string Key(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var plain = Letters(Utf16.ReplaceLoneSurrogates(word).Normalize(NormalizationForm.FormC).ToLowerInvariant());
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

    /// <inheritdoc />
    string IPhoneticKey.Compute(string word) => Key(word);

    // ä ö ü ß as the lower-case ASCII digraphs; every other mark is stripped (café, naïve).
    private static string Letters(string lowered)
    {
        var folded = lowered.Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal).Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal);
        var decomposed = folded.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsKeyChar(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';

    private static void AppendWord(StringBuilder key, ReadOnlySpan<char> word)
    {
        var start = key.Length;
        var i = 0;
        while (i < word.Length)
        {
            var c = word[i];
            var rest = word[i..];
            var first = i == 0;
            var afterVowel = i > 0 && IsVowel(word[i - 1]);

            // Three- and four-letter units first, then two-letter ones, then single letters.
            if (rest.StartsWith("tsch", StringComparison.Ordinal))
            {
                Emit(key, start, "c");
                i += 4;
            }
            else if (rest.StartsWith("chs", StringComparison.Ordinal))
            {
                Emit(key, start, "ks");
                i += 3;
            }
            else if (rest.StartsWith("sch", StringComparison.Ordinal))
            {
                Emit(key, start, "c");
                i += 3;
            }
            else if (rest.StartsWith("aeu", StringComparison.Ordinal))
            {
                Emit(key, start, "oy");
                i += 3;
            }
            else if (first && rest.StartsWith("st", StringComparison.Ordinal))
            {
                Emit(key, start, "ct");
                i += 2;
            }
            else if (first && rest.StartsWith("sp", StringComparison.Ordinal))
            {
                Emit(key, start, "cp");
                i += 2;
            }
            else if (rest.StartsWith("ch", StringComparison.Ordinal))
            {
                Emit(key, start, "x");
                i += 2;
            }
            else if (rest.StartsWith("ck", StringComparison.Ordinal))
            {
                Emit(key, start, "k");
                i += 2;
            }
            else if (rest.StartsWith("tz", StringComparison.Ordinal))
            {
                Emit(key, start, "z");
                i += 2;
            }
            else if (rest.StartsWith("ts", StringComparison.Ordinal))
            {
                Emit(key, start, "z");
                i += 2;
            }
            else if (rest.StartsWith("ph", StringComparison.Ordinal))
            {
                Emit(key, start, "f");
                i += 2;
            }
            else if (rest.StartsWith("qu", StringComparison.Ordinal))
            {
                Emit(key, start, "kv");
                i += 2;
            }
            else if (rest.StartsWith("dt", StringComparison.Ordinal) || rest.StartsWith("th", StringComparison.Ordinal))
            {
                Emit(key, start, "t");
                i += 2;
            }
            else if (rest.StartsWith("eu", StringComparison.Ordinal))
            {
                Emit(key, start, "oy");
                i += 2;
            }
            else if (rest.StartsWith("ei", StringComparison.Ordinal) || rest.StartsWith("ai", StringComparison.Ordinal) || rest.StartsWith("ey", StringComparison.Ordinal) || rest.StartsWith("ay", StringComparison.Ordinal))
            {
                Emit(key, start, "ai");
                i += 2;
            }
            else if (rest.StartsWith("ie", StringComparison.Ordinal))
            {
                Emit(key, start, "i");
                i += 2;
            }
            else if (rest.StartsWith("ae", StringComparison.Ordinal))
            {
                Emit(key, start, "e");
                i += 2;
            }
            else if (c == 'h' && afterVowel)
            {
                i++;
            }
            else
            {
                Emit(key, start, c switch
                {
                    'v' => "f",
                    'w' => "v",
                    'j' => "y",
                    'x' => "ks",
                    'z' => "z",
                    'c' => i + 1 < word.Length && word[i + 1] is 'e' or 'i' or 'y' ? "z" : "k",
                    'q' => "k",
                    's' => "s",
                    _ => c.ToString(),
                });
                i++;
            }
        }

        FinalDevoicing(key, start);
    }

    // Appends the symbols unless they repeat the symbol just written (doubled letters and doubled vowels are one sound).
    private static void Emit(StringBuilder key, int wordStart, string symbols)
    {
        if (key.Length > wordStart && symbols.Length == 1 && key[^1] == symbols[0] && !char.IsAsciiDigit(symbols[0]))
        {
            return;
        }

        key.Append(symbols);
    }

    private static void FinalDevoicing(StringBuilder key, int start)
    {
        if (key.Length <= start)
        {
            return;
        }

        key[^1] = key[^1] switch { 'b' => 'p', 'd' => 't', 'g' => 'k', var other => other };
    }
}
