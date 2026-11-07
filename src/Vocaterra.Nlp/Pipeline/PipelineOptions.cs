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

    /// <summary>
    /// M80 (the owner's answer, 2026-11-07): the weakest-word floor of the second, open pass. Its reading is taken only when the lowest word confidence of its
    /// recognition is at least this value, so a free decode that heard one word badly (Vosk's «ese» as «el» at 0.45) does not put a meaning on the line. A
    /// recognition with no words (an empty one) has no weakest word and is not taken, unless the floor is 0, which switches the check off. Used by the open
    /// pass only; the first, constrained pass is never held to it. Between 0 and 1 (an <see cref="ArgumentOutOfRangeException"/> from the pipeline's constructor
    /// otherwise); it has no effect while <see cref="OpenRecognitionFallback"/> is off.
    /// </summary>
    public double OpenPassWordFloor { get; init; } = DefaultOpenPassWordFloor;

    /// <summary>The default of <see cref="OpenPassWordFloor"/>.</summary>
    public const double DefaultOpenPassWordFloor = 0.5;

    /// <summary>How a verdict's correctness becomes an FSRS rating.</summary>
    public RatingThresholds RatingThresholds { get; init; } = RatingThresholds.Default;
}
