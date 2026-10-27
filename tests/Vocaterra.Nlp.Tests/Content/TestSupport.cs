using System.Text;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Locations and helpers shared by the content tests.</summary>
internal static class TestSupport
{
    /// <summary>The repository's one content bundle, <c>content/game-v1</c>, found by walking up from the test assembly (not copied to the output).</summary>
    public static string BundleDir { get; } = GameBundleLocator.Directory;

    /// <summary>The hand-written importer fixture.</summary>
    public static string KaikkiFixture { get; } = Path.Combine(AppContext.BaseDirectory, "Data", "kaikki-sample.jsonl");

    /// <summary>The model manifest, copied next to the test assembly by the project file.</summary>
    public static string ModelManifest { get; } = Path.Combine(AppContext.BaseDirectory, "models", "manifest.json");

    /// <summary>The bundle, loaded once with hash verification.</summary>
    public static ContentBundle Bundle { get; } = SpanishContent.Load(BundleDir, new ContentLoadOptions { VerifyHashes = true });

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
