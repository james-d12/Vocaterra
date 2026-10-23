using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.Testing;

/// <summary>
/// A writable copy of the repository's content bundle (<c>content/game-v1</c>) in a temporary directory; deleted on dispose. The copy never has
/// <c>speech/</c> (433 clips, 33 MB). By default it is the light copy: without the imported lexicon and its sidecar (and their provenance items), so
/// that a test which damages some other file loads a bundle of a few thousand lexicon entries in milliseconds instead of parsing the 40 000 imported
/// entries each time. A test about the imported lexicon asks for the full copy. Linked into every core test project that edits the bundle; one source
/// file, so the copy rules live in one place.
/// </summary>
internal sealed class TempBundle : IDisposable
{
    private const string SpeechFolder = "speech/";

    private static readonly string[] ImportedFiles = ["lexicon/wiktionary-es.json", "lexicon/wiktionary-es.source.json"];

    public TempBundle(bool full = false)
    {
        Directory = Path.Combine(Path.GetTempPath(), "vocaterra-content-tests-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(GameBundleLocator.Directory, Directory, full ? [] : ImportedFiles);
        var provenance = PathOf("provenance.json");
        var kept = File.ReadAllLines(provenance).Where(line =>
            !line.Contains("\"" + SpeechFolder, StringComparison.Ordinal)
            && (full || !ImportedFiles.Any(file => line.Contains("\"" + file + "\"", StringComparison.Ordinal)))).ToList();

        // The speech items are the last ones of the list, so the last kept item may now end in a comma.
        var last = kept.FindLastIndex(line => line.TrimStart().StartsWith("{ \"path\"", StringComparison.Ordinal));
        kept[last] = kept[last].TrimEnd().TrimEnd(',');
        File.WriteAllText(provenance, string.Join('\n', kept) + "\n", new UTF8Encoding(false));
        Rehash();
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

    /// <summary>
    /// Edits a JSON file of the copy through its parsed tree, so the edit does not depend on the text layout of the file (and breaks only when the
    /// structure it names changes). The file is written back indented; run <see cref="Rehash"/> afterwards if the load verifies hashes.
    /// </summary>
    public void EditJson(string relative, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(Read(relative))!;
        edit(node);
        Write(relative, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>Appends a newline to the file: changes its bytes (so its hash) and nothing else.</summary>
    public void Touch(string relative)
    {
        Write(relative, Read(relative) + "\n");
    }

    /// <summary>Rewrites manifest.json from the files on disk, as the hash command does.</summary>
    public ContentManifest Rehash()
    {
        var manifest = ContentManifest.Compute(Directory);
        File.WriteAllText(PathOf(ContentPaths.Manifest), manifest.ToJson(), new UTF8Encoding(false));
        return manifest;
    }

    /// <summary>Loads the copy. By default the hashes are not verified, because the copy is usually edited and not rehashed.</summary>
    public ContentBundle Load(bool verifyHashes = false)
    {
        return ContentLoader.Load(Directory, new ContentLoadOptions { VerifyHashes = verifyHashes });
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
            if ((relative + name + "/") == SpeechFolder)
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(target, name), skip, relative + name + "/");
        }
    }
}
