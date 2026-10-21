using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Response.Effects;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// Loads a content bundle directory: verifies the manifest (schema version, every file hash, the file list and the bundle hash),
/// parses every catalog with the formats owned by the module projects, and cross-checks the references between them.
/// </summary>
/// <remarks>
/// Problems are collected, not thrown one at a time: a <see cref="ContentValidationException"/> lists everything wrong with the bundle.
/// Cross-reference checks: every prompt's answer set, allowed intents, agent and slot grammar ids exist; every agent condition intent and dialogue
/// intent exists, and each variant pool is no larger than the dialogue pool it draws from; every effect intent exists; the dialogue catalog
/// has a <c>not_understood</c> pool; every dialogue template renders against a sample slot map (see <c>SampleSlots</c>) using the
/// bundle's own lexicon; provenance covers every file and a language-model draft is approved by a person.
/// </remarks>
public static class ContentLoader
{
    /// <summary>Loads and validates the bundle in the directory; throws <see cref="ContentValidationException"/> listing every problem.</summary>
    public static ContentBundle Load(string bundleDir, ContentLoadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(bundleDir);
        options ??= ContentLoadOptions.Default;
        if (!Directory.Exists(bundleDir))
        {
            throw new ContentValidationException([$"Bundle directory '{bundleDir}' does not exist."]);
        }

        var problems = new List<string>();
        var manifest = ReadManifest(bundleDir, problems);
        if (manifest is null)
        {
            throw new ContentValidationException(problems);
        }

        var files = new BundleFiles(bundleDir);
        if (options.VerifyHashes)
        {
            VerifyHashes(manifest, files, problems);
        }

        var parts = Parse(files, options, problems);
        ContentValidator.Validate(parts, files, problems);
        if (problems.Count > 0)
        {
            throw new ContentValidationException(problems);
        }

        return new ContentBundle(
            manifest,
            parts.Provenance!,
            parts.Lexicon!,
            parts.LexiconSources,
            parts.AnswerSets,
            parts.Intents!,
            parts.Prompts,
            parts.Agents,
            parts.Dialogue!,
            parts.FeedbackPolicy!,
            parts.EvaluationConfig!,
            parts.Effects!,
            parts.Cefr,
            HostFiles(files, options));
    }

    // The host-owned files: every file the loader does not parse, with the bytes the hash check read. Only a verified load exposes them.
    private static Dictionary<string, ReadOnlyMemory<byte>> HostFiles(BundleFiles files, ContentLoadOptions options)
    {
        var hostFiles = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        if (!options.VerifyHashes)
        {
            return hostFiles;
        }

        foreach (var path in files.Paths)
        {
            if (!ContentPaths.IsLoaderOwned(path))
            {
                hostFiles.Add(path, files.Bytes(path));
            }
        }

        return hostFiles;
    }

    private static ContentManifest? ReadManifest(string bundleDir, List<string> problems)
    {
        var path = Path.Combine(bundleDir, ContentPaths.Manifest);
        if (!File.Exists(path))
        {
            problems.Add($"{ContentPaths.Manifest}: file is missing.");
            return null;
        }

        ContentManifest manifest;
        try
        {
            manifest = ContentManifest.FromJson(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (JsonException ex)
        {
            problems.Add($"{ContentPaths.Manifest}: {FirstLine(ex.Message)}");
            return null;
        }

        if (manifest.SchemaVersion != ContentManifest.CurrentSchemaVersion)
        {
            problems.Add($"{ContentPaths.Manifest}: unsupported schemaVersion {manifest.SchemaVersion}; expected {ContentManifest.CurrentSchemaVersion}.");
            return null;
        }

        return manifest;
    }

    private static void VerifyHashes(ContentManifest manifest, BundleFiles files, List<string> problems)
    {
        var listed = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (var entry in manifest.Files)
        {
            if (previous is not null && string.CompareOrdinal(previous, entry.Path) > 0)
            {
                problems.Add($"{ContentPaths.Manifest}: files are not sorted by path ('{entry.Path}' follows '{previous}').");
            }

            previous = entry.Path;
            if (!listed.Add(entry.Path))
            {
                problems.Add($"{ContentPaths.Manifest}: '{entry.Path}' is listed twice.");
                continue;
            }

            if (!files.Contains(entry.Path))
            {
                problems.Add($"{entry.Path}: file is missing (listed in {ContentPaths.Manifest}).");
                continue;
            }

            var actual = ContentFiles.Digest(entry.Path, files.Bytes(entry.Path));
            if (!string.Equals(actual.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                problems.Add($"{entry.Path}: hash mismatch (manifest {entry.Sha256}, actual {actual.Sha256}).");
            }
            else if (actual.Bytes != entry.Bytes)
            {
                problems.Add($"{entry.Path}: size mismatch (manifest {entry.Bytes} bytes, actual {actual.Bytes}).");
            }
        }

        foreach (var path in files.Paths)
        {
            if (!listed.Contains(path))
            {
                problems.Add($"{path}: file is not listed in {ContentPaths.Manifest}.");
            }
        }

        var computed = ContentManifest.ComputeBundleHash(manifest.Files);
        if (!string.Equals(computed, manifest.BundleHash, StringComparison.Ordinal))
        {
            problems.Add($"{ContentPaths.Manifest}: bundleHash mismatch (manifest {manifest.BundleHash}, computed {computed}).");
        }
    }

    private static LoadedParts Parse(BundleFiles files, ContentLoadOptions options, List<string> problems)
    {
        var parts = new LoadedParts
        {
            Provenance = ParseRequired(files, ContentPaths.Provenance, ContentProvenance.FromJson, problems),
            Intents = ParseRequired(files, ContentPaths.Intents, IntentCatalog.Load, problems),
            Dialogue = ParseRequired(files, ContentPaths.Dialogue, DialogueCatalog.FromJson, problems),
            FeedbackPolicy = ParseRequired(files, ContentPaths.FeedbackPolicy, FeedbackPolicy.FromJson, problems),
            EvaluationConfig = ParseRequired(files, ContentPaths.EvaluationConfig, ParseEvaluationConfig, problems),
            Effects = ParseRequired(files, ContentPaths.Effects, EffectTable.FromJson, problems),
        };

        parts.Lexicon = ParseLexicon(files, parts, options, problems);
        parts.Prompts = ParseRequired(files, ContentPaths.Prompts, ParsePrompts, problems) ?? LoadedParts.EmptyMap<PromptSpec>();
        parts.AnswerSets = ParseFolder(files, ContentPaths.AnswerSetFolder, ".json", AnswerSet.FromJson, static set => set.AnswerSetId, "answerSetId", problems);
        parts.Agents = ParseFolder(files, ContentPaths.AgentFolder, ".json", AgentStateMachine.FromJson, static machine => machine.AgentId, "agentId", problems);
        parts.Cefr = ParseCefr(files, problems);
        return parts;
    }

    /// <summary>
    /// Builds the bundle lexicon from every <c>lexicon/*.json</c> file except the sidecars (<c>*.source.json</c>, which describe a generated file and hold
    /// no entries). The merge policy: the files are unioned; entries with the same form, lemma, part of speech and features are one reading and are
    /// stored once, so a word the hand-authored seed and an import both list is not duplicated; entries that differ in any of those four members are
    /// different readings of the form, they coexist, and the analyzer reads a token as all of them. File order does not matter: the merged entries are
    /// sorted ordinally. A file that fails to parse is reported with its path and left out.
    /// </summary>
    private static NlpLexicon? ParseLexicon(BundleFiles files, LoadedParts parts, ContentLoadOptions options, List<string> problems)
    {
        var paths = files.InFolder(ContentPaths.LexiconFolder, ".json").Where(static path => !LexiconSourceInfo.IsSidecarPath(path)).ToList();
        string? key = null;
        LoadedLexicons? loaded = null;
        Dictionary<string, string>? hashes = null;
        if (options.LexiconCache is { } cache && paths.Count > 0)
        {
            hashes = paths.ToDictionary(static path => path, path => DeterministicHash.Sha256Hex(files.Bytes(path)), StringComparer.Ordinal);
            key = DeterministicHash.Combine([.. paths.SelectMany(path => new[] { path, hashes[path] })]);
            loaded = cache.Find(key);
        }

        if (loaded is null)
        {
            var before = problems.Count;
            loaded = ReadLexicons(files, paths, options.LexiconCache, hashes, problems);
            if (loaded?.Lexicon is not null && key is not null && problems.Count == before)
            {
                options.LexiconCache!.Add(key, loaded);
            }
        }

        parts.LexiconGeneratedBy = (loaded?.Files ?? new Dictionary<string, LexiconFileSummary>())
            .ToImmutableSortedDictionary(static pair => pair.Key, static pair => pair.Value.GeneratedBy, StringComparer.Ordinal);
        ParseLexiconSidecars(files, loaded?.Files ?? new Dictionary<string, LexiconFileSummary>(), parts, problems);
        if (loaded is null)
        {
            if (!problems.Exists(static problem => problem.StartsWith(ContentPaths.LexiconFolder, StringComparison.Ordinal)))
            {
                problems.Add($"{ContentPaths.LexiconFolder}: no lexicon file (*.json) found.");
            }

            return null;
        }

        return loaded.Lexicon;
    }

    private static LoadedLexicons? ReadLexicons(BundleFiles files, List<string> paths, LexiconCache? cache, Dictionary<string, string>? hashes, List<string> problems)
    {
        var before = problems.Count;
        var entries = new List<LexiconEntry>();
        var summaries = new SortedDictionary<string, LexiconFileSummary>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var sha = hashes?[path];
            var file = sha is null ? null : cache!.FindFile(sha);
            if (file is null)
            {
                file = ParseBytes(files, path, NlpLexicon.ReadFile, problems);
                if (file is null)
                {
                    continue;
                }

                if (sha is not null)
                {
                    cache!.AddFile(sha, file);
                }
            }

            summaries.Add(path, new LexiconFileSummary(file.GeneratedBy, file.Entries.Count, DistinctLemmas(file)));
            entries.AddRange(file.Entries);
        }

        // A damaged file is already reported with its path; merging and indexing the rest would only cost time and invite follow-on problems about a lexicon that is incomplete.
        return summaries.Count == 0 ? null : new LoadedLexicons(problems.Count > before ? null : NlpLexicon.FromEntries(entries), summaries);
    }

    // A sidecar belongs to exactly one generated lexicon file in the same folder, and its entry and lemma counts must match that file.
    private static void ParseLexiconSidecars(BundleFiles files, IReadOnlyDictionary<string, LexiconFileSummary> parsed, LoadedParts parts, List<string> problems)
    {
        var sidecars = ImmutableSortedDictionary.CreateBuilder<string, LexiconSourceInfo>(StringComparer.Ordinal);
        foreach (var path in files.InFolder(ContentPaths.LexiconFolder, ".json").Where(LexiconSourceInfo.IsSidecarPath))
        {
            var info = ParseText(files, path, LexiconSourceInfo.FromJson, problems);
            if (info is null)
            {
                continue;
            }

            sidecars.Add(path, info);
            var folder = path[..(path.LastIndexOf('/') + 1)];
            long? actualEntries = null;
            long? actualLemmas = null;
            if (info.GeneratedFile is { Length: > 0 } name)
            {
                if (name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal)
                    || !parsed.TryGetValue(folder + name, out var lexicon))
                {
                    problems.Add($"{path}: generatedFile '{name}' is not a lexicon file next to the sidecar.");
                }
                else
                {
                    actualEntries = lexicon.Entries;
                    actualLemmas = lexicon.Lemmas;
                    if (!string.Equals(Path.GetFileName(path), LexiconSourceInfo.SidecarNameFor(name), StringComparison.Ordinal))
                    {
                        problems.Add($"{path}: a sidecar must be named '{LexiconSourceInfo.SidecarNameFor(name)}' after the file it describes.");
                    }
                }
            }

            foreach (var problem in info.Problems(actualEntries, actualLemmas))
            {
                problems.Add($"{path}: {problem}");
            }
        }

        parts.LexiconSources = sidecars.ToImmutable();
    }

    private static int DistinctLemmas(LexiconFile file)
    {
        var lemmas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in file.Entries)
        {
            lemmas.Add(entry.Lemma.Normalize(NormalizationForm.FormC).ToLowerInvariant());
        }

        return lemmas.Count;
    }

    private static ImmutableSortedDictionary<string, PromptSpec> ParsePrompts(string json)
    {
        var list = CanonicalJson.Deserialize<List<PromptSpec>>(json);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, PromptSpec>(StringComparer.Ordinal);
        foreach (var prompt in list)
        {
            if ((object?)prompt is null
                || string.IsNullOrWhiteSpace(prompt.PromptId)
                || (object?)prompt.AnswerSetId is null
                || (object?)prompt.AllowedIntentIds is null
                || (object?)prompt.SlotGrammarIds is null)
            {
                throw new JsonException("Every prompt needs promptId, answerSetId, allowedIntentIds and slotGrammarIds.");
            }

            if (!builder.TryAdd(prompt.PromptId, prompt))
            {
                throw new JsonException($"Duplicate promptId '{prompt.PromptId}'.");
            }
        }

        return builder.ToImmutable();
    }

    private static EvaluationConfig ParseEvaluationConfig(string json)
    {
        var config = CanonicalJson.Deserialize<EvaluationConfig>(json);
        if ((object?)config.Beginner is null || (object?)config.Intermediate is null || (object?)config.Advanced is null)
        {
            throw new JsonException("The evaluation config needs beginner, intermediate and advanced.");
        }

        return config;
    }

    private static ImmutableSortedDictionary<string, IReadOnlySet<string>> ParseCefr(BundleFiles files, List<string> problems)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var path in files.InFolder(ContentPaths.CefrFolder, ".txt"))
        {
            var level = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var lemmas = ParseText(files, path, ParseLemmaList, problems);
            if (lemmas is null)
            {
                continue;
            }

            if (lemmas.Count == 0)
            {
                problems.Add($"{path}: the lemma list is empty.");
            }

            if (!builder.TryAdd(level, lemmas))
            {
                problems.Add($"{path}: duplicate CEFR level '{level}'.");
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableSortedSet<string> ParseLemmaList(string text)
    {
        var set = ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0 && line[0] != '#')
            {
                set.Add(line.Normalize(NormalizationForm.FormC).ToLowerInvariant());
            }
        }

        return set.ToImmutable();
    }

    private static ImmutableSortedDictionary<string, T> ParseFolder<T>(
        BundleFiles files,
        string folder,
        string extension,
        Func<string, T> parse,
        Func<T, string> idOf,
        string idName,
        List<string> problems)
        where T : class
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, T>(StringComparer.Ordinal);
        foreach (var path in files.InFolder(folder, extension))
        {
            var item = ParseText(files, path, parse, problems);
            if (item is null)
            {
                continue;
            }

            var id = idOf(item);
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(id, stem, StringComparison.Ordinal))
            {
                problems.Add($"{path}: {idName} '{id}' must equal the file name '{stem}'.");
            }
            else
            {
                builder.Add(id, item);
            }
        }

        return builder.ToImmutable();
    }

    private static T? ParseRequired<T>(BundleFiles files, string path, Func<string, T> parse, List<string> problems)
        where T : class
    {
        if (!files.Contains(path))
        {
            problems.Add($"{path}: required file is missing.");
            return null;
        }

        return ParseText(files, path, parse, problems);
    }

    private static T? ParseText<T>(BundleFiles files, string path, Func<string, T> parse, List<string> problems)
        where T : class
    {
        var text = files.Text(path);
        if (text is null)
        {
            problems.Add($"{path}: the file is not valid UTF-8 text.");
            return null;
        }

        try
        {
            return parse(text);
        }
        catch (Exception ex) when (IsContentError(ex))
        {
            problems.Add($"{path}: {FirstLine(ex.Message)}");
            return null;
        }
    }

    private delegate T BytesParser<out T>(ReadOnlySpan<byte> utf8);

    // Like ParseText for a parser that reads UTF-8 bytes directly, which saves decoding and encoding a multi-megabyte file; invalid UTF-8 is the parser's JsonException.
    private static T? ParseBytes<T>(BundleFiles files, string path, BytesParser<T> parse, List<string> problems)
        where T : class
    {
        try
        {
            return parse(files.Bytes(path));
        }
        catch (Exception ex) when (IsContentError(ex))
        {
            problems.Add($"{path}: {FirstLine(ex.Message)}");
            return null;
        }
    }

    internal static bool IsContentError(Exception ex)
    {
        return ex is JsonException or FormatException or ArgumentException or InvalidDataException
            or InvalidOperationException or KeyNotFoundException or NotSupportedException or TemplateException;
    }

    internal static string FirstLine(string message)
    {
        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).TrimEnd('\r');
    }
}

/// <summary>Everything parsed from the bundle; members are null or empty when their file failed to parse.</summary>
internal sealed class LoadedParts
{
    public ContentProvenance? Provenance { get; set; }

    public NlpLexicon? Lexicon { get; set; }

    /// <summary>The <c>generated_by</c> marker of each lexicon file by bundle-relative path (null when the file has none); sidecars are not listed.</summary>
    public IReadOnlyDictionary<string, string?> LexiconGeneratedBy { get; set; } = EmptyMap<string?>();

    /// <summary>The parsed sidecars by bundle-relative path.</summary>
    public IReadOnlyDictionary<string, LexiconSourceInfo> LexiconSources { get; set; } = EmptyMap<LexiconSourceInfo>();

    public IntentCatalog? Intents { get; set; }

    public DialogueCatalog? Dialogue { get; set; }

    public FeedbackPolicy? FeedbackPolicy { get; set; }

    public EvaluationConfig? EvaluationConfig { get; set; }

    public EffectTable? Effects { get; set; }

    public IReadOnlyDictionary<string, PromptSpec> Prompts { get; set; } = EmptyMap<PromptSpec>();

    public IReadOnlyDictionary<string, AnswerSet> AnswerSets { get; set; } = EmptyMap<AnswerSet>();

    public IReadOnlyDictionary<string, AgentStateMachine> Agents { get; set; } = EmptyMap<AgentStateMachine>();

    public IReadOnlyDictionary<string, IReadOnlySet<string>> Cefr { get; set; } = EmptyMap<IReadOnlySet<string>>();

    /// <summary>An empty map with the ordinal key comparer (the default comparer of an immutable sorted dictionary is culture-aware).</summary>
    public static ImmutableSortedDictionary<string, T> EmptyMap<T>()
    {
        return ImmutableSortedDictionary.Create<string, T>(StringComparer.Ordinal);
    }
}

/// <summary>All files of a bundle directory read into memory, addressed by relative path.</summary>
internal sealed class BundleFiles
{
    private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);

    public BundleFiles(string bundleDir)
    {
        var paths = ContentFiles.Enumerate(bundleDir);
        var root = Path.GetFullPath(bundleDir);
        foreach (var path in paths)
        {
            _bytes.Add(path, File.ReadAllBytes(Path.Combine(root, path)));
        }

        Paths = paths;
    }

    /// <summary>Every relative path, in ordinal order.</summary>
    public IReadOnlyList<string> Paths { get; }

    public bool Contains(string path)
    {
        return _bytes.ContainsKey(path);
    }

    public byte[] Bytes(string path)
    {
        return _bytes[path];
    }

    /// <summary>Returns the decoded text, or null when the file is not valid UTF-8.</summary>
    public string? Text(string path)
    {
        return ContentFiles.DecodeText(_bytes[path]);
    }

    /// <summary>Returns the paths below the folder (any depth) with the extension, in ordinal order.</summary>
    public IEnumerable<string> InFolder(string folder, string extension)
    {
        return Paths.Where(path => path.StartsWith(folder, StringComparison.Ordinal)
            && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
