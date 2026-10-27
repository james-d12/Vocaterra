namespace Vocaterra.Nlp.Es.Phonology;

/// <summary>
/// Stage 4 of the G2P: lexical stress. A written accent wins; otherwise monosyllables are stressed and a
/// word ending in a vowel, <c>n</c> or <c>s</c> is stressed on the penultimate syllable, any other word on
/// the last. A final <c>y</c> counts as a consonant.
/// </summary>
internal static class StressAssigner
{
    /// <summary>Returns the index of the stressed syllable, or -1 for a word without syllables.</summary>
    public static int Assign(SyllabifiedWord word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var syllables = word.Syllables;
        if (syllables.Count == 0)
        {
            return -1;
        }

        for (var s = 0; s < syllables.Count; s++)
        {
            for (var g = syllables[s].FirstGrapheme; g < syllables[s].EndGrapheme; g++)
            {
                if (word.Graphemes[g].IsAccented)
                {
                    return s;
                }
            }
        }

        if (syllables.Count == 1)
        {
            return 0;
        }

        return EndsInVowelNOrS(word.Graphemes[^1]) ? syllables.Count - 2 : syllables.Count - 1;
    }

    private static bool EndsInVowelNOrS(Grapheme last)
    {
        return last.Text is "a" or "e" or "i" or "o" or "u" or "n" or "s";
    }
}
