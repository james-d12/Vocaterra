using System.Collections.Immutable;
using Slg.Core.Audio;
using Slg.Core.Stages;

namespace Slg.Asr;

/// <summary>Test double that returns configured results: a fixed one for any clip, or one picked by clip hash.</summary>
public sealed class ScriptedWordRecognizer : IWordRecognizer
{
    private readonly AsrResult? _default;
    private readonly ImmutableSortedDictionary<string, AsrResult> _byClipHash;
    private readonly object _gate = new();
    private int _callCount;
    private RecognitionRequest? _lastRequest;

    /// <summary>Returns <paramref name="result"/> for every clip.</summary>
    public ScriptedWordRecognizer(AsrResult result)
        : this(result, ImmutableSortedDictionary<string, AsrResult>.Empty.WithComparers(StringComparer.Ordinal))
    {
    }

    /// <summary>Returns the result mapped to the clip's hash, else <paramref name="fallback"/> (when null, unmapped clips throw).</summary>
    public ScriptedWordRecognizer(AsrResult? fallback, IReadOnlyDictionary<string, AsrResult> byClipHash)
    {
        ArgumentNullException.ThrowIfNull(byClipHash);
        _default = fallback;
        _byClipHash = byClipHash.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }

    /// <summary>The model hash of the fallback result, or <c>scripted</c> when there is none.</summary>
    public string ModelHash => _default?.ModelHash ?? "scripted";

    /// <summary>How many times <see cref="Recognize"/> was called.</summary>
    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _callCount;
            }
        }
    }

    /// <summary>The request of the most recent call, or null before the first call.</summary>
    public RecognitionRequest? LastRequest
    {
        get
        {
            lock (_gate)
            {
                return _lastRequest;
            }
        }
    }

    /// <inheritdoc />
    public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _callCount++;
            _lastRequest = request;
        }

        if (_byClipHash.TryGetValue(clip.ClipHash, out var mapped))
        {
            return mapped;
        }

        return _default ?? throw new InvalidOperationException($"No scripted result for clip {clip.ClipHash} and no fallback result.");
    }
}
