using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Host;

/// <summary>
/// The player's conversation with a content bundle. Each typed line is evaluated on the prompt the player chose; the agent states, the
/// learner model and the repeat counts are carried from one utterance to the next in a <see cref="ConversationContext"/>.
/// </summary>
/// <remarks>
/// The word recognizer is the core's scripted one: the typed line is the transcript, so no model and no audio take part. The embedding
/// model and the synthesizer are the core's stand-ins. The host owns the state the core cannot know (the world seed, the stored time, the
/// learner model and the repeat counts), as docs/API.md describes.
/// </remarks>
public sealed class ConversationSession
{
    /// <summary>The agent id for a prompt that names no agent, as the core's own CLI uses.</summary>
    public const string NoAgentId = "player";

    /// <summary>The world seed of a new conversation. The host uses it until the game has save files.</summary>
    public const ulong DefaultWorldSeed = 42;

    private const string ScriptedModelHash = "scripted-transcript";

    private readonly IEmbeddingModel _embeddings = new FakeEmbeddingModel();
    private readonly ISpeechSynthesizer _synthesizer = new SilentSynthesizer();
    private readonly Dictionary<string, AgentState> _agentStates = new(StringComparer.Ordinal);
    private ConversationContext _context;

    /// <summary>Starts a conversation over the bundle: every agent in its initial state and a learner model with no evidence yet.</summary>
    public ConversationSession(ContentBundle bundle, ulong worldSeed = DefaultWorldSeed)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        Bundle = bundle;
        _context = ConversationContext.Create(
            worldSeed,
            NoAgentId,
            new AgentState(NoAgentId, "none", 0),
            new StoredTimestamp(0),
            LearnerModel.New());
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
    /// Evaluates one typed line on a prompt and carries the outcome into <see cref="Context"/>: the agent's new state, the learner model after
    /// this utterance and the repeat counts including it.
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

        var asr = new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, ScriptedModelHash, prompt.Mode);
        var services = new PipelineServices(new ScriptedWordRecognizer(asr), null, _embeddings, _synthesizer, Bundle);
        var result = new UtterancePipeline(services).Evaluate(PcmClip.FromSamples([]), prompt, utterance);

        var nextState = result.Decision.Agent?.Next ?? agentState;
        _agentStates[agentId] = nextState;
        _context = ConversationContext.Create(
            _context.WorldSeed,
            agentId,
            nextState,
            now,
            result.Decision.Learner,
            NextRepeatCounts(_context.RepeatCountsByTag, result.Verdict));
        return result;
    }

    /// <summary>
    /// The repeat counts after one verdict. Each error tag in the verdict, and <c>not_communicated</c> when the player was not understood,
    /// counts one more than before; a tag the verdict does not carry starts again from zero, because the counts are "in a row".
    /// </summary>
    public static IReadOnlyDictionary<string, int> NextRepeatCounts(IReadOnlyDictionary<string, int> before, Verdict verdict)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(verdict);
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var error in verdict.Errors)
        {
            tags.Add(error.Tag);
        }

        if (!verdict.Communicated)
        {
            tags.Add(FeedbackPolicy.NotCommunicatedTag);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            counts[tag] = (before.TryGetValue(tag, out var seen) ? seen : 0) + 1;
        }

        return counts;
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
