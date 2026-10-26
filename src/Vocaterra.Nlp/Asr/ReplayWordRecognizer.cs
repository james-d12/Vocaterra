using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Asr;

/// <summary>Thrown by <see cref="ReplayWordRecognizer"/> when a clip hash has no recorded result.</summary>
public sealed class ReplayMissException : Exception
{
    /// <summary>Creates the exception for the given unknown clip hash.</summary>
    public ReplayMissException(string clipHash)
        : base($"No recorded recognition result for clip {clipHash}.")
    {
        ClipHash = clipHash;
    }

    /// <summary>Creates an exception with a message.</summary>
    public ReplayMissException(string message, Exception innerException)
        : base(message, innerException)
    {
        ClipHash = string.Empty;
    }

    /// <summary>Creates an empty exception.</summary>
    public ReplayMissException()
        : base("No recorded recognition result.")
    {
        ClipHash = string.Empty;
    }

    /// <summary>The clip hash that had no recording.</summary>
    public string ClipHash { get; }
}

/// <summary>Returns recorded results keyed by clip hash, regardless of the request; used by the replayer instead of a real model.</summary>
public sealed class ReplayWordRecognizer : IWordRecognizer
{
    private readonly ImmutableSortedDictionary<string, AsrResult> _byClipHash;

    /// <summary>Creates the recognizer over the recorded results.</summary>
    public ReplayWordRecognizer(IReadOnlyDictionary<string, AsrResult> byClipHash)
    {
        ArgumentNullException.ThrowIfNull(byClipHash);
        _byClipHash = byClipHash.ToImmutableSortedDictionary(StringComparer.Ordinal);
        var hashes = _byClipHash.Values.Select(static r => r.ModelHash).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        ModelHash = hashes.Length == 1 ? hashes[0] : DeterministicHash.Combine(hashes);
    }

    /// <summary>The model hash common to all recordings, or the combined hash of the distinct ones.</summary>
    public string ModelHash { get; }

    /// <summary>Number of recorded clips.</summary>
    public int Count => _byClipHash.Count;

    /// <inheritdoc />
    /// <exception cref="ReplayMissException">The clip hash has no recording.</exception>
    public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);
        return _byClipHash.TryGetValue(clip.ClipHash, out var result) ? result : throw new ReplayMissException(clip.ClipHash);
    }
}
