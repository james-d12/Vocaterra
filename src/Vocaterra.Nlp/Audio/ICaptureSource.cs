using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Audio;

/// <summary>A source of one captured utterance as a processed 16 kHz mono clip.</summary>
public interface ICaptureSource
{
    /// <summary>Captures one utterance.</summary>
    PcmClip Capture();
}
