using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>The structural damage applied to a copy of the content bundle.</summary>
public enum ContentMutationKind
{
    /// <summary>Remove one property of a JSON object.</summary>
    RemoveProperty,

    /// <summary>Replace one value with a value of another JSON kind (null, empty string, number, boolean, array, object).</summary>
    ChangeKind,

    /// <summary>Replace a reference to an id (answer set, intent, agent, state, pool, grammar) with an id that does not exist.</summary>
    DanglingReference,

    /// <summary>Replace the value of an enumerated field with a value its enumeration does not have.</summary>
    UnknownEnum,

    /// <summary>Append a copy of an array element that carries an id, so the id exists twice.</summary>
    DuplicateId,

    /// <summary>Flip one bit of one byte of a file and leave the manifest as it was.</summary>
    FlipByte,

    /// <summary>
    /// Replace the value of a required non-empty string with the empty string: an effect's <c>effectId</c>, <c>intent</c> or <c>kind</c>, a prompt's
    /// <c>promptId</c>, and a prompt's <c>text</c> where it is present (optional, but never empty). The loader refuses each of them when empty.
    /// </summary>
    EmptyRequiredString,
}

/// <summary>One mutation: what kind, which file, which place in it (a path of property names and array indexes), and a detail (the new value or the byte).</summary>
/// <param name="Kind">The kind of damage.</param>
/// <param name="File">The bundle-relative path of the file the mutation touches.</param>
/// <param name="Path">The place inside a JSON file (property names and array indexes), empty for a byte flip.</param>
/// <param name="Detail">The replacement value as JSON text, or for a byte flip the offset and the bit as <c>offset:bit</c>.</param>
public sealed record ContentMutation(ContentMutationKind Kind, string File, IReadOnlyList<string> Path, string Detail)
{
    /// <summary>Whether the mutation changes a value the loader must read, so the loader has to reject the bundle (everything except dropping or retyping an optional field).</summary>
    public bool MustBeRejected => Kind is ContentMutationKind.DanglingReference or ContentMutationKind.UnknownEnum or ContentMutationKind.DuplicateId or ContentMutationKind.FlipByte
        or ContentMutationKind.EmptyRequiredString;

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Kind} file={File} path=/{string.Join('/', Path)} detail={(Detail.Length > 80 ? Detail[..80] + "..." : Detail)}";
    }
}

/// <summary>
/// Structural mutations of the <c>content/game-v1</c> JSON files, found by walking the files themselves (so a new file or a new field widens the
/// coverage): remove a property, change a value's JSON kind, point a reference at an id that does not exist, give an enumerated field an unknown
/// value, empty a required non-empty string (an effect's id, intent or kind, a prompt's id or text), duplicate an element that has an id, and flip a byte
/// without touching the manifest. Every mutated bundle is written to a unique temporary
/// directory, with the manifest recomputed from the mutated files (so the loader's schema and reference checks run, not just its hash check),
/// except for the byte flip, which leaves the manifest alone on purpose. The bundle's 433 speech clips (33 MB, host-owned audio the loader only hashes) are
/// linked into each copy instead of written, so an iteration does not write 38 MB; a byte flip in a clip writes that one file.
/// </summary>
public sealed class ContentMutationGen
{
    // Properties whose string value is a reference to an id defined elsewhere in the bundle.
    private static readonly HashSet<string> ReferenceProperties = new(StringComparer.Ordinal)
    {
        "answerSetId", "allowedIntentIds", "agentId", "slotGrammarIds", "intent", "dialogueIntent", "to", "initialState", "variantPoolId",
    };

    // Properties whose string value is a member of an enumeration. (The lexicon file's own generated_by is free text, not an enumeration; the
    // enumerated generatedBy is the provenance item's.) An effect's kind is not listed: since M19a it is an opaque outcome kind, so no
    // unknown value is a defect. Its empty value is a defect instead (RequiredNonEmptyProperties).
    private static readonly HashSet<string> EnumProperties = new(StringComparer.Ordinal)
    {
        "mode", "difficulty", "type", "notCommunicatedType", "generatedBy", "pos", "person", "number", "tense", "mood", "gender",
    };

    // The string fields that the loader refuses when empty, per file: the effect table's constructors (an id, an intent and an outcome kind are
    // required), the prompt parser (a promptId is required) and the prompt validator (a text that is present must not be empty). Only these places
    // are candidates for EmptyRequiredString, so an empty string the loader accepts elsewhere is never reported as a defect.
    private static readonly Dictionary<string, HashSet<string>> RequiredNonEmptyProperties = new(StringComparer.Ordinal)
    {
        [ContentPaths.Effects] = new(StringComparer.Ordinal) { "effectId", "intent", "kind" },
        [ContentPaths.Prompts] = new(StringComparer.Ordinal) { "promptId", "text" },
    };

    // Object properties that carry an id; an array of such objects can have an element duplicated.
    private static readonly HashSet<string> IdProperties = new(StringComparer.Ordinal)
    {
        "id", "intentId", "promptId", "answerId", "effectId", "lineId", "path",
    };

    /// <summary>Files larger than this are not walked for structural mutations (see the constructor).</summary>
    private const int MaxStructuralBytes = 1_000_000;

    private static int _counter;

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<ContentMutationKind, List<(string File, string[] Path)>> _candidates = [];
    private readonly Dictionary<string, JsonNode> _parsed = new(StringComparer.Ordinal);
    private readonly ContentManifest _original;
    private readonly Dictionary<ContentMutationKind, (string File, string[] Path)[][]> _byFile = [];
    private readonly ContentMutationKind[] _kinds;

    private ContentMutationGen(string directory, IReadOnlyCollection<string> hostFiles)
    {
        Directory = directory;
        var host = new HashSet<string>(hostFiles, StringComparer.Ordinal);
        foreach (var path in System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            _files[relative] = File.ReadAllBytes(path);
        }

        foreach (var kind in Enum.GetValues<ContentMutationKind>())
        {
            _candidates[kind] = [];
        }

        _original = ContentManifest.FromJson(Encoding.UTF8.GetString(_files["manifest.json"]));
        foreach (var (relative, bytes) in _files)
        {
            // The imported lexicon (megabytes, tens of thousands of entries) has the schema of the small hand-authored lexicon files, so structural damage
            // is drawn from those; parsing and writing it back for every mutation would cost more than all the other files together. A flipped byte
            // still reaches it (the flip kind needs no parsing).
            // A host-owned file (the game's own JSON under game/, the speech clips) is hashed but not read by the loader, so damage to its structure is no
            // defect of the loader's; only a flipped byte reaches it, through the hash.
            if (!relative.EndsWith(".json", StringComparison.Ordinal) || bytes.Length > MaxStructuralBytes || host.Contains(relative))
            {
                continue;
            }

            var root = JsonNode.Parse(bytes)!;
            _parsed[relative] = root;
            Walk(relative, root, [], null);
        }

        foreach (var file in _files.Keys.Where(static f => f != "manifest.json"))
        {
            _candidates[ContentMutationKind.FlipByte].Add((file, []));
        }

        // The candidates are grouped by file once: the imported lexicon alone has hundreds of thousands of places, and a mutation is drawn hundreds of times.
        foreach (var kind in Enum.GetValues<ContentMutationKind>())
        {
            _byFile[kind] = _candidates[kind]
                .GroupBy(static c => c.File, StringComparer.Ordinal)
                .OrderBy(static g => g.Key, StringComparer.Ordinal)
                .Select(static g => g.ToArray())
                .ToArray();
        }

        _kinds = Enum.GetValues<ContentMutationKind>().Where(kind => _candidates[kind].Count > 0).ToArray();
    }

    /// <summary>The repository's bundle, <c>content/game-v1</c>.</summary>
    public static ContentMutationGen Shared { get; } = new(PipelineHarness.Shared.BundleDirectory, PipelineHarness.Shared.Bundle.HostFilePaths);

    /// <summary>
    /// The same walk over a copy of the bundle that carries the effects fixture (<see cref="PipelineHarness.EffectsJson"/>), because the game's own effect table is
    /// empty and the loader's effect checks (an effect's id, intent and kind, its curve, its intent reference) would never be mutated otherwise. The copy is
    /// rehashed, so its bundle hash is its own (<see cref="BaseHash"/>) and not the repository bundle's; it lives in a temporary folder until the process ends.
    /// </summary>
    public static ContentMutationGen WithEffects => WithEffectsLazy.Value;

    private static readonly Lazy<ContentMutationGen> WithEffectsLazy = new(() =>
    {
        var copy = new TempBundle(full: true);
        copy.Write("effects/effects.json", PipelineHarness.EffectsJson);
        copy.Rehash();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => copy.Dispose();
        return new ContentMutationGen(copy.Directory, PipelineHarness.Shared.Bundle.HostFilePaths);
    });

    /// <summary>The parsed, unmutated JSON of a file of the catalog (a file the walk parsed: loader-owned and under the size limit).</summary>
    public JsonNode OriginalJson(string file)
    {
        return _parsed[file];
    }

    /// <summary>The bundle hash of the unmutated files this catalog was read from.</summary>
    public string BaseHash => _original.BundleHash;

    /// <summary>A generator over mutations of this catalog: the seed picks the kind, the file, the place and the new value.</summary>
    public Gen<ContentMutation> Draws => Gen.ULong.Select(seed => Build(new SeededRng(seed)));

    /// <summary>The directory the catalog was read from.</summary>
    public string Directory { get; }

    /// <summary>The bundle-relative paths of the files, in ordinal order.</summary>
    public IReadOnlyList<string> Files => [.. _files.Keys];

    /// <summary>A generator over mutations: the seed picks the kind, the file, the place and the new value.</summary>
    public static Gen<ContentMutation> Mutation { get; } = Gen.ULong.Select(static seed => Shared.Build(new SeededRng(seed)));

    /// <summary>Draws the mutation a random stream decides on.</summary>
    public ContentMutation Build(SeededRng rng)
    {
        var kind = rng.Pick(_kinds);

        // Choose the file first, so the thousands of lexicon entries do not drown out the dozen structural files.
        var group = rng.Pick(_byFile[kind]);
        var (file, path) = rng.Pick(group);

        switch (kind)
        {
            case ContentMutationKind.ChangeKind:
                var current = KindOf(Navigate(_parsed[file], path));
                var options = AdversarialCorpus.JsonKinds.Where(json => KindOf(JsonNode.Parse(json)) != current).ToArray();
                return new ContentMutation(kind, file, path, rng.Pick(options));
            case ContentMutationKind.DanglingReference:
                return new ContentMutation(kind, file, path, JsonSerializer.Serialize("fresh-id-" + rng.NextInt(100_000)));
            case ContentMutationKind.UnknownEnum:
                return new ContentMutation(kind, file, path, JsonSerializer.Serialize(Navigate(_parsed[file], path)!.GetValue<string>() + "-unknown"));
            case ContentMutationKind.FlipByte:
                return new ContentMutation(kind, file, [], $"{rng.NextInt(_files[file].Length)}:{rng.NextInt(8)}");
            case ContentMutationKind.EmptyRequiredString:
                return new ContentMutation(kind, file, path, JsonSerializer.Serialize(string.Empty));
            default:
                return new ContentMutation(kind, file, path, string.Empty);
        }
    }

    /// <summary>
    /// Writes the bundle with the mutation applied to a new unique temporary directory and returns it; the caller deletes it. For every kind but the
    /// byte flip the manifest is recomputed from the mutated files.
    /// </summary>
    public string Materialize(ContentMutation? mutation)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vocaterra-fuzz-content-{Environment.ProcessId}-{Interlocked.Increment(ref _counter)}");
        System.IO.Directory.CreateDirectory(directory);
        foreach (var (relative, bytes) in _files)
        {
            var target = Path.Combine(directory, relative);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (relative.StartsWith("speech/", StringComparison.Ordinal) && mutation?.File != relative && TryLink(target, Path.Combine(Directory, relative)))
            {
                continue;
            }

            File.WriteAllBytes(target, bytes);
        }

        if (mutation is null)
        {
            return directory;
        }

        var file = Path.Combine(directory, mutation.File);
        if (mutation.Kind == ContentMutationKind.FlipByte)
        {
            var parts = mutation.Detail.Split(':');
            var data = File.ReadAllBytes(file);
            data[int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture)] ^= (byte)(1 << int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllBytes(file, data);
            return directory;
        }

        var root = JsonNode.Parse(_files[mutation.File])!;
        Apply(ref root, mutation);
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(file, root.ToJsonString(options) + "\n", new UTF8Encoding(false));
        if (mutation.File != "manifest.json")
        {
            File.WriteAllText(Path.Combine(directory, "manifest.json"), ManifestAfterMutating(mutation.File, directory).ToJson(), new UTF8Encoding(false));
        }

        return directory;
    }

    // A symbolic link to the original file; false where the platform or the account cannot make one (the caller then writes the bytes).
    private static bool TryLink(string target, string source)
    {
        try
        {
            File.CreateSymbolicLink(target, source);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    // The manifest of the mutated copy: the committed manifest with the entry of the one file that changed recomputed from the file as written (the other
    // entries are those of byte-identical files), so the megabytes of the unchanged lexicon are not read and hashed again for every mutation.
    private ContentManifest ManifestAfterMutating(string relative, string directory)
    {
        var scratch = directory + "-digest";
        var target = Path.Combine(scratch, relative);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            File.Copy(Path.Combine(directory, relative), target);
            var digest = ContentManifest.Compute(scratch).Files.Single();
            var files = _original.Files.Select(file => file.Path == relative ? digest : file).ToList();
            return _original with { Files = files, BundleHash = ContentManifest.ComputeBundleHash(files) };
        }
        finally
        {
            System.IO.Directory.Delete(scratch, recursive: true);
        }
    }

    private static void Apply(ref JsonNode root, ContentMutation mutation)
    {
        var path = mutation.Path;
        if (mutation.Kind is ContentMutationKind.DuplicateId)
        {
            var element = Navigate(root, path)!;
            var array = (JsonArray)Navigate(root, [.. path.Take(path.Count - 1)])!;
            array.Add(element.DeepClone());
            return;
        }

        var parent = Navigate(root, [.. path.Take(path.Count - 1)])!;
        var last = path[^1];
        if (mutation.Kind == ContentMutationKind.RemoveProperty)
        {
            ((JsonObject)parent).Remove(last);
            return;
        }

        var replacement = JsonNode.Parse(mutation.Detail);
        if (parent is JsonObject jsonObject)
        {
            jsonObject[last] = replacement;
        }
        else
        {
            ((JsonArray)parent)[int.Parse(last, System.Globalization.CultureInfo.InvariantCulture)] = replacement;
        }
    }

    // True and false are one kind for this purpose: replacing true by false changes the value, not the JSON kind.
    private static JsonValueKind KindOf(JsonNode? node)
    {
        var kind = node is null ? JsonValueKind.Null : node.GetValueKind();
        return kind == JsonValueKind.False ? JsonValueKind.True : kind;
    }

    private static JsonNode? Navigate(JsonNode? node, IReadOnlyList<string> path)
    {
        foreach (var segment in path)
        {
            node = node is JsonArray array ? array[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)] : node![segment];
        }

        return node;
    }

    // Records, for every place in the file, which kinds of mutation apply there.
    private void Walk(string file, JsonNode? node, string[] path, string? parentName)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, child) in jsonObject)
                {
                    var childPath = Append(path, name);
                    _candidates[ContentMutationKind.RemoveProperty].Add((file, childPath));
                    Consider(file, child, childPath, name);
                    Walk(file, child, childPath, name);
                }

                break;
            case JsonArray array:
                if (array.Count > 0 && array.All(item => item is JsonObject o && o.Any(p => IdProperties.Contains(p.Key) && p.Value is JsonValue)))
                {
                    // Duplicate the first element only: a catalog of 1 200 lexicon rows would otherwise be all duplicates (and the lexicon has no ids).
                    _candidates[ContentMutationKind.DuplicateId].Add((file, Append(path, "0")));
                }

                for (var i = 0; i < array.Count; i++)
                {
                    var childPath = Append(path, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (array[i] is JsonValue)
                    {
                        Consider(file, array[i], childPath, parentName);
                    }

                    Walk(file, array[i], childPath, parentName);
                }

                break;
            default:
                break;
        }
    }

    private void Consider(string file, JsonNode? node, string[] path, string? name)
    {
        _candidates[ContentMutationKind.ChangeKind].Add((file, path));
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && name is not null)
        {
            if (ReferenceProperties.Contains(name) && !IsDefinition(file, name) && text.Length > 0)
            {
                _candidates[ContentMutationKind.DanglingReference].Add((file, path));
            }

            if (EnumProperties.Contains(name) && text.Length > 0)
            {
                _candidates[ContentMutationKind.UnknownEnum].Add((file, path));
            }

            if (text.Length > 0 && RequiredNonEmptyProperties.TryGetValue(file, out var required) && required.Contains(name))
            {
                _candidates[ContentMutationKind.EmptyRequiredString].Add((file, path));
            }
        }
    }

    // The same property name defines an id in some files (answer-set files carry their own answerSetId, agent files their own agentId).
    private static bool IsDefinition(string file, string name)
    {
        return (name == "answerSetId" && file.StartsWith("answer-sets/", StringComparison.Ordinal))
            || (name == "agentId" && file.StartsWith("agents/", StringComparison.Ordinal));
    }

    private static string[] Append(string[] path, string segment)
    {
        var copy = new string[path.Length + 1];
        path.CopyTo(copy, 0);
        copy[^1] = segment;
        return copy;
    }
}
