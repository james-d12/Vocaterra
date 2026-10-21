using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Phonology;

/// <summary>
/// Deterministic rule-based Spanish grapheme-to-phoneme conversion (stage D0). The pipeline is
/// normalize, group digraphs, syllabify, assign stress, map phones, apply allophone rules across words,
/// with an exception lexicon for loanwords and irregular spellings.
/// </summary>
public static class SpanishG2p
{
    /// <summary>
    /// Phonemizes a text for one dialect. Punctuation and digits are dropped (numbers must be spelled out),
    /// every word keeps its own lexical stress (monosyllables count as stressed), and syllable spans index into
    /// <see cref="G2pResult.Text"/>, the normalized text.
    /// </summary>
    public static G2pResult Phonemize(string text, DialectConfig dialect)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(dialect);

        var normalized = G2pNormalizer.Normalize(text);
        var lexicon = ExceptionLexicon.Default;

        var phones = new List<string>();
        var phoneSyllable = new List<int>();
        var phoneSpans = new List<TextSpan>();
        var syllables = new List<SyllableAlignment>();
        var silent = new List<SilentH>();

        for (var w = 0; w < normalized.Tokens.Count; w++)
        {
            var token = normalized.Tokens[w];
            var word = lexicon.TryGet(token.Text, out var entry)
                ? WordPipeline.FromException(token, entry, dialect)
                : WordPipeline.FromRules(token, dialect);

            var phoneBase = phones.Count;
            var syllableBase = syllables.Count;
            var next = 0;
            for (var s = 0; s < word.SyllableSpans.Count; s++)
            {
                var start = next;
                while (next < word.Phones.Count && word.PhoneSyllable[next] == s)
                {
                    next++;
                }

                syllables.Add(new SyllableAlignment(word.SyllableSpans[s], phoneBase + start, next - start, s == word.StressedSyllable, w));
            }

            for (var i = 0; i < word.Phones.Count; i++)
            {
                phones.Add(word.Phones[i]);
                phoneSyllable.Add(syllableBase + word.PhoneSyllable[i]);
                phoneSpans.Add(word.PhoneSpans[i]);
            }

            foreach (var site in word.SilentH)
            {
                silent.Add(site with { PhoneIndex = phoneBase + site.PhoneIndex });
            }
        }

        AllophoneRules.Apply(phones, phoneSyllable, dialect);

        var stressedIndex = -1;
        for (var s = 0; s < syllables.Count; s++)
        {
            if (syllables[s].Stressed)
            {
                stressedIndex = s;
                break;
            }
        }

        return new G2pResult(phones.AsReadOnly(), syllables.AsReadOnly(), stressedIndex, dialect)
        {
            Text = normalized.Text,
            PhoneSpans = phoneSpans.AsReadOnly(),
            SilentLetters = silent.AsReadOnly(),
        };
    }

    /// <summary>
    /// Phonemizes a text for several dialects and returns the distinct realizations (different phones or
    /// syllable stress), keeping the order of <paramref name="dialects"/> so the primary variant comes first.
    /// </summary>
    public static IReadOnlyList<G2pResult> PhonemizeAll(string text, IEnumerable<DialectConfig> dialects)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(dialects);

        var results = new List<G2pResult>();
        foreach (var dialect in dialects)
        {
            var result = Phonemize(text, dialect);
            if (!results.Exists(existing => IsSameRealization(existing, result)))
            {
                results.Add(result);
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>Phonemizes a text for every shipped dialect (see <see cref="DialectConfig.AllShipped"/>) and returns the distinct realizations.</summary>
    public static IReadOnlyList<G2pResult> PhonemizeAll(string text)
    {
        return PhonemizeAll(text, DialectConfig.AllShipped);
    }

    private static bool IsSameRealization(G2pResult left, G2pResult right)
    {
        return ListEquality.Equal(left.Phones, right.Phones) && ListEquality.Equal(left.Syllables, right.Syllables);
    }
}
