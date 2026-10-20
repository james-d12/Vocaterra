using Slg.Core.Audio;

namespace Slg.Core.Stages;

/// <summary>Adapter interface for text-to-speech models (D1); output must be deterministic for a given request.</summary>
public interface ISpeechSynthesizer
{
    /// <summary>Hash of the model files this synthesizer runs.</summary>
    string ModelHash { get; }

    /// <summary>Renders the request to audio.</summary>
    AudioBuffer Synthesize(SynthesisRequest request);
}
