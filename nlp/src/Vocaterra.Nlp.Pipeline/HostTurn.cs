using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// What a host-initiated agent turn produced (<see cref="UtterancePipeline.Advance"/>): the agent's step and the lines it says. There is no verdict,
/// learner update, rating or repeat count, because no player utterance was graded.
/// </summary>
/// <param name="HostEvent">The host event name that triggered the turn.</param>
/// <param name="Agent">The agent step: the next state (interaction counter advanced by one) and the actions, whose <see cref="AgentAction.HostEvent"/> values are the host events to act on.</param>
/// <param name="Response">The lines the agent says, one per action whose dialogue pool exists, rendered with the context's host slots.</param>
/// <param name="HostStateHash">The context's host state hash (copied from the context the turn ran in).</param>
/// <param name="ContentHash">The content bundle hash the turn ran with. No model takes part in a host turn, so no model hash is recorded and a replay needs no recognizer.</param>
public sealed record HostTurnResult(
    string HostEvent,
    AgentStepResult Agent,
    ResponsePlan Response,
    string HostStateHash,
    string ContentHash);

/// <summary>
/// One replay log entry for a host-initiated turn: the event, the context it ran in and the recorded result. It is a second entry kind next to
/// <see cref="ReplayEnvelope"/>, told apart by <see cref="Kind"/> (envelopes have no such property). Replay it with
/// <see cref="Replayer.Replay(HostTurnEnvelope, UtterancePipeline)"/>.
/// </summary>
/// <param name="Kind">Always <see cref="KindName"/>; the discriminator that <see cref="ReplayReader"/> looks for.</param>
/// <param name="HostEvent">The host event that was raised.</param>
/// <param name="Context">The conversation context the turn ran in.</param>
/// <param name="Result">The recorded result.</param>
public sealed record HostTurnEnvelope(string Kind, string HostEvent, ConversationContext Context, HostTurnResult Result)
{
    /// <summary>The value of <see cref="Kind"/>: <c>hostTurn</c>.</summary>
    public const string KindName = "hostTurn";

    /// <summary>Builds the envelope for a host turn run in <paramref name="context"/>.</summary>
    public static HostTurnEnvelope From(HostTurnResult result, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);
        return new HostTurnEnvelope(KindName, result.HostEvent, context, result);
    }
}

/// <summary>One entry of a replay log: exactly one of the two members is set.</summary>
/// <param name="Utterance">An evaluated utterance, or null.</param>
/// <param name="HostTurn">A host-initiated turn, or null.</param>
public sealed record ReplayEntry(ReplayEnvelope? Utterance, HostTurnEnvelope? HostTurn);
