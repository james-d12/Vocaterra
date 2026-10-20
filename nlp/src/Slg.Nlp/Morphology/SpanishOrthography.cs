namespace Slg.Nlp.Morphology;

/// <summary>Spanish spelling helpers: vowel classes, syllable nuclei and accent placement. All logic is culture-free.</summary>
internal static class SpanishOrthography
{
    public static bool IsVowel(char c)
    {
        return c is 'a' or 'e' or 'i' or 'o' or 'u' or 'á' or 'é' or 'í' or 'ó' or 'ú' or 'ü';
    }

    public static bool IsAccentedVowel(char c)
    {
        return c is 'á' or 'é' or 'í' or 'ó' or 'ú';
    }

    public static bool IsStrongVowel(char c)
    {
        return c is 'a' or 'e' or 'o' or 'á' or 'é' or 'ó';
    }

    public static char RemoveAccent(char c)
    {
        return c switch
        {
            'á' => 'a',
            'é' => 'e',
            'í' => 'i',
            'ó' => 'o',
            'ú' => 'u',
            _ => c,
        };
    }

    public static char AddAccent(char c)
    {
        return c switch
        {
            'a' => 'á',
            'e' => 'é',
            'i' => 'í',
            'o' => 'ó',
            'u' => 'ú',
            _ => c,
        };
    }

    /// <summary>Returns the vowel groups (syllable nuclei) of a word: a diphthong or triphthong counts once, a hiatus twice.</summary>
    public static List<(int Start, int Length)> Nuclei(string word)
    {
        var result = new List<(int Start, int Length)>();
        var i = 0;
        while (i < word.Length)
        {
            if (!IsVowel(word[i]))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < word.Length && IsVowel(word[i]) && Joins(word[i - 1], word[i]))
            {
                i++;
            }

            result.Add((start, i - start));
        }

        return result;
    }

    /// <summary>Index of the character in the nucleus that carries (or would carry) the written accent.</summary>
    public static int AccentPosition(string word, (int Start, int Length) nucleus)
    {
        for (var i = nucleus.Start; i < nucleus.Start + nucleus.Length; i++)
        {
            if (IsStrongVowel(word[i]))
            {
                return i;
            }
        }

        return nucleus.Start + nucleus.Length - 1;
    }

    /// <summary>Index of the written accent inside the nucleus, or -1 when the nucleus has none.</summary>
    public static int WrittenAccentIndex(string word, (int Start, int Length) nucleus)
    {
        for (var i = nucleus.Start; i < nucleus.Start + nucleus.Length; i++)
        {
            if (IsAccentedVowel(word[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the word with the accent at the given index removed, keeping an i/u accent that marks a hiatus.</summary>
    public static string RemoveAccentAt(string word, int index)
    {
        var c = word[index];
        if (c is 'í' or 'ú' && index > 0 && IsVowel(word[index - 1]))
        {
            return word;
        }

        return string.Concat(word.AsSpan(0, index), RemoveAccent(c).ToString(), word.AsSpan(index + 1));
    }

    /// <summary>Returns the word with a written accent added at the given index.</summary>
    public static string AddAccentAt(string word, int index)
    {
        return string.Concat(word.AsSpan(0, index), AddAccent(word[index]).ToString(), word.AsSpan(index + 1));
    }

    private static bool Joins(char previous, char current)
    {
        if (previous is 'í' or 'ú' || current is 'í' or 'ú')
        {
            return false;
        }

        var previousWeak = previous is 'i' or 'u' or 'ü';
        var currentWeak = current is 'i' or 'u' or 'ü';
        return previousWeak || currentWeak;
    }
}
