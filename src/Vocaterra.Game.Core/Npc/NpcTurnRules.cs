using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// The parts of an NPC's turn that the game and the console host must do alike (V21b, item 8): the NPC's answer to an exchange the world cannot make (the dropped gift,
/// <c>cant_pay</c> and the rest) and the lines of the reply that are left once the turn's rules have run (one recast or praise, no bare acknowledgement beside it, no
/// line of an action that was not made). <see cref="NpcService"/> and the host's <c>ConversationSession</c> both call this class, so <c>--transcript</c> shows what the
/// game says. Pure: no clock, no random numbers.
/// </summary>
public static class NpcTurnRules
{
    /// <summary>The prefix of the core's correction lines that are no recast (a hint, an explicit correction).</summary>
    public const string FeedbackLinePrefix = "feedback.";

    /// <summary>
    /// The answers of a turn's missed actions. For each action of the decision whose host event makes a miss (<paramref name="missOf"/>), the NPC takes the host event of
    /// the miss (<c>stall.wont_sell</c>, <c>cant_pay</c>, …) from the context after the turn, and says its line instead of the action's; a gift it refuses is not made and
    /// the lines that would have followed it are not said either (V21).
    /// </summary>
    /// <param name="agentId">The NPC's agent id.</param>
    /// <param name="result">The turn's result.</param>
    /// <param name="hostEvents">The table of what each host event does.</param>
    /// <param name="typed">The evaluator that takes the host turns.</param>
    /// <param name="context">The context after the turn.</param>
    /// <param name="now">The stored time of the turn.</param>
    /// <param name="hostStateHash">The digest of the world the context keeps again after a host turn, or <see langword="null"/> to leave it.</param>
    /// <param name="orderHeld">Whether a lesson keeps back the order that pays (V21): a purchase's <c>cant_pay</c> is then <c>cant_pay_wait</c>, where the agent says it.</param>
    /// <param name="missOf">The miss the world makes of an effect, or <see langword="null"/> when it can be carried out.</param>
    /// <param name="hostEventsOfTurn">The turn's host events; a refused gift leaves it and the miss's own events join it.</param>
    /// <param name="onTurn">Called for each answer taken, with its host event and the context it started from (the game records and notes it).</param>
    public static MissAnswers AnswerMisses(
        string agentId,
        PipelineResult result,
        HostEventTable hostEvents,
        TypedUtteranceEvaluator typed,
        ConversationContext context,
        StoredTimestamp now,
        string? hostStateHash,
        bool orderHeld,
        Func<NpcEffect, string?> missOf,
        List<string> hostEventsOfTurn,
        Action<string, HostTurnResult, ConversationContext>? onTurn = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(hostEvents);
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(missOf);
        ArgumentNullException.ThrowIfNull(hostEventsOfTurn);
        var unsaid = new List<string>();
        var lines = new List<string>();
        var actions = result.Decision.Agent?.Actions ?? [];
        foreach (var action in actions)
        {
            if (action.HostEvent is not { } hostEvent || missOf(hostEvents.Of(agentId, hostEvent)) is not { } found)
            {
                continue;
            }

            var miss = found;
            var before = context;
            if (orderHeld && string.Equals(miss, NpcHostEvents.CantPay, StringComparison.Ordinal))
            {
                miss = NpcHostEvents.CantPayWait; // the coins are short because a lesson keeps back the order that pays; an agent that cannot say so says cant_pay
            }

            var turn = typed.Advance(NpcHostEvents.Of(agentId, miss), before);
            if (turn is null && string.Equals(miss, NpcHostEvents.CantPayWait, StringComparison.Ordinal))
            {
                miss = NpcHostEvents.CantPay;
                turn = typed.Advance(NpcHostEvents.Of(agentId, miss), before);
            }

            if (turn is null)
            {
                continue; // an agent with no transition for the miss says its line as before
            }

            onTurn?.Invoke(miss, turn, before);
            context = hostStateHash is null ? before.Next(turn, now) : before.Next(turn, now) with { HostStateHash = hostStateHash };
            var events = turn.Agent.Actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();
            lines.AddRange(turn.Response.Lines.Select(static l => l.Text));
            hostEventsOfTurn.AddRange(events);
            unsaid.Add(action.DialogueIntent); // a pool is its dialogue intent's (every agent of the bundle names them alike)
            if (hostEvents.Of(agentId, hostEvent).Kind == NpcEffectKind.GiveItem)
            {
                hostEventsOfTurn.Remove(hostEvent); // the gift the NPC refused is not made (the sales' misses are held back by the session)
                unsaid.AddRange(actions.SkipWhile(a => a != action).Skip(1).Select(static a => a.DialogueIntent)); // and what the NPC would have said next ("¿Quieres pan?" after the bottle)
            }
        }

        return new MissAnswers(unsaid, lines, context);
    }

    /// <summary>
    /// The lines of a reply once the turn's rules have run: a recast or a core hint is the feedback (the first one; a line the NPC took for no intent gets only the retry,
    /// QA-language-9), the lines of the actions the world could not make are left out (<paramref name="unsaid"/>), and with a recast its praise and the NPC's bare
    /// acknowledgement («¡Qué bien!») are one reply twice, so the recast stays (V21).
    /// </summary>
    /// <param name="result">The turn's result.</param>
    /// <param name="understood">Whether the NPC took the line for an intent.</param>
    /// <param name="unsaid">The pools of the lines not said (<see cref="MissAnswers.Unsaid"/>).</param>
    /// <param name="names">The NPCs' names, spelled as they are in a correction.</param>
    /// <param name="skip">The ids of other lines the game does not say (the bottle the player holds already), or <see langword="null"/>.</param>
    /// <param name="freeTalkPools">The NPC's lesson pools by the pool said in free talk instead (<see cref="NpcEntry.FreeTalkPools"/>), or <see langword="null"/>.</param>
    /// <param name="freeTalk">Whether the line was said in free talk, not as the answer of a lesson step or a review.</param>
    public static ComposedReply Compose(PipelineResult result, bool understood, IReadOnlyList<string> unsaid, IEnumerable<string> names, Func<string, bool>? skip = null, IReadOnlyDictionary<string, string>? freeTalkPools = null, bool freeTalk = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(unsaid);
        string? feedback = null;
        var replies = new List<string>();
        var acknowledgements = new List<bool>();
        var recasted = false;
        foreach (var line in result.Response.Lines)
        {
            if (line.IsRecast && !understood)
            {
                // a line the NPC did not take for any intent gets only the retry; the recast's praise would say it was right
            }
            else if (line.IsRecast || line.LineId.StartsWith(FeedbackLinePrefix, StringComparison.Ordinal))
            {
                recasted |= line.IsRecast;
                feedback ??= RecastNames.Capitalise(line.Text, names); // the core writes the learner's words in lower case: «buscaré madera para Luz» (V20)
            }
            else if (skip is not null && skip(line.LineId))
            {
                // a line the game does not say
            }
            else if (freeTalkPools is { Count: > 0 } && (freeTalk ? freeTalkPools.Keys : freeTalkPools.Values).Any(pool => line.LineId.StartsWith(pool + ".", StringComparison.Ordinal)))
            {
                // V21b: the other of a lesson pool and its free-talk twin (the agent says both)
            }
            else if (unsaid.Any(pool => line.LineId.StartsWith(pool + ".", StringComparison.Ordinal)))
            {
                // the line of an action the world cannot do; the NPC's answer to the miss follows
            }
            else
            {
                replies.Add(line.Text);
                acknowledgements.Add(IsAcknowledgement(line.LineId));
            }
        }

        if (recasted)
        {
            for (var i = replies.Count - 1; i >= 0; i--)
            {
                if (acknowledgements[i])
                {
                    replies.RemoveAt(i);
                }
            }
        }

        return new ComposedReply(feedback, replies);
    }

    // Whether a line of the agent is a bare acknowledgement (V20's free-talk replies: «¡Qué bien!», «Muy bien»): the answer_accepted pool and each NPC's <npc>_nice.
    private static bool IsAcknowledgement(string lineId) =>
        lineId.StartsWith("answer_accepted.", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(lineId, @"^[a-z]+_nice\.\d+$");
}

/// <summary>The NPC's answers to a turn's missed actions (<see cref="NpcTurnRules.AnswerMisses"/>).</summary>
/// <param name="Unsaid">The dialogue intents of the lines not said.</param>
/// <param name="Lines">The NPC's answers, in the order they are said.</param>
/// <param name="Context">The context after the answers.</param>
public sealed record MissAnswers(IReadOnlyList<string> Unsaid, IReadOnlyList<string> Lines, ConversationContext Context);

/// <summary>A turn's reply (<see cref="NpcTurnRules.Compose"/>): the correction and the NPC's lines.</summary>
/// <param name="Feedback">The recast or hint, with the names spelled, or <see langword="null"/>.</param>
/// <param name="Replies">The NPC's lines.</param>
public sealed record ComposedReply(string? Feedback, IReadOnlyList<string> Replies);
