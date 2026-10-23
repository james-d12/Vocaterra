using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game;

/// <summary>
/// What the player sees when the panel opens on an NPC: the lines the NPC says first. When the NPC's state has a transition for the opening host
/// event the lines come from a core turn (<c>UtterancePipeline.Advance</c>, CR-015), which is in the replay record; otherwise the line is the
/// text of the prompt the NPC is waiting on.
/// </summary>
/// <param name="Npc">The NPC's agent id.</param>
/// <param name="Prompt">The prompt the NPC's next question is evaluated on.</param>
/// <param name="Lines">What the NPC says, in order.</param>
/// <param name="HostTurn">Whether the lines came from a core host turn (otherwise they are the prompt's text).</param>
/// <param name="HostEvents">The host events the host turn raised (<c>stall.give_water</c> for the fail-forward opening).</param>
/// <param name="FailForward">Whether the opening handed over the bottle without a request (the shopkeeper's second visit).</param>
public sealed record NpcOpening(string Npc, string Prompt, IReadOnlyList<string> Lines, bool HostTurn, IReadOnlyList<string> HostEvents, bool FailForward);

/// <summary>Where a line sent to an NPC came from, when it was not recognized from speech.</summary>
public enum LineOrigin
{
    /// <summary>The player typed it.</summary>
    Typed,

    /// <summary>The player picked it among the choices the repair ladder offered (a recognition signal; S7).</summary>
    Choice,

    /// <summary>A speech button sent it: <c>Ayuda</c> sends "ayuda", <c>No sé</c> sends "no sé" (S7).</summary>
    Button,
}

/// <summary>
/// An NPC's conversation state at one moment, kept so that a turn the player cancelled can be undone: the turn ran on the worker (it cannot be
/// stopped half way), so when it finishes the worker rolls the NPC back and the turn never happened (S7). Opaque outside <see cref="NpcService"/>.
/// </summary>
public sealed class NpcCheckpoint
{
    internal NpcCheckpoint(string npc, ConversationContext context, int visits, int unproductive, int uncertain, bool helped, bool bottleGiven, bool offered, RepairKind? lastRepair, int records)
    {
        Npc = npc;
        Context = context;
        Visits = visits;
        Unproductive = unproductive;
        Uncertain = uncertain;
        Helped = helped;
        BottleGiven = bottleGiven;
        Offered = offered;
        LastRepair = lastRepair;
        Records = records;
    }

    internal string Npc { get; }

    internal ConversationContext Context { get; }

    internal int Visits { get; }

    internal int Unproductive { get; }

    internal int Uncertain { get; }

    internal bool Helped { get; }

    internal bool BottleGiven { get; }

    internal bool Offered { get; }

    internal RepairKind? LastRepair { get; }

    internal int Records { get; }
}

/// <summary>
/// The conversations with the NPCs of the route, one agent each (the guide, the shopkeeper and the villager): a
/// <see cref="ConversationContext"/> per agent, the opening line through <c>pipeline.Advance</c> (CR-015), and the turn in two phases
/// (<c>Analyze</c> once, then <c>Complete</c> with the host's facts, CR-017). It generalises the guide's conversation of S3: her world lines
/// are in <c>NpcService.Guide.cs</c> and behave as they did.
/// </summary>
/// <remarks>
/// <para>
/// <b>A turn.</b> The line is analysed once (typed: the core's scripted recognizer; spoken: Vosk, one decode). The guide's turn then completes the
/// analysis once to see which host events the verdict raises, computes the facts of her answer (the place, the direction, the recipe) and
/// completes it again with those facts as host slots and the word skills as extra skills, so the learner is updated once and the replay record
/// holds the inputs of her line; the shopkeeper and the villager say only slot-free lines and complete the analysis once. After <c>Next</c> the
/// agent's context takes the simulation's digest again, so the next turn starts from the state the world was in when the line was sent.
/// </para>
/// <para>
/// <b>The host decides what the world does with an event</b> (<see cref="NpcHostEvents"/>), never this class: it reports the events a turn raised
/// and the session applies them on the game's thread. What this class does decide is the shopkeeper's fail-forward (rule 8, nothing is gated
/// behind language): when the stall's exchange ends without a request (the repair ladder gives up, the learner has asked for help and then fails
/// again, or four lines in a row were no request) or the player walks away and comes back, the shopkeeper still hands over the bottle with the
/// core's own line, as a host turn (<c>shopkeeper.give</c>) that is in the replay record.
/// </para>
/// <para>
/// <b>Threads.</b> The turn worker is the only thread that calls <see cref="Say(string, GuideScene, StoredTimestamp, string)"/> and the
/// <c>SaySpoken</c> methods once the worker has started; <see cref="Open"/> runs on the game's thread when no turn is pending
/// (<c>GuideSession.OpenTalk</c> checks). Nothing here reads the clock or draws a random number of its own: the stored time and the state
/// hash come from the caller, and the variant of the game's own lines is drawn from the core's <see cref="SeededRng"/> keyed by the world seed
/// and the interaction counter.
/// </para>
/// </remarks>
public sealed partial class NpcService
{
    /// <summary>The guide's agent id in the bundle.</summary>
    public const string GuideAgent = "guide";

    /// <summary>The shopkeeper's agent id.</summary>
    public const string ShopkeeperAgent = "shopkeeper";

    /// <summary>The villager's agent id.</summary>
    public const string VillagerAgent = "villager";

    /// <summary>The bundle's prompt for talking to the guide.</summary>
    public const string GuidePrompt = "guide-open";

    /// <summary>The bundle's prompt for the guide's review question ("¿Qué es esto?"): the answer to a review is evaluated on it.</summary>
    public const string GuideReviewPrompt = "guide-review";

    /// <summary>The bundle's prompt for the guide's transfer offer ("¿Quieres agua?").</summary>
    public const string GuideOfferPrompt = "guide-offer";

    /// <summary>The bundle's prompt for the shopkeeper's opening question ("¿Qué necesitas?").</summary>
    public const string StallNeedPrompt = "stall-need";

    /// <summary>The bundle's prompt for the shopkeeper's bread offer ("¿Quieres pan?").</summary>
    public const string StallOfferPrompt = "stall-offer";

    /// <summary>The bundle's prompt for the villager.</summary>
    public const string VillagerPrompt = "villager-open";

    /// <summary>The NPC agents, in ordinal order.</summary>
    public static IReadOnlyList<string> AgentIds { get; } = [GuideAgent, ShopkeeperAgent, VillagerAgent];

    /// <summary>The random stream of the game's own lines is keyed by this id (the core's own draws use the agent's id).</summary>
    private const string HostLineStream = "guide.host";

    /// <summary>The random stream of the line an NPC says when the recognizer was not sure (S7), keyed by the agent's interaction counter and how many such lines it has said.</summary>
    private const string UncertainStream = "guide.uncertain";

    /// <summary>The bundle pool of the line an NPC says when the recognizer was not sure: "No te entendí bien. ¿Lo dices otra vez?".</summary>
    private const string UncertainPool = "guide.uncertain";

    /// <summary>The skill id prefix of a catalogue noun's learner skill (CR-005): <c>word.árbol</c>.</summary>
    private const string WordSkillPrefix = "word.";

    /// <summary>The skill id prefix of a phrase's learner skill (S8): <c>phrase.pedir-agua</c>.</summary>
    public const string PhraseSkillPrefix = "phrase.";

    /// <summary>The failed lines in a row at the stall after which the shopkeeper hands the bottle over anyway (four is also where the core's repair ladder gives up).</summary>
    private const int StallPatience = 4;

    /// <summary>The lines after a request for help at the stall after which the shopkeeper hands the bottle over anyway (the model answer was heard once).</summary>
    private const int StallPatienceAfterHelp = 2;

    private readonly ContentBundle _bundle;
    private readonly TypedUtteranceEvaluator _typed;
    private readonly IGuideFacts _worldFacts;
    private readonly NounCatalog _nouns;
    private readonly RecipeBook _recipes;
    private readonly PhraseSheet _phrases;
    private readonly Dictionary<string, Agent> _agents = new(StringComparer.Ordinal);
    private readonly List<ReplayEntry> _records = [];
    private volatile SpokenUtteranceEvaluator? _spoken;
    private IGuideFacts _facts;

    /// <summary>One NPC's conversation state: its context between turns and the small facts the host decisions use.</summary>
    private sealed class Agent(string id, ConversationContext context)
    {
        private volatile ConversationContext _context = context;

        public string Id { get; } = id;

        public ConversationContext Context
        {
            get => _context;
            set => _context = value;
        }

        public int Visits { get; set; }

        public int Unproductive { get; set; }

        public bool Helped { get; set; }

        public bool BottleGiven { get; set; }

        public bool Offered { get; set; }

        public int Uncertain { get; set; }

        public RepairKind? LastRepair { get; set; }
    }

    /// <summary>Starts the conversations: each NPC in its initial state, a learner model with no evidence and no repeat counts.</summary>
    /// <param name="bundle">The content bundle; it must hold the three agents and their prompts.</param>
    /// <param name="facts">The world the guide speaks of.</param>
    /// <param name="nouns">The noun catalogue of the same bundle: the nouns a turn exercises get their learner skills.</param>
    /// <param name="worldSeed">The world seed, which seeds every random draw with the agent id and the interaction counter.</param>
    /// <param name="start">The stored time before the first turn.</param>
    /// <param name="recipes">The bundle's recipes, for the crafting answers; read from the bundle when <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The bundle lacks an agent or a prompt of the route.</exception>
    public NpcService(ContentBundle bundle, IGuideFacts facts, NounCatalog nouns, ulong worldSeed, StoredTimestamp start, RecipeBook? recipes = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(nouns);
        foreach (var prompt in new[] { GuidePrompt, GuideReviewPrompt, GuideOfferPrompt, StallNeedPrompt, StallOfferPrompt, VillagerPrompt })
        {
            if (!bundle.Prompts.ContainsKey(prompt))
            {
                throw new ArgumentException($"The bundle has no prompt '{prompt}'.", nameof(bundle));
            }
        }

        _bundle = bundle;
        _worldFacts = facts;
        _facts = facts;
        _nouns = nouns;
        _recipes = recipes ?? RecipeBook.Load(bundle, nouns);
        _typed = new TypedUtteranceEvaluator(bundle);
        _phrases = PhraseSheet.Load(bundle);
        foreach (var id in AgentIds)
        {
            if (!bundle.Agents.TryGetValue(id, out var machine))
            {
                throw new ArgumentException($"The bundle has no agent '{id}'.", nameof(bundle));
            }

            var context = ConversationContext.Create(worldSeed, id, machine.InitialState(), start, LearnerModel.New(), new Dictionary<string, int>(StringComparer.Ordinal), SlotMap.Empty, []);
            _agents.Add(id, new Agent(id, context));
        }
    }

    /// <summary>How many replay entries the conversations have recorded so far (the session log numbers them; S11).</summary>
    public int RecordCount
    {
        get
        {
            lock (_records)
            {
                return _records.Count;
            }
        }
    }

    /// <summary>Whether spoken lines can be evaluated (<see cref="AttachSpeech"/> was called).</summary>
    public bool CanHear => _spoken is not null;

    /// <summary>
    /// Gives the NPCs the means to evaluate spoken lines. Call it once, from the thread that will call the <c>SaySpoken</c> methods, after the
    /// recognizer has loaded.
    /// </summary>
    /// <param name="evaluator">The spoken-line evaluator over the loaded recognizer.</param>
    public void AttachSpeech(SpokenUtteranceEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        _spoken = evaluator;
    }

    /// <summary>A copy of the replay entries so far, in order: every utterance's envelope and every host turn's envelope (S11 writes them into the session log).</summary>
    public IReadOnlyList<ReplayEntry> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    /// <summary>The guide's context: the state she is in, the learner model and the repeat counts (the learner model is the guide's; each agent keeps its own).</summary>
    public ConversationContext Context => _agents[GuideAgent].Context;

    /// <summary>The guide's current state in her state machine.</summary>
    public AgentState GuideState => _agents[GuideAgent].Context.AgentState;

    /// <summary>The context the next turn with an NPC starts from.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    public ConversationContext ContextOf(string npc) => AgentOf(npc).Context;

    /// <summary>The line the guide says when the dialogue opens (<c>PromptSpec.Text</c> of her prompt).</summary>
    public string OpeningLine => _bundle.Prompts[GuidePrompt].Text ?? string.Empty;

    /// <summary>The prompt the NPC's next line is evaluated on, from its current state.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="review">Whether the guide's review question is open (her review prompt).</param>
    public string PromptOf(string npc, bool review = false) => PromptFor(AgentOf(npc), review);

    /// <summary>The text of the prompt the NPC is waiting on (what it says when nothing else opens the talk).</summary>
    /// <param name="npc">The NPC's agent id.</param>
    public string PromptTextOf(string npc) => _bundle.Prompts[PromptOf(npc)].Text ?? string.Empty;

    /// <summary>
    /// The guide's review question, drawn from the bundle's <c>guide.review.ask</c> pool: the variant depends on the world seed and the
    /// interaction counter, so the same conversation gives the same question. It is a host line outside the core's replay record.
    /// </summary>
    public string ReviewQuestion()
    {
        var context = _agents[GuideAgent].Context;
        var rng = SeededRng.ForInteraction(context.WorldSeed, ReviewStream, context.AgentState.InteractionCounter);
        return Render(ReviewAsk, SlotMap.Empty, rng);
    }

    /// <summary>
    /// Runs one throwaway evaluation so that the first real turn does not pay for the pipeline's first-call costs (JIT, lazy tables) in the
    /// middle of a frame. It changes no state: the conversations, the learner model and the repeat counts stay as they were. Call it while a
    /// loading screen is up.
    /// </summary>
    public void WarmUp()
    {
        var context = _agents[GuideAgent].Context;
        var analysis = _typed.Analyze("hola", _bundle.Prompts[GuidePrompt]);
        _ = _typed.CompleteTrace(analysis, context);
    }

    /// <summary>
    /// Runs one throwaway spoken evaluation of half a second of silence, so that the first real clip does not pay for Vosk's first decode and
    /// the pipeline's first spoken call. It changes no state. Call it from the speech thread, after <see cref="AttachSpeech"/>.
    /// </summary>
    public void WarmUpSpeech()
    {
        var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached.");
        var analysis = spoken.Analyze(PcmClip.FromSamples(new short[AudioClips.SampleRate / 2]), _bundle.Prompts[GuidePrompt]);
        _ = spoken.CompleteTrace(analysis, _agents[GuideAgent].Context);
    }

    /// <summary>
    /// The player starts talking to an NPC. When the NPC's state has a transition for the opening event the game picks (the shopkeeper's
    /// "¿Qué necesitas?", the villager's "Hola.", the guide's transfer offer), the NPC takes it through <c>pipeline.Advance</c> and the opening is
    /// a core turn in <see cref="Records"/>; otherwise the opening is the prompt's text. Call it between turns, never while a turn is on the worker.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="objectiveComplete">Whether the camp objective is complete (the guide then makes her transfer offer, once).</param>
    /// <param name="now">The stored time.</param>
    /// <param name="hostStateHash">The simulation's state digest.</param>
    public NpcOpening Open(string npc, bool objectiveComplete, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(hostStateHash);
        var agent = AgentOf(npc);
        agent.Visits++;
        var hostEvent = OpeningEvent(agent, objectiveComplete);
        if (hostEvent is not null)
        {
            var before = agent.Context with { Now = now, HostStateHash = hostStateHash };
            var turn = _typed.Advance(hostEvent, before);
            if (turn is not null)
            {
                Record(new ReplayEntry(null, HostTurnEnvelope.From(turn, before)));
                agent.Context = before.Next(turn, now) with { HostStateHash = hostStateHash };
                var events = HostEventsOf(turn.Agent.Actions);
                Note(agent, hostEvent, events);
                return new NpcOpening(
                    npc,
                    PromptFor(agent, false),
                    turn.Response.Lines.Select(static l => l.Text).ToList(),
                    true,
                    events,
                    string.Equals(hostEvent, NpcHostEvents.ShopkeeperGive, StringComparison.Ordinal));
            }
        }

        var prompt = PromptFor(agent, false);
        return new NpcOpening(npc, prompt, [_bundle.Prompts[prompt].Text ?? string.Empty], false, [], false);
    }

    /// <summary>
    /// Evaluates one typed line to the guide and returns the turn. The conversation moves on whatever the verdict: an unclear line also counts, as
    /// a repeated mistake or a not-communicated attempt.
    /// </summary>
    /// <param name="line">What the player typed.</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn (the game's start time plus its ticks; never the clock).</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state, copied into the replay record (the simulation's state digest).</param>
    public GuideTurn Say(string line, GuideScene scene, StoredTimestamp now, string hostStateHash) => Say(GuideAgent, line, scene, now, hostStateHash);

    /// <summary>Evaluates one typed line to an NPC and returns the turn (see the guide's overload).</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">What the player typed.</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn.</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state.</param>
    /// <param name="origin">Where the line came from: typed (the default), a choice the repair ladder offered, or a speech button (S7).</param>
    public GuideTurn Say(string npc, string line, GuideScene scene, StoredTimestamp now, string hostStateHash, LineOrigin origin = LineOrigin.Typed)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Take(npc, line, null, scene, now, hostStateHash, origin);
    }

    /// <summary>
    /// Recognizes and evaluates one spoken line to the guide and returns the turn, whose <see cref="GuideTurn.Line"/> is what the recognizer
    /// heard. The recognizer decodes the clip once. The conversation moves on whatever the verdict.
    /// </summary>
    /// <param name="clip">The recorded or synthesized clip, 16 kHz mono (see <see cref="AudioClips.Prepare"/>).</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn.</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state.</param>
    /// <exception cref="InvalidOperationException">No speech is attached (<see cref="AttachSpeech"/>).</exception>
    public GuideTurn SaySpoken(PcmClip clip, GuideScene scene, StoredTimestamp now, string hostStateHash) => SaySpoken(GuideAgent, clip, scene, now, hostStateHash);

    /// <summary>Recognizes and evaluates one spoken line to an NPC and returns the turn (see the guide's overload).</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="clip">The recorded or synthesized clip, 16 kHz mono.</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn.</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state.</param>
    /// <exception cref="InvalidOperationException">No speech is attached (<see cref="AttachSpeech"/>).</exception>
    public GuideTurn SaySpoken(string npc, PcmClip clip, GuideScene scene, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Take(npc, null, clip, scene, now, hostStateHash);
    }

    private Agent AgentOf(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        return _agents.TryGetValue(npc, out var agent) ? agent : throw new ArgumentException($"There is no NPC '{npc}'.", nameof(npc));
    }

    private string PromptFor(Agent agent, bool review) => PromptAfter(agent.Id, agent.Context.AgentState.StateId, review);

    /// <summary>
    /// The prompt an NPC waits on when it is in a given state: the shopkeeper's bread offer and the guide's transfer offer in <c>offering</c>, the
    /// stall's question otherwise. The session uses it to know the prompt after an applied turn (the turn carries the state it left the NPC in).
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="stateId">The NPC's state id (<see cref="GuideTurn.AgentState"/>).</param>
    /// <param name="review">Whether the guide's review question is open (her review prompt).</param>
    public static string PromptAfter(string npc, string stateId, bool review = false)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(stateId);
        return npc switch
        {
            GuideAgent => review ? GuideReviewPrompt : string.Equals(stateId, "offering", StringComparison.Ordinal) ? GuideOfferPrompt : GuidePrompt,
            ShopkeeperAgent => string.Equals(stateId, "offering", StringComparison.Ordinal) ? StallOfferPrompt : StallNeedPrompt,
            _ => VillagerPrompt,
        };
    }

    /// <summary>
    /// Steps learner skills of an NPC's learner model without an utterance (S8: "the player was shown this phrase's card", the core's
    /// <c>LearnerModel.StepSkills</c>). Call it between turns, never while a turn is on the worker.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="skills">The skill ids (<c>phrase.pedir-agua</c>).</param>
    /// <param name="correct">Whether the learner is credited with them.</param>
    public void StepSkills(string npc, IReadOnlyList<string> skills, bool correct)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var agent = AgentOf(npc);
        agent.Context = agent.Context with { Learner = agent.Context.Learner.StepSkills(skills, correct) };
    }

    // The host event the NPC opens the talk with, or null when the prompt's text is the opening.
    private string? OpeningEvent(Agent agent, bool objectiveComplete)
    {
        var state = agent.Context.AgentState.StateId;
        switch (agent.Id)
        {
            case GuideAgent:
                return objectiveComplete && !agent.Offered ? NpcHostEvents.GuideOffer : null;
            case ShopkeeperAgent:
                if (agent.Visits <= 1)
                {
                    return string.Equals(state, "idle", StringComparison.Ordinal) ? NpcHostEvents.ShopkeeperAsk : null;
                }

                // Walking away and coming back: the bottle is never withheld. The shopkeeper hands it over as a core turn; once it has been
                // given, the talk picks up where the exchange was (the question again, or the pending bread offer).
                if (!agent.BottleGiven)
                {
                    return NpcHostEvents.ShopkeeperGive;
                }

                return state switch
                {
                    "done" => NpcHostEvents.ShopkeeperAsk,
                    "offering" => NpcHostEvents.ShopkeeperOffer,
                    _ => null,
                };
            default:
                return NpcHostEvents.VillagerGreet; // the villager says "Hola." on every visit, from either of his states
        }
    }

    // What the host remembers about the events a host turn or a turn raised.
    private static void Note(Agent agent, string hostEvent, IReadOnlyList<string> events)
    {
        if (events.Contains(NpcHostEvents.GiveWater, StringComparer.Ordinal))
        {
            agent.BottleGiven = true;
            agent.Unproductive = 0;
            agent.Helped = false;
        }

        if (string.Equals(hostEvent, NpcHostEvents.GuideOffer, StringComparison.Ordinal))
        {
            agent.Offered = true;
        }
    }

    private GuideTurn Take(string npc, string? typed, PcmClip? clip, GuideScene scene, StoredTimestamp now, string hostStateHash, LineOrigin origin = LineOrigin.Typed)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(hostStateHash);
        var agent = AgentOf(npc);
        var isGuide = string.Equals(npc, GuideAgent, StringComparison.Ordinal);
        var promptId = scene.PromptOverride ?? PromptFor(agent, scene.Review);
        var prompt = _bundle.Prompts[promptId];
        var utterance = agent.Context with { Now = now, HostStateHash = hostStateHash };
        if (isGuide)
        {
            _facts = scene.Facts ?? _worldFacts; // the world as it was when the player spoke, not as it is when the worker gets to the turn
        }

        // Phase 1: the line is read once. A spoken line is decoded here and nowhere else.
        UtteranceAnalysis analysis;
        if (clip is null)
        {
            analysis = _typed.Analyze(typed!, prompt);
        }
        else
        {
            var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached to the NPCs.");
            analysis = spoken.Analyze(clip, prompt);
        }

        var line = CleanTranscript(analysis.Asr.Transcript);
        var verdict = analysis.Verdict;
        double? confidence = clip is null ? null : UncertaintyRule.MeanWordConfidence(analysis.Asr);
        if (confidence is { } sure && UncertaintyRule.IsTooUnsure(verdict.Communicated, sure))
        {
            // The core would understand the line but the recognizer was not sure of its words (D-game-9): ask again and let nothing happen. The
            // turn is not completed, so the conversation, the learner, the repeat counts and the world stay as they were.
            return Unsure(agent, npc, promptId, line, sure, utterance, hostStateHash);
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        string? worldLine = null;
        GuidePointer? pointer = null;
        string? identified = null;
        var naming = NameCheck.None;
        string? nameTarget = null;
        if (isGuide && verdict.Communicated)
        {
            // The facts of the guide's answer depend on which of her host events the verdict raises, which only phase 2 shows: complete once
            // without facts to see them, then again with the facts. Phase 2 is pure, so this repeats nothing of the recognition or the analysis.
            var probe = Complete(clip is null, analysis, utterance).Result;
            var rng = SeededRng.ForInteraction(utterance.WorldSeed, HostLineStream, utterance.AgentState.InteractionCounter);
            foreach (var hostEvent in HostEventsOf(probe))
            {
                switch (hostEvent)
                {
                    case GuideEvents.Point:
                        (worldLine, pointer) = Point(verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Craft:
                        worldLine = Craft(verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Find:
                        (worldLine, pointer) = Find(verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Identify:
                        (worldLine, identified) = Identify(scene, rng, facts);
                        break;
                    case GuideEvents.Help:
                        worldLine = Render(scene.PointerActive ? HelpWalk : HelpAsk, SlotMap.Empty, rng);
                        break;
                    case GuideEvents.Name:
                        (worldLine, naming, nameTarget) = Name(verdict, scene, rng, facts);
                        break;
                    default:
                        break;
                }
            }
        }

        // Phase 2: the decision, the response and the replay record, with the host's facts and the word skills in the context. The learner is
        // updated once, here. The digest covers the slots and skills (HostDigest, the core's contract); without them the context keeps the bare
        // digest, as the core does when no slots are given.
        var skills = verdict.Communicated ? WordSkillsOf(GuideWord.From(analysis.Words), PhraseSkillsOf(verdict, promptId, origin)) : [];
        var created = ConversationContext.Create(
            utterance.WorldSeed,
            utterance.AgentId,
            utterance.AgentState,
            now,
            utterance.Learner,
            utterance.RepeatCountsByTag,
            SlotMap.From(facts),
            skills);
        var digest = facts.Count == 0 && skills.Count == 0 ? hostStateHash : HostDigest.Of(hostStateHash, facts, skills);
        var carried = created with { HostStateHash = digest };
        var trace = Complete(clip is null, analysis, carried);
        var result = trace.Result;
        Record(new ReplayEntry(ReplayEnvelope.From(trace, carried), null));
        var uncertain = confidence is not null && UncertaintyRule.IsUncertain(verdict.Communicated, confidence);
        agent.Context = carried.Next(result, now) with { HostStateHash = hostStateHash }; // the digest of the world again, not the core's hash of the values
        if (uncertain)
        {
            // A spoken line the NPC did not understand is the recognition's trouble before it is the language's (D-game-9): the repeat counts
            // move on (the repair ladder needs them) but the learner model is not marked down for it.
            agent.Context = agent.Context with { Learner = utterance.Learner };
            agent.Uncertain++;
        }

        var hostEvents = HostEventsOf(result);
        Note(agent, string.Empty, hostEvents);
        var holdsBottle = scene.Inventory is { } inventory
            && (inventory.Count(Simulation.Items.ItemId.Bottle) + inventory.Count(Simulation.Items.ItemId.WaterBottle)) > 0;
        string? feedback = null;
        var replies = new List<string>();
        foreach (var responseLine in result.Response.Lines)
        {
            if (responseLine.IsRecast || responseLine.LineId.StartsWith(FeedbackLinePrefix, StringComparison.Ordinal))
            {
                feedback ??= responseLine.Text;
            }
            else if (holdsBottle && hostEvents.Contains(NpcHostEvents.GiveWater, StringComparer.Ordinal)
                && responseLine.LineId.StartsWith("shop_give.", StringComparison.Ordinal))
            {
                // The shopkeeper's line gives the bottle; the player has it already, so nothing is given and the line is not said.
            }
            else
            {
                replies.Add(responseLine.Text);
            }
        }

        var followUp = new List<string>();
        var failForward = false;
        if (string.Equals(npc, ShopkeeperAgent, StringComparison.Ordinal) && string.Equals(promptId, StallNeedPrompt, StringComparison.Ordinal))
        {
            failForward = ShopkeeperPatience(agent, verdict, result, now, hostStateHash, hostEvents, followUp);
        }

        agent.LastRepair = result.Repair?.Kind;
        IReadOnlyList<string>? choices = result.Repair is { Kind: RepairKind.OfferChoices, RetryRequest: { } retry } ? retry.GrammarPhrases : null;
        return new GuideTurn(
            line,
            verdict.Intent.Value,
            verdict.Communicated,
            verdict.Correctness,
            verdict.Errors.Select(static e => new GuideError(e.Tag, e.Expected, e.Actual, e.Span)).ToList(),
            feedback,
            replies,
            result.Repair?.AgentLine,
            agent.Context.AgentState.StateId,
            hostEvents,
            worldLine,
            pointer,
            identified,
            result.Replay.HostStateHash,
            verdict.Slots,
            naming,
            nameTarget,
            GuideWord.From(result),
            clip is not null ? InputKind.Spoken : origin == LineOrigin.Typed ? InputKind.Typed : InputKind.Button,
            confidence,
            npc,
            promptId,
            result.Repair?.Kind,
            followUp,
            failForward,
            choices,
            uncertain,
            origin == LineOrigin.Choice);
    }

    // The stall's fail-forward (rule 8). After a line at the stall that did not ask for anything (the repair ladder's next step, a help request, a
    // thanks), count it; when the exchange has run out of road the shopkeeper hands the bottle over with the core's own line, as a host turn.
    private bool ShopkeeperPatience(
        Agent agent,
        Verdict verdict,
        PipelineResult result,
        StoredTimestamp now,
        string hostStateHash,
        List<string> hostEvents,
        List<string> followUp)
    {
        if (agent.BottleGiven)
        {
            return false;
        }

        agent.Unproductive++;
        if (verdict.Communicated && (string.Equals(verdict.Intent.Value, "ask_help", StringComparison.Ordinal) || string.Equals(verdict.Intent.Value, "dont_know", StringComparison.Ordinal)))
        {
            agent.Helped = true;
        }

        var spent = result.Repair?.Kind == RepairKind.GiveUp
            || agent.Unproductive >= StallPatience
            || (agent.Helped && agent.Unproductive >= StallPatienceAfterHelp);
        if (!spent)
        {
            return false;
        }

        var before = agent.Context;
        var turn = _typed.Advance(NpcHostEvents.ShopkeeperGive, before);
        if (turn is null)
        {
            return false;
        }

        Record(new ReplayEntry(null, HostTurnEnvelope.From(turn, before)));
        agent.Context = before.Next(turn, now) with { HostStateHash = hostStateHash };
        var events = HostEventsOf(turn.Agent.Actions);
        Note(agent, NpcHostEvents.ShopkeeperGive, events);
        followUp.AddRange(turn.Response.Lines.Select(static l => l.Text));
        hostEvents.AddRange(events);
        return true;
    }

    private void Record(ReplayEntry entry)
    {
        lock (_records)
        {
            _records.Add(entry);
        }
    }

    private PipelineTrace Complete(bool typed, UtteranceAnalysis analysis, ConversationContext context)
    {
        if (typed)
        {
            return _typed.CompleteTrace(analysis, context);
        }

        var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached to the NPCs.");
        return spoken.CompleteTrace(analysis, context);
    }

    // The transcript as the player sees it: the recognizer's [unk] marks (speech outside the grammar) are not words.
    private static string CleanTranscript(string transcript) =>
        string.Join(' ', transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // The turn for a spoken line the recognizer was not sure of: the NPC asks again from the bundle's guide.uncertain pool, nothing else happens.
    private GuideTurn Unsure(Agent agent, string npc, string promptId, string line, double confidence, ConversationContext utterance, string hostStateHash)
    {
        var rng = SeededRng.ForInteraction(utterance.WorldSeed, UncertainStream, utterance.AgentState.InteractionCounter + agent.Uncertain);
        agent.Uncertain++;
        return new GuideTurn(
            line,
            "unknown",
            false,
            0,
            [],
            null,
            [Render(UncertainPool, SlotMap.Empty, rng)],
            null,
            agent.Context.AgentState.StateId,
            [],
            null,
            null,
            null,
            hostStateHash,
            SlotMap.Empty,
            Input: InputKind.Spoken,
            Confidence: confidence,
            Npc: npc,
            Prompt: promptId,
            Uncertain: true);
    }

    /// <summary>
    /// Takes a checkpoint of an NPC's conversation, for <see cref="Rollback"/>. The turn worker takes one before each turn it evaluates, so a turn
    /// the player cancels while it runs can be undone.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    public NpcCheckpoint Checkpoint(string npc)
    {
        var agent = AgentOf(npc);
        lock (_records)
        {
            return new NpcCheckpoint(npc, agent.Context, agent.Visits, agent.Unproductive, agent.Uncertain, agent.Helped, agent.BottleGiven, agent.Offered, agent.LastRepair, _records.Count);
        }
    }

    /// <summary>
    /// Puts an NPC back as it was at the checkpoint: its state, its learner model, its repeat counts, the host decisions it remembers (the bottle,
    /// the offer, the patience) and the replay records, so a cancelled turn leaves no trace in the conversation. Call it from the thread that
    /// evaluated the turn, before anything else is evaluated.
    /// </summary>
    /// <param name="checkpoint">The checkpoint taken before the turn.</param>
    public void Rollback(NpcCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var agent = AgentOf(checkpoint.Npc);
        agent.Context = checkpoint.Context;
        agent.Visits = checkpoint.Visits;
        agent.Unproductive = checkpoint.Unproductive;
        agent.Uncertain = checkpoint.Uncertain;
        agent.Helped = checkpoint.Helped;
        agent.BottleGiven = checkpoint.BottleGiven;
        agent.Offered = checkpoint.Offered;
        agent.LastRepair = checkpoint.LastRepair;
        lock (_records)
        {
            if (_records.Count > checkpoint.Records)
            {
                _records.RemoveRange(checkpoint.Records, _records.Count - checkpoint.Records);
            }
        }
    }

    private static List<string> HostEventsOf(PipelineResult result) => HostEventsOf(result.Decision.Agent?.Actions ?? []);

    private static List<string> HostEventsOf(IReadOnlyList<AgentAction> actions) =>
        actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();

    // The learner skills of the phrases in play in a turn (S8): the learner phrase the line used and the phrase of the prompt the NPC asked, both only
    // when the line was understood and had no mistake; a choice picked from the offered buttons is a recognition signal and exercises no skill; the
    // service words (help, "no sé") exercise none either.
    private List<string> PhraseSkillsOf(Verdict verdict, string promptId, LineOrigin origin)
    {
        var skills = new List<string>();
        if (!verdict.Communicated || verdict.Errors.Count > 0 || origin == LineOrigin.Choice
            || string.Equals(verdict.Intent.Value, "ask_help", StringComparison.Ordinal) || string.Equals(verdict.Intent.Value, "dont_know", StringComparison.Ordinal))
        {
            return skills;
        }

        if (PhraseIds.For(verdict.Intent.Value, verdict.Slots) is { } used)
        {
            skills.Add(PhraseSkillPrefix + used);
        }

        foreach (var row in _phrases.Rows)
        {
            if (string.Equals(row.Says, "hears", StringComparison.Ordinal) && string.Equals(row.Prompt, promptId, StringComparison.Ordinal))
            {
                skills.Add(PhraseSkillPrefix + row.Id); // the learner understood what the NPC asked and answered it
            }
        }

        return skills;
    }

    // The learner skill of each catalogue noun the line's words are read as (CR-005), and any extra skills: one each, in ordinal order.
    private List<string> WordSkillsOf(IReadOnlyList<GuideWord> words, IReadOnlyList<string>? extra = null)
    {
        var skills = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var skill in extra ?? [])
        {
            skills.Add(skill);
        }

        foreach (var word in words)
        {
            var noun = _nouns.ForReading(word.Lemma, word.Pos);
            if (noun is not null)
            {
                skills.Add(WordSkillPrefix + noun.Lemma);
            }
        }

        return [.. skills];
    }
}
