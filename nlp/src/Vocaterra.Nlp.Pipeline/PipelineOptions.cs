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

    /// <summary>How a verdict's correctness becomes an FSRS rating.</summary>
    public RatingThresholds RatingThresholds { get; init; } = RatingThresholds.Default;
}
