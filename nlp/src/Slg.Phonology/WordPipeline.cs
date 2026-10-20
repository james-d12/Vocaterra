using Slg.Core.Stages;
using Slg.Core.Verdicts;

namespace Slg.Phonology;

/// <summary>The phonology of one word before the cross-word allophone pass.</summary>
/// <param name="Phones">Phones in order.</param>
/// <param name="PhoneSyllable">Word-local syllable index of each phone, non-decreasing.</param>
/// <param name="PhoneSpans">Grapheme span each phone came from.</param>
/// <param name="SyllableSpans">Grapheme span of each syllable, partitioning the word.</param>
/// <param name="StressedSyllable">Word-local index of the stressed syllable, or -1.</param>
/// <param name="SilentH">Silent <c>h</c> letters with word-local phone insertion indexes.</param>
internal sealed record WordPhonology(
    IReadOnlyList<string> Phones,
    IReadOnlyList<int> PhoneSyllable,
    IReadOnlyList<TextSpan> PhoneSpans,
    IReadOnlyList<TextSpan> SyllableSpans,
    int StressedSyllable,
    IReadOnlyList<SilentH> SilentH);

/// <summary>Runs the per-word stages (grouping, syllabification, stress, mapping) or applies an exception entry.</summary>
internal static class WordPipeline
{
    /// <summary>Derives a word's phonology from the spelling rules.</summary>
    public static WordPhonology FromRules(Token token, DialectConfig dialect)
    {
        var graphemes = DigraphGrouper.Group(token.Text, token.Span.Start);
        var syllabified = Syllabifier.Syllabify(graphemes);
        var stressed = StressAssigner.Assign(syllabified);
        var mapped = PhoneMapper.Map(syllabified, dialect);

        var syllableSpans = SpellingSpans(syllabified);
        var phones = new string[mapped.Phones.Count];
        var phoneSyllable = new int[phones.Length];
        var phoneSpans = new TextSpan[phones.Length];
        for (var i = 0; i < phones.Length; i++)
        {
            var mappedPhone = mapped.Phones[i];
            var grapheme = graphemes[mappedPhone.GraphemeIndex];
            phones[i] = mappedPhone.Phone;
            phoneSyllable[i] = mappedPhone.Syllable;
            phoneSpans[i] = new TextSpan(grapheme.Start, grapheme.Length);
        }

        var silent = new SilentH[mapped.SilentH.Count];
        for (var i = 0; i < silent.Length; i++)
        {
            var grapheme = graphemes[mapped.SilentH[i].GraphemeIndex];
            silent[i] = new SilentH(new TextSpan(grapheme.Start, grapheme.Length), mapped.SilentH[i].PhoneIndex);
        }

        return new WordPhonology(phones, phoneSyllable, phoneSpans, syllableSpans, stressed, silent);
    }

    /// <summary>
    /// Applies an exception entry. Grapheme spans come from the spelling syllabification when its syllable
    /// count matches the entry, and from an even split of the word otherwise.
    /// </summary>
    public static WordPhonology FromException(Token token, ExceptionEntry entry, DialectConfig dialect)
    {
        var graphemes = DigraphGrouper.Group(token.Text, token.Span.Start);
        var syllabified = Syllabifier.Syllabify(graphemes);
        var spellingSpans = SpellingSpans(syllabified);
        var syllableSpans = spellingSpans.Count == entry.Syllables.Count
            ? spellingSpans
            : EvenSplit(token.Span, entry.Syllables.Count);

        var phones = new List<string>();
        var phoneSyllable = new List<int>();
        var phoneSpans = new List<TextSpan>();
        for (var s = 0; s < entry.Syllables.Count; s++)
        {
            foreach (var phone in entry.Syllables[s])
            {
                phones.Add(Adapt(phone, dialect));
                phoneSyllable.Add(s);
                phoneSpans.Add(syllableSpans[s]);
            }
        }

        return new WordPhonology(phones, phoneSyllable, phoneSpans, syllableSpans, entry.StressedSyllable, []);
    }

    /// <summary>Adapts a phone written for a distinción/yeísta speaker to the dialect.</summary>
    private static string Adapt(string phone, DialectConfig dialect)
    {
        return phone switch
        {
            PhoneInventory.Theta => dialect.Seseo ? PhoneInventory.S : PhoneInventory.Theta,
            PhoneInventory.Yod => dialect.YPhone,
            _ => phone,
        };
    }

    private static IReadOnlyList<TextSpan> SpellingSpans(SyllabifiedWord word)
    {
        var spans = new TextSpan[word.Syllables.Count];
        for (var s = 0; s < spans.Length; s++)
        {
            var first = word.Graphemes[word.Syllables[s].FirstGrapheme];
            var last = word.Graphemes[word.Syllables[s].EndGrapheme - 1];
            spans[s] = new TextSpan(first.Start, last.End - first.Start);
        }

        return spans;
    }

    private static IReadOnlyList<TextSpan> EvenSplit(TextSpan word, int parts)
    {
        var spans = new TextSpan[parts];
        for (var k = 0; k < parts; k++)
        {
            var start = word.Start + (k * word.Length / parts);
            var end = word.Start + ((k + 1) * word.Length / parts);
            spans[k] = new TextSpan(start, end - start);
        }

        return spans;
    }
}
