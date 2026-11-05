using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Host;

/// <summary>
/// The player's conversation with a content bundle. Each typed line is evaluated on the prompt the player chose; the agent states, the
/// learner model and the repeat counts are carried from one utterance to the next in a <see cref="ConversationContext"/>.
/// </summary>
/// <remarks>
/// The word recognizer is the core's scripted one: the typed line is the transcript, so no model and no audio take part (the call
/// sequence is <see cref="TypedUtteranceEvaluator"/> in <c>Vocaterra.Game.Core</c>, shared with the guide). The host owns the state the core
/// cannot know (the world seed, the stored time, the learner model and the repeat counts), as docs/nlp/api.md describes.
/// </remarks>
public sealed class ConversationSession
{
    /// <summary>The agent id for a prompt that names no agent, as the core's own CLI uses.</summary>
    public const string NoAgentId = "player";

    /// <summary>The world seed of a new conversation. The host uses it until the game has save files.</summary>
    public const ulong DefaultWorldSeed = 42;

    private readonly TypedUtteranceEvaluator _evaluator;
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly Dictionary<string, AgentState> _agentStates = new(StringComparer.Ordinal);
    private readonly Inventory? _bag;
    private readonly HostEventTable _hostEvents;
    private readonly NounCatalog _nouns;
    private readonly NpcRoster _roster;
    private ConversationContext _context;

    /// <summary>
    /// Starts a conversation over the bundle: every agent in its initial state and a learner model with no evidence yet. A restored
    /// conversation passes the repeat counts it saved; a new one passes none. With the NPCs' names (by agent id, from the NPC files), a line that
    /// names its listener as a vocative (<c>Luz, quiero pan</c> at the baker) is read without the name, as the game reads it (<see cref="Vocative"/>, V14).
    /// </summary>
    public ConversationSession(
        ContentBundle bundle,
        ulong worldSeed = DefaultWorldSeed,
        IReadOnlyDictionary<string, int>? repeatCountsByTag = null,
        IReadOnlyDictionary<string, string>? listenerNames = null,
        Inventory? bag = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        Bundle = bundle;

        // A bag makes a sale or a purchase follow it (V23a); a gift is refused for what the NPC does not give with or without one. The NPC answers a miss as it does in the
        // game, through the same code (NpcTurnRules, V21b).
        _bag = bag;
        _hostEvents = HostEventTable.Load(bundle);
        _nouns = NounCatalog.Load(bundle);
        _roster = NpcRoster.Load(bundle);

        _evaluator = new TypedUtteranceEvaluator(bundle);
        _names = listenerNames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        _context = ConversationContext.Create(
            worldSeed,
            NoAgentId,
            new AgentState(NoAgentId, "none", 0),
            new StoredTimestamp(0),
            LearnerModel.New(),
            repeatCountsByTag);
    }

    /// <summary>The content bundle the conversation runs on.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>
    /// The context the next utterance starts from: the agent the last utterance spoke to and its state, the learner model, the repeat counts
    /// and the time of the last utterance. Before any utterance the time is zero and the agent is <see cref="NoAgentId"/>.
    /// </summary>
    public ConversationContext Context => _context;

    /// <summary>The last <see cref="Evaluate"/>'s reply as the game says it (the one reply of the turn, the refused gift and the misses a bag makes); <see langword="null"/> before the first.</summary>
    public ExchangeAnswer? LastExchange { get; private set; }

    /// <summary>The ids of the bundle's prompts, in ordinal order.</summary>
    public IReadOnlyList<string> PromptIds => Bundle.Prompts.Keys.Order(StringComparer.Ordinal).ToArray();

    /// <summary>Whether the bundle has a prompt with this id.</summary>
    public bool HasPrompt(string promptId)
    {
        ArgumentNullException.ThrowIfNull(promptId);
        return Bundle.Prompts.ContainsKey(promptId);
    }

    /// <summary>Returns the prompt with this id.</summary>
    /// <exception cref="ArgumentException">The bundle has no such prompt.</exception>
    public PromptSpec Prompt(string promptId)
    {
        ArgumentNullException.ThrowIfNull(promptId);
        return Bundle.Prompts.TryGetValue(promptId, out var prompt)
            ? prompt
            : throw new ArgumentException($"There is no prompt '{promptId}' in the bundle.", nameof(promptId));
    }

    /// <summary>
    /// Evaluates one typed line on a prompt and carries the outcome into <see cref="Context"/> with <c>ConversationContext.Next</c>: the
    /// agent's new state, the learner model after this utterance and the repeat counts including it (<c>RepeatCounts.Next</c>).
    /// </summary>
    /// <exception cref="ArgumentException">The prompt is not in the bundle.</exception>
    public PipelineResult Evaluate(string transcript, string promptId, StoredTimestamp now)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        var prompt = Prompt(promptId);
        var agentId = prompt.AgentId ?? NoAgentId;
        var agentState = StateOf(agentId);
        var utterance = ConversationContext.Create(
            _context.WorldSeed, agentId, agentState, now, _context.Learner, _context.RepeatCountsByTag);

        // The listener's name said as a vocative is taken out (V14), and a greeting that opens a line with something else to say is answered by the rest (V15): the game's
        // own reading of a line (LineReading, V19).
        var result = LineReading.Read(transcript, _names.GetValueOrDefault(agentId), prompt, line => _evaluator.Evaluate(line, prompt, utterance), static r => r.Verdict);

        _agentStates[agentId] = result.Decision.Agent?.Next ?? agentState;
        _context = utterance.Next(result, now);
        LastExchange = AnswerTurn(agentId, result, transcript, now, promptId, agentState.InteractionCounter);
        if (LastExchange.OffPromptIntent is not null)
        {
            _context = _context with { RepeatCountsByTag = utterance.RepeatCountsByTag, Learner = utterance.Learner }; // V27: an off-prompt line is no failed attempt, and the learner is not marked down
        }
        return result;
    }

    // The game's own turn rules (NpcTurnRules, shared with NpcService): the NPC's answer to a gift it refuses and, with a bag, to a purchase or a sale the bag cannot make,
    // and the one reply of the turn. The bag is only read; nothing changes hands in the host.
    private ExchangeAnswer AnswerTurn(string agentId, PipelineResult result, string transcript, StoredTimestamp now, string promptId, long counter)
    {
        var slots = NpcEffect.CountedSlots(result.Verdict.Slots, transcript);
        var verdict = result.Verdict;
        var understood = verdict.Communicated && !string.Equals(verdict.Intent.Value, "unknown", StringComparison.Ordinal);
        var events = NpcTurnRules.HostEventsOf(result);
        var answers = NpcTurnRules.AnswerMisses(
            agentId,
            result,
            _hostEvents,
            _evaluator,
            _context,
            now,
            null,
            false,
            effect => effect.Kind == NpcEffectKind.GiveItem || _bag is not null ? effect.ExchangeMiss(slots, _bag, _nouns) : null,
            events);
        _context = answers.Context;
        _agentStates[agentId] = _context.AgentState; // as the game: the agent's state after the misses, whether or not they gave lines (V30)
        var npc = _roster.All.FirstOrDefault(entry => string.Equals(entry.Id, agentId, StringComparison.Ordinal));
        var composed = NpcTurnRules.Compose(
            result,
            understood,
            answers.Unsaid,
            _names.Values,
            NpcTurnRules.SkipLineOfBottleGift(_bag, events),
            npc?.FreeTalkPools,
            bundle: Bundle,
            npc: npc,
            worldSeed: _context.WorldSeed,
            counter: counter);
        if (!verdict.Communicated && verdict.OffPromptIntent is { } offPrompt && NpcTurnRules.OffPromptPoolOf(Bundle, agentId) is { } pool && npc is not null
            && npc.Prompts.Values.Contains(promptId, StringComparer.Ordinal))
        {
            // V27: in free talk (the NPC's own prompt, not a lesson step) the NPC's one off-prompt line and nothing else.
            return new ExchangeAnswer(answers.Unsaid, [], null, [NpcTurnRules.OffPromptReply(Bundle, pool, _context.WorldSeed, counter)], offPrompt.Value);
        }

        return new ExchangeAnswer(answers.Unsaid, answers.Lines, composed.Feedback, composed.Replies);
    }

    private AgentState StateOf(string agentId)
    {
        if (_agentStates.TryGetValue(agentId, out var state))
        {
            return state;
        }

        // A prompt whose agent has no state machine in the bundle gets the core's placeholder state, as the core's CLI does.
        return Bundle.Agents.TryGetValue(agentId, out var machine)
            ? machine.InitialState()
            : new AgentState(agentId, "none", 0);
    }
}

/// <summary>What the game's turn rules made of a turn (<see cref="ConversationSession.LastExchange"/>): the NPC's lines as the game says them.</summary>
/// <param name="Unsaid">The dialogue intents of the actions the world cannot carry out; their lines (<c>&lt;intent&gt;.1</c>) are left out of the report.</param>
/// <param name="Lines">The NPC's answers to the misses.</param>
/// <param name="Feedback">The turn's one recast or hint, names spelled, or <see langword="null"/>.</param>
/// <param name="Replies">The NPC's lines of the turn, once the rules have run (one reply, V21).</param>
/// <param name="OffPromptIntent">The intent the core found for a line the prompt does not take, when the NPC answered it with its off-prompt line (V27); <see langword="null"/> otherwise.</param>
public sealed record ExchangeAnswer(IReadOnlyList<string> Unsaid, IReadOnlyList<string> Lines, string? Feedback, IReadOnlyList<string> Replies, string? OffPromptIntent = null);
