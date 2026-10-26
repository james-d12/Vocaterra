using System.Collections.Immutable;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Content;

/// <summary>One input file of a lexicon import, as recorded in <c>lexicon-sources/sources.json</c> and copied into the sidecar.</summary>
/// <param name="Id">Short id of the source (<c>kaikki-es</c>, <c>frequency-es-50k</c>).</param>
/// <param name="File">File name of the download.</param>
/// <param name="Url">The URL the file was fetched from (an immutable commit URL where one exists).</param>
/// <param name="Sha256">Lower-case hex SHA-256 of the file, verified by the importer against the file it read.</param>
/// <param name="Bytes">Size of the file in bytes.</param>
/// <param name="Lines">Non-blank lines the importer read from the file (fewer than the file has when <c>--max</c> stopped the read early).</param>
/// <param name="RetrievedOn">Date of the download, <c>YYYY-MM-DD</c>.</param>
/// <param name="License">The licence of the data, as text.</param>
public sealed record LexiconSourceFile(string Id, string File, string Url, string Sha256, long? Bytes, long? Lines, string RetrievedOn, string License);

/// <summary>The command-line parameters of an import that decide what the generated file contains.</summary>
/// <param name="Top">The number of lemmas kept by the frequency filter, or null when every lemma was kept.</param>
/// <param name="MaxEntries">The cap on imported Spanish entries (<c>--max</c>), or null.</param>
/// <param name="FrequencySourceId">The id of the frequency list that ranked the lemmas, or null.</param>
/// <param name="HandAuthoredFiles">File names of the hand-authored lexicons whose lemmas the import left alone (verbs excepted), or null.</param>
public sealed record LexiconImportParameters(int? Top, int? MaxEntries, string? FrequencySourceId, IReadOnlyList<string>? HandAuthoredFiles);

/// <summary>What the import read, skipped and wrote; informative, the loader does not depend on these numbers.</summary>
/// <param name="KaikkiLinesRead">Non-blank lines read from the Wiktionary extract.</param>
/// <param name="SpanishEntries">Lines whose language code is <c>es</c>.</param>
/// <param name="ImportedEntries">Spanish entries that produced lexicon entries.</param>
/// <param name="SkippedEntries">Spanish entries that produced nothing.</param>
/// <param name="SkippedByReason">The skipped entries by reason (ordinal key order).</param>
/// <param name="FrequencyWords">Distinct case-folded words of the frequency list, or 0 without one.</param>
/// <param name="PluralLookalikeLemmas">Noun lemmas dropped because the lemma is only the plural of another noun (corazones, diamantes).</param>
/// <param name="LemmasConsidered">Distinct lemmas the importer produced before the frequency filter.</param>
/// <param name="LemmasWithFrequency">Lemmas with at least one form in the frequency list.</param>
/// <param name="LemmasKept">Lemmas written to the file.</param>
/// <param name="LemmasDroppedNoFrequency">Lemmas dropped because none of their forms is in the frequency list.</param>
/// <param name="LemmasDroppedUnattestedHeadWord">Lemmas with a frequency dropped because their own head word is not in the frequency list.</param>
/// <param name="LemmasDroppedBelowCutoff">Lemmas with a frequency that did not make the top N.</param>
/// <param name="CutoffFrequency">With a top N, the summed frequency of the last (least frequent) kept lemma; otherwise 0.</param>
/// <param name="EntriesByPartOfSpeech">Entries written, by part of speech.</param>
/// <param name="LemmasByPartOfSpeech">Distinct lemmas written, by part of speech (a lemma with two parts of speech counts under both).</param>
public sealed record LexiconImportReport(
    long KaikkiLinesRead,
    long SpanishEntries,
    long ImportedEntries,
    long SkippedEntries,
    IReadOnlyDictionary<string, int> SkippedByReason,
    long FrequencyWords,
    long PluralLookalikeLemmas,
    long LemmasConsidered,
    long LemmasWithFrequency,
    long LemmasKept,
    long LemmasDroppedNoFrequency,
    long LemmasDroppedUnattestedHeadWord,
    long LemmasDroppedBelowCutoff,
    long CutoffFrequency,
    IReadOnlyDictionary<string, int> EntriesByPartOfSpeech,
    IReadOnlyDictionary<string, int> LemmasByPartOfSpeech);

/// <summary>
/// The sidecar of a generated lexicon file, <c>lexicon/&lt;name&gt;.source.json</c>: where the data came from (URL, retrieval date, SHA-256, size and
/// line count of every input file), which importer version and parameters produced the file and what the import did. With the pinned inputs and
/// the same importer version the file can be reproduced byte for byte.
/// </summary>
/// <param name="SchemaVersion">The sidecar schema version, 1.</param>
/// <param name="ImporterVersion">The importer version constant (<c>KaikkiImporter.ImporterVersion</c>) that wrote the file.</param>
/// <param name="GeneratedFile">File name (in the same folder) of the lexicon this sidecar describes.</param>
/// <param name="OutputEntries">Number of entries in the generated file; the loader checks it against the file.</param>
/// <param name="OutputLemmas">Number of distinct lemmas in the generated file; the loader checks it against the file.</param>
/// <param name="Sources">The input files.</param>
/// <param name="Parameters">The import parameters.</param>
/// <param name="Report">The import counts.</param>
public sealed record LexiconSourceInfo(
    int SchemaVersion,
    string ImporterVersion,
    string GeneratedFile,
    long? OutputEntries,
    long? OutputLemmas,
    IReadOnlyList<LexiconSourceFile> Sources,
    LexiconImportParameters Parameters,
    LexiconImportReport Report)
{
    /// <summary>The only supported sidecar schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The file-name suffix that marks a sidecar next to a lexicon file; such files are metadata, not lexicons.</summary>
    public const string FileSuffix = ".source.json";

    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>True when the bundle-relative path is a lexicon sidecar (<c>lexicon/*.source.json</c>).</summary>
    public static bool IsSidecarPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.StartsWith(ContentPaths.LexiconFolder, StringComparison.Ordinal)
            && path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The sidecar file name for a lexicon file name: <c>wiktionary-es.json</c> becomes <c>wiktionary-es.source.json</c>.</summary>
    public static string SidecarNameFor(string lexiconFileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(lexiconFileName);
        return Path.GetFileNameWithoutExtension(lexiconFileName) + FileSuffix;
    }

    /// <summary>Parses sidecar JSON; throws <see cref="JsonException"/> or <see cref="FormatException"/> when it is malformed.</summary>
    public static LexiconSourceInfo FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var info = CanonicalJson.Deserialize<LexiconSourceInfo>(json);
        if ((object?)info.Sources is null || (object?)info.Parameters is null || (object?)info.Report is null)
        {
            throw new FormatException("The sidecar needs sources, parameters and report.");
        }

        if ((object?)info.Report.SkippedByReason is null
            || (object?)info.Report.EntriesByPartOfSpeech is null
            || (object?)info.Report.LemmasByPartOfSpeech is null)
        {
            throw new FormatException("The sidecar report needs skippedByReason, entriesByPartOfSpeech and lemmasByPartOfSpeech.");
        }

        foreach (var source in info.Sources)
        {
            if ((object?)source is null)
            {
                throw new FormatException("The sidecar lists a null source.");
            }
        }

        return info;
    }

    /// <summary>
    /// Serializes as canonical JSON (declaration order, sorted maps), indented by two spaces with LF line ends and a final line feed, so equal sidecars
    /// produce equal text and a person can read the file.
    /// </summary>
    public string ToJson()
    {
        var node = JsonNode.Parse(CanonicalJson.Serialize(this))!;
        return node.ToJsonString(IndentedOptions) + "\n";
    }

    /// <summary>
    /// Checks the fields the loader relies on and returns one message per problem (empty when the sidecar is sound): the schema version, the
    /// importer version, the generated file name, the source records (https URL, 64-digit hex SHA-256, size, date) and, when given, that the entry
    /// and lemma counts agree with the generated file.
    /// </summary>
    /// <param name="actualEntries">The number of entries in the generated file, or null when the file is unknown.</param>
    /// <param name="actualLemmas">The number of distinct lemmas in the generated file, or null when the file is unknown.</param>
    public IReadOnlyList<string> Problems(long? actualEntries, long? actualLemmas)
    {
        var problems = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"unsupported sidecar schemaVersion {SchemaVersion}; expected {CurrentSchemaVersion}."));
        }

        if (string.IsNullOrWhiteSpace(ImporterVersion))
        {
            problems.Add("importerVersion must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(GeneratedFile))
        {
            problems.Add("generatedFile must not be empty.");
        }

        if (OutputEntries is null || OutputEntries < 0)
        {
            problems.Add("outputEntries must be a non-negative number.");
        }
        else if (actualEntries is { } entries && entries != OutputEntries)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"outputEntries is {OutputEntries} but '{GeneratedFile}' has {entries} entries; regenerate the file and its sidecar together."));
        }

        if (OutputLemmas is null || OutputLemmas < 0)
        {
            problems.Add("outputLemmas must be a non-negative number.");
        }
        else if (actualLemmas is { } lemmas && lemmas != OutputLemmas)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"outputLemmas is {OutputLemmas} but '{GeneratedFile}' has {lemmas} lemmas; regenerate the file and its sidecar together."));
        }

        if (Sources.Count == 0)
        {
            problems.Add("sources must list at least one input file.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Sources.Count; i++)
        {
            SourceProblems(Sources[i], i, problems);
            if (Sources[i].Id is { Length: > 0 } id && !ids.Add(id))
            {
                problems.Add($"sources: the id '{id}' is listed twice.");
            }
        }

        return problems;
    }

    private static void SourceProblems(LexiconSourceFile source, int index, List<string> problems)
    {
        var where = string.Create(CultureInfo.InvariantCulture, $"sources[{index}]");
        if (string.IsNullOrWhiteSpace(source.Id))
        {
            problems.Add($"{where}: id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(source.File))
        {
            problems.Add($"{where}: file must not be empty.");
        }

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            problems.Add($"{where}: url must be an absolute https URL.");
        }

        if (!IsSha256(source.Sha256))
        {
            problems.Add($"{where}: sha256 must be 64 lower-case hex digits.");
        }

        if (source.Bytes is null or < 0)
        {
            problems.Add($"{where}: bytes must be a non-negative number.");
        }

        if (source.Lines is null or < 0)
        {
            problems.Add($"{where}: lines must be a non-negative number.");
        }

        if (string.IsNullOrWhiteSpace(source.License))
        {
            problems.Add($"{where}: license must name the licence of the data.");
        }

        if (!DateOnly.TryParseExact(source.RetrievedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            problems.Add($"{where}: retrievedOn '{source.RetrievedOn}' is not a YYYY-MM-DD date.");
        }
    }

    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds an immutable ordinal-sorted count map for the report.</summary>
    public static IReadOnlyDictionary<string, int> SortedCounts(IEnumerable<KeyValuePair<string, int>> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return counts.ToImmutableSortedDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
    }
}
