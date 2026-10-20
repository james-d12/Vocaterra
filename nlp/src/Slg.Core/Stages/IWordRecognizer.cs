using Slg.Core.Audio;

namespace Slg.Core.Stages;

/// <summary>Adapter interface for speech-to-text models (D1); implementations must be deterministic for a given clip and request.</summary>
public interface IWordRecognizer
{
    /// <summary>Hash of the model files this recognizer runs.</summary>
    string ModelHash { get; }

    /// <summary>Transcribes the clip under the request's mode and grammar.</summary>
    AsrResult Recognize(PcmClip clip, RecognitionRequest request);
}
