namespace Slg.Phonology;

/// <summary>A letter or digraph of a word, with its offset in the normalized text.</summary>
/// <param name="Text">The lowercase letters (<c>a</c>, <c>ch</c>, <c>qu</c>, <c>gü</c>, ...).</param>
/// <param name="Start">Offset of the first letter in the normalized text.</param>
internal readonly record struct Grapheme(string Text, int Start)
{
    /// <summary>Number of letters.</summary>
    public int Length => Text.Length;

    /// <summary>Offset one past the last letter.</summary>
    public int End => Start + Text.Length;

    /// <summary>True for a single plain or accented vowel letter (<c>a e i o u á é í ó ú ü</c>).</summary>
    public bool IsVowelLetter => Text.Length == 1 && DigraphGrouper.IsVowelChar(Text[0]);

    /// <summary>True when the grapheme is one of <c>á é í ó ú</c>.</summary>
    public bool IsAccented => Text.Length == 1 && Text[0] is 'á' or 'é' or 'í' or 'ó' or 'ú';
}

/// <summary>Stage 2 of the G2P: groups the digraphs <c>ch ll rr qu gu gü</c> into single graphemes.</summary>
internal static class DigraphGrouper
{
    /// <summary>True for <c>a e i o u á é í ó ú ü</c>.</summary>
    public static bool IsVowelChar(char c)
    {
        return c is 'a' or 'e' or 'i' or 'o' or 'u' or 'á' or 'é' or 'í' or 'ó' or 'ú' or 'ü';
    }

    /// <summary>True for <c>e i é í</c>, the vowels that soften <c>c</c> and <c>g</c> and make <c>qu</c>/<c>gu</c> digraphs.</summary>
    public static bool IsFrontVowel(string? text)
    {
        return text is "e" or "i" or "é" or "í";
    }

    /// <summary>
    /// Groups a word into graphemes. <c>qu</c> and <c>gu</c> are digraphs only before <c>e</c>/<c>i</c>;
    /// <c>gü</c> is always one grapheme.
    /// </summary>
    public static IReadOnlyList<Grapheme> Group(string word, int offset)
    {
        ArgumentNullException.ThrowIfNull(word);
        var graphemes = new List<Grapheme>(word.Length);
        var i = 0;
        while (i < word.Length)
        {
            var length = DigraphLength(word, i);
            graphemes.Add(new Grapheme(word.Substring(i, length), offset + i));
            i += length;
        }

        return graphemes;
    }

    private static int DigraphLength(string word, int i)
    {
        if (i + 1 >= word.Length)
        {
            return 1;
        }

        var first = word[i];
        var second = word[i + 1];
        switch (first)
        {
            case 'c' when second == 'h':
            case 'l' when second == 'l':
            case 'r' when second == 'r':
                return 2;
            case 'g' when second == 'ü':
                return 2;
            case 'q' or 'g' when second == 'u' && i + 2 < word.Length && word[i + 2] is 'e' or 'i' or 'é' or 'í':
                return 2;
            default:
                return 1;
        }
    }
}
