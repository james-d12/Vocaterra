namespace Vocaterra.Nlp.Es.Phonology;

/// <summary>The syllabic role of a grapheme.</summary>
internal enum GraphemeRole
{
    /// <summary>A consonant (including the silent <c>h</c> and the digraphs).</summary>
    Consonant,

    /// <summary>The vowel at the centre of a syllable.</summary>
    Nucleus,

    /// <summary>A weak vowel (<c>i u ü y</c>) that forms a diphthong or triphthong with the nucleus.</summary>
    Glide,
}

/// <summary>A syllable as a range of graphemes within its word.</summary>
/// <param name="FirstGrapheme">Index of the first grapheme.</param>
/// <param name="GraphemeCount">Number of graphemes.</param>
internal readonly record struct SyllableSpan(int FirstGrapheme, int GraphemeCount)
{
    /// <summary>Index one past the last grapheme.</summary>
    public int EndGrapheme => FirstGrapheme + GraphemeCount;
}

/// <summary>A word split into graphemes, with a role for each grapheme and the syllable boundaries.</summary>
/// <param name="Graphemes">The word's graphemes.</param>
/// <param name="Roles">The role of each grapheme, parallel to <paramref name="Graphemes"/>.</param>
/// <param name="Syllables">The syllables in order; they partition the graphemes.</param>
internal sealed record SyllabifiedWord(
    IReadOnlyList<Grapheme> Graphemes,
    IReadOnlyList<GraphemeRole> Roles,
    IReadOnlyList<SyllableSpan> Syllables);

/// <summary>
/// Stage 3 of the G2P: Spanish syllabification by onset maximization. Diphthongs and triphthongs
/// (weak vowel next to a strong vowel, or two different weak vowels) stay together; two strong vowels,
/// or a strong vowel and a weak vowel carrying a written accent, form a hiatus. A word-final or
/// post-vocalic <c>y</c> is a glide. Silent <c>h</c> is an ordinary consonant here, so syllabification
/// follows the spelling.
/// </summary>
internal static class Syllabifier
{
    /// <summary>Splits a word into syllables.</summary>
    public static SyllabifiedWord Syllabify(IReadOnlyList<Grapheme> graphemes)
    {
        ArgumentNullException.ThrowIfNull(graphemes);
        var count = graphemes.Count;
        var roles = new GraphemeRole[count];
        var groups = FindNuclei(graphemes, roles);

        if (groups.Count == 0)
        {
            var whole = count == 0 ? Array.Empty<SyllableSpan>() : [new SyllableSpan(0, count)];
            return new SyllabifiedWord(graphemes, Array.AsReadOnly(roles), Array.AsReadOnly(whole));
        }

        var syllables = new List<SyllableSpan>(groups.Count);
        var start = 0;
        for (var k = 0; k < groups.Count; k++)
        {
            int end;
            if (k == groups.Count - 1)
            {
                end = count;
            }
            else
            {
                var runStart = groups[k].Last + 1;
                var runEnd = groups[k + 1].First;
                end = runStart + CodaLength(graphemes, runStart, runEnd);
            }

            syllables.Add(new SyllableSpan(start, end - start));
            start = end;
        }

        return new SyllabifiedWord(graphemes, Array.AsReadOnly(roles), syllables.AsReadOnly());
    }

    /// <summary>True when the two consonants can begin a syllable together (<c>pl pr bl br fl fr tr dr cl cr gl gr kl kr</c>).</summary>
    public static bool IsOnsetCluster(string first, string second)
    {
        var liquid = second is "l" or "r";
        var stopOrFricative = first is "p" or "b" or "f" or "c" or "k" or "g";
        return (liquid && stopOrFricative) || (second == "r" && first is "t" or "d");
    }

    private static int CodaLength(IReadOnlyList<Grapheme> graphemes, int runStart, int runEnd)
    {
        var length = runEnd - runStart;
        if (length <= 1)
        {
            return 0;
        }

        return IsOnsetCluster(graphemes[runEnd - 2].Text, graphemes[runEnd - 1].Text) ? length - 2 : length - 1;
    }

    private static List<NucleusGroup> FindNuclei(IReadOnlyList<Grapheme> graphemes, GraphemeRole[] roles)
    {
        var groups = new List<NucleusGroup>();
        var count = graphemes.Count;
        var i = 0;
        while (i < count)
        {
            if (!IsVowel(graphemes, i))
            {
                i++;
                continue;
            }

            var runEnd = i;
            while (runEnd < count && IsVowel(graphemes, runEnd))
            {
                runEnd++;
            }

            SplitRun(graphemes, i, runEnd, groups);
            i = runEnd;
        }

        foreach (var group in groups)
        {
            for (var g = group.First; g <= group.Last; g++)
            {
                roles[g] = g == group.Nucleus ? GraphemeRole.Nucleus : GraphemeRole.Glide;
            }
        }

        return groups;
    }

    private static void SplitRun(IReadOnlyList<Grapheme> graphemes, int runStart, int runEnd, List<NucleusGroup> groups)
    {
        NucleusGroup? current = null;
        for (var index = runStart; index < runEnd; index++)
        {
            var kind = Classify(graphemes, index, index > runStart);
            if (current is null || !TryJoin(current, graphemes, index, kind))
            {
                current = new NucleusGroup(index, index, index, kind == VowelKind.Strong, kind == VowelKind.Strong ? 0 : 1, false);
                groups.Add(current);
            }
        }
    }

    private static bool TryJoin(NucleusGroup group, IReadOnlyList<Grapheme> graphemes, int index, VowelKind kind)
    {
        switch (kind)
        {
            case VowelKind.Strong:
                if (group.HasStrong || group.WeakCount != 1)
                {
                    return false;
                }

                group.Last = index;
                group.Nucleus = index;
                group.HasStrong = true;
                return true;

            case VowelKind.Weak:
                if (group.HasStrong)
                {
                    if (group.TrailingGlide)
                    {
                        return false;
                    }

                    group.Last = index;
                    group.TrailingGlide = true;
                    return true;
                }

                if (group.WeakCount == 1 && WeakFamily(graphemes[group.Nucleus]) != WeakFamily(graphemes[index]))
                {
                    group.Last = index;
                    group.Nucleus = index;
                    group.WeakCount = 2;
                    return true;
                }

                return false;

            default:
                if (group.TrailingGlide)
                {
                    return false;
                }

                group.Last = index;
                group.TrailingGlide = true;
                return true;
        }
    }

    private static char WeakFamily(Grapheme grapheme)
    {
        return grapheme.Text[0] is 'u' or 'ü' ? 'u' : 'i';
    }

    private static VowelKind Classify(IReadOnlyList<Grapheme> graphemes, int index, bool followsVowel)
    {
        var text = graphemes[index].Text;
        if (text == "y")
        {
            return followsVowel ? VowelKind.TrailingY : VowelKind.Weak;
        }

        return text[0] is 'i' or 'u' or 'ü' ? VowelKind.Weak : VowelKind.Strong;
    }

    /// <summary>True for vowel letters and for a <c>y</c> that is not followed by a vowel letter.</summary>
    private static bool IsVowel(IReadOnlyList<Grapheme> graphemes, int index)
    {
        var grapheme = graphemes[index];
        if (grapheme.IsVowelLetter)
        {
            return true;
        }

        return grapheme.Text == "y" && !(index + 1 < graphemes.Count && graphemes[index + 1].IsVowelLetter);
    }

    private enum VowelKind
    {
        Strong,
        Weak,
        TrailingY,
    }

    private sealed class NucleusGroup(int first, int last, int nucleus, bool hasStrong, int weakCount, bool trailingGlide)
    {
        public int First { get; } = first;

        public int Last { get; set; } = last;

        public int Nucleus { get; set; } = nucleus;

        public bool HasStrong { get; set; } = hasStrong;

        public int WeakCount { get; set; } = weakCount;

        public bool TrailingGlide { get; set; } = trailingGlide;
    }
}
