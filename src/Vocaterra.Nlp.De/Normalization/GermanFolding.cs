using System.Text;

namespace Vocaterra.Nlp.De.Normalization;

/// <summary>
/// The German pack's <see cref="ILexiconFolding"/> (M37): how a typed spelling reaches the lexicon form it stands for. The umlauts and the sharp s have a second
/// spelling that every German writer may use and that no reader takes for a mistake: <c>ä ö ü</c> as <c>ae oe ue</c> (a keyboard without umlauts, the spelling
/// of crosswords and of Swiss typing), <c>ß</c> as <c>ss</c> (the Swiss spelling, and the rule after a short vowel in Switzerland and Liechtenstein). The two
/// spellings are the same word, so typed <c>Strasse</c>, <c>Grüsse</c> and <c>schoen</c> reach <c>straße</c>, <c>grüße</c> and <c>schön</c>.
/// </summary>
/// <remarks>
/// <see cref="Key"/> keeps the umlauts and <c>ß</c> (NFC and invariant lower case), so a spelling that is a lexicon form is found as itself;
/// <see cref="LenientKey"/> writes <c>ä ö ü ß</c> as <c>ae oe ue ss</c>, which is where <c>Lexicon.Lookup</c> goes when the key finds nothing, so a folded spelling
/// that is itself a word keeps its own reading (<c>schon</c> is the adverb, not <c>schön</c>; <c>Bar</c> is not <c>Bär</c>; <c>Maße</c> and <c>Masse</c> are both
/// forms). <see cref="UnmarkedSpellings"/> adds the third spelling, an umlaut typed as its bare vowel (<c>fur</c>, <c>uber</c>, <c>konnen</c>), under the same
/// rule: it is read only when no lexicon form folds to it, so <c>schon</c> keeps its own reading. The folding runs inside the
/// lexicon lookup, before any reading of a typo (M79), and a typo reading never fires on a spelling the lexicon reads.
/// </remarks>
public sealed class GermanFolding : ILexiconFolding
{
    private GermanFolding()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanFolding Instance { get; } = new();

    /// <inheritdoc />
    public string Key(string text) => TextKey.Of(text);

    /// <inheritdoc />
    public string LenientKey(string text) => Spell(TextKey.Of(text), upperAware: false);

    /// <summary>
    /// The matching key: <c>ä ö ü ß</c> written as <c>ae oe ue ss</c> (<c>Ä Ö Ü</c> as <c>Ae Oe Ue</c>, <c>ẞ</c> as <c>SS</c>), the case of the text kept, so the spelled-out
    /// and the umlaut spelling of an answer are one phrase. Applied to the text as it is; a lone surrogate reads as U+FFFD first, so the key of any string is computed.
    /// </summary>
    /// <param name="text">The text.</param>
    public string MatchingKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Spell(Utf16.ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormC), upperAware: true);
    }

    /// <summary>
    /// The spelling of a lexicon form with each umlaut typed as its bare vowel, in <see cref="LenientKey"/> form (<c>für</c> gives <c>fur</c>, <c>schön</c>
    /// <c>schon</c>, <c>größer</c> <c>grosser</c>); none for a form without an umlaut. The lexicon reads such a spelling only when no form folds to it
    /// (<c>Lexicon.Lookup</c>), so a word that is another word without its umlaut (<c>schon</c>, <c>Bar</c>) keeps its own reading.
    /// </summary>
    /// <param name="form">A lexicon form, in <see cref="Key"/> form.</param>
    public IReadOnlyList<string> UnmarkedSpellings(string form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.AsSpan().IndexOfAny("äöü") < 0)
        {
            return [];
        }

        var builder = new StringBuilder(form.Length);
        foreach (var c in form)
        {
            builder.Append(c switch { 'ä' => 'a', 'ö' => 'o', 'ü' => 'u', _ => c });
        }

        return [LenientKey(builder.ToString())];
    }

    // Writes the umlauts and the sharp s in their two-letter spellings; the capital forms keep the case of their first letter.
    private static string Spell(string text, bool upperAware)
    {
        if (text.AsSpan().IndexOfAny("äöüßÄÖÜẞ") < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            switch (c)
            {
                case 'ä':
                    builder.Append("ae");
                    break;
                case 'ö':
                    builder.Append("oe");
                    break;
                case 'ü':
                    builder.Append("ue");
                    break;
                case 'ß':
                    builder.Append("ss");
                    break;
                case 'Ä':
                    builder.Append(upperAware ? "Ae" : "ae");
                    break;
                case 'Ö':
                    builder.Append(upperAware ? "Oe" : "oe");
                    break;
                case 'Ü':
                    builder.Append(upperAware ? "Ue" : "ue");
                    break;
                case 'ẞ':
                    builder.Append(upperAware ? "SS" : "ss");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
