using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>One input file as recorded in <c>lexicon-sources/sources.json</c> by <c>scripts/fetch-lexicon-sources.sh</c>.</summary>
/// <param name="Id">Short id (<c>kaikki-es</c>, <c>frequency-es-50k</c>).</param>
/// <param name="File">File name of the download.</param>
/// <param name="Url">The URL it was fetched from.</param>
/// <param name="Sha256">Lower-case hex SHA-256.</param>
/// <param name="Bytes">Size in bytes.</param>
/// <param name="RetrievedOn">Download date, <c>YYYY-MM-DD</c>.</param>
/// <param name="License">The licence of the data.</param>
public sealed record SourceRecord(string Id, string File, string Url, string Sha256, long Bytes, string RetrievedOn, string License);

/// <summary>Reads <c>sources.json</c>.</summary>
public static class SourcesFile
{
    /// <summary>The file name the fetch script writes next to the downloads.</summary>
    public const string FileName = "sources.json";

    /// <summary>Reads the records; throws <see cref="FormatException"/> when the file is missing a field or is not the expected shape.</summary>
    public static IReadOnlyList<SourceRecord> Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Sources file not found: {path}. Run scripts/fetch-lexicon-sources.sh first.", path);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"{path}: expected an object with a 'sources' array.");
            }

            var records = new List<SourceRecord>();
            foreach (var item in sources.EnumerateArray())
            {
                records.Add(new SourceRecord(
                    Text(item, "id", path),
                    Text(item, "file", path),
                    Text(item, "url", path),
                    Text(item, "sha256", path),
                    Number(item, "bytes", path),
                    Text(item, "retrievedOn", path),
                    Text(item, "license", path)));
            }

            return records;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"{path}: invalid JSON ({ex.Message}).", ex);
        }
    }

    private static string Text(JsonElement item, string name, string path)
    {
        return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new FormatException($"{path}: every source needs a string '{name}'.");
    }

    private static long Number(JsonElement item, string name, string path)
    {
        return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : throw new FormatException($"{path}: every source needs a whole number '{name}'.");
    }
}

/// <summary>Builds the sidecar of a generated lexicon file from an import result and the recorded sources.</summary>
public static class LexiconSidecarBuilder
{
    /// <summary>
    /// Builds the sidecar. Each input file is looked up in the sources by file name, and its size and SHA-256 are checked against the record
    /// (so the sidecar can only claim what the importer actually read); the record's URL, retrieval date and licence are copied.
    /// </summary>
    /// <param name="result">The import result.</param>
    /// <param name="options">The options the import ran with.</param>
    /// <param name="kaikkiPath">The Wiktionary JSONL file that was imported.</param>
    /// <param name="frequency">The frequency list and its path, or null.</param>
    /// <param name="sources">The records from <c>sources.json</c>.</param>
    /// <param name="generatedFile">File name of the lexicon the import wrote.</param>
    /// <param name="handAuthoredFiles">File names of the hand-authored lexicons the import deferred to, or null.</param>
    /// <exception cref="FormatException">A file has no record, or differs from its record.</exception>
    public static LexiconSourceInfo Build(
        KaikkiImportResult result,
        KaikkiImportOptions options,
        string kaikkiPath,
        (FrequencyList List, string Path)? frequency,
        IReadOnlyList<SourceRecord> sources,
        string generatedFile,
        IReadOnlyList<string>? handAuthoredFiles = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(kaikkiPath);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(generatedFile);

        var files = new List<LexiconSourceFile> { Describe(kaikkiPath, result.LinesRead, sources) };
        string? frequencyId = null;
        if (frequency is { } list)
        {
            var described = Describe(list.Path, list.List.LinesRead, sources);
            files.Add(described);
            frequencyId = described.Id;
        }

        files.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        var filter = result.Filter;
        var report = new LexiconImportReport(
            result.LinesRead,
            result.SpanishEntries,
            result.ImportedEntries,
            result.SkippedEntries,
            LexiconSourceInfo.SortedCounts(result.SkippedByReason),
            filter?.FrequencyWords ?? 0,
            result.PluralLookalikeLemmas,
            filter?.LemmasConsidered ?? 0,
            filter?.LemmasWithFrequency ?? 0,
            filter?.LemmasKept ?? 0,
            filter?.LemmasDroppedNoFrequency ?? 0,
            filter?.LemmasDroppedUnattestedHeadWord ?? 0,
            filter?.LemmasDroppedBelowCutoff ?? 0,
            filter?.CutoffFrequency ?? 0,
            LexiconSourceInfo.SortedCounts(EntriesByPos(result.Entries)),
            LexiconSourceInfo.SortedCounts(LemmasByPos(result.Entries)));
        return new LexiconSourceInfo(
            LexiconSourceInfo.CurrentSchemaVersion,
            KaikkiImporter.ImporterVersion,
            generatedFile,
            result.Entries.Count,
            result.Entries.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Count(),
            files,
            new LexiconImportParameters(options.Top, options.MaxEntries, frequencyId, handAuthoredFiles),
            report);
    }

    /// <summary>Counts entries by part of speech.</summary>
    public static IEnumerable<KeyValuePair<string, int>> EntriesByPos(IReadOnlyList<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.GroupBy(static entry => entry.Pos, StringComparer.Ordinal).Select(static group => new KeyValuePair<string, int>(group.Key, group.Count()));
    }

    /// <summary>Counts distinct lemmas by part of speech (a lemma with entries of two parts of speech counts under both).</summary>
    public static IEnumerable<KeyValuePair<string, int>> LemmasByPos(IReadOnlyList<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .GroupBy(static entry => entry.Pos, StringComparer.Ordinal)
            .Select(static group => new KeyValuePair<string, int>(group.Key, group.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Count()));
    }

    // Finds the record for the file name and proves the file on disk is the file that was recorded.
    private static LexiconSourceFile Describe(string path, long lines, IReadOnlyList<SourceRecord> sources)
    {
        var name = Path.GetFileName(path);
        var record = sources.FirstOrDefault(source => string.Equals(source.File, name, StringComparison.Ordinal))
            ?? throw new FormatException($"{SourcesFile.FileName} has no record for '{name}'. Run scripts/fetch-lexicon-sources.sh.");
        var size = new FileInfo(path).Length;
        if (size != record.Bytes)
        {
            throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"'{name}' has {size} bytes but {SourcesFile.FileName} records {record.Bytes}; fetch the pinned file again."));
        }

        string actual;
        using (var stream = File.OpenRead(path))
        {
            actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        if (!string.Equals(actual, record.Sha256, StringComparison.Ordinal))
        {
            throw new FormatException($"'{name}' has sha256 {actual} but {SourcesFile.FileName} records {record.Sha256}; fetch the pinned file again.");
        }

        return new LexiconSourceFile(record.Id, record.File, record.Url, record.Sha256, record.Bytes, lines, record.RetrievedOn, record.License);
    }
}
