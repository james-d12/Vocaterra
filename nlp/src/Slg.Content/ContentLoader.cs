using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Decision.Feedback;
using Slg.Decision.Npc;
using Slg.Evaluation;
using Slg.Nlp.Intent;
using Slg.Nlp.Lexicon;
using Slg.Nlp.Matching;
using Slg.Response.Effects;
using Slg.Response.Templates;
using NlpLexicon = Slg.Nlp.Lexicon.Lexicon;

namespace Slg.Content;

/// <summary>
/// Loads a content bundle directory: verifies the manifest (schema version, every file hash, the file list and the bundle hash),
/// parses every catalog with the formats owned by the module projects, and cross-checks the references between them.
/// </summary>
/// <remarks>
/// Problems are collected, not thrown one at a time: a <see cref="ContentValidationException"/> lists everything wrong with the bundle.
/// Cross-reference checks: every prompt's answer set, allowed intents, NPC and slot grammar ids exist; every NPC condition intent and dialogue
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

        var parts = Parse(files, problems);
        ContentValidator.Validate(parts, files, problems);
        if (problems.Count > 0)
        {
            throw new ContentValidationException(problems);
        }

        return new ContentBundle(
            manifest,
            parts.Provenance!,
            parts.Lexicon!,
            parts.AnswerSets,
            parts.Intents!,
            parts.Prompts,
            parts.Npcs,
            parts.Dialogue!,
            parts.FeedbackPolicy!,
            parts.EvaluationConfig!,
            parts.Effects!,
            parts.Cefr);
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

    private static LoadedParts Parse(BundleFiles files, List<string> problems)
    {
        var parts = new LoadedParts
        {
            Provenance = ParseRequired(files, ContentPaths.Provenance, ContentProvenance.FromJson, problems),
            Intents = ParseRequired(files, ContentPaths.Intents, IntentCatalog.Load, problems),
            Dialogue = ParseRequired(files, ContentPaths.Dialogue, DialogueCatalog.FromJson, problems),
            FeedbackPolicy = ParseRequired(files, ContentPaths.FeedbackPolicy, FeedbackPolicy.FromJson, problems),
            EvaluationConfig = ParseRequired(files, ContentPaths.EvaluationConfig, ParseEvaluationConfig, problems),
            Effects = ParseRequired(files, ContentPaths.Effects, EffectTable.FromJson, problems),
            Lexicon = ParseLexicon(files, problems),
        };

        parts.Prompts = ParseRequired(files, ContentPaths.Prompts, ParsePrompts, problems) ?? LoadedParts.EmptyMap<PromptSpec>();
        parts.AnswerSets = ParseFolder(files, ContentPaths.AnswerSetFolder, ".json", AnswerSet.FromJson, static set => set.AnswerSetId, "answerSetId", problems);
        parts.Npcs = ParseFolder(files, ContentPaths.NpcFolder, ".json", NpcStateMachine.FromJson, static machine => machine.NpcId, "npcId", problems);
        parts.Cefr = ParseCefr(files, problems);
        return parts;
    }

    private static NlpLexicon? ParseLexicon(BundleFiles files, List<string> problems)
    {
        var entries = new List<LexiconEntry>();
        var any = false;
        foreach (var path in files.InFolder(ContentPaths.LexiconFolder, ".json"))
        {
            var lexicon = ParseText(files, path, NlpLexicon.Load, problems);
            if (lexicon is null)
            {
                continue;
            }

            any = true;
            entries.AddRange(lexicon.Entries);
        }

        if (!any)
        {
            if (!problems.Exists(static problem => problem.StartsWith(ContentPaths.LexiconFolder, StringComparison.Ordinal)))
            {
                problems.Add($"{ContentPaths.LexiconFolder}: no lexicon file (*.json) found.");
            }

            return null;
        }

        return NlpLexicon.FromEntries(entries);
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

    public IntentCatalog? Intents { get; set; }

    public DialogueCatalog? Dialogue { get; set; }

    public FeedbackPolicy? FeedbackPolicy { get; set; }

    public EvaluationConfig? EvaluationConfig { get; set; }

    public EffectTable? Effects { get; set; }

    public IReadOnlyDictionary<string, PromptSpec> Prompts { get; set; } = EmptyMap<PromptSpec>();

    public IReadOnlyDictionary<string, AnswerSet> AnswerSets { get; set; } = EmptyMap<AnswerSet>();

    public IReadOnlyDictionary<string, NpcStateMachine> Npcs { get; set; } = EmptyMap<NpcStateMachine>();

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
