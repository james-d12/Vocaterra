namespace Vocaterra.Nlp.Response.Tests.Speech;

using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Response.Speech;

public sealed class CachedSynthesizerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vocaterra-cache-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void SecondIdenticalRequestIsAHitAndDoesNotCallTheInnerSynthesizerAgain()
    {
        var inner = new SilentSynthesizer();
        var cached = new CachedSynthesizer(inner, new InMemorySynthesisCache());
        var request = new SynthesisRequest("¿Tienes dos espadas?", "agent-1", null);

        var first = cached.Synthesize(request);
        var second = cached.Synthesize(request);

        Assert.Equal(1, inner.CallCount);
        Assert.Same(first, second);
        Assert.Equal(1, cached.Misses);
        Assert.Equal(1, cached.Hits);
    }

    [Fact]
    public void DifferentTextOrVoiceIsAMiss()
    {
        var inner = new SilentSynthesizer();
        var cached = new CachedSynthesizer(inner, new InMemorySynthesisCache());
        cached.Synthesize(new SynthesisRequest("hola", "a", null));
        cached.Synthesize(new SynthesisRequest("adiós", "a", null));
        cached.Synthesize(new SynthesisRequest("hola", "b", null));
        cached.Synthesize(new SynthesisRequest("hola", "a", ["o", "l", "a"]));
        Assert.Equal(4, inner.CallCount);
        Assert.Equal(0, cached.Hits);
        Assert.Equal(4, cached.Misses);
        cached.Synthesize(new SynthesisRequest("hola", "a", ["o", "l", "a"]));
        Assert.Equal(4, inner.CallCount);
        Assert.Equal(1, cached.Hits);
    }

    [Fact]
    public void KeyIsTheCombinedHashOfTextVoiceAndModelHash()
    {
        var request = new SynthesisRequest("hola", "voz", null);
        Assert.Equal(DeterministicHash.Combine("hola", "voz", "model-1"), CachedSynthesizer.KeyFor(request, "model-1"));
        Assert.NotEqual(CachedSynthesizer.KeyFor(request, "model-1"), CachedSynthesizer.KeyFor(request, "model-2"));
        Assert.NotEqual(CachedSynthesizer.KeyFor(request, "model-1"), CachedSynthesizer.KeyFor(request with { Phones = ["x"] }, "model-1"));

        var cache = new InMemorySynthesisCache();
        var cached = new CachedSynthesizer(new SilentSynthesizer(), cache);
        cached.Synthesize(request);
        Assert.True(cache.TryGet(DeterministicHash.Combine("hola", "voz", cached.ModelHash), out _));
        Assert.Equal("silent-synthesizer-v1", cached.ModelHash);
    }

    [Fact]
    public void ModelHashChangeInvalidatesEntries()
    {
        var cache = new InMemorySynthesisCache();
        var request = new SynthesisRequest("hola", "voz", null);
        new CachedSynthesizer(new FixedModelSynthesizer("m1"), cache).Synthesize(request);
        var second = new FixedModelSynthesizer("m2");
        new CachedSynthesizer(second, cache).Synthesize(request);
        Assert.Equal(1, second.Calls);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void DirectoryCacheWritesWavFilesNamedByKeyAndServesThemToANewInstance()
    {
        var request = new SynthesisRequest("¿Tienes dos espadas?", "agent-1", null);
        var firstInner = new SilentSynthesizer();
        var produced = new CachedSynthesizer(firstInner, new DirectorySynthesisCache(_directory)).Synthesize(request);

        var key = DeterministicHash.Combine(request.Text, request.VoiceId, firstInner.ModelHash);
        Assert.True(File.Exists(Path.Combine(_directory, key + ".wav")));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));

        var secondInner = new SilentSynthesizer();
        var again = new CachedSynthesizer(secondInner, new DirectorySynthesisCache(_directory)).Synthesize(request);
        Assert.Equal(0, secondInner.CallCount);
        Assert.Equal(produced, again);
    }

    [Fact]
    public void DirectoryCacheTreatsACorruptFileAsAMissAndRepairsIt()
    {
        var cache = new DirectorySynthesisCache(_directory);
        var request = new SynthesisRequest("hola", "voz", null);
        var inner = new SilentSynthesizer();
        var key = CachedSynthesizer.KeyFor(request, inner.ModelHash);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, key + ".wav"), [1, 2, 3]);
        Assert.False(cache.TryGet(key, out _));

        var result = new CachedSynthesizer(inner, cache).Synthesize(request);
        Assert.Equal(1, inner.CallCount);
        Assert.True(cache.TryGet(key, out var repaired));
        Assert.Equal(result, repaired);
    }

    [Fact]
    public void DirectoryCacheRejectsKeysThatAreNotFileNames()
    {
        var cache = new DirectorySynthesisCache(_directory);
        Assert.Throws<ArgumentException>(() => cache.TryGet("../escape", out _));
        Assert.Throws<ArgumentException>(() => cache.Put("a/b", new AudioBuffer([], 16_000)));
        Assert.Throws<ArgumentException>(() => cache.TryGet(string.Empty, out _));
    }

    [Fact]
    public void InMemoryCacheOverwritesAndReportsMisses()
    {
        var cache = new InMemorySynthesisCache();
        Assert.False(cache.TryGet("k", out var none));
        Assert.Null(none);
        var first = new AudioBuffer([1], 16_000);
        var second = new AudioBuffer([2], 16_000);
        cache.Put("k", first);
        cache.Put("k", second);
        Assert.True(cache.TryGet("k", out var found));
        Assert.Same(second, found);
        Assert.Equal(1, cache.Count);
    }

    private sealed class FixedModelSynthesizer(string modelHash) : ISpeechSynthesizer
    {
        public int Calls { get; private set; }

        public string ModelHash => modelHash;

        public AudioBuffer Synthesize(SynthesisRequest request)
        {
            Calls++;
            return new AudioBuffer([1, 2, 3], 16_000);
        }
    }
}
