using System.Security.Cryptography;
using Slg.Core.Determinism;

namespace Slg.Asr.Models;

/// <summary>A models root directory (<c>root/&lt;id&gt;/...</c>) together with the manifest that pins its files.</summary>
public sealed class ModelStore
{
    /// <summary>Name of the environment variable holding the models root directory.</summary>
    public const string EnvironmentVariable = "SLG_MODELS_DIR";

    /// <summary>File name of the manifest inside the models root directory.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>Creates a store whose manifest is read from <c>rootDir/manifest.json</c>.</summary>
    /// <exception cref="FileNotFoundException">The manifest file does not exist.</exception>
    public ModelStore(string rootDir)
        : this(rootDir, ModelManifest.Load(Path.Combine(rootDir ?? throw new ArgumentNullException(nameof(rootDir)), ManifestFileName)))
    {
    }

    /// <summary>Creates a store over an explicit manifest.</summary>
    public ModelStore(string rootDir, ModelManifest manifest)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDir);
        ArgumentNullException.ThrowIfNull(manifest);
        RootDirectory = rootDir;
        Manifest = manifest;
    }

    /// <summary>The models root directory.</summary>
    public string RootDirectory { get; }

    /// <summary>The manifest this store verifies against.</summary>
    public ModelManifest Manifest { get; }

    /// <summary>
    /// Creates a store from the <c>SLG_MODELS_DIR</c> environment variable, or returns null when it is unset or blank.
    /// The manifest is read from <c>manifest.json</c> in that directory; when there is none the store has an empty manifest.
    /// </summary>
    public static ModelStore? FromEnvironment()
    {
        return FromEnvironment(Environment.GetEnvironmentVariable);
    }

    /// <summary>Creates a store from the given variable lookup; null when <c>SLG_MODELS_DIR</c> is unset or blank.</summary>
    public static ModelStore? FromEnvironment(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        var root = getVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var manifestPath = Path.Combine(root, ManifestFileName);
        return new ModelStore(root, File.Exists(manifestPath) ? ModelManifest.Load(manifestPath) : ModelManifest.Empty);
    }

    /// <summary>Returns <c>root/&lt;id&gt;</c>; the directory need not exist.</summary>
    public string ModelDirectory(string id)
    {
        if (!IsSafeName(id))
        {
            throw new ArgumentException("A model id must be a single plain directory name.", nameof(id));
        }

        return Path.Combine(RootDirectory, id);
    }

    /// <summary>Returns the absolute location of a manifest-relative file of the given model.</summary>
    public string ResolvePath(string id, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            throw new ArgumentException("The path must be relative and must not contain '..' segments.", nameof(relativePath));
        }

        return Path.Combine(ModelDirectory(id), relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Recomputes the SHA-256 of every manifest file of the model and reports the outcome. Never throws for absent or unreadable
    /// files or unknown ids; those yield <see cref="ModelStatus.Missing"/>.
    /// </summary>
    public ModelVerification Verify(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = Manifest.Find(id);
        if (entry is null || !IsSafeName(id))
        {
            return new ModelVerification(id, ModelStatus.Missing, []);
        }

        var files = new List<FileVerification>(entry.Files.Count);
        foreach (var file in entry.Files.OrderBy(static f => f.Path, StringComparer.Ordinal))
        {
            files.Add(VerifyFile(id, file));
        }

        return new ModelVerification(id, Summarize(files), files);
    }

    /// <summary>
    /// Returns <see cref="DeterministicHash.Combine"/> of the ordinal-sorted manifest file hashes of the model.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The id is not in the manifest.</exception>
    /// <exception cref="InvalidOperationException">The model has no files or a file whose hash is not pinned yet.</exception>
    public string ModelHash(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = Manifest.Find(id) ?? throw new KeyNotFoundException($"Model '{id}' is not in the manifest.");
        if (!entry.IsPinned)
        {
            throw new InvalidOperationException($"Model '{id}' has files without a pinned SHA-256; run scripts/fetch-models.sh and pin them first.");
        }

        return CombineSorted(entry.Files.Select(static f => f.Sha256!));
    }

    /// <summary>Hashes every file below a directory the same way <see cref="ModelHash"/> does for a manifest entry.</summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public static string HashDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Model directory '{directory}' does not exist.");
        }

        return HashFiles(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));
    }

    /// <summary>Hashes the given files: SHA-256 of each, ordinal-sorted, then <see cref="DeterministicHash.Combine"/>.</summary>
    public static string HashFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return CombineSorted(paths.Select(Sha256OfFile));
    }

    /// <summary>Returns the lowercase hex SHA-256 of a file, read as a stream.</summary>
    public static string Sha256OfFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal static bool IsSafeName(string? name)
    {
        return !string.IsNullOrEmpty(name)
            && !string.Equals(name, ".", StringComparison.Ordinal)
            && !string.Equals(name, "..", StringComparison.Ordinal)
            && name.IndexOfAny(['/', '\\', ':', '\0']) < 0;
    }

    internal static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\', StringComparison.Ordinal) || path.Contains('\0', StringComparison.Ordinal)
            || path[0] == '/' || path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsSha256Hex(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private static string CombineSorted(IEnumerable<string> hashes)
    {
        var sorted = hashes.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);
        return DeterministicHash.Combine(sorted);
    }

    private static ModelStatus Summarize(List<FileVerification> files)
    {
        if (files.Count == 0)
        {
            return ModelStatus.Unpinned;
        }

        if (files.Any(static f => f.Status == FileStatus.Corrupt))
        {
            return ModelStatus.Corrupt;
        }

        if (files.Any(static f => f.Status == FileStatus.Missing))
        {
            return ModelStatus.Missing;
        }

        return files.Any(static f => f.Status == FileStatus.Unpinned) ? ModelStatus.Unpinned : ModelStatus.Ok;
    }

    private FileVerification VerifyFile(string id, ModelFile file)
    {
        var fullPath = ResolvePath(id, file.Path);
        try
        {
            if (!File.Exists(fullPath))
            {
                return new FileVerification(file.Path, FileStatus.Missing, file.Sha256, null);
            }

            if (file.Sha256 is null)
            {
                return new FileVerification(file.Path, FileStatus.Unpinned, null, null);
            }

            if (file.Bytes is { } expectedBytes && new FileInfo(fullPath).Length != expectedBytes)
            {
                return new FileVerification(file.Path, FileStatus.Corrupt, file.Sha256, null);
            }

            var actual = Sha256OfFile(fullPath);
            var status = string.Equals(actual, file.Sha256, StringComparison.Ordinal) ? FileStatus.Ok : FileStatus.Corrupt;
            return new FileVerification(file.Path, status, file.Sha256, actual);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileVerification(file.Path, FileStatus.Missing, file.Sha256, null);
        }
    }
}
