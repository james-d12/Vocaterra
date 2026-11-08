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
    /// With the option off, or when the first reading is taken and does not look forced (<see cref="OpenPassOnLooseMatch"/>, <see cref="OpenPassMinCoverage"/>, M84),
    /// the recognizer is called once and nothing differs from before.
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

    /// <summary>
    /// M84 (free talk's forced rows): a constrained reading that was <b>taken</b> but is not an answer row heard whole also gets the open decode. The constrained
    /// decode can force a different line onto an answer row, or onto the words of one, with confidence (V31: «me gusta el río» heard «me cuesta el río» and read
    /// as a price question), so a taken reading whose answer match is <c>None</c> (taken only through the intent catalog) or
    /// <c>Fuzzy</c> (<see cref="MatchType"/>) is checked against the clip heard freely. The free reading then wins only when it is taken, clears
    /// <see cref="OpenPassWordFloor"/> and has a different intent from the first reading; otherwise the first reading stands, and the free recognition is
    /// <see cref="UtteranceAnalysis.OtherPass"/>. An exact or alias match is trusted here, unless <see cref="OpenPassMinCoverage"/> says the words left most of the speech
    /// unused. On by default; it has no effect while <see cref="OpenRecognitionFallback"/> is off. Switch it off, with <see cref="OpenPassMinCoverage"/> 0, for the M80
    /// behaviour (the open decode only after a reading that is not taken).
    /// </summary>
    public bool OpenPassOnLooseMatch { get; init; } = true;

    /// <summary>
    /// M84: the share of the clip's voiced audio that the recognized words must cover (frames of 20 ms at or above -40 dBFS whose middle falls inside a recognized word,
    /// <c>[unk]</c> left out) for a taken constrained reading to be trusted without the open decode. Under it, a taken reading gets the open decode too, and the free
    /// reading wins by the rule of <see cref="OpenPassOnLooseMatch"/>. It catches a clip whose speech the decode forced onto a short answer row («el río es grande»
    /// heard «es gracias»). A clip with no voiced frame, or a recognition with no timed word, has no coverage and is not checked. 0 switches the check off. Between 0
    /// and 1 (an <see cref="ArgumentOutOfRangeException"/> from the pipeline's constructor otherwise); it has no effect while <see cref="OpenRecognitionFallback"/> is off.
    /// </summary>
    public double OpenPassMinCoverage { get; init; } = DefaultOpenPassMinCoverage;

    /// <summary>The default of <see cref="OpenPassMinCoverage"/>.</summary>
    public const double DefaultOpenPassMinCoverage = 0.6;

    /// <summary>How a verdict's correctness becomes an FSRS rating.</summary>
    public RatingThresholds RatingThresholds { get; init; } = RatingThresholds.Default;
}
