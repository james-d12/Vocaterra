using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Templates;

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
    public const string FeedbackLinePrefix = ReservedPools.FeedbackPrefix; // the core's one definition

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

            // W10: the answer a miss sends to (the stall's flour sold instead of the bottle) may itself be an exchange the world cannot make (no coin, a full bag): its miss is answered, not its success line.
            if (turn is not null)
            {
                foreach (var next in turn.Agent.Actions.Where(static a => a.HostEvent is not null))
                {
                    if (missOf(hostEvents.Of(agentId, next.HostEvent!)) is { } again && !string.Equals(again, miss, StringComparison.Ordinal)
                        && typed.Advance(NpcHostEvents.Of(agentId, again), before) is { } second)
                    {
                        miss = again;
                        turn = second;
                    }

                    break;
                }
            }

            var refusedGift = hostEvents.Of(agentId, hostEvent).Kind == NpcEffectKind.GiveItem;
            if (turn is null)
            {
                if (refusedGift)
                {
                    hostEventsOfTurn.Remove(hostEvent); // V30: the gift the NPC refused is not made, with or without a line for the refusal
                }

                continue; // an agent with no transition for the miss says its line as before
            }

            onTurn?.Invoke(miss, turn, before);
            context = hostStateHash is null ? before.Next(turn, now) : before.Next(turn, now) with { HostStateHash = hostStateHash };
            var events = turn.Agent.Actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();
            lines.AddRange(turn.Response.Lines.Select(static l => l.Text));
            hostEventsOfTurn.AddRange(events);
            unsaid.Add(action.DialogueIntent); // a pool is its dialogue intent's (every agent of the bundle names them alike)
            if (refusedGift)
            {
                hostEventsOfTurn.Remove(hostEvent); // the gift the NPC refused is not made (the sales' misses are held back by the session)
                unsaid.AddRange(actions.SkipWhile(a => a != action).Skip(1).Select(static a => a.DialogueIntent)); // and what the NPC would have said next ("¿Quieres pan?" after the bottle)
            }
        }

        return new MissAnswers(unsaid, lines, context);
    }

    /// <summary>The host events a turn's decision raises, in order (the game and the host read them alike, V30).</summary>
    /// <param name="result">The turn's result.</param>
    public static List<string> HostEventsOf(PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (result.Decision.Agent?.Actions ?? []).Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();
    }

    /// <summary>The stall's bottle rule (V30): a line of the stall's gift is not said when the player holds a bottle already, since nothing is given.</summary>
    /// <param name="bag">What the player carries, or <see langword="null"/> for nothing.</param>
    /// <param name="hostEvents">The turn's host events.</param>
    /// <returns>The test that tells a line id to leave out.</returns>
    public static Func<string, bool> SkipLineOfBottleGift(Inventory? bag, IReadOnlyList<string> hostEvents)
    {
        ArgumentNullException.ThrowIfNull(hostEvents);
        var holdsBottle = bag is { } held && (held.Count(ItemId.Bottle) + held.Count(ItemId.WaterBottle)) > 0;
        return lineId => holdsBottle && hostEvents.Contains(NpcHostEvents.GiveWater, StringComparer.Ordinal) && lineId.StartsWith("shop_give.", StringComparison.Ordinal);
    }

    /// <summary>The stream of the draw of an off-prompt reply (V27), the same in the game and the host.</summary>
    public const string OffPromptStream = "guide.offprompt";

    /// <summary>The host event whose transition names an NPC's off-prompt pool (<c>baker.understood</c>): the game never fires it (V28).</summary>
    /// <param name="npc">The NPC's id.</param>
    public static string UnderstoodEvent(string npc) => npc + ".understood";

    /// <summary>The host event whose transition names the NPC's «Sí, es verdad.» pool (<c>baker.true</c>): the game never fires it (V28).</summary>
    /// <param name="npc">The NPC's id.</param>
    public static string TrueEvent(string npc) => npc + ".true";

    /// <summary>
    /// The pool the NPC's agent says in its transition on a host event that the game never fires (V28): the agent file is the one place that names such a pool (and the lint
    /// counts it used), the game reads the name, so no turn is run and no state or counter moves. <see langword="null"/> for an agent with no such transition.
    /// </summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="npc">The NPC's id (its agent's id).</param>
    /// <param name="hostEvent">The event of the transition.</param>
    public static string? PoolSaidOn(ContentBundle bundle, string npc, string hostEvent)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(npc);
        return bundle.Agents.TryGetValue(npc, out var machine)
            ? machine.Definition.States
                .SelectMany(static state => state.Transitions)
                .Where(transition => string.Equals(transition.OnHostEvent, hostEvent, StringComparison.Ordinal))
                .SelectMany(static transition => transition.Actions)
                .Select(static action => action.VariantPoolId)
                .FirstOrDefault(static pool => pool is not null)
            : null;
    }

    /// <summary>
    /// The pool of the NPC's reply to a line in free talk that its prompt does not take but the core understands (V27, <c>Verdict.OffPromptIntent</c>): the pool its agent says
    /// in the <c>&lt;npc&gt;.understood</c> transition (<see cref="PoolSaidOn"/>), or <see langword="null"/>, and the NPC then answers such a line as one it did not understand.
    /// </summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="npc">The NPC's id (its agent's id).</param>
    public static string? OffPromptPoolOf(ContentBundle bundle, string npc) => PoolSaidOn(bundle, npc, UnderstoodEvent(npc));

    /// <summary>
    /// The NPC's reply to a line in free talk that its prompt does not take but the core understands (V27, <c>Verdict.OffPromptIntent</c>): the line of the NPC's own pool
    /// (<see cref="OffPromptPoolOf"/>), «Te entiendo. Yo hago hachas.», drawn from the interaction's seed, with no repair step and no correction.
    /// </summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="pool">The NPC's off-prompt pool.</param>
    /// <param name="worldSeed">The world seed.</param>
    /// <param name="counter">The agent's interaction counter before the turn.</param>
    public static string OffPromptReply(ContentBundle bundle, string pool, ulong worldSeed, long counter)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(pool);
        var found = bundle.Dialogue.TryGetPool(pool, out var p) ? p : throw new InvalidOperationException($"The bundle has no dialogue pool '{pool}'.");
        var variant = DialogueSelector.Select(found, SeededRng.ForInteraction(worldSeed, OffPromptStream, counter));
        return bundle.TemplateEngine.Render(variant, SlotMap.Empty);
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
    /// <param name="words">The game's words (the determiners before a name, the address pronouns).</param>
    /// <param name="skip">The ids of other lines the game does not say (the bottle the player holds already), or <see langword="null"/>.</param>
    /// <param name="freeTalkPools">The NPC's lesson pools by the pool said in free talk instead (<see cref="NpcEntry.FreeTalkPools"/>), or <see langword="null"/>.</param>
    /// <param name="freeTalk">Whether the line was said in free talk, not as the answer of a lesson step or a review.</param>
    /// <param name="bundle">The content bundle, for the NPC's «Sí, es verdad.» (V28): without it a statement about the listener is answered as any other.</param>
    /// <param name="npc">The NPC, whose <c>&lt;id&gt;.true</c> transition names the pool of «Sí, es verdad.» and whose file declares its acknowledgement pools (<see cref="NpcEntry.AcknowledgementPools"/>) and its slot replies (<see cref="NpcEntry.SlotReplies"/>, V29).</param>
    /// <param name="worldSeed">The world seed, for the draw among the variants of the NPC's replies (V29).</param>
    /// <param name="counter">The agent's interaction counter before the turn, for the same draw.</param>
    /// <param name="weatherKey">The dialogue key of the weather now (E07, <see cref="GuideScene.WeatherKey"/>): the answer of an intent in <see cref="NpcEntry.WeatherAnswers"/> is the NPC's pool for it (<see cref="WeatherReplyOf"/>).</param>
    public static ComposedReply Compose(PipelineResult result, bool understood, IReadOnlyList<string> unsaid, IEnumerable<string> names, GameWords words, Func<string, bool>? skip = null, IReadOnlyDictionary<string, string>? freeTalkPools = null, bool freeTalk = true, ContentBundle? bundle = null, NpcEntry? npc = null, ulong worldSeed = 0, long counter = 0, string? weatherKey = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var listenerPool = freeTalk && bundle is not null && npc is not null && AboutListener(result, words) ? PoolSaidOn(bundle, npc.Id, TrueEvent(npc.Id)) : null;
        var acknowledgementPools = npc?.AcknowledgementPools ?? [];
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
                feedback ??= RecastNames.Capitalise(line.Text, names, words); // the core writes the learner's words in lower case: «buscaré madera para Luz» (V20)
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
                var acknowledgement = acknowledgementPools.FirstOrDefault(pool => line.LineId.StartsWith(pool + ".", StringComparison.Ordinal));
                replies.Add(listenerPool is not null && acknowledgement is not null && npc!.OwnsPool(acknowledgement) ? ListenerReply(bundle!, listenerPool, worldSeed, counter, line.Text) : line.Text);
                acknowledgements.Add(acknowledgement is not null);
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

        if (freeTalk && bundle is not null && npc is not null && replies.Count > 0 && SlotReplyOf(bundle, npc, result.Verdict, worldSeed, counter) is { } echo)
        {
            replies[0] = echo;
        }
        else if (bundle is not null && npc is not null && replies.Count > 0 && WeatherReplyOf(bundle, npc, result.Verdict, weatherKey, worldSeed, counter) is { } weather)
        {
            replies[0] = weather;
        }

        return new ComposedReply(feedback, replies);
    }

    /// <summary>The stream of the draw among the variants of an NPC's «Sí, es verdad.» and of its slot replies (V29), the same in the game and the host.</summary>
    public const string ReplyStream = "npc.reply";

    /// <summary>The variant of an NPC's pool for an interaction, drawn from the world seed and the agent's counter on <see cref="ReplyStream"/>: no clock, no <c>System.Random</c>.</summary>
    /// <param name="pool">The pool.</param>
    /// <param name="worldSeed">The world seed.</param>
    /// <param name="counter">The agent's interaction counter before the turn.</param>
    public static DialogueTemplate Pick(VariantPool pool, ulong worldSeed, long counter) =>
        DialogueSelector.Select(pool, SeededRng.ForInteraction(worldSeed, ReplyStream, counter));

    /// <summary>
    /// The NPC's reply that says a word of the line back (V29, <see cref="NpcEntry.SlotReplies"/>): «Sí, hace frío.» for <c>hace frío</c>, rendered from the pool its agent names on the
    /// reply's host event with the verdict's slot; <see langword="null"/> when the verdict's intent has no such reply, the line has no such slot or its value is not on the reply's list
    /// (the plain reply then stands).
    /// </summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="npc">The NPC.</param>
    /// <param name="verdict">The turn's verdict.</param>
    /// <param name="worldSeed">The world seed.</param>
    /// <param name="counter">The agent's interaction counter before the turn.</param>
    public static string? SlotReplyOf(ContentBundle bundle, NpcEntry npc, Verdict verdict, ulong worldSeed, long counter)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(verdict);
        if (!verdict.Communicated || !npc.SlotReplies.TryGetValue(verdict.Intent.Value, out var reply)
            || !verdict.Slots.TryGetValue(reply.Slot, out var value) || !reply.Values.Contains(value, StringComparer.Ordinal)
            || PoolSaidOn(bundle, npc.Id, reply.Event) is not { } pool || !bundle.Dialogue.TryGetPool(pool, out var found) || found.Variants.Count == 0)
        {
            return null;
        }

        return bundle.TemplateEngine.Render(Pick(found, worldSeed, counter), SlotMap.From(new Dictionary<string, string>(StringComparer.Ordinal) { [reply.Slot] = value }));
    }

    /// <summary>The host event whose transition names the NPC's pool for a weather's dialogue key (<c>baker.cloudy</c>): the game never fires it (E07).</summary>
    /// <param name="npc">The NPC's id.</param>
    /// <param name="key">The dialogue key (a kind's <c>say</c>).</param>
    public static string WeatherEvent(string npc, string key) => npc + "." + key;

    /// <summary>
    /// The NPC's answer to «¿Qué tiempo hace?» (E07, <see cref="NpcEntry.WeatherAnswers"/>): the line of the pool its agent names on <see cref="WeatherEvent"/> for the weather's dialogue key,
    /// drawn from the interaction's seed; <see langword="null"/> when the verdict's intent is not one of the NPC's, the world has no key or the NPC has no such pool (the plain reply then stands).
    /// </summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="npc">The NPC.</param>
    /// <param name="verdict">The turn's verdict.</param>
    /// <param name="weatherKey">The dialogue key of the weather now, or <see langword="null"/>.</param>
    /// <param name="worldSeed">The world seed.</param>
    /// <param name="counter">The agent's interaction counter before the turn.</param>
    public static string? WeatherReplyOf(ContentBundle bundle, NpcEntry npc, Verdict verdict, string? weatherKey, ulong worldSeed, long counter)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(verdict);
        if (weatherKey is null || !verdict.Communicated || !npc.WeatherAnswers.Contains(verdict.Intent.Value, StringComparer.Ordinal)
            || PoolSaidOn(bundle, npc.Id, WeatherEvent(npc.Id, weatherKey)) is not { } pool || !bundle.Dialogue.TryGetPool(pool, out var found) || found.Variants.Count == 0)
        {
            return null;
        }

        return bundle.TemplateEngine.Render(Pick(found, worldSeed, counter), SlotMap.Empty);
    }

    /// <summary>
    /// Whether the line says something about the listener (V28): the core's analysis finds a verb of the second person (<c>tienes</c>, <c>eres</c>) or the pronoun
    /// <c>tú</c>, <c>usted</c> or <c>ustedes</c> not after a preposition (<c>Usted tiene muchas manzanas.</c>: <c>tiene</c> reads as the third person, the pronoun says who).
    /// It is the person of the words the resolver chose, not a search of the text.
    /// </summary>
    /// <param name="result">The turn's result.</param>
    /// <param name="words">The game's words (the address pronouns, <c>game/words.json</c>, addressPronouns).</param>
    public static bool AboutListener(PipelineResult result, GameWords words)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(words);
        if (result.Words is not { } heard)
        {
            return false;
        }

        for (var i = 0; i < heard.Count; i++)
        {
            if (heard[i].Reading is not { } reading)
            {
                continue;
            }

            if (string.Equals(reading.Pos, "verb", StringComparison.Ordinal) && string.Equals(reading.Person, "2", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(reading.Pos, "pron", StringComparison.Ordinal) && reading.Lemma is { } lemma && words.Matching.AddressPronouns.All.Contains(lemma, StringComparer.Ordinal)
                && (i == 0 || heard[i - 1].Reading is not { Pos: "prep" }))
            {
                return true;
            }
        }

        return false;
    }

    // The NPC's «Sí, es verdad.» in the place of its bare «¡Qué bien!» (the pool its agent says on <npc>.true), drawn from the interaction's seed, when the bundle has the pool.
    private static string ListenerReply(ContentBundle bundle, string pool, ulong worldSeed, long counter, string otherwise) =>
        bundle.Dialogue.TryGetPool(pool, out var found) && found.Variants.Count > 0
            ? bundle.TemplateEngine.Render(Pick(found, worldSeed, counter), SlotMap.Empty)
            : otherwise;
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
