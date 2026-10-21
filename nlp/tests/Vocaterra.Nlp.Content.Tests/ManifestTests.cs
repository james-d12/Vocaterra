using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for the manifest, the per-file hashes and the bundle hash.</summary>
public class ManifestTests
{
    /// <summary>
    /// Self-recorded golden value of the committed bundle. When bundle content changes on purpose, run the ContentBuilder
    /// <c>hash</c> command on <c>content/v1</c> and copy the new bundle hash here.
    /// </summary>
    private const string GoldenBundleHash = "1c2240c78d8a811afd4da12076b41055bab5a35bdc7d24b8d4b07fb16c9d38f2";

    [Fact]
    public void TheCommittedBundleHashMatchesTheGoldenValue()
    {
        Assert.Equal(GoldenBundleHash, TestSupport.Bundle.ContentHash);
    }

    [Fact]
    public void TheBundleHashRecomputedFromTheFilesEqualsTheManifest()
    {
        var recomputed = ContentManifest.Compute(TestSupport.BundleDir);

        Assert.Equal(TestSupport.Bundle.Manifest, recomputed);
        Assert.Equal(GoldenBundleHash, recomputed.BundleHash);
    }

    [Fact]
    public void TheBundleHashIsTheCombinedHashOfPathsAndFileHashesInOrdinalOrder()
    {
        var manifest = TestSupport.Bundle.Manifest;
        var parts = manifest.Files.SelectMany(file => new[] { file.Path, file.Sha256 }).ToArray();

        Assert.Equal(DeterministicHash.Combine(parts), manifest.BundleHash);
        Assert.Equal(manifest.BundleHash, ContentManifest.ComputeBundleHash(manifest.Files.Reverse()));
        Assert.Equal(manifest.Files.Select(file => file.Path).Order(StringComparer.Ordinal), manifest.Files.Select(file => file.Path));
    }

    [Fact]
    public void TheManifestListsEveryFileButItself()
    {
        var manifest = TestSupport.Bundle.Manifest;

        Assert.DoesNotContain(manifest.Files, file => file.Path == ContentPaths.Manifest);
        Assert.Contains(manifest.Files, file => file.Path == "lexicon/seed.json");
        Assert.Contains(manifest.Files, file => file.Path == "provenance.json");
        Assert.Equal("1.0.0", manifest.ContentVersion);
    }

    [Fact]
    public void TextFilesAreHashedLikeDeterministicHashOfText()
    {
        var path = Path.Combine(TestSupport.BundleDir, "intents", "intents.json");
        var entry = TestSupport.Bundle.Manifest.Files.Single(file => file.Path == "intents/intents.json");
        var text = File.ReadAllText(path, Encoding.UTF8);

        Assert.Equal(DeterministicHash.OfText(text), entry.Sha256);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.Replace("\r\n", "\n", StringComparison.Ordinal)), entry.Bytes);
    }

    [Fact]
    public void LineEndingsAndDecompositionDoNotChangeATextFileHash()
    {
        var composed = ContentFiles.Digest("a.json", Encoding.UTF8.GetBytes("{\"x\":\"día\"}\n"));
        var crlf = ContentFiles.Digest("a.json", Encoding.UTF8.GetBytes("{\"x\":\"día\"}\r\n"));
        var decomposed = ContentFiles.Digest("a.json", Encoding.UTF8.GetBytes("{\"x\":\"día\"}\n"));
        var bom = ContentFiles.Digest("a.json", [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. Encoding.UTF8.GetBytes("{\"x\":\"día\"}\n")]);

        Assert.Equal(composed, crlf);
        Assert.Equal(composed, decomposed);
        Assert.Equal(composed, bom);
    }

    [Fact]
    public void BinaryFilesAreHashedAsRawBytes()
    {
        byte[] data = [1, 2, 3, 13, 10, 4];

        var entry = ContentFiles.Digest("audio/line.wav", data);

        Assert.Equal(DeterministicHash.Sha256Hex(data), entry.Sha256);
        Assert.Equal(6, entry.Bytes);
    }

    [Fact]
    public void ATextFileThatIsNotUtf8IsHashedAsRawBytes()
    {
        byte[] data = [0x7B, 0xFF, 0x7D];

        Assert.Equal(DeterministicHash.Sha256Hex(data), ContentFiles.Digest("bad.json", data).Sha256);
    }

    [Fact]
    public void ComputeIsDeterministicAndKeepsTheExistingIdentity()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Edit("manifest.json", "\"contentVersion\":\"1.0.0\"", "\"contentVersion\":\"1.4.2\"");

        var first = ContentManifest.Compute(bundle.Directory);
        var second = ContentManifest.Compute(bundle.Directory);

        Assert.Equal(first, second);
        Assert.Equal("1.4.2", first.ContentVersion);
        Assert.Equal("v1", first.BundleId);
        Assert.Equal(GoldenBundleHash, first.BundleHash);
    }

    [Fact]
    public void ComputeUsesTheArgumentsOrTheDirectoryNameWhenThereIsNoManifest()
    {
        using var bundle = new TempBundle();
        bundle.Delete("manifest.json");

        var byDefault = ContentManifest.Compute(bundle.Directory);
        var explicitIdentity = ContentManifest.Compute(bundle.Directory, "spanish", "2.0.0");

        Assert.Equal(new DirectoryInfo(bundle.Directory).Name, byDefault.BundleId);
        Assert.Equal(ContentManifest.DefaultContentVersion, byDefault.ContentVersion);
        Assert.Equal("spanish", explicitIdentity.BundleId);
        Assert.Equal("2.0.0", explicitIdentity.ContentVersion);
        Assert.Equal(byDefault.BundleHash, explicitIdentity.BundleHash);
    }

    [Fact]
    public void ComputeFailsForAMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() => ContentManifest.Compute(Path.Combine(Path.GetTempPath(), "vocaterra-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void ChangingOneByteChangesTheBundleHash()
    {
        using var bundle = new TempBundle();
        bundle.Edit("effects/effects.json", "\"value\": 1.3", "\"value\": 1.4");

        var changed = bundle.Rehash();

        Assert.NotEqual(GoldenBundleHash, changed.BundleHash);
        Assert.Equal(changed.BundleHash, ContentLoader.Load(bundle.Directory).ContentHash);
    }

    [Fact]
    public void TheManifestSerializesOneFilePerLineAndRoundTrips()
    {
        var manifest = TestSupport.Bundle.Manifest;

        var json = manifest.ToJson();

        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        Assert.Equal(manifest.Files.Count + 2, json.Split('\n').Length - 1);
        Assert.Equal(manifest, ContentManifest.FromJson(json));
        Assert.Equal(json, ContentManifest.FromJson(json).ToJson());
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["schemaVersion", "bundleId", "contentVersion", "files", "bundleHash"], document.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void AnEmptyManifestSerializes()
    {
        var empty = new ContentManifest(1, "e", "0.0.1", [], ContentManifest.ComputeBundleHash([]));

        Assert.Equal(empty, ContentManifest.FromJson(empty.ToJson()));
    }

    [Fact]
    public void TheCommittedManifestFileIsExactlyWhatToJsonWrites()
    {
        var onDisk = File.ReadAllText(Path.Combine(TestSupport.BundleDir, ContentPaths.Manifest), Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(TestSupport.Bundle.Manifest.ToJson(), onDisk);
    }

    [Fact]
    public void ProvenanceRoundTrips()
    {
        var provenance = TestSupport.Bundle.Provenance;

        Assert.Equal(provenance, ContentProvenance.FromJson(provenance.ToJson()));
        Assert.Equal(provenance.GetHashCode(), ContentProvenance.FromJson(provenance.ToJson()).GetHashCode());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"items\":[null]}")]
    [InlineData("{\"items\":[{\"generatedBy\":\"hand-authored\"}]}")]
    public void MalformedProvenanceIsRejected(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ContentProvenance.FromJson(json));
    }
}
