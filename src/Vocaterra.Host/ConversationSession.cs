using Vocaterra.Game.Core.Language.Speech;
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
        IReadOnlyDictionary<string, string>? listenerNames = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        Bundle = bundle;
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

        // The listener's name said as a vocative is taken out (V14), and a greeting that opens a line with something else to say is answered by the rest (V15), as in the game.
        var bare = Vocative.Without(transcript, _names.GetValueOrDefault(agentId));
        var result = _evaluator.Evaluate(bare, prompt, utterance);
        if (Greeting.Rest(bare) is { } rest && _evaluator.Evaluate(rest, prompt, utterance) is var asked && Greeting.AnswersRest(result.Verdict, asked.Verdict, prompt))
        {
            result = asked;
        }

        _agentStates[agentId] = result.Decision.Agent?.Next ?? agentState;
        _context = utterance.Next(result, now);
        return result;
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
