using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Nlp.Intent;
using Vocaterra.Nlp.Response.Effects;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>What the decision layer made of a verdict: the agent's step, the updated learner, the FSRS rating and the feedback plan.</summary>
/// <param name="Agent">The agent state machine step, or null when the bundle has no state machine for the context's agent.</param>
/// <param name="Learner">The learner model after this utterance.</param>
/// <param name="Rating">The spaced-repetition rating of the utterance.</param>
/// <param name="Feedback">The feedback policy's plan for the verdict's errors.</param>
public sealed record DecisionOutcome(AgentStepResult? Agent, LearnerModel Learner, Rating Rating, FeedbackPlan Feedback);

/// <summary>Everything one utterance produced: verdict, decision, spoken response, effects, optional repair step and the replay record.</summary>
/// <param name="Verdict">The graded verdict.</param>
/// <param name="Decision">The decision layer's outcome.</param>
/// <param name="Response">The lines the agent says.</param>
/// <param name="Effects">The effects the verdict triggers, in ordinal effect id order.</param>
/// <param name="Repair">The repair step when the player was not understood; otherwise null.</param>
/// <param name="Replay">The record that lets this evaluation be replayed.</param>
public sealed record PipelineResult(
    Verdict Verdict,
    DecisionOutcome Decision,
    ResponsePlan Response,
    IReadOnlyList<ResolvedEffect> Effects,
    RepairStep? Repair,
    ReplayRecord Replay)
{
    /// <summary>Compares every member, including the effect list.</summary>
    public bool Equals(PipelineResult? other)
    {
        return other is not null
            && Verdict.Equals(other.Verdict)
            && Decision.Equals(other.Decision)
            && Response.Equals(other.Response)
            && ListEquality.Equal(Effects, other.Effects)
            && EqualityComparer<RepairStep?>.Default.Equals(Repair, other.Repair)
            && Replay.Equals(other.Replay);
    }

    /// <summary>Hash consistent with <see cref="Equals(PipelineResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Verdict);
        hash.Add(Decision);
        hash.Add(Response);
        ListEquality.Add(ref hash, Effects);
        hash.Add(Repair);
        hash.Add(Replay);
        return hash.ToHashCode();
    }
}

/// <summary>A <see cref="PipelineResult"/> together with the recognizer outputs that produced it, which is what a replay log persists.</summary>
/// <param name="Result">The pipeline result.</param>
/// <param name="Asr">The word recognizer's output.</param>
/// <param name="Posteriors">The phoneme recognizer's output, or null when pronunciation scoring did not run.</param>
public sealed record PipelineTrace(PipelineResult Result, AsrResult Asr, PhonePosteriors? Posteriors);
