namespace Vocaterra.Nlp.Decision.Feedback;

/// <summary>The kind of corrective feedback an NPC gives for one error.</summary>
public enum FeedbackType
{
    /// <summary>Repeat the correct form naturally inside the NPC's reply.</summary>
    Recast,

    /// <summary>Point at the problem without giving the answer.</summary>
    Hint,

    /// <summary>State the correct form and the rule.</summary>
    ExplicitCorrection,

    /// <summary>Say the target word or sound clearly for the learner to imitate.</summary>
    ModelPronunciation,

    /// <summary>Repeat the expected phrase slowly because the learner was not understood.</summary>
    SlowRepeat,
}
