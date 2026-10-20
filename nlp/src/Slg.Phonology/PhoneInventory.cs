using System.Text;

namespace Slg.Phonology;

/// <summary>
/// The IPA phone tokens used by the Spanish G2P, as string constants. Phone sequences are always
/// lists of these tokens, never concatenated strings, because some tokens span two code points
/// (for example <c>tʃ</c>).
/// </summary>
public static class PhoneInventory
{
    /// <summary>Vowel <c>a</c>.</summary>
    public const string A = "a";

    /// <summary>Vowel <c>e</c>.</summary>
    public const string E = "e";

    /// <summary>Vowel <c>i</c>.</summary>
    public const string I = "i";

    /// <summary>Vowel <c>o</c>.</summary>
    public const string O = "o";

    /// <summary>Vowel <c>u</c>.</summary>
    public const string U = "u";

    /// <summary>Palatal glide <c>j</c>.</summary>
    public const string J = "j";

    /// <summary>Labial-velar glide <c>w</c>.</summary>
    public const string W = "w";

    /// <summary>Voiceless bilabial stop <c>p</c>.</summary>
    public const string P = "p";

    /// <summary>Voiced bilabial stop <c>b</c>.</summary>
    public const string B = "b";

    /// <summary>Voiceless alveolar stop <c>t</c>.</summary>
    public const string T = "t";

    /// <summary>Voiced alveolar stop <c>d</c>.</summary>
    public const string D = "d";

    /// <summary>Voiceless velar stop <c>k</c>.</summary>
    public const string K = "k";

    /// <summary>Voiced velar stop <c>g</c> (ASCII g).</summary>
    public const string G = "g";

    /// <summary>Voiceless labiodental fricative <c>f</c>.</summary>
    public const string F = "f";

    /// <summary>Voiceless dental fricative <c>θ</c> (distinción).</summary>
    public const string Theta = "θ";

    /// <summary>Voiceless alveolar fricative <c>s</c>.</summary>
    public const string S = "s";

    /// <summary>Voiceless velar fricative <c>x</c> (jota).</summary>
    public const string X = "x";

    /// <summary>Palatal approximant or fricative <c>ʝ</c> (yeísmo, <c>y</c>).</summary>
    public const string Yod = "ʝ";

    /// <summary>Palatal lateral <c>ʎ</c> (<c>ll</c> without yeísmo).</summary>
    public const string PalatalLateral = "ʎ";

    /// <summary>Voiceless palatal affricate <c>tʃ</c> (<c>ch</c>).</summary>
    public const string Ch = "tʃ";

    /// <summary>Bilabial nasal <c>m</c>.</summary>
    public const string M = "m";

    /// <summary>Alveolar nasal <c>n</c>.</summary>
    public const string N = "n";

    /// <summary>Palatal nasal <c>ɲ</c> (<c>ñ</c>).</summary>
    public const string PalatalNasal = "ɲ";

    /// <summary>Alveolar lateral <c>l</c>.</summary>
    public const string L = "l";

    /// <summary>Alveolar tap <c>ɾ</c>.</summary>
    public const string Tap = "ɾ";

    /// <summary>Alveolar trill <c>r</c>.</summary>
    public const string Trill = "r";

    /// <summary>Bilabial approximant <c>β</c>, allophone of <c>b</c>.</summary>
    public const string Beta = "β";

    /// <summary>Dental approximant <c>ð</c>, allophone of <c>d</c>.</summary>
    public const string Eth = "ð";

    /// <summary>Velar approximant <c>ɣ</c>, allophone of <c>g</c>.</summary>
    public const string Gamma = "ɣ";

    /// <summary>Velar nasal <c>ŋ</c>, allophone of <c>n</c>.</summary>
    public const string VelarNasal = "ŋ";

    /// <summary>Voiced alveolar fricative <c>z</c>, allophone of <c>s</c>.</summary>
    public const string VoicedS = "z";

    /// <summary>Glottal fricative <c>h</c>, aspirated <c>s</c> (also the learner's pronounced silent h).</summary>
    public const string H = "h";

    /// <summary>Voiceless postalveolar fricative <c>ʃ</c> (rioplatense <c>ll</c>/<c>y</c>, loanwords).</summary>
    public const string Sh = "ʃ";

    /// <summary>Voiced postalveolar fricative <c>ʒ</c> (rioplatense <c>ll</c>/<c>y</c>).</summary>
    public const string Zh = "ʒ";

    /// <summary>English approximant <c>ɹ</c>, the predicted learner error for tap and trill.</summary>
    public const string EnglishR = "ɹ";

    /// <summary>Schwa <c>ə</c>, the predicted learner reduction of unstressed <c>a</c>.</summary>
    public const string Schwa = "ə";

    /// <summary>Diphthong token <c>eɪ</c>, the predicted learner diphthongization of <c>e</c>.</summary>
    public const string EnglishEi = "eɪ";

    /// <summary>Diphthong token <c>oʊ</c>, the predicted learner diphthongization of <c>o</c>.</summary>
    public const string EnglishOu = "oʊ";

    /// <summary>
    /// The primary stress mark. Stress is NOT a token in phone sequences: it is recorded as
    /// <see cref="G2pResult.StressedSyllableIndex"/> and <see cref="SyllableAlignment.Stressed"/>, and the mark
    /// only appears in <see cref="G2pResult.ToIpaString"/>.
    /// </summary>
    public const string StressMark = "ˈ";

    /// <summary>The syllable separator used by <see cref="G2pResult.ToIpaString"/>.</summary>
    public const string SyllableSeparator = ".";

    private static readonly string[] AllTokens =
    [
        A, E, I, O, U, J, W,
        P, B, T, D, K, G, F, Theta, S, X, Yod, PalatalLateral, Ch, M, N, PalatalNasal, L, Tap, Trill,
        Beta, Eth, Gamma, VelarNasal, VoicedS, H, Sh, Zh,
        EnglishR, Schwa, EnglishEi, EnglishOu,
    ];

    /// <summary>All phone tokens in a fixed order: vowels, glides, consonants, allophones, learner phones.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(AllTokens);

    /// <summary>Returns true when the token is one of the five vowels.</summary>
    public static bool IsVowel(string phone)
    {
        return phone is A or E or I or O or U;
    }

    /// <summary>Returns true when the token is a glide (<c>j</c> or <c>w</c>).</summary>
    public static bool IsGlide(string phone)
    {
        return phone is J or W;
    }

    /// <summary>Returns true when the token is a member of <see cref="All"/> (ordinal comparison).</summary>
    public static bool IsKnown(string phone)
    {
        return Array.IndexOf(AllTokens, phone) >= 0;
    }

    /// <summary>
    /// Splits a concatenated IPA string into phone tokens using greedy longest match over
    /// <see cref="All"/>. Characters that cannot start a token throw <see cref="FormatException"/>.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string ipa)
    {
        ArgumentNullException.ThrowIfNull(ipa);
        var tokens = new List<string>(ipa.Length);
        var position = 0;
        while (position < ipa.Length)
        {
            string? match = null;
            foreach (var token in AllTokens)
            {
                if (token.Length > (match?.Length ?? 0)
                    && string.CompareOrdinal(ipa, position, token, 0, token.Length) == 0)
                {
                    match = token;
                }
            }

            if (match is null)
            {
                throw new FormatException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Unknown IPA character '{ipa[position]}' at position {position} in '{ipa}'."));
            }

            tokens.Add(match);
            position += match.Length;
        }

        return tokens;
    }

    /// <summary>Concatenates phone tokens into a single IPA string.</summary>
    public static string Join(IEnumerable<string> phones)
    {
        ArgumentNullException.ThrowIfNull(phones);
        var builder = new StringBuilder();
        foreach (var phone in phones)
        {
            builder.Append(phone);
        }

        return builder.ToString();
    }
}
