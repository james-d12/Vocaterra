using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>
/// Wraps a synthesizer with a cache. The key is <c>DeterministicHash.Combine(text, voiceId, modelHash)</c>, extended with the phones
/// when the request carries them, so a hit is exactly what the inner synthesizer would return. Not safe for concurrent requests of the same key.
/// </summary>
public sealed class CachedSynthesizer : ISpeechSynthesizer
{
    private readonly ISpeechSynthesizer _inner;
    private readonly ISynthesisCache _cache;
    private long _hits;
    private long _misses;

    /// <summary>Creates a caching wrapper around <paramref name="inner"/>.</summary>
    public CachedSynthesizer(ISpeechSynthesizer inner, ISynthesisCache cache)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);
        _inner = inner;
        _cache = cache;
    }

    /// <summary>The model hash of the inner synthesizer.</summary>
    public string ModelHash => _inner.ModelHash;

    /// <summary>Number of requests answered from the cache.</summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>Number of requests that had to call the inner synthesizer.</summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>Builds the cache key of a request for a model hash.</summary>
    public static string KeyFor(SynthesisRequest request, string modelHash)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(modelHash);
        if (request.Phones is null)
        {
            return DeterministicHash.Combine(request.Text, request.VoiceId, modelHash);
        }

        var parts = new List<string>(request.Phones.Count + 4) { request.Text, request.VoiceId, modelHash, "phones" };
        parts.AddRange(request.Phones);
        return DeterministicHash.Combine([.. parts]);
    }

    /// <summary>Returns the cached audio when present, otherwise synthesizes, stores and returns it.</summary>
    public AudioBuffer Synthesize(SynthesisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = KeyFor(request, _inner.ModelHash);
        if (_cache.TryGet(key, out var cached))
        {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        Interlocked.Increment(ref _misses);
        var buffer = _inner.Synthesize(request);
        _cache.Put(key, buffer);
        return buffer;
    }
}
