using System.Text;
using Slg.Core.Verdicts;

namespace Slg.Phonology;

/// <summary>
/// One syllable of a phonemized text: where it sits in the normalized text, which slice of the phone
/// list it owns, and whether it carries its word's lexical stress.
/// </summary>
/// <param name="GraphemeSpan">The letters of the syllable, as a range in the normalized text.</param>
/// <param name="PhoneStart">Index of the syllable's first phone in <see cref="G2pResult.Phones"/>.</param>
/// <param name="PhoneCount">Number of phones the syllable owns (can be zero for a fully silent syllable).</param>
/// <param name="Stressed">True when this is the stressed syllable of its word.</param>
/// <param name="WordIndex">Zero-based index of the word the syllable belongs to.</param>
public sealed record SyllableAlignment(TextSpan GraphemeSpan, int PhoneStart, int PhoneCount, bool Stressed, int WordIndex = 0);

/// <summary>A letter <c>h</c> that is silent in Spanish, and where in the phone list a learner would insert an <c>h</c>.</summary>
/// <param name="Span">The letter's range in the normalized text.</param>
/// <param name="PhoneIndex">The index in <see cref="G2pResult.Phones"/> before which the learner's <c>h</c> would be inserted.</param>
public readonly record struct SilentH(TextSpan Span, int PhoneIndex);

/// <summary>
/// The phonemization of a text for one dialect: phone tokens, per-syllable alignment to the normalized text,
/// and stress. Stress is carried by <see cref="StressedSyllableIndex"/> and <see cref="SyllableAlignment.Stressed"/>,
/// never as a token in <see cref="Phones"/>.
/// </summary>
/// <param name="Phones">Phone tokens for the whole text, word after word, with no boundary tokens.</param>
/// <param name="Syllables">Syllables in text order; their phone ranges partition <paramref name="Phones"/>.</param>
/// <param name="StressedSyllableIndex">Index into <paramref name="Syllables"/> of the first stressed syllable, or -1 when there are none.</param>
/// <param name="Dialect">The dialect the result was produced for.</param>
public sealed record G2pResult(
    IReadOnlyList<string> Phones,
    IReadOnlyList<SyllableAlignment> Syllables,
    int StressedSyllableIndex,
    DialectConfig Dialect)
{
    /// <summary>The normalized text that all spans index into; empty on hand-built results.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The grapheme span each phone came from, parallel to <see cref="Phones"/>; empty on hand-built results.</summary>
    public IReadOnlyList<TextSpan> PhoneSpans { get; init; } = [];

    /// <summary>Silent <c>h</c> letters and their insertion points; empty on hand-built results.</summary>
    public IReadOnlyList<SilentH> SilentLetters { get; init; } = [];

    /// <summary>
    /// Formats the result for display: words separated by a space, syllables by <c>.</c>, and the stressed
    /// syllable of each word prefixed with <c>ˈ</c> (for example <c>te.ˈle.fo.no</c>).
    /// </summary>
    public string ToIpaString()
    {
        var builder = new StringBuilder();
        var previousWord = -1;
        foreach (var syllable in Syllables)
        {
            if (builder.Length > 0)
            {
                builder.Append(syllable.WordIndex != previousWord ? ' ' : '.');
            }

            previousWord = syllable.WordIndex;
            if (syllable.Stressed)
            {
                builder.Append(PhoneInventory.StressMark);
            }

            for (var i = 0; i < syllable.PhoneCount; i++)
            {
                builder.Append(Phones[syllable.PhoneStart + i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Structural equality including the contents of every list.</summary>
    public bool Equals(G2pResult? other)
    {
        return other is not null
            && ListEquality.Equal(Phones, other.Phones)
            && ListEquality.Equal(Syllables, other.Syllables)
            && StressedSyllableIndex == other.StressedSyllableIndex
            && Dialect.Equals(other.Dialect)
            && string.Equals(Text, other.Text, StringComparison.Ordinal)
            && ListEquality.Equal(PhoneSpans, other.PhoneSpans)
            && ListEquality.Equal(SilentLetters, other.SilentLetters);
    }

    /// <summary>Hash consistent with <see cref="Equals(G2pResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Phones);
        ListEquality.Add(ref hash, Syllables);
        hash.Add(StressedSyllableIndex);
        hash.Add(Dialect);
        hash.Add(Text, StringComparer.Ordinal);
        ListEquality.Add(ref hash, PhoneSpans);
        ListEquality.Add(ref hash, SilentLetters);
        return hash.ToHashCode();
    }
}
