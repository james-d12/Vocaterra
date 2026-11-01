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
/// cannot know (the world seed, the stored time, the learner model and the repeat counts), as docs/nlp/API.md describes.
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
    private readonly HostEventTable? _hostEvents;
    private readonly NounCatalog? _nouns;
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
        if (bag is not null)
        {
            // A bag makes a sale or a purchase follow it (V23a): the NPC answers a miss as it does in the game (NpcService.AnswerMisses).
            _bag = bag;
            _hostEvents = HostEventTable.Load(bundle);
            _nouns = NounCatalog.Load(bundle);
        }

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

    /// <summary>What the bag changed in the last <see cref="Evaluate"/>: the lines of the actions the bag cannot carry out and the NPC's answers to them; <see langword="null"/> when there is no bag or nothing missed.</summary>
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
        LastExchange = AnswerMisses(agentId, result, transcript, now);
        return result;
    }

    // The game's own answer to a purchase or a sale the bag cannot make (NpcService.AnswerMisses): the NPC takes the host event of the miss (cant_pay, cant_sell and the
    // rest) and says its line instead of the success line of the action. The bag is only read; nothing changes hands in the host.
    private ExchangeAnswer? AnswerMisses(string agentId, PipelineResult result, string transcript, StoredTimestamp now)
    {
        if (_bag is null || _hostEvents is null || _nouns is null)
        {
            return null;
        }

        var slots = NpcEffect.CountedSlots(result.Verdict.Slots, transcript);
        var unsaid = new List<string>();
        var lines = new List<string>();
        foreach (var action in result.Decision.Agent?.Actions ?? [])
        {
            if (action.HostEvent is not { } hostEvent || _hostEvents.Of(agentId, hostEvent).ExchangeMiss(slots, _bag, _nouns) is not { } miss)
            {
                continue;
            }

            var turn = _evaluator.Advance(NpcHostEvents.Of(agentId, miss), _context);
            if (turn is null)
            {
                continue; // an agent with no transition for the miss says its line as before
            }

            _agentStates[agentId] = turn.Agent.Next;
            _context = _context.Next(turn, now);
            unsaid.Add(action.DialogueIntent);
            lines.AddRange(turn.Response.Lines.Select(static line => line.Text));
        }

        return unsaid.Count == 0 ? null : new ExchangeAnswer(unsaid, lines);
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

/// <summary>What a bag made of a turn (<see cref="ConversationSession.LastExchange"/>): the dialogue intents whose lines are not said and the NPC's answers that follow.</summary>
/// <param name="Unsaid">The dialogue intents of the actions the bag cannot carry out; their lines (<c>&lt;intent&gt;.1</c>) are left out of the report.</param>
/// <param name="Lines">The NPC's answers to the misses.</param>
public sealed record ExchangeAnswer(IReadOnlyList<string> Unsaid, IReadOnlyList<string> Lines);
