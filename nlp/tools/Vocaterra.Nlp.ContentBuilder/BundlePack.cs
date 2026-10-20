using System.IO.Compression;
using System.Text;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// Packs a content bundle as a zip archive and verifies such an archive. The archive holds every file of the bundle directory, the manifest
/// included; dot-files are left out, as the bundle hash leaves them out. The sidecar <c>&lt;archive&gt;.sha256</c> holds the bundle hash on one line,
/// the value <c>validate</c> and <c>hash</c> report. A host ships the extracted folder: the loader reads a directory, not an archive.
/// </summary>
public static class BundlePack
{
    /// <summary>The suffix of the sidecar file that holds the bundle hash.</summary>
    public const string SidecarSuffix = ".sha256";

    // Every entry carries this time, so the same bundle always gives the same archive bytes.
    private static readonly DateTimeOffset FixedEntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Validates the bundle (every hash and cross-reference, as <see cref="ContentLoader.Load(string, ContentLoadOptions?)"/> checks them) and writes
    /// the archive and its sidecar. An invalid bundle throws and writes nothing.
    /// </summary>
    /// <returns>The bundle hash and the number of files in the archive.</returns>
    public static (string BundleHash, int Files) Pack(string bundleDir, string archivePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(bundleDir);
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        var bundle = ContentLoader.Load(bundleDir);
        var entries = BundleEntries(bundleDir);
        var root = Path.GetFullPath(bundleDir);

        using (var output = File.Create(archivePath))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            foreach (var relative in entries)
            {
                var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedEntryTime;
                using var source = File.OpenRead(Path.Combine(root, relative));
                using var target = entry.Open();
                source.CopyTo(target);
            }
        }

        File.WriteAllText(archivePath + SidecarSuffix, bundle.ContentHash + "\n", new UTF8Encoding(false));
        return (bundle.ContentHash, entries.Count);
    }

    /// <summary>
    /// Extracts the archive to a new temporary folder, validates the bundle there and compares its hash with the sidecar. The folder is deleted
    /// afterwards. Throws <see cref="BundlePackException"/> for an archive problem and <see cref="ContentValidationException"/> for a bundle problem.
    /// </summary>
    /// <returns>The bundle hash and the number of entries in the archive.</returns>
    public static (string BundleHash, int Files) Verify(string archivePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        if (!File.Exists(archivePath))
        {
            throw new BundlePackException($"Archive not found: {archivePath}");
        }

        var sidecar = archivePath + SidecarSuffix;
        if (!File.Exists(sidecar))
        {
            throw new BundlePackException($"Sidecar not found: {sidecar}");
        }

        var expected = File.ReadAllText(sidecar, Encoding.UTF8).Trim();
        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            int files;
            using (var archive = OpenArchive(archivePath))
            {
                files = archive.Entries.Count;
                Extract(archive, folder);
            }

            var bundle = ContentLoader.Load(folder);
            if (!string.Equals(bundle.ContentHash, expected, StringComparison.Ordinal))
            {
                throw new BundlePackException($"The sidecar names bundle hash {expected}, but the archive's bundle hashes to {bundle.ContentHash}.");
            }

            return (bundle.ContentHash, files);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The bundle's files: every file under the directory whose path has no dot-segment, relative with '/' separators in ordinal order.
    private static List<string> BundleEntries(string bundleDir)
    {
        var root = Path.GetFullPath(bundleDir);
        var entries = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Split('/').Any(static segment => segment.StartsWith('.')))
            {
                continue;
            }

            entries.Add(relative);
        }

        entries.Sort(StringComparer.Ordinal);
        return entries;
    }

    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException)
        {
            throw new BundlePackException($"Not a zip archive: {archivePath}");
        }
    }

    // Writes every entry under the folder. An entry whose name would leave the folder (an absolute path, a parent segment, a backslash) or that
    // names a file twice is refused before anything is written for it.
    private static void Extract(ZipArchive archive, string folder)
    {
        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (name.Length == 0 || name.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(name))
            {
                throw new BundlePackException($"Unsafe entry name '{name}'.");
            }

            var target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(root, StringComparison.Ordinal))
            {
                throw new BundlePackException($"Unsafe entry name '{name}' leaves the bundle folder.");
            }

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            if (File.Exists(target))
            {
                throw new BundlePackException($"Duplicate entry '{name}'.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open();
            using var sink = File.Create(target);
            source.CopyTo(sink);
        }
    }
}

/// <summary>A problem with a packed archive or its sidecar, as opposed to a problem with the bundle inside it.</summary>
public sealed class BundlePackException : Exception
{
    /// <summary>Creates the exception with the message the command prints.</summary>
    public BundlePackException(string message)
        : base(message)
    {
    }
}
