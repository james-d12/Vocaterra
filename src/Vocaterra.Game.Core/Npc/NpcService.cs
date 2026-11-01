using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Npc;

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
/// <param name="Delivered">Whether the opening was the NPC's thanks for an order the player brought (VILLAGE_PLAN V03, <c>&lt;npc&gt;.delivered</c>).</param>
public sealed record NpcOpening(string Npc, string Prompt, IReadOnlyList<string> Lines, bool HostTurn, IReadOnlyList<string> HostEvents, bool FailForward, bool Delivered = false);

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
/// The conversations with the NPCs, one agent each (the NPCs of <c>game/npcs.json</c>: the guide, the shopkeeper, the villager and the village's fisher, baker,
/// smith and farmer, VILLAGE_PLAN V10 to V12): a
/// <see cref="ConversationContext"/> per agent, the opening line through <c>pipeline.Advance</c> (CR-015), and the turn in two phases
/// (<c>Analyze</c> once, then <c>Complete</c> with the host's facts, CR-017). It generalises the guide's conversation of S3: her world lines
/// are in <c>NpcService.Guide.cs</c> and behave as they did. Which prompt an NPC waits on in each state and how it opens a talk are its NPC file's
/// (<see cref="NpcEntry.Prompts"/>, <see cref="NpcEntry.Opening"/>; VILLAGE_PLAN V01), so a new NPC needs no code here.
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
/// core's own line, as a host turn (<c>shopkeeper.give</c>) that is in the replay record. And when an event asks for what the world of the turn cannot do
/// (VILLAGE_PLAN V02: directions to a place the scene does not have; V03: a purchase the learner's coins do not cover), the NPC does not say the line of that
/// action and answers it with a host turn (<c>&lt;npc&gt;.unknown_place</c>, <c>&lt;npc&gt;.cant_pay</c>); the session then does nothing in the world for it. The
/// opening of a talk may also be the NPC's thanks for an order (<c>&lt;npc&gt;.delivered</c>, <see cref="Open"/>).
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

    // The bundle's prompts the code itself names: the guide's (her opening line, the warm-up, the transfer offer's log) and the stall's question (the
    // fail-forward counts misses at it). Which prompt an NPC waits on in a state is its NPC file's (NpcEntry.Prompts); the tests name the others.

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

    /// <summary>The NPC agents, in the order of <c>game/npcs.json</c> (C08; the saves keep the NPCs in this order).</summary>
    public IReadOnlyList<string> AgentIds { get; }

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
    private readonly IReadOnlyList<string> _practised;
    private readonly GameWords _words;
    private readonly HostEventTable _hostEvents;
    private readonly ScenePlaces _places;
    private readonly Dictionary<string, Agent> _agents = new(StringComparer.Ordinal);
    private readonly List<ReplayEntry> _records = [];
    private volatile SpokenUtteranceEvaluator? _spoken;
    private IGuideFacts _facts;

    /// <summary>One NPC's conversation state: its context between turns and the small facts the host decisions use.</summary>
    private sealed class Agent(string id, ConversationContext context, NpcEntry npc, string initialState)
    {
        private volatile ConversationContext _context = context;

        public string Id { get; } = id;

        /// <summary>The NPC's data: the prompts it waits on and how it opens a talk.</summary>
        public NpcEntry Npc { get; } = npc;

        /// <summary>The agent's initial state: a state with no prompt of its own waits on this one's, and an opening "first" is raised only from it.</summary>
        public string InitialState { get; } = initialState;

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
    /// <param name="data">The game's content; its bundle must hold the agent of every NPC of the roster and the prompts the NPC files name. The nouns a
    /// turn exercises get their learner skills, and the recipes give the crafting answers.</param>
    /// <param name="facts">The world the guide speaks of.</param>
    /// <param name="worldSeed">The world seed, which seeds every random draw with the agent id and the interaction counter.</param>
    /// <param name="start">The stored time before the first turn.</param>
    /// <param name="places">The places of the scene an NPC's directions can name (VILLAGE_PLAN V02), or <see langword="null"/> for none (a catalogue noun's block is still found).</param>
    /// <exception cref="ArgumentException">The bundle lacks an NPC's agent or a prompt its file names, or an NPC has no prompt for its agent's initial state.</exception>
    public NpcService(GameData data, IGuideFacts facts, ulong worldSeed, StoredTimestamp start, ScenePlaces? places = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(facts);
        var bundle = data.Bundle;
        _bundle = bundle;
        _worldFacts = facts;
        _facts = facts;
        _nouns = data.Nouns;
        _recipes = data.Recipes;
        _typed = new TypedUtteranceEvaluator(bundle);
        _phrases = data.Phrases;
        _practised = [.. data.Npcs.Homes.Keys];
        _words = data.Words;
        _hostEvents = data.HostEvents;
        _places = places ?? ScenePlaces.None;
        AgentIds = data.Npcs.Ids;
        foreach (var npc in data.Npcs.All)
        {
            if (!bundle.Agents.TryGetValue(npc.Id, out var machine))
            {
                throw new ArgumentException($"The bundle has no agent '{npc.Id}'.", nameof(data));
            }

            // The prompts an NPC waits on are its file's (VILLAGE_PLAN V01; a fixed list of six before): each is the bundle's, and the initial state has one.
            var initial = machine.InitialState();
            if (npc.Prompts.Values.Append(npc.ReviewPrompt).OfType<string>().FirstOrDefault(prompt => !bundle.Prompts.ContainsKey(prompt)) is { } missing)
            {
                throw new ArgumentException($"The bundle has no prompt '{missing}' ({NpcRoster.Folder}{npc.Id}.json).", nameof(data));
            }

            if (npc.PromptIn(initial.StateId, initial.StateId) is null)
            {
                throw new ArgumentException($"The NPC '{npc.Id}' has no prompt for its initial state '{initial.StateId}' ({NpcRoster.Folder}{npc.Id}.json).", nameof(data));
            }

            var context = ConversationContext.Create(worldSeed, npc.Id, initial, start, LearnerModel.New(), new Dictionary<string, int>(StringComparer.Ordinal), SlotMap.Empty, []);
            _agents.Add(npc.Id, new Agent(npc.Id, context, npc, initial.StateId));
        }
    }

    /// <summary>The target-language words the game uses (<c>game/words.json</c>): the guide's units and joiner, the greeting.</summary>
    public GameWords Words => _words;

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

    /// <summary>Whether the guide has made her transfer offer (<c>¿Quieres agua?</c>, once the camp is done): the host decision a save keeps.</summary>
    public bool GuideOffered => _agents[GuideAgent].Offered;

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
        // The guide's review stays code, by id (CONTENT_PLAN 3.6): what is due comes from the learner's progress, not a file; only its prompt is data (prompts.review).
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
        var analysis = _typed.Analyze(_words.Greeting, _bundle.Prompts[GuidePrompt]);
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
    /// <param name="delivered">
    /// The host event of an order the player has brought the goods of (VILLAGE_PLAN V03, <c>&lt;npc&gt;.delivered</c>), or <see langword="null"/>: when the NPC's state
    /// takes it, it is the opening instead of the NPC's own (<see cref="NpcOpening.Delivered"/>); when it does not, the talk opens as usual.
    /// </param>
    public NpcOpening Open(string npc, bool objectiveComplete, StoredTimestamp now, string hostStateHash, string? delivered = null)
    {
        ArgumentNullException.ThrowIfNull(hostStateHash);
        var agent = AgentOf(npc);
        agent.Visits++;
        if (delivered is not null)
        {
            var before = agent.Context with { Now = now, HostStateHash = hostStateHash };
            if (_typed.Advance(delivered, before) is { } thanks)
            {
                Record(new ReplayEntry(null, HostTurnEnvelope.From(thanks, before)));
                agent.Context = before.Next(thanks, now) with { HostStateHash = hostStateHash };
                var rewards = HostEventsOf(thanks.Agent.Actions);
                Note(agent, delivered, rewards);
                return new NpcOpening(npc, PromptFor(agent, false), thanks.Response.Lines.Select(static l => l.Text).ToList(), true, rewards, false, Delivered: true);
            }
        }

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

    private string PromptFor(Agent agent, bool review) => PromptIn(agent, agent.Context.AgentState.StateId, review);

    // The NPC file's prompt for the state (the constructor checked that the initial state has one, so a state with none of its own falls back to it).
    private static string PromptIn(Agent agent, string stateId, bool review) => agent.Npc.PromptIn(stateId, agent.InitialState, review)!;

    /// <summary>
    /// The prompt an NPC waits on when it is in a given state, from its NPC file (<see cref="NpcEntry.PromptIn"/>: the shopkeeper's bread offer and the
    /// guide's transfer offer in <c>offering</c>, the initial state's prompt in a state the file does not list). The session uses it to know the prompt after
    /// an applied turn (the turn carries the state it left the NPC in).
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="stateId">The NPC's state id (<see cref="GuideTurn.AgentState"/>).</param>
    /// <param name="review">Whether the guide's review question is open (her review prompt).</param>
    /// <exception cref="ArgumentException">There is no such NPC.</exception>
    public string PromptAfter(string npc, string stateId, bool review = false)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        return PromptIn(AgentOf(npc), stateId, review);
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

    // The host event the NPC opens the talk with, or null when the prompt's text is the opening. The NPC file's rule decides (VILLAGE_PLAN V01): the villager's
    // "Hola." always, the shopkeeper's question at the first talk, the guide never.
    private static string? OpeningEvent(Agent agent, bool objectiveComplete)
    {
        var state = agent.Context.AgentState.StateId;

        // The guide's transfer offer stays code (CONTENT_PLAN 3.6): it waits on the camp objective and is made once, which the opening rule cannot say.
        if (string.Equals(agent.Id, GuideAgent, StringComparison.Ordinal) && objectiveComplete && !agent.Offered)
        {
            return NpcHostEvents.GuideOffer;
        }

        // The shopkeeper's later visits stay code with his fail-forward (CONTENT_PLAN 3.6, rule 8): walking away and coming back, the bottle is never withheld.
        // He hands it over as a core turn; once it has been given, the talk picks up where the exchange was (the question again, or the pending bread offer).
        if (string.Equals(agent.Id, ShopkeeperAgent, StringComparison.Ordinal) && agent.Visits > 1)
        {
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
        }

        var opening = agent.Npc.Opening;
        return opening.When switch
        {
            OpeningWhen.Always => opening.Event,
            OpeningWhen.First => agent.Visits <= 1 && string.Equals(state, agent.InitialState, StringComparison.Ordinal) ? opening.Event : null,
            _ => null,
        };
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

        // Phase 1: the line is read once. A spoken line is decoded here and nowhere else. The listener's name said as a vocative (Luz, quiero pan) is no part of
        // the sentence, so the core reads the line without it (V14); the turn keeps the line as it was said. A greeting that opens a line with something else to say
        // (hola, quiero dos panes) is answered by the rest, which the core reads as a line of its own (V15, Greeting).
        UtteranceAnalysis analysis;
        string said;
        if (clip is null)
        {
            said = typed!;
            var bare = Vocative.Without(typed!, agent.Npc.Name);
            analysis = _typed.Analyze(bare, prompt);
            if (Greeting.Rest(bare) is { } rest && _typed.Analyze(rest, prompt) is var asked && Greeting.AnswersRest(analysis.Verdict, asked.Verdict, prompt))
            {
                analysis = asked;
            }
        }
        else
        {
            var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached to the NPCs.");
            analysis = spoken.Analyze(clip, prompt);
            said = analysis.Asr.Transcript;
            if (Vocative.Without(analysis.Asr, agent.Npc.Name) is { } bare)
            {
                analysis = spoken.AnalyzeRecognized(bare, clip, prompt); // the recognizer is not called again
            }

            if (Greeting.Rest(analysis.Asr) is { } rest && spoken.AnalyzeRecognized(rest, clip, prompt) is var asked && Greeting.AnswersRest(analysis.Verdict, asked.Verdict, prompt))
            {
                analysis = asked;
            }
        }

        var line = CleanTranscript(said);
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
        var skills = verdict.Communicated ? WordSkillsOf(GuideWord.From(analysis.Words), PhraseSkillsOf(verdict, promptId, origin, scene.PromptOverride is null)) : [];
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
            && (inventory.Count(ItemId.Bottle) + inventory.Count(ItemId.WaterBottle)) > 0;
        var followUp = new List<string>();
        var unsaid = AnswerMisses(agent, result, verdict.Slots, scene, scene.Facts ?? _worldFacts, now, hostStateHash, hostEvents, followUp);
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
            else if (unsaid.Any(pool => responseLine.LineId.StartsWith(pool + ".", StringComparison.Ordinal)))
            {
                // The line of an action the world cannot do (VILLAGE_PLAN V02: "Mira, está allí." for a place there is not); the NPC's answer to the miss follows.
            }
            else
            {
                replies.Add(responseLine.Text);
            }
        }

        var failForward = false;

        // The stall's fail-forward stays code, by id (CONTENT_PLAN 3.6): the bottle after four misses is a rule about this exchange, not data.
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

    // The host events of a turn that the world of the turn cannot carry out, judged on the turn's own copy of the world, as the guide's answers are (VILLAGE_PLAN
    // V02: directions to a place the scene does not have; V03: a purchase the coins the player carried do not cover). Each is answered by the NPC as a host turn,
    // <npc>.unknown_place or <npc>.cant_pay, whose lines follow the turn's and whose host events join it; the line of the action that raised the missed event is
    // not said. Returns the pools of those unsaid lines. An agent with no transition for the answer says its line as before.
    private List<string> AnswerMisses(
        Agent agent,
        PipelineResult result,
        IReadOnlyDictionary<string, string> slots,
        GuideScene scene,
        IGuideFacts facts,
        StoredTimestamp now,
        string hostStateHash,
        List<string> hostEvents,
        List<string> followUp)
    {
        var unsaid = new List<string>();
        foreach (var action in result.Decision.Agent?.Actions ?? [])
        {
            if (action.HostEvent is not { } hostEvent || Miss(_hostEvents.Of(agent.Id, hostEvent), slots, scene, facts) is not { } miss)
            {
                continue;
            }

            var before = agent.Context;
            var turn = _typed.Advance(NpcHostEvents.Of(agent.Id, miss), before);
            if (turn is null)
            {
                continue;
            }

            Record(new ReplayEntry(null, HostTurnEnvelope.From(turn, before)));
            agent.Context = before.Next(turn, now) with { HostStateHash = hostStateHash };
            var events = HostEventsOf(turn.Agent.Actions);
            Note(agent, NpcHostEvents.Of(agent.Id, miss), events);
            followUp.AddRange(turn.Response.Lines.Select(static l => l.Text));
            hostEvents.AddRange(events);
            unsaid.Add(action.DialogueIntent); // a pool is its dialogue intent's (every agent of the bundle names them alike)
        }

        return unsaid;
    }

    // What the world of the turn cannot do of an action, as the host event its NPC answers (NpcHostEvents.UnknownPlace, WontSell, CantPay, WontBuy, CantSell), or null
    // when it can. The coins and the goods are the turn's copy of the inventory: a scene with none carries none. A purchase or a sale names its item (V15).
    private string? Miss(NpcEffect effect, IReadOnlyDictionary<string, string> slots, GuideScene scene, IGuideFacts facts) => effect.Kind switch
    {
        NpcEffectKind.PointAtSlot when !slots.TryGetValue(effect.Slot, out var word) || _places.Resolve(word, scene.Player, facts) is null => NpcHostEvents.UnknownPlace,
        NpcEffectKind.Sell when !effect.ItemSaidIn(slots, _nouns) => NpcHostEvents.WontSell,
        NpcEffectKind.Sell when (scene.Inventory?.Count(ItemId.Coin) ?? 0) < effect.CostFor(slots) => NpcHostEvents.CantPay,
        NpcEffectKind.Buy when !effect.ItemSaidIn(slots, _nouns) => NpcHostEvents.WontBuy,
        NpcEffectKind.Buy when (scene.Inventory?.Count(effect.Item) ?? 0) == 0 => NpcHostEvents.CantSell,
        _ => null,
    };

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
    // service words (help, "no sé") exercise none either. In free talk (the NPC's own prompt, no lesson step or review) a place the prompt's one row of the
    // intent does not name is the row practised elsewhere that names it (V14, PhraseSheet.ByPlace).
    private List<string> PhraseSkillsOf(Verdict verdict, string promptId, LineOrigin origin, bool freeTalk)
    {
        var skills = new List<string>();
        if (!verdict.Communicated || verdict.Errors.Count > 0 || origin == LineOrigin.Choice
            || string.Equals(verdict.Intent.Value, "ask_help", StringComparison.Ordinal) || string.Equals(verdict.Intent.Value, "dont_know", StringComparison.Ordinal))
        {
            return skills;
        }

        if (_phrases.PhraseFor(promptId, verdict.Intent.Value, verdict.Slots, _words.Noun("river"), freeTalk ? _practised : null) is { } used) // the prompt's own row first (V12b)
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
