using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Content;

/// <summary>One file listed in the manifest: its bundle-relative path, content hash and size.</summary>
/// <param name="Path">Relative path with '/' separators.</param>
/// <param name="Sha256">Lowercase hex SHA-256 of the canonical file content (see <see cref="ContentManifest"/>).</param>
/// <param name="Bytes">Size in bytes of the canonical file content.</param>
public sealed record ManifestFile(string Path, string Sha256, long Bytes);

/// <summary>
/// The bundle manifest: schema version, identity, the hash of every file and the bundle hash.
/// </summary>
/// <remarks>
/// Text files (<c>.json</c>, <c>.jsonl</c>, <c>.md</c>, <c>.txt</c>) are hashed as UTF-8 after Unicode NFC normalization and
/// CRLF to LF conversion (the same canonical form as <see cref="DeterministicHash.OfText(string)"/>), so a checkout with
/// different line endings still verifies; every other file is hashed as raw bytes. <c>Bytes</c> is the size of the hashed form.
/// The bundle hash is <see cref="DeterministicHash.Combine(string[])"/> over <c>path, sha256</c> of every file in ordinal path order; when
/// <see cref="Language"/> is present it is over <c>"language", tag, path, sha256, ...</c> instead, so a bundle of another language never shares
/// a hash with a Spanish one while a bundle without the field keeps the hash it always had. <see cref="SupportLanguage"/> is not hashed.
/// </remarks>
/// <param name="SchemaVersion">The manifest schema version.</param>
/// <param name="BundleId">The bundle id.</param>
/// <param name="ContentVersion">The content version.</param>
/// <param name="Files">Every file of the bundle except the manifest, in ordinal path order.</param>
/// <param name="BundleHash">The bundle hash.</param>
/// <param name="Language">
/// The BCP-47 tag of the language the bundle teaches (for example <c>es</c>, <c>de-DE</c>); the pack is chosen by its primary subtag.
/// Absent (null) means Spanish and keeps the pre-language hash.
/// </param>
/// <param name="SupportLanguage">The BCP-47 tag of the learner's support language. Recorded and passed through, never interpreted, not hashed.</param>
public sealed record ContentManifest(
    int SchemaVersion,
    string BundleId,
    string ContentVersion,
    IReadOnlyList<ManifestFile> Files,
    string BundleHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Language = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SupportLanguage = null)
{
    /// <summary>The primary subtag of a bundle whose manifest has no <c>language</c>.</summary>
    public const string DefaultLanguage = "es";

    /// <summary>The only manifest schema version this library reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The content version given to a bundle whose manifest does not exist yet.</summary>
    public const string DefaultContentVersion = "1.0.0";

    /// <summary>Parses manifest JSON; throws <see cref="JsonException"/> when it is malformed or incomplete.</summary>
    public static ContentManifest FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var manifest = CanonicalJson.Deserialize<ContentManifest>(json);

        // Missing members arrive as null even though the record is declared non-nullable, so check explicitly.
        if ((object?)manifest.Files is null
            || (object?)manifest.BundleId is null
            || (object?)manifest.ContentVersion is null
            || (object?)manifest.BundleHash is null)
        {
            throw new JsonException("The manifest needs bundleId, contentVersion, files and bundleHash.");
        }

        foreach (var file in manifest.Files)
        {
            if ((object?)file is null || (object?)file.Path is null || (object?)file.Sha256 is null)
            {
                throw new JsonException("Every manifest file needs a path and a sha256.");
            }
        }

        return manifest;
    }

    /// <summary>Computes the bundle hash over the files in ordinal path order (the given order does not matter).</summary>
    public static string ComputeBundleHash(IEnumerable<ManifestFile> files)
    {
        return ComputeBundleHash(files, null);
    }

    /// <summary>
    /// Computes the bundle hash over the language (when present) and the files in ordinal path order: <c>Combine("language", tag, path, sha, ...)</c>
    /// for a non-null <paramref name="language"/> (the tag exactly as written in the manifest), otherwise exactly <c>Combine(path, sha, ...)</c>.
    /// </summary>
    public static string ComputeBundleHash(IEnumerable<ManifestFile> files, string? language)
    {
        ArgumentNullException.ThrowIfNull(files);
        var parts = new List<string>();
        if (language is not null)
        {
            parts.Add("language");
            parts.Add(language);
        }

        foreach (var file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            parts.Add(file.Path);
            parts.Add(file.Sha256);
        }

        return DeterministicHash.Combine([.. parts]);
    }

    /// <summary>
    /// Builds a manifest from the files on disk (every file under the directory except <c>manifest.json</c> and dot-files).
    /// Identity fields and the language fields come from the arguments, else from an existing manifest when it parses, else the directory name and <see cref="DefaultContentVersion"/> (the language fields stay absent).
    /// </summary>
    public static ContentManifest Compute(string bundleDir, string? bundleId = null, string? contentVersion = null, string? language = null, string? supportLanguage = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(bundleDir);
        if (!Directory.Exists(bundleDir))
        {
            throw new DirectoryNotFoundException($"Bundle directory not found: {bundleDir}");
        }

        ContentManifest? existing = null;
        var manifestPath = Path.Combine(bundleDir, ContentPaths.Manifest);
        if (File.Exists(manifestPath))
        {
            try
            {
                existing = FromJson(File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch (JsonException)
            {
                existing = null;
            }
        }

        var files = new List<ManifestFile>();
        foreach (var path in ContentFiles.Enumerate(bundleDir))
        {
            files.Add(ContentFiles.Digest(path, File.ReadAllBytes(Path.Combine(bundleDir, path))));
        }

        var id = bundleId ?? existing?.BundleId ?? new DirectoryInfo(Path.GetFullPath(bundleDir)).Name;
        var version = contentVersion ?? existing?.ContentVersion ?? DefaultContentVersion;
        var targetLanguage = language ?? existing?.Language;
        return new ContentManifest(CurrentSchemaVersion, id, version, files, ComputeBundleHash(files, targetLanguage), targetLanguage, supportLanguage ?? existing?.SupportLanguage);
    }

    /// <summary>
    /// Serializes as canonical JSON with one file entry per line (every value still goes through <see cref="CanonicalJson"/>),
    /// ending in a line feed. Equal manifests produce equal text.
    /// </summary>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append("{\"schemaVersion\":").Append(SchemaVersion.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"bundleId\":").Append(CanonicalJson.Serialize(BundleId));
        builder.Append(",\"contentVersion\":").Append(CanonicalJson.Serialize(ContentVersion));
        if (Language is not null)
        {
            builder.Append(",\"language\":").Append(CanonicalJson.Serialize(Language));
        }

        if (SupportLanguage is not null)
        {
            builder.Append(",\"supportLanguage\":").Append(CanonicalJson.Serialize(SupportLanguage));
        }

        builder.Append(",\"files\":[");
        for (var i = 0; i < Files.Count; i++)
        {
            builder.Append(i == 0 ? "\n" : ",\n").Append(CanonicalJson.Serialize(Files[i]));
        }

        builder.Append(Files.Count == 0 ? "]" : "\n]");
        builder.Append(",\"bundleHash\":").Append(CanonicalJson.Serialize(BundleHash));
        builder.Append("}\n");
        return builder.ToString();
    }

    /// <summary>Structural equality including the file list.</summary>
    public bool Equals(ContentManifest? other)
    {
        return other is not null
            && SchemaVersion == other.SchemaVersion
            && string.Equals(BundleId, other.BundleId, StringComparison.Ordinal)
            && string.Equals(ContentVersion, other.ContentVersion, StringComparison.Ordinal)
            && string.Equals(BundleHash, other.BundleHash, StringComparison.Ordinal)
            && string.Equals(Language, other.Language, StringComparison.Ordinal)
            && string.Equals(SupportLanguage, other.SupportLanguage, StringComparison.Ordinal)
            && Files.SequenceEqual(other.Files);
    }

    /// <summary>Hash consistent with <see cref="Equals(ContentManifest)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(SchemaVersion);
        hash.Add(BundleId, StringComparer.Ordinal);
        hash.Add(ContentVersion, StringComparer.Ordinal);
        hash.Add(BundleHash, StringComparer.Ordinal);
        hash.Add(Language, StringComparer.Ordinal);
        hash.Add(SupportLanguage, StringComparer.Ordinal);
        foreach (var file in Files)
        {
            hash.Add(file);
        }

        return hash.ToHashCode();
    }
}

/// <summary>File discovery and hashing shared by the loader and the manifest builder.</summary>
internal static class ContentFiles
{
    private static readonly string[] TextExtensions = [".json", ".jsonl", ".md", ".txt"];

    public static bool IsText(string path)
    {
        foreach (var extension in TextExtensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the relative paths ('/' separators, ordinal order) of every file under the directory except the manifest and dot-files.</summary>
    public static List<string> Enumerate(string bundleDir)
    {
        var root = Path.GetFullPath(bundleDir);
        var paths = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (string.Equals(relative, ContentPaths.Manifest, StringComparison.Ordinal)
                || relative.Split('/').Any(static segment => segment.StartsWith('.')))
            {
                continue;
            }

            paths.Add(relative);
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    /// <summary>Computes the manifest entry of a file from its raw bytes.</summary>
    public static ManifestFile Digest(string path, byte[] data)
    {
        if (IsText(path) && DecodeText(data) is { } text)
        {
            // A file that is already canonical (no byte order mark, no carriage return, composed Unicode) is its own canonical form: hash its bytes
            // without encoding a multi-megabyte copy of the text.
            var alreadyCanonical = !HasByteOrderMark(data) && data.AsSpan().IndexOf((byte)'\r') < 0 && text.IsNormalized(NormalizationForm.FormC);
            var canonical = alreadyCanonical ? data : Encoding.UTF8.GetBytes(text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal));
            return new ManifestFile(path, DeterministicHash.Sha256Hex(canonical), canonical.Length);
        }

        return new ManifestFile(path, DeterministicHash.Sha256Hex(data), data.Length);
    }

    private static bool HasByteOrderMark(byte[] data)
    {
        return data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;
    }

    /// <summary>Decodes UTF-8 (skipping a byte order mark); returns null when the bytes are not valid UTF-8.</summary>
    public static string? DecodeText(byte[] data)
    {
        var start = HasByteOrderMark(data) ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(data, start, data.Length - start);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
