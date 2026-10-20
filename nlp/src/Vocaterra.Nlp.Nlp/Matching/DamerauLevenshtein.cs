namespace Vocaterra.Nlp.Nlp.Matching;

/// <summary>Damerau-Levenshtein edit distance, optimal string alignment variant.</summary>
/// <remarks>
/// Counts insertions, deletions, substitutions and transpositions of two adjacent characters, each as one edit, and
/// never edits a substring more than once (so <c>ca</c> to <c>abc</c> costs 3, not 2). Characters are compared ordinally as UTF-16 code units.
/// </remarks>
public static class DamerauLevenshtein
{
    /// <summary>Returns the optimal-string-alignment distance between two strings.</summary>
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var width = b.Length + 1;
        var twoBack = new int[width];
        var previous = new int[width];
        var current = new int[width];
        for (var j = 0; j < width; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var best = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    best = Math.Min(best, twoBack[j - 2] + 1);
                }

                current[j] = best;
            }

            (twoBack, previous, current) = (previous, current, twoBack);
        }

        return previous[b.Length];
    }
}
