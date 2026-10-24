using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>The counts of one lexicon file of a bundle.</summary>
/// <param name="Path">Bundle-relative path.</param>
/// <param name="GeneratedBy">The file's <c>generated_by</c> marker, or null.</param>
/// <param name="Entries">Entries after normalization and de-duplication.</param>
/// <param name="Forms">Distinct surface forms.</param>
/// <param name="Lemmas">Distinct lemmas.</param>
/// <param name="LemmasByPos">Distinct lemmas by part of speech, in schema order.</param>
public sealed record LexiconFileStats(string Path, string? GeneratedBy, int Entries, int Forms, int Lemmas, IReadOnlyList<KeyValuePair<string, int>> LemmasByPos);

/// <summary>How two lexicon files of a bundle overlap, counted after normalization.</summary>
/// <param name="First">Path of the first file (ordinal order).</param>
/// <param name="Second">Path of the second file.</param>
/// <param name="SharedLemmas">Lemmas that both files have.</param>
/// <param name="SharedForms">Surface forms that both files have.</param>
/// <param name="IdenticalReadings">Entries with the same form, lemma, part of speech and features in both files; the merge stores each once.</param>
/// <param name="FormsWithDifferentReadings">Shared forms whose set of readings differs between the files, so the merged lexicon gives the form readings from both.</param>
public sealed record LexiconOverlap(string First, string Second, int SharedLemmas, int SharedForms, int IdenticalReadings, int FormsWithDifferentReadings);

/// <summary>The report of <c>lexicon-report</c>: each lexicon file, how the files overlap and what the merged lexicon looks like.</summary>
/// <param name="Files">Per-file counts, in ordinal path order.</param>
/// <param name="Overlaps">Pairwise overlaps, in ordinal order of the pair.</param>
/// <param name="MergedEntries">Entries of the merged lexicon.</param>
/// <param name="MergedForms">Distinct forms of the merged lexicon.</param>
/// <param name="MergedLemmas">Distinct lemmas of the merged lexicon.</param>
/// <param name="HomographForms">Forms of the merged lexicon with readings under more than one lemma.</param>
/// <param name="AmbiguousForms">Forms of the merged lexicon with more than one reading.</param>
public sealed record LexiconReportData(
    IReadOnlyList<LexiconFileStats> Files,
    IReadOnlyList<LexiconOverlap> Overlaps,
    int MergedEntries,
    int MergedForms,
    int MergedLemmas,
    int HomographForms,
    int AmbiguousForms);

/// <summary>Computes and formats the <c>lexicon-report</c>.</summary>
public static class LexiconReport
{
    /// <summary>Reads every lexicon file of the bundle directory (sidecars excluded) and builds the report.</summary>
    /// <exception cref="FormatException">A lexicon file is invalid.</exception>
    public static LexiconReportData Build(string bundleDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(bundleDir);
        var folder = Path.Combine(bundleDir, ContentPaths.LexiconFolder.TrimEnd('/'));
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"No lexicon folder in '{bundleDir}'.");
        }

        var paths = Directory.EnumerateFiles(folder, "*.json")
            .Select(path => ContentPaths.LexiconFolder + Path.GetFileName(path))
            .Where(static path => !LexiconSourceInfo.IsSidecarPath(path))
            .Order(StringComparer.Ordinal)
            .ToList();
        var files = new List<(string Path, LexiconFile File, Dictionary<string, HashSet<LexiconEntry>> ByForm)>();
        foreach (var path in paths)
        {
            var file = ReadFile(bundleDir, path);
            var byForm = new Dictionary<string, HashSet<LexiconEntry>>(StringComparer.Ordinal);
            foreach (var entry in Normalize(file.Entries))
            {
                if (!byForm.TryGetValue(entry.Form, out var set))
                {
                    set = [];
                    byForm.Add(entry.Form, set);
                }

                set.Add(entry);
            }

            files.Add((path, file, byForm));
        }

        var stats = files.Select(static f => Stats(f.Path, f.File, f.ByForm)).ToList();
        var overlaps = new List<LexiconOverlap>();
        for (var i = 0; i < files.Count; i++)
        {
            for (var j = i + 1; j < files.Count; j++)
            {
                overlaps.Add(Overlap(files[i], files[j]));
            }
        }

        var merged = Lexicon.FromEntries(files.SelectMany(static f => f.File.Entries));
        var forms = merged.Entries.GroupBy(static entry => entry.Form, StringComparer.Ordinal).ToList();
        var homographs = forms.Count(static group => group.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Skip(1).Any());
        var ambiguous = forms.Count(static group => group.Skip(1).Any());
        return new LexiconReportData(stats, overlaps, merged.Entries.Count, forms.Count, merged.Lemmas.Count, homographs, ambiguous);
    }

    /// <summary>Formats the report as plain text.</summary>
    public static string Format(LexiconReportData report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{report.Files.Count} lexicon files\n");
        foreach (var file in report.Files)
        {
            builder.Append(CultureInfo.InvariantCulture, $"  {file.Path}: generated_by {file.GeneratedBy ?? "(none)"}, {file.Entries} entries, {file.Forms} forms, {file.Lemmas} lemmas (")
                .Append(string.Join(", ", file.LemmasByPos.Select(static pair => string.Create(CultureInfo.InvariantCulture, $"{pair.Key} {pair.Value}"))))
                .Append(")\n");
        }

        foreach (var overlap in report.Overlaps)
        {
            builder.Append(CultureInfo.InvariantCulture, $"overlap {overlap.First} / {overlap.Second}: {overlap.SharedLemmas} shared lemmas, {overlap.SharedForms} shared forms, ")
                .Append(CultureInfo.InvariantCulture, $"{overlap.IdenticalReadings} identical readings (stored once), {overlap.FormsWithDifferentReadings} shared forms with different readings in the two files\n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"merged: {report.MergedEntries} entries, {report.MergedForms} forms, {report.MergedLemmas} lemmas; ")
            .Append(CultureInfo.InvariantCulture, $"{report.AmbiguousForms} forms with more than one reading, {report.HomographForms} forms read under more than one lemma\n");
        return builder.ToString();
    }

    private static LexiconFile ReadFile(string bundleDir, string path)
    {
        var full = Path.Combine(bundleDir, path.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            return Lexicon.ReadFile(File.ReadAllText(full, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or ArgumentException)
        {
            throw new FormatException($"{path}: {ex.Message}", ex);
        }
    }

    private static IEnumerable<LexiconEntry> Normalize(IEnumerable<LexiconEntry> entries)
    {
        return Lexicon.FromEntries(entries).Entries;
    }

    private static LexiconFileStats Stats(string path, LexiconFile file, Dictionary<string, HashSet<LexiconEntry>> byForm)
    {
        var entries = byForm.Values.SelectMany(static set => set).ToList();
        var lemmas = entries.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Count();
        var byPos = LexiconSchema.PartsOfSpeech
            .Select(pos => new KeyValuePair<string, int>(pos, entries.Where(entry => entry.Pos == pos).Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Count()))
            .Where(static pair => pair.Value > 0)
            .ToList();
        return new LexiconFileStats(path, file.GeneratedBy, entries.Count, byForm.Count, lemmas, byPos);
    }

    private static LexiconOverlap Overlap(
        (string Path, LexiconFile File, Dictionary<string, HashSet<LexiconEntry>> ByForm) first,
        (string Path, LexiconFile File, Dictionary<string, HashSet<LexiconEntry>> ByForm) second)
    {
        var firstLemmas = first.ByForm.Values.SelectMany(static set => set).Select(static entry => entry.Lemma).ToHashSet(StringComparer.Ordinal);
        var secondLemmas = second.ByForm.Values.SelectMany(static set => set).Select(static entry => entry.Lemma).ToHashSet(StringComparer.Ordinal);
        var sharedForms = 0;
        var identical = 0;
        var different = 0;
        foreach (var (form, readings) in first.ByForm)
        {
            if (!second.ByForm.TryGetValue(form, out var other))
            {
                continue;
            }

            sharedForms++;
            var same = readings.Count(other.Contains);
            identical += same;
            if (same != readings.Count || same != other.Count)
            {
                different++;
            }
        }

        return new LexiconOverlap(first.Path, second.Path, firstLemmas.Count(secondLemmas.Contains), sharedForms, identical, different);
    }
}
