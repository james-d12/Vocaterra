using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>Tuning knobs of the <see cref="UtterancePipeline"/>; the defaults are the shipped behaviour.</summary>
public sealed record PipelineOptions
{
    /// <summary>The shipped options.</summary>
    public static PipelineOptions Default { get; } = new();

    /// <summary>Which pronunciation scorer to run when a phoneme recognizer is present.</summary>
    public ScoringMode PronunciationMode { get; init; } = ScoringMode.Gop;

    /// <summary>How many n-best hypotheses to ask the word recognizer for.</summary>
    public int MaxAlternatives { get; init; } = RecognitionRequest.DefaultMaxAlternatives;

    /// <summary>Start ASR and phoneme recognition on separate tasks; the results are consumed in a fixed order either way.</summary>
    public bool RunRecognizersConcurrently { get; init; } = true;

    /// <summary>
    /// M80: at a <see cref="Vocaterra.Nlp.Core.Prompts.RecognitionMode.Constrained"/> prompt, decode the clip a second time, freely, when the constrained reading is not taken
    /// (not communicated and no off-prompt intent), and keep the free reading when it is taken. Off by default; a host turns it on for free talk only.
    /// With the option off, or when the first reading is taken, the recognizer is called once and nothing differs from before.
    /// </summary>
    public bool OpenRecognitionFallback { get; init; }

    /// <summary>How a verdict's correctness becomes an FSRS rating.</summary>
    public RatingThresholds RatingThresholds { get; init; } = RatingThresholds.Default;
}
