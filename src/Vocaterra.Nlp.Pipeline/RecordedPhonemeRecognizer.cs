using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>A phoneme recognizer that returns recorded posteriors whatever the clip, so a replay needs no model.</summary>
public sealed class RecordedPhonemeRecognizer : IPhonemeRecognizer
{
    private readonly PhonePosteriors _posteriors;

    /// <summary>Creates a recognizer that always returns <paramref name="posteriors"/> and reports <paramref name="modelHash"/>.</summary>
    public RecordedPhonemeRecognizer(PhonePosteriors posteriors, string modelHash)
    {
        ArgumentNullException.ThrowIfNull(posteriors);
        ArgumentNullException.ThrowIfNull(modelHash);
        _posteriors = posteriors;
        ModelHash = modelHash;
    }

    /// <inheritdoc />
    public string ModelHash { get; }

    /// <inheritdoc />
    public PhonePosteriors Recognize(PcmClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return _posteriors;
    }
}
