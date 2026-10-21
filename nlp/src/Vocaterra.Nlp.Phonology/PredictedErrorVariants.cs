using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Phonology;

/// <summary>
/// A phone sequence an English speaker is predicted to produce for one error site, tagged with the error it represents.
/// For stress errors the phones are unchanged (stress is not a token) and <see cref="StressedSyllableIndex"/> names the
/// syllable that wrongly receives the stress.
/// </summary>
/// <param name="Phones">The learner's phone tokens for the whole text.</param>
/// <param name="ErrorTag">A stable <see cref="ErrorTags"/> identifier (<c>pron.r</c>, <c>pron.silent_h</c>, <c>pron.vowel</c>, <c>pron.stress</c>).</param>
/// <param name="GraphemeSpan">The letters of the error site, in the normalized text.</param>
/// <param name="StressedSyllableIndex">For <c>pron.stress</c>, the index into <see cref="G2pResult.Syllables"/> of the wrongly stressed syllable; otherwise -1.</param>
public sealed record PredictedErrorVariant(
    IReadOnlyList<string> Phones,
    string ErrorTag,
    TextSpan GraphemeSpan,
    int StressedSyllableIndex = -1)
{
    /// <summary>Structural equality including the phone list.</summary>
    public bool Equals(PredictedErrorVariant? other)
    {
        return other is not null
            && ListEquality.Equal(Phones, other.Phones)
            && string.Equals(ErrorTag, other.ErrorTag, StringComparison.Ordinal)
            && GraphemeSpan.Equals(other.GraphemeSpan)
            && StressedSyllableIndex == other.StressedSyllableIndex;
    }

    /// <summary>Hash consistent with <see cref="Equals(PredictedErrorVariant)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Phones);
        hash.Add(ErrorTag, StringComparer.Ordinal);
        hash.Add(GraphemeSpan);
        hash.Add(StressedSyllableIndex);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Generates the pronunciation errors an English speaker is predicted to make for a phonemized text: tap or trill
/// as <c>ɹ</c>, a pronounced silent <c>h</c>, vowel reduction or diphthongization, and moved stress. One variant per error site.
/// </summary>
public static class PredictedErrorVariants
{
    private static readonly string[] TagOrder =
    [
        ErrorTags.PronR,
        ErrorTags.PronSilentH,
        ErrorTags.PronVowel,
        ErrorTags.PronStress,
    ];

    /// <summary>Returns the predicted error variants, ordered by position in the text, then by tag order (r, silent h, vowel, stress).</summary>
    public static IReadOnlyList<PredictedErrorVariant> Generate(G2pResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var phones = result.Phones;
        var syllableOfPhone = SyllableOfPhone(result);
        var candidates = new List<PredictedErrorVariant>();

        for (var i = 0; i < phones.Count; i++)
        {
            switch (phones[i])
            {
                case PhoneInventory.Tap or PhoneInventory.Trill:
                    candidates.Add(new PredictedErrorVariant(Replace(phones, i, PhoneInventory.EnglishR), ErrorTags.PronR, SpanOf(result, i, syllableOfPhone)));
                    break;
                case PhoneInventory.A when !IsStressed(result, syllableOfPhone[i]) && IsPlainVowel(result, i, syllableOfPhone):
                    candidates.Add(new PredictedErrorVariant(Replace(phones, i, PhoneInventory.Schwa), ErrorTags.PronVowel, SpanOf(result, i, syllableOfPhone)));
                    break;
                case PhoneInventory.E when IsPlainVowel(result, i, syllableOfPhone):
                    candidates.Add(new PredictedErrorVariant(Replace(phones, i, PhoneInventory.EnglishEi), ErrorTags.PronVowel, SpanOf(result, i, syllableOfPhone)));
                    break;
                case PhoneInventory.O when IsPlainVowel(result, i, syllableOfPhone):
                    candidates.Add(new PredictedErrorVariant(Replace(phones, i, PhoneInventory.EnglishOu), ErrorTags.PronVowel, SpanOf(result, i, syllableOfPhone)));
                    break;
                default:
                    break;
            }
        }

        foreach (var site in result.SilentLetters)
        {
            var inserted = new List<string>(phones);
            inserted.Insert(Math.Clamp(site.PhoneIndex, 0, inserted.Count), PhoneInventory.H);
            candidates.Add(new PredictedErrorVariant(inserted.AsReadOnly(), ErrorTags.PronSilentH, site.Span));
        }

        AddStressVariants(result, candidates);

        return candidates
            .Select(static (variant, order) => (Variant: variant, Order: order))
            .OrderBy(static item => item.Variant.GraphemeSpan.Start)
            .ThenBy(static item => Array.IndexOf(TagOrder, item.Variant.ErrorTag))
            .ThenBy(static item => item.Order)
            .Select(static item => item.Variant)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    private static void AddStressVariants(G2pResult result, List<PredictedErrorVariant> candidates)
    {
        var syllables = result.Syllables;
        var first = 0;
        while (first < syllables.Count)
        {
            var last = first;
            while (last + 1 < syllables.Count && syllables[last + 1].WordIndex == syllables[first].WordIndex)
            {
                last++;
            }

            var stressed = -1;
            for (var s = first; s <= last; s++)
            {
                if (syllables[s].Stressed)
                {
                    stressed = s;
                    break;
                }
            }

            if (stressed >= 0 && last > first)
            {
                foreach (var neighbour in new[] { stressed - 1, stressed + 1 })
                {
                    if (neighbour >= first && neighbour <= last)
                    {
                        candidates.Add(new PredictedErrorVariant(result.Phones, ErrorTags.PronStress, syllables[neighbour].GraphemeSpan, neighbour));
                    }
                }
            }

            first = last + 1;
        }
    }

    private static int[] SyllableOfPhone(G2pResult result)
    {
        var owner = new int[result.Phones.Count];
        Array.Fill(owner, -1);
        for (var s = 0; s < result.Syllables.Count; s++)
        {
            var syllable = result.Syllables[s];
            for (var k = 0; k < syllable.PhoneCount; k++)
            {
                var phoneIndex = syllable.PhoneStart + k;
                if (phoneIndex >= 0 && phoneIndex < owner.Length)
                {
                    owner[phoneIndex] = s;
                }
            }
        }

        return owner;
    }

    private static bool IsStressed(G2pResult result, int syllable)
    {
        return syllable >= 0 && result.Syllables[syllable].Stressed;
    }

    /// <summary>A vowel with no glide next to it in its own syllable (a diphthong is not a monophthong to reduce or diphthongize).</summary>
    private static bool IsPlainVowel(G2pResult result, int index, int[] syllableOfPhone)
    {
        var syllable = syllableOfPhone[index];
        return !IsGlideInSyllable(result, index - 1, syllable, syllableOfPhone)
            && !IsGlideInSyllable(result, index + 1, syllable, syllableOfPhone);
    }

    private static bool IsGlideInSyllable(G2pResult result, int index, int syllable, int[] syllableOfPhone)
    {
        return index >= 0
            && index < result.Phones.Count
            && syllableOfPhone[index] == syllable
            && PhoneInventory.IsGlide(result.Phones[index]);
    }

    private static TextSpan SpanOf(G2pResult result, int phoneIndex, int[] syllableOfPhone)
    {
        if (result.PhoneSpans.Count == result.Phones.Count)
        {
            return result.PhoneSpans[phoneIndex];
        }

        var syllable = syllableOfPhone[phoneIndex];
        return syllable >= 0 ? result.Syllables[syllable].GraphemeSpan : default;
    }

    private static IReadOnlyList<string> Replace(IReadOnlyList<string> phones, int index, string replacement)
    {
        var copy = new string[phones.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = i == index ? replacement : phones[i];
        }

        return Array.AsReadOnly(copy);
    }
}
