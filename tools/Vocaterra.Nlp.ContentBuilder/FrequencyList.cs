using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// A word frequency list in the format of hermitdave/FrequencyWords (<c>es_50k.txt</c>): one <c>word count</c> pair per line. Words are
/// case-folded (NFC and invariant lower case) and accents are kept, so <c>él</c> and <c>el</c> are different words. A word that appears on
/// several lines (for example in two cases) has its counts added.
/// </summary>
public sealed class FrequencyList
{
    private readonly Dictionary<string, long> _counts;

    private FrequencyList(Dictionary<string, long> counts, int linesRead, long totalCount)
    {
        _counts = counts;
        LinesRead = linesRead;
        TotalCount = totalCount;
    }

    /// <summary>Non-blank lines read.</summary>
    public int LinesRead { get; }

    /// <summary>Number of distinct case-folded words.</summary>
    public int DistinctWords => _counts.Count;

    /// <summary>The sum of all counts.</summary>
    public long TotalCount { get; }

    /// <summary>Reads a frequency list file (UTF-8).</summary>
    /// <exception cref="FormatException">A line is not <c>word count</c>.</exception>
    public static FrequencyList ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Parse(reader);
    }

    /// <summary>Parses frequency list text; blank lines are ignored.</summary>
    /// <exception cref="FormatException">A line is not <c>word count</c>, or its count is not a non-negative whole number.</exception>
    public static FrequencyList Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var lines = 0;
        var lineNumber = 0;
        long total = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines++;
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"Frequency list line {lineNumber}: expected 'word count'."));
            }

            var word = Fold(parts[0]);
            counts[word] = counts.GetValueOrDefault(word) + count;
            total += count;
        }

        return new FrequencyList(counts, lines, total);
    }

    /// <summary>The folding applied to words: NFC, then invariant lower case. Accents are kept.</summary>
    public static string Fold(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Returns the count of an already folded form, or false when the list does not have it.</summary>
    public bool TryGetCount(string foldedForm, out long count)
    {
        ArgumentNullException.ThrowIfNull(foldedForm);
        return _counts.TryGetValue(foldedForm, out count);
    }
}

/// <summary>The entries of one lemma while an import runs, with the summed frequency of its distinct forms found in the list.</summary>
internal sealed class LemmaBucket(string lemma)
{
    /// <summary>The lemma.</summary>
    public string Lemma { get; } = lemma;

    /// <summary>Every entry made for the lemma, from all its parts of speech.</summary>
    public List<LexiconEntry> Entries { get; } = [];

    /// <summary>The forms whose count has been added to <see cref="Frequency"/>, so a form shared by two entries counts once.</summary>
    public HashSet<string> CountedForms { get; } = new(StringComparer.Ordinal);

    /// <summary>The summed count of the distinct forms found in the list.</summary>
    public long Frequency { get; set; }

    /// <summary>Recomputes the frequency and the head-word flag from the entries that are left.</summary>
    public void Recount(FrequencyList frequency, Func<string, string>? key = null)
    {
        CountedForms.Clear();
        Frequency = 0;
        HeadWordInList = false;
        foreach (var entry in Entries)
        {
            if (frequency.TryGetCount(key is null ? entry.Form : key(entry.Form), out var count))
            {
                if (CountedForms.Add(entry.Form))
                {
                    Frequency += count;
                }

                HeadWordInList |= string.Equals(entry.Form, Lemma, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>True when the lemma's own head word (the infinitive of a verb, the base form of a noun or adjective) is in the list.</summary>
    public bool HeadWordInList { get; set; }
}

/// <summary>Ranks lemmas by frequency and keeps the first N.</summary>
internal static class LemmaFrequencyFilter
{
    /// <summary>
    /// Adds the entries of the kept lemmas to <paramref name="output"/> and returns the report. The ranking is by frequency, highest first, and
    /// ties are broken by ordinal lemma order. With <paramref name="top"/> only a lemma whose own head word is in the list can be kept: a lemma
    /// scores the counts of all its forms, and without that condition a rare verb such as <c>perar</c> would earn the count of <c>pero</c> (its first
    /// person singular) and take the place of a word learners meet. Without <paramref name="top"/> every lemma is kept.
    /// </summary>
    public static FrequencyFilterReport Apply(IEnumerable<LemmaBucket> buckets, FrequencyList frequency, int? top, List<LexiconEntry> output)
    {
        var all = buckets.Where(static bucket => bucket.Entries.Count > 0).ToList();
        var withFrequency = all.Count(static bucket => bucket.Frequency > 0);
        var ranked = all.Where(static bucket => bucket.Frequency > 0 && bucket.HeadWordInList).ToList();
        ranked.Sort(static (left, right) =>
        {
            var cmp = right.Frequency.CompareTo(left.Frequency);
            return cmp != 0 ? cmp : string.CompareOrdinal(left.Lemma, right.Lemma);
        });

        IEnumerable<LemmaBucket> kept;
        var belowCutoff = 0;
        var noFrequency = 0;
        var unattested = 0;
        long cutoff = 0;
        if (top is { } count)
        {
            var head = ranked.Take(count).ToList();
            kept = head;
            belowCutoff = ranked.Count - head.Count;
            noFrequency = all.Count - withFrequency;
            unattested = withFrequency - ranked.Count;
            cutoff = head.Count > 0 ? head[^1].Frequency : 0;
        }
        else
        {
            kept = all;
        }

        var keptCount = 0;
        foreach (var bucket in kept)
        {
            keptCount++;
            output.AddRange(bucket.Entries);
        }

        return new FrequencyFilterReport(
            frequency.LinesRead,
            frequency.DistinctWords,
            frequency.TotalCount,
            top,
            all.Count,
            withFrequency,
            keptCount,
            noFrequency,
            unattested,
            belowCutoff,
            cutoff);
    }
}
