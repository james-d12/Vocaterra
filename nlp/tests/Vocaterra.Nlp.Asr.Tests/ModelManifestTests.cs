using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Tests for <see cref="ModelManifest"/> parsing and the committed <c>models/manifest.json</c>.</summary>
public class ModelManifestTests
{
    private const string Hash1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void CommittedManifestLoadsAndListsTheFiveModels()
    {
        var path = ModelGate.FindManifest();
        Assert.NotNull(path);

        var manifest = ModelManifest.Load(path);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(
            [
                "kokoro-multi-lang-v1_0",
                "multilingual-e5-small",
                "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
                "vosk-model-small-es-0.42",
                "wav2vec2-xlsr-53-espeak-cv-ft",
            ],
            manifest.Models.Select(m => m.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(ModelRoles.AsrConstrained, manifest.Find("vosk-model-small-es-0.42")?.Role);
        Assert.Equal(ModelRoles.AsrOpen, manifest.Find("sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8")?.Role);
        Assert.Equal(ModelRoles.Phoneme, manifest.Find("wav2vec2-xlsr-53-espeak-cv-ft")?.Role);
        Assert.Equal(ModelRoles.Embedding, manifest.Find("multilingual-e5-small")?.Role);
        Assert.Equal(ModelRoles.Tts, manifest.Find("kokoro-multi-lang-v1_0")?.Role);
    }

    [Fact]
    public void CommittedVoskEntryIsFullyPinned()
    {
        var manifest = ModelManifest.Load(ModelGate.FindManifest()!);

        var vosk = manifest.Find("vosk-model-small-es-0.42");

        Assert.NotNull(vosk);
        Assert.True(vosk.IsPinned);
        Assert.Equal("Apache-2.0", vosk.License);
        Assert.Equal("https://alphacephei.com/vosk/models/vosk-model-small-es-0.42.zip", vosk.Source);
        Assert.Equal("zip", vosk.Archive?.Format);
        Assert.NotNull(vosk.Archive?.Sha256);
        Assert.All(vosk.Files, f => Assert.True(f.Bytes > 0));
    }

    [Fact]
    public void CommittedEntriesWithoutPinnedFilesAreMarkedWithNullHashesAndNotes()
    {
        var manifest = ModelManifest.Load(ModelGate.FindManifest()!);

        foreach (var id in new[] { "wav2vec2-xlsr-53-espeak-cv-ft", "multilingual-e5-small" })
        {
            var entry = manifest.Find(id);
            Assert.NotNull(entry);
            Assert.Null(entry.Archive);
            Assert.False(entry.IsPinned);
            Assert.All(entry.Files, f => Assert.Null(f.Sha256));
            Assert.Contains("TODO", entry.Notes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ParseToleratesIndentationAndUnknownProperties()
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "extra": true,
              "models": [
                {
                  "id": "m1", "role": "tts", "name": "n", "version": "1", "license": "MIT", "source": "https://example.test/m1.zip",
                  "archive": null,
                  "files": [ { "path": "a/b.bin", "sha256": "{{Hash1}}", "bytes": 3 }, { "path": "c.txt", "sha256": null, "bytes": null } ],
                  "notes": null
                }
              ]
            }
            """;

        var manifest = ModelManifest.Parse(json);

        var entry = Assert.Single(manifest.Models);
        Assert.Equal("m1", entry.Id);
        Assert.Equal(2, entry.Files.Count);
        Assert.Equal(new ModelFile("c.txt", null, null), entry.Files[1]);
        Assert.False(entry.IsPinned);
    }

    [Fact]
    public void ToJsonThenParseRoundTrips()
    {
        var manifest = ModelManifest.Load(ModelGate.FindManifest()!);

        var again = ModelManifest.Parse(manifest.ToJson());

        Assert.Equal(manifest, again);
        Assert.Equal(manifest.ToJson(), again.ToJson());
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"models\":[]}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void ParseRejectsMalformedManifests(string json)
    {
        Assert.Throws<InvalidDataException>(() => ModelManifest.Parse(json));
    }

    [Theory]
    [InlineData("bad-role", "m1", "a.bin", Hash1)]
    [InlineData("tts", "../m1", "a.bin", Hash1)]
    [InlineData("tts", "m1", "../a.bin", Hash1)]
    [InlineData("tts", "m1", "/etc/passwd", Hash1)]
    [InlineData("tts", "m1", "a\\b.bin", Hash1)]
    [InlineData("tts", "m1", "a.bin", "XYZ")]
    [InlineData("tts", "", "a.bin", Hash1)]
    public void ParseRejectsInvalidEntries(string role, string id, string path, string sha256)
    {
        var json = $$"""
            {"schemaVersion":1,"models":[{"id":"{{id}}","role":"{{role}}","name":"n","version":"1","license":"MIT","source":"s",
            "archive":null,"files":[{"path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}","sha256":"{{sha256}}","bytes":1}],"notes":null}]}
            """;

        Assert.Throws<InvalidDataException>(() => ModelManifest.Parse(json));
    }

    [Fact]
    public void ParseRejectsDuplicateIds()
    {
        const string entry = "{\"id\":\"m1\",\"role\":\"tts\",\"name\":\"n\",\"version\":\"1\",\"license\":\"MIT\",\"source\":\"s\",\"archive\":null,\"files\":[],\"notes\":null}";

        Assert.Throws<InvalidDataException>(() => ModelManifest.Parse($"{{\"schemaVersion\":1,\"models\":[{entry},{entry}]}}"));
    }
}
