using System.Text;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Locations and helpers shared by the content tests.</summary>
internal static class TestSupport
{
    /// <summary>The committed bundle, copied next to the test assembly by the project file.</summary>
    public static string BundleDir { get; } = Path.Combine(AppContext.BaseDirectory, "content", "v1");

    /// <summary>The hand-written importer fixture.</summary>
    public static string KaikkiFixture { get; } = Path.Combine(AppContext.BaseDirectory, "Data", "kaikki-sample.jsonl");

    /// <summary>The model manifest, copied next to the test assembly by the project file.</summary>
    public static string ModelManifest { get; } = Path.Combine(AppContext.BaseDirectory, "models", "manifest.json");

    /// <summary>The sample bundle, loaded once with hash verification.</summary>
    public static ContentBundle Bundle { get; } = ContentLoader.Load(BundleDir, new ContentLoadOptions { VerifyHashes = true });

    /// <summary>
    /// Writes a <c>sources.json</c> for the given files into the directory, with their real size and SHA-256 (so the importer's check against the file
    /// passes) and a made-up URL and date, and returns its path.
    /// </summary>
    public static string WriteSources(string directory, params (string Id, string Path)[] files)
    {
        System.IO.Directory.CreateDirectory(directory);
        var sources = files.Select(file => new
        {
            id = file.Id,
            file = Path.GetFileName(file.Path),
            url = "https://example.org/data/" + Path.GetFileName(file.Path),
            sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file.Path))),
            bytes = new FileInfo(file.Path).Length,
            retrievedOn = "2026-10-20",
            license = "CC-BY-SA-4.0 (test data)",
        }).ToArray();
        var path = Path.Combine(directory, "sources.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, sources }), new UTF8Encoding(false));
        return path;
    }

    public static Verdict MakeVerdict(string intent, bool communicated = true, double correctness = 1.0, SlotMap? slots = null)
    {
        return new Verdict(
            new IntentId(intent),
            slots ?? SlotMap.Empty,
            communicated,
            correctness,
            [],
            [],
            1.0,
            ResolvedBy.Grammar,
            VersionStamp.Empty);
    }

    public static SlotMap Slots(params (string Key, string Value)[] pairs)
    {
        return SlotMap.From(pairs.Select(static pair => new KeyValuePair<string, string>(pair.Key, pair.Value)));
    }
}

/// <summary>
/// A writable copy of the sample bundle in a temporary directory; deleted on dispose. By default it is the light copy: without the imported lexicon and its
/// sidecar (and their provenance items), so that a test which damages some other file loads a bundle of a few thousand lexicon entries in about ten
/// milliseconds instead of parsing the 40 000 imported entries each time. A test about the imported lexicon asks for the full copy.
/// </summary>
internal sealed class TempBundle : IDisposable
{
    private static readonly string[] ImportedFiles = ["lexicon/wiktionary-es.json", "lexicon/wiktionary-es.source.json"];

    public TempBundle(bool full = false)
    {
        Directory = Path.Combine(Path.GetTempPath(), "vocaterra-content-tests-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(TestSupport.BundleDir, Directory, full ? [] : ImportedFiles);
        if (!full)
        {
            var provenance = PathOf("provenance.json");
            var kept = File.ReadAllLines(provenance).Where(line => !ImportedFiles.Any(file => line.Contains("\"" + file + "\"", StringComparison.Ordinal)));
            File.WriteAllText(provenance, string.Join('\n', kept) + "\n", new UTF8Encoding(false));
            Rehash();
        }
    }

    public string Directory { get; }

    public string PathOf(string relative)
    {
        return Path.Combine(Directory, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    public string Read(string relative)
    {
        return File.ReadAllText(PathOf(relative), Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public void Write(string relative, string text)
    {
        var path = PathOf(relative);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    public void Delete(string relative)
    {
        File.Delete(PathOf(relative));
    }

    /// <summary>Replaces the one occurrence of <paramref name="oldText"/> (line endings compared as LF).</summary>
    public void Edit(string relative, string oldText, string newText)
    {
        var text = Read(relative);
        var first = text.IndexOf(oldText, StringComparison.Ordinal);
        Assert.True(first >= 0, $"'{oldText}' not found in {relative}");
        Assert.True(text.IndexOf(oldText, first + 1, StringComparison.Ordinal) < 0, $"'{oldText}' occurs more than once in {relative}");
        Write(relative, text.Remove(first, oldText.Length).Insert(first, newText));
    }

    /// <summary>Rewrites manifest.json from the files on disk, as the hash command does.</summary>
    public ContentManifest Rehash()
    {
        var manifest = ContentManifest.Compute(Directory);
        File.WriteAllText(PathOf(ContentPaths.Manifest), manifest.ToJson(), new UTF8Encoding(false));
        return manifest;
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory must not fail a test.
        }
    }

    private static void CopyDirectory(string source, string target, string[] skip, string relative = "")
    {
        System.IO.Directory.CreateDirectory(target);
        foreach (var file in System.IO.Directory.EnumerateFiles(source))
        {
            if (!skip.Contains(relative + Path.GetFileName(file), StringComparer.Ordinal))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
        }

        foreach (var directory in System.IO.Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            CopyDirectory(directory, Path.Combine(target, name), skip, relative + name + "/");
        }
    }
}

/// <summary>An empty temporary directory; deleted on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vocaterra-content-tests-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Directory { get; }

    public string PathOf(string name)
    {
        return Path.Combine(Directory, name);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory must not fail a test.
        }
    }
}
