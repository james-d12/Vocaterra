using System.Globalization;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Nlp.Response.Tests.Speech;

/// <summary>Tests for <see cref="KokoroSynthesizer.FromStore"/> against a throw-away model directory; no native engine is loaded.</summary>
public sealed class KokoroStoreTests : IDisposable
{
    private const string ModelId = "kokoro-fake";
    private static readonly string[] AllFiles = ["model.onnx", "voices.bin", "tokens.txt", "espeak-ng-data/es_dict"];

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("vocaterra-kokoro-store-");

    public void Dispose()
    {
        _root.Delete(recursive: true);
    }

    [Fact]
    public void FromStoreUsesTheManifestHashAndDoesNotLoadTheEngine()
    {
        var store = CreateStore(AllFiles);

        using var synthesizer = KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28);

        var speed = 1.0f.ToString("R", CultureInfo.InvariantCulture);
        Assert.Equal(DeterministicHash.Combine("kokoro", store.ModelHash(ModelId), "28", speed), synthesizer.ModelHash);
    }

    [Fact]
    public void FromStoreHashChangesWithTheSpeakerAndTheSpeed()
    {
        var store = CreateStore(AllFiles);

        using var female = KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28);
        using var male = KokoroSynthesizer.FromStore(store, ModelId, speakerId: 29);
        using var slow = KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28, speed: 0.9f);

        Assert.NotEqual(female.ModelHash, male.ModelHash);
        Assert.NotEqual(female.ModelHash, slow.ModelHash);
    }

    [Fact]
    public void FromStoreRejectsUnknownIds()
    {
        var store = CreateStore(AllFiles);

        Assert.Throws<KeyNotFoundException>(() => KokoroSynthesizer.FromStore(store, "no-such-model", speakerId: 28));
    }

    [Fact]
    public void FromStoreRejectsAnUnpinnedEntry()
    {
        var store = CreateStore(AllFiles, pinned: false);

        Assert.Throws<InvalidOperationException>(() => KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28));
    }

    [Theory]
    [InlineData("voices.bin")]
    [InlineData("tokens.txt")]
    [InlineData("model.onnx")]
    public void FromStoreRejectsAManifestThatDoesNotPinARequiredFile(string omitted)
    {
        var store = CreateStore(AllFiles.Where(f => f != omitted).ToArray());

        var ex = Assert.Throws<InvalidOperationException>(() => KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28));
        Assert.Contains(omitted, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromStoreRejectsAManifestWithoutTheEspeakDataDirectory()
    {
        var store = CreateStore(["model.onnx", "voices.bin", "tokens.txt"]);

        Assert.Throws<InvalidOperationException>(() => KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28));
    }

    [Fact]
    public void FromStoreRejectsNegativeSpeakersAndBadSpeeds()
    {
        var store = CreateStore(AllFiles);

        Assert.Throws<ArgumentOutOfRangeException>(() => KokoroSynthesizer.FromStore(store, ModelId, speakerId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => KokoroSynthesizer.FromStore(store, ModelId, speakerId: 28, speed: 0f));
    }

    /// <summary>Writes the given files (contents are arbitrary; the adapter checks only their presence) and a manifest pinning them.</summary>
    private ModelStore CreateStore(string[] files, bool pinned = true)
    {
        var modelDir = Path.Combine(_root.FullName, ModelId);
        var entries = new List<ModelFile>();
        foreach (var file in files)
        {
            var path = Path.Combine(modelDir, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fake " + file);
            entries.Add(pinned
                ? new ModelFile(file, ModelStore.Sha256OfFile(path), new FileInfo(path).Length)
                : new ModelFile(file, null, null));
        }

        var entry = new ModelEntry(ModelId, ModelRoles.Tts, "Fake Kokoro", "0", "Apache-2.0", "https://example.invalid/kokoro", null, entries, null);
        return new ModelStore(_root.FullName, new ModelManifest(ModelManifest.CurrentSchemaVersion, [entry]));
    }
}
