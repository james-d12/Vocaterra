using System.Globalization;
using System.Text;
using System.Text.Json;
using Slg.Core.Determinism;

namespace Slg.Content;

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
/// The bundle hash is <see cref="DeterministicHash.Combine(string[])"/> over <c>path, sha256</c> of every file in ordinal path order.
/// </remarks>
public sealed record ContentManifest(
    int SchemaVersion,
    string BundleId,
    string ContentVersion,
    IReadOnlyList<ManifestFile> Files,
    string BundleHash)
{
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
        ArgumentNullException.ThrowIfNull(files);
        var parts = new List<string>();
        foreach (var file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            parts.Add(file.Path);
            parts.Add(file.Sha256);
        }

        return DeterministicHash.Combine([.. parts]);
    }

    /// <summary>
    /// Builds a manifest from the files on disk (every file under the directory except <c>manifest.json</c> and dot-files).
    /// Identity fields come from an existing manifest when it parses, else from the arguments, else the directory name and <see cref="DefaultContentVersion"/>.
    /// </summary>
    public static ContentManifest Compute(string bundleDir, string? bundleId = null, string? contentVersion = null)
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
        return new ContentManifest(CurrentSchemaVersion, id, version, files, ComputeBundleHash(files));
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
        if (IsText(path) && TryCanonicalText(data, out var canonical))
        {
            return new ManifestFile(path, DeterministicHash.Sha256Hex(canonical), canonical.Length);
        }

        return new ManifestFile(path, DeterministicHash.Sha256Hex(data), data.Length);
    }

    /// <summary>Decodes UTF-8 (skipping a byte order mark); returns null when the bytes are not valid UTF-8.</summary>
    public static string? DecodeText(byte[] data)
    {
        var start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(data, start, data.Length - start);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool TryCanonicalText(byte[] data, out byte[] canonical)
    {
        var text = DecodeText(data);
        if (text is null)
        {
            canonical = [];
            return false;
        }

        canonical = Encoding.UTF8.GetBytes(text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal));
        return true;
    }
}
