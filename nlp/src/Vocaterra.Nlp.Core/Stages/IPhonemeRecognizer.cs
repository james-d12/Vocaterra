using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Adapter interface for phoneme posterior models (D1); output must be deterministic for a given clip.</summary>
public interface IPhonemeRecognizer
{
    /// <summary>Hash of the model files this recognizer runs.</summary>
    string ModelHash { get; }

    /// <summary>Computes per-frame phone log-probabilities for the clip.</summary>
    PhonePosteriors Recognize(PcmClip clip);
}
