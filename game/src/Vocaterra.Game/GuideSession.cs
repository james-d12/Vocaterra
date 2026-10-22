using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// An arrow the guide put out: it points at a place until the player gets there or closes it.
/// </summary>
/// <param name="Place">The noun the player asked about (<c>árbol</c>).</param>
/// <param name="Target">The tile or block it points at.</param>
/// <param name="Quiz">Whether it is the guide's review question: the front-end then hides the noun, which is the answer.</param>
public sealed record PointerMarker(string Place, GuideCell Target, bool Quiz = false);

/// <summary>
/// The guide's review question in progress: she pointed at an example of a due word and asked what it is. The player's answer is graded
/// against <see cref="Target"/> (the pointed cell), not against the mouse cursor.
/// </summary>
/// <param name="Lemma">The due word whose example she pointed at.</param>
/// <param name="Target">The cell of the example (the nearest to the player when the dialogue opened).</param>
/// <param name="Question">What she asks (<c>¿Qué es esto?</c>).</param>
public sealed record GuideReview(string Lemma, GuideCell Target, string Question);

/// <summary>A turn the worker finished and <see cref="GuideSession.Collect"/> applied.</summary>
/// <param name="Outcome">The request, the turn (or why there is none) and the timings.</param>
/// <param name="Words">What the turn did to the learner's word counts, or <see langword="null"/> when there was no turn.</param>
public sealed record AppliedTurn(TurnOutcome Outcome, WordTurnResult? Words)
{
    /// <summary>The turn, or <see langword="null"/> when the request failed.</summary>
    public GuideTurn? Turn => Outcome.Turn;
}

/// <summary>
/// The part of a running game that the NPCs and the learner share, whatever the world is: the conversations (<see cref="NpcService"/>), the
/// word counts, the arrow, the review question, the stored time, the turn worker and the game events (<see cref="Events"/>). The 3D scene derives from it (<see cref="VoxelSession"/>):
/// a derived class says where the player is, what the facts are and what the state digest is; everything else is here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time.</b> The stored time the core sees is a fixed start (<see cref="StartedAt"/>, Unix seconds, passed in) plus the game's ticks
/// (<c>Tick / 60</c> seconds) plus the skipped seconds. Nothing here reads the clock, so a scripted run gives the same turns on every machine.
/// </para>
/// <para>
/// <b>Words.</b> Every turn is counted in <see cref="Words"/> (the learner's progress with the catalogue nouns, D-game-6) with the
/// <see cref="Credit"/> rules for the kind of input of the turn and the stored time of the turn (spaced uses, D-game-6 as amended).
/// </para>
/// <para>
/// <b>Talking to an NPC (S6).</b> <see cref="OpenTalk"/> starts a talk with the guide, the shopkeeper or the villager: the NPC's opening is a core
/// host turn when its state has one, and what the NPC's host events mean in the world (an item, the arrow) is applied by the derived class
/// (<see cref="ApplyHostEvents"/>), in the order the turns were submitted. Every phrase of the phrase sheet a turn uses, every world event and every
/// voice event is appended to <see cref="Events"/> for the phrase progress (S8) and the session log (S11).
/// </para>
/// <para>
/// <b>Turns on a worker (P5).</b> After <see cref="StartWorker"/> a turn is <i>submitted</i> (<see cref="SubmitTyped"/>, <see cref="SubmitClip"/>,
/// <see cref="SubmitWav"/>, <see cref="SubmitSpeak"/>) and the frame goes on: the stored time, the state digest, a copy of the world around the
/// player and a copy of the inventory are captured at the submission; a dedicated thread (<see cref="TurnWorker"/>) evaluates, and
/// <see cref="Collect"/> applies the finished turns on the caller's thread, in the order they were submitted. Nothing here blocks.
/// <see cref="Say"/> is the synchronous path (tests and the console host's sibling): do not mix it with the worker.
/// </para>
/// </remarks>
public abstract class GuideSession : IDisposable
{
    /// <summary>The line the <c>Ayuda</c> button sends (the intent <c>ask_help</c> is allowed at every prompt).</summary>
    public const string HelpLine = "ayuda";

    /// <summary>The line the <c>No sé</c> button sends (the intent <c>dont_know</c> is allowed at every prompt).</summary>
    public const string DontKnowLine = "no sé";

    /// <summary>The start time used when a caller gives none: 2025-10-09 08:53:20 UTC, the time the console host's one-shot runs use.</summary>
    public const long DefaultStartedAt = 1_760_000_000;

    private readonly NpcService _npcs;
    private readonly IGuideFacts _liveFacts;
    private readonly List<GameEvent> _events = [];
    private readonly Dictionary<string, RepairKind?> _lastRepair = new(StringComparer.Ordinal);
    private TurnWorker? _worker;
    private long _nextTurnId = 1;

    /// <summary>
    /// The shared part of a game, on a loaded bundle.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: it generates the world and seeds the conversation's random draws.</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle (<see cref="NounCatalog.Load"/>).</param>
    /// <param name="liveFacts">What the guide can say about the world as it is now (read on the simulation's thread).</param>
    /// <param name="recipes">The crafting recipes of the bundle.</param>
    /// <param name="playSession">The number of this play session (1 for a new game; the saves of S10 will bump it on each load).</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> (typed half, spoken full) when <see langword="null"/>.</param>
    protected GuideSession(
        ContentBundle bundle,
        ulong worldSeed,
        long startedAt,
        NounCatalog nouns,
        IGuideFacts liveFacts,
        RecipeBook recipes,
        int playSession,
        CreditRules? credit)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(liveFacts);
        ArgumentNullException.ThrowIfNull(recipes);
        Bundle = bundle;
        Nouns = nouns;
        Credit = credit ?? CreditRules.Default;
        WorldSeed = worldSeed;
        StartedAt = startedAt;
        Recipes = recipes;
        Words = new WordProgress(nouns, playSession);
        Phrases = PhraseSheet.Load(bundle);
        _liveFacts = liveFacts;
        _npcs = new NpcService(bundle, liveFacts, nouns, worldSeed, new StoredTimestamp(startedAt), recipes);
    }

    /// <summary>The content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The credit a correct use earns per kind of input, and the gaps between credited uses (D-game-6).</summary>
    public CreditRules Credit { get; }

    /// <summary>The worker that evaluates submitted turns, or <see langword="null"/> before <see cref="StartWorker"/>.</summary>
    public TurnWorker? Worker => _worker;

    /// <summary>Submitted turns not yet applied by <see cref="Collect"/> (the guide is thinking).</summary>
    public int PendingTurns => _worker?.Pending ?? 0;

    /// <summary>The noun catalogue: which tile or block is which noun, and each noun's article and gender.</summary>
    public NounCatalog Nouns { get; }

    /// <summary>The crafting recipes of the bundle.</summary>
    public RecipeBook Recipes { get; }

    /// <summary>The phrase sheet (<c>game/phrases.json</c>): the expected phrase of a prompt, the labels of the offered choices.</summary>
    public PhraseSheet Phrases { get; }

    /// <summary>The learner's progress with the catalogue nouns, fed from every turn.</summary>
    public WordProgress Words { get; }

    /// <summary>What the last turn changed in the word counts: the nouns it credited and the nouns it made known (null before the first turn).</summary>
    public WordTurnResult? LastWordTurn { get; private set; }

    /// <summary>The world seed.</summary>
    public ulong WorldSeed { get; }

    /// <summary>The stored time at tick 0, in Unix seconds.</summary>
    public long StartedAt { get; }

    /// <summary>The conversations with the NPCs: each one's state, the learner model and the repeat counts (the guide's are <see cref="NpcService.Context"/>).</summary>
    public NpcService Conversation => _npcs;

    /// <summary>The phrase uses, voice events and world events so far, in the order they happened (an in-memory list for S8 and S11).</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>The NPC the panel is open on (the one the next typed or spoken line goes to); the guide until <see cref="OpenTalk"/> says otherwise.</summary>
    public string CurrentNpc { get; private set; } = NpcService.GuideAgent;

    /// <summary>The arrow now on screen, or <see langword="null"/>.</summary>
    public PointerMarker? Pointer { get; protected set; }

    /// <summary>The stored seconds added by <see cref="SkipTime"/> on top of the stepped game time.</summary>
    public long SkippedSeconds { get; private set; }

    /// <summary>The stored time now: the start plus the seconds of game time that have been stepped plus the skipped seconds.</summary>
    public StoredTimestamp StoredTime => new(StartedAt + (Tick / VoxelSimulation.TicksPerSecond) + SkippedSeconds);

    /// <summary>The review question in progress, or <see langword="null"/>.</summary>
    public GuideReview? Review { get; private set; }

    /// <summary>The game's tick count now (the front-end stamps its highlights with it; the session logs its events at it).</summary>
    public long CurrentTick => Tick;

    /// <summary>The line the guide says when the dialogue opens (<c>PromptSpec.Text</c> of the prompt).</summary>
    public string OpeningLine => _npcs.OpeningLine;

    /// <summary>Whether the camp objective is complete (the guide then makes her transfer offer when the player talks to her). The 3D scene overrides it.</summary>
    protected virtual bool ObjectiveComplete => false;

    /// <summary>The game's tick count (the simulation's own tick: 60 per second of game time).</summary>
    protected abstract long Tick { get; }

    /// <summary>Where the player stands, for the guide's facts.</summary>
    protected abstract GuideViewpoint Viewpoint { get; }

    /// <summary>What the player carries now (read on the simulation's thread).</summary>
    protected abstract Inventory LiveInventory { get; }

    /// <summary>A copy of the world around the player as the facts of one turn (the worker reads it while the simulation goes on).</summary>
    protected abstract IGuideFacts SnapshotFacts();

    /// <summary>The game's digest of the rest of its state, copied into the replay record of a turn.</summary>
    protected abstract string StateDigest();

    /// <summary>The facts the guide was built with: the world as it is now.</summary>
    protected IGuideFacts LiveFacts => _liveFacts;

    /// <summary>
    /// Moves the stored time on by <paramref name="seconds"/> without stepping the world (the player waits, or the demo skips ahead). The
    /// next turn sees the later time.
    /// </summary>
    public void SkipTime(long seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        SkippedSeconds += seconds;
    }

    /// <summary>
    /// The player opens the dialogue with the guide. When a word is due (<see cref="WordProgress.DueWords"/>, the most overdue first) and an
    /// example of it is in range, the guide points at the nearest example (the arrow, without the noun) and asks what it is; that is the
    /// returned <see cref="Review"/>. When no word is due, or no example is in range, nothing changes and the result is
    /// <see langword="null"/>. An earlier review left open is replaced.
    /// </summary>
    public GuideReview? BeginTalk()
    {
        EndReview();
        if (PendingTurns > 0)
        {
            return null; // the words and the guide's state are about to change: no review question until the pending turns are in
        }

        foreach (var due in Words.DueWords(StoredTime.UnixSeconds, Credit))
        {
            var found = _liveFacts.NearestOfNoun(Viewpoint, due.Lemma);
            if (found is null)
            {
                continue;
            }

            Review = new GuideReview(due.Lemma, found.Cell, _npcs.ReviewQuestion());
            Pointer = new PointerMarker(due.Lemma, found.Cell, Quiz: true);
            return Review;
        }

        return null;
    }

    /// <summary>Drops the review question and its arrow (the dialogue closed, or the answer was understood).</summary>
    public void EndReview()
    {
        if (Review is not null && Pointer is { Quiz: true })
        {
            Pointer = null;
        }

        Review = null;
    }

    /// <summary>
    /// The player starts talking to an NPC (S6). The NPC opens with a core host turn when its state has one (the shopkeeper's "¿Qué necesitas?",
    /// the villager's "Hola.", the guide's transfer offer), and with its prompt's text otherwise; the guide may instead open with her review
    /// question (<see cref="BeginTalk"/>, returned in <see cref="Review"/>). What the opening's host events mean in the world is applied here
    /// (the shopkeeper's second visit hands over the bottle). Later typed and spoken lines go to this NPC until <see cref="EndTalk"/>.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <returns>What the NPC says first.</returns>
    public NpcOpening OpenTalk(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        EndReview();
        CurrentNpc = npc;
        NpcOpening opening;
        if (PendingTurns > 0)
        {
            // A turn is still on the worker (the player closed the panel while it thought): the NPC's state is about to change, so no host turn now.
            opening = new NpcOpening(npc, _npcs.PromptOf(npc), [_npcs.PromptTextOf(npc)], false, [], false);
        }
        else
        {
            opening = _npcs.Open(npc, ObjectiveComplete, StoredTime, StateDigest());
            if (string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal) && !opening.HostTurn
                && !string.Equals(_npcs.GuideState.StateId, "offering", StringComparison.Ordinal))
            {
                _ = BeginTalk();
            }
        }

        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkOpened, npc, opening.HostTurn ? "host_turn" : null));
        if (opening.HostEvents.Count > 0)
        {
            ApplyHostEvents(npc, opening.HostEvents, opening.FailForward ? "fail_forward" : "offer");
            if (opening.FailForward)
            {
                Log(new PhraseUse(Tick, StoredTime.UnixSeconds, PhraseIds.RequestWater, PhraseUseKind.FailForward, npc, NpcService.StallNeedPrompt, string.Empty, "second visit"));
            }
        }

        if (opening.HostTurn && opening.HostEvents.Contains(NpcHostEvents.GuideOffered, StringComparer.Ordinal))
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TransferOffered, npc, "¿Quieres agua?"));
        }

        return opening;
    }

    /// <summary>The panel closed: later lines go to the guide again (the default), and the end of the talk is logged.</summary>
    public void EndTalk()
    {
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkClosed, CurrentNpc));
        EndReview();
        CurrentNpc = NpcService.GuideAgent;
    }

    /// <summary>
    /// The player types a line to the NPC the panel is open on (the guide by default). Evaluates it, moves the conversation on, applies what its
    /// host events mean in the world, and, if the guide points at something, puts the arrow out (replacing any earlier one). Call it between
    /// ticks, never while the worker is running.
    /// </summary>
    /// <param name="line">The typed line.</param>
    /// <param name="cursor">The cell under the mouse pointer or the crosshair, for "¿qué es esto?" (ignored while a <see cref="Review"/> is open: the pointed cell counts).</param>
    public GuideTurn Say(string line, GuideCell? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        var review = Review;
        var scene = review is null
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: LiveInventory)
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: LiveInventory);
        var turn = _npcs.Say(CurrentNpc, line, scene, StoredTime, StateDigest());
        if (review is not null && turn.Communicated)
        {
            EndReview(); // understood: the question is answered (a line the guide did not understand leaves it open for another try)
        }

        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target);
        }

        LastWordTurn = Words.RecordTurn(turn, turn.Input, Credit, StoredTime.UnixSeconds);
        AfterTurn(turn, Tick, StoredTime.UnixSeconds);
        return turn;
    }

    /// <summary>
    /// Starts the turn worker: a dedicated thread that loads the bundle's pipeline and the speech models (the loading screen shows until
    /// <see cref="TurnWorker.IsReady"/>) and then evaluates the submitted turns. From here on use the <c>Submit</c> methods and
    /// <see cref="Collect"/>, not <see cref="Say"/>.
    /// </summary>
    /// <param name="clockMilliseconds">A clock for the timings only (the front-end's); <see langword="null"/> gives zeros.</param>
    /// <param name="loadSpeech">Whether to load Vosk (false: typed turns only, nothing is read from the models folder).</param>
    public TurnWorker StartWorker(Func<double>? clockMilliseconds = null, bool loadSpeech = true)
    {
        if (_worker is not null)
        {
            throw new InvalidOperationException("The turn worker is already started.");
        }

        _worker = new TurnWorker(_npcs, Bundle, clockMilliseconds, loadSpeech);
        _worker.Start();
        return _worker;
    }

    /// <summary>Stops the worker (after the turn it is on) and waits for its thread; the recognizer is disposed on that thread.</summary>
    public void StopWorker()
    {
        _worker?.Dispose();
        _worker = null;
    }

    /// <summary>Stops the worker, if one is running (see <see cref="StopWorker"/>).</summary>
    public void Dispose() => StopWorker();

    /// <summary>Submits a typed line. Returns at once; the turn is applied by a later <see cref="Collect"/>.</summary>
    /// <param name="line">The line.</param>
    /// <param name="cursor">The cell under the mouse pointer or the crosshair (ignored while a review is open: the pointed cell counts).</param>
    public TurnRequest SubmitTyped(string line, GuideCell? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Submit(TurnSource.Typed, line, null, null, cursor);
    }

    /// <summary>
    /// Submits the choice the player picked among those the repair ladder offered (S7). The line goes to the NPC the panel is open on like a typed
    /// one, marked as a choice: a recognition signal that is logged as a phrase use of kind <see cref="PhraseUseKind.Choice"/> and earns no word credit.
    /// The press is logged as a voice event. Returns at once.
    /// </summary>
    /// <param name="line">The phrase the NPC offered (<see cref="ChoiceOption.Line"/>).</param>
    /// <param name="cursor">The cell under the crosshair.</param>
    public TurnRequest SubmitChoice(string line, GuideCell? cursor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        LogVoice(VoiceEventKind.Choice, line);
        return Submit(TurnSource.Choice, line, null, null, cursor);
    }

    /// <summary>The <c>Ayuda</c> button: sends "ayuda" to the NPC the panel is open on (it answers with the model phrase or the hint), logged as a voice event. Returns at once.</summary>
    /// <param name="cursor">The cell under the crosshair.</param>
    public TurnRequest SubmitHelp(GuideCell? cursor = null)
    {
        LogVoice(VoiceEventKind.Hint);
        return Submit(TurnSource.Button, HelpLine, null, null, cursor);
    }

    /// <summary>The <c>No sé</c> button: sends "no sé" to the NPC the panel is open on (a <c>dont_know</c> turn, never a failure), logged as a voice event. Returns at once.</summary>
    /// <param name="cursor">The cell under the crosshair.</param>
    public TurnRequest SubmitDontKnow(GuideCell? cursor = null)
    {
        LogVoice(VoiceEventKind.NoSe);
        return Submit(TurnSource.Button, DontKnowLine, null, null, cursor);
    }

    /// <summary>Submits a clip for the mic check: the worker recognizes it and the outcome carries what it heard (<see cref="TurnOutcome.MicCheck"/>). It is no turn. Returns at once.</summary>
    /// <param name="clip">The prepared clip (16 kHz), or <see langword="null"/> for silence.</param>
    public TurnRequest SubmitHear(PcmClip? clip) => Submit(TurnSource.Hear, null, clip, null, null);

    /// <summary>Submits a line for the worker to synthesize with Kokoro and hear for the mic check (the scripted mic check). Returns at once.</summary>
    /// <param name="text">The text to speak.</param>
    public TurnRequest SubmitHearSpeak(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return Submit(TurnSource.HearSpeak, text, null, null, null);
    }

    /// <summary>
    /// Cancels the turns that are still on the worker (Esc while "Pensando…", S7): they are rolled back there, their outcomes come back marked
    /// cancelled and <see cref="Collect"/> drops them, so nothing of them reaches the conversation, the words or the world. Logged as a voice event.
    /// </summary>
    /// <returns>How many requests were cancelled.</returns>
    public int CancelPending()
    {
        var cancelled = _worker?.CancelPending() ?? 0;
        if (cancelled > 0)
        {
            LogVoice(VoiceEventKind.Cancel);
        }

        return cancelled;
    }

    /// <summary>
    /// Logs a voice event at the current tick and stored time, against the NPC the panel is open on (the front-end's controls: replay, slower, the
    /// timeout, the typing fallback). The turns log their own voice events.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="detail">The line concerned or the reason, or <see langword="null"/>.</param>
    public void LogVoice(VoiceEventKind kind, string? detail = null) => Log(new VoiceEvent(Tick, StoredTime.UnixSeconds, kind, CurrentNpc, detail));

    /// <summary>Logs the result of the mic check (no NPC is involved).</summary>
    /// <param name="kind">One of the three mic-check kinds.</param>
    /// <param name="heard">What the recognizer heard, or <see langword="null"/>.</param>
    public void LogMicCheck(VoiceEventKind kind, string? heard = null)
    {
        if (kind is not (VoiceEventKind.MicCheckPassed or VoiceEventKind.MicCheckFailed or VoiceEventKind.MicCheckSkipped))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a mic-check event.");
        }

        Log(new VoiceEvent(Tick, StoredTime.UnixSeconds, kind, null, heard));
    }

    /// <summary>Submits a recorded clip (16 kHz, from <see cref="AudioClips.Prepare"/>). Returns at once.</summary>
    public TurnRequest SubmitClip(PcmClip clip, GuideCell? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Submit(TurnSource.Clip, null, clip, null, cursor);
    }

    /// <summary>Submits a WAV file for the worker to read, convert and recognize (<c>--wav</c>). Returns at once.</summary>
    public TurnRequest SubmitWav(string path, GuideCell? cursor = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Submit(TurnSource.Wav, null, null, path, cursor);
    }

    /// <summary>Submits a line for the worker to synthesize with Kokoro and then recognize as spoken input (<c>--speak</c>). Returns at once.</summary>
    public TurnRequest SubmitSpeak(string text, GuideCell? cursor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return Submit(TurnSource.Speak, text, null, null, cursor);
    }

    /// <summary>
    /// Applies the turns the worker has finished, in the order they were submitted, and returns them (empty when none). The arrow, the
    /// review question and the word counts change here, on the caller's thread, never on the worker's; <see cref="LastWordTurn"/> is the
    /// last applied turn's. A request that failed (no microphone model, an unreadable file) is returned with its problem and changes nothing.
    /// </summary>
    public IReadOnlyList<AppliedTurn> Collect()
    {
        var applied = new List<AppliedTurn>();
        while (_worker is { } worker && worker.TryTake(out var outcome))
        {
            applied.Add(Apply(outcome!));
        }

        return applied;
    }

    // One finished request, on the caller's thread: a cancelled one is dropped (its turn never happened), a mic check is no turn, and a turn changes
    // the arrow, the review, the words and the events.
    internal AppliedTurn Apply(TurnOutcome outcome)
    {
        WordTurnResult? words = null;
        if (!outcome.Cancelled && outcome.Turn is { } turn)
        {
            if (outcome.Request.Review is { } review && turn.Communicated && ReferenceEquals(review, Review))
            {
                EndReview();
            }

            if (turn.Pointer is { Blocks: > 0 } pointer)
            {
                Pointer = new PointerMarker(pointer.Place, pointer.Target);
            }

            words = Words.RecordTurn(turn, turn.Input, Credit, outcome.Request.Now.UnixSeconds);
            LastWordTurn = words;
            AfterTurn(turn, outcome.Request.Tick, outcome.Request.Now.UnixSeconds);
        }

        return new AppliedTurn(outcome, words);
    }

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;

    /// <summary>
    /// Applies what the host events of a turn or an opening mean in the world: an item in the inventory, the arrow on the river. The game's
    /// words for the events are in <see cref="NpcHostEvents"/>; the derived class owns the world they act on. Called on the caller's thread.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="hostEvents">The events, in the order they were raised.</param>
    /// <param name="how">How they came about: <c>request</c>, <c>offer</c> or <c>fail_forward</c> (for the log).</param>
    protected virtual void ApplyHostEvents(string npc, IReadOnlyList<string> hostEvents, string how)
    {
    }

    /// <summary>Appends a game event to <see cref="Events"/>.</summary>
    protected void Log(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        _events.Add(gameEvent);
    }

    // What follows an applied turn: the phrase it used, the voice event it was, and what its host events do in the world.
    private void AfterTurn(GuideTurn turn, long tick, long time)
    {
        var npc = turn.Npc;
        _lastRepair.TryGetValue(npc, out var previousRepair);
        if (!turn.Unsure)
        {
            _lastRepair[npc] = turn.RepairKind; // a line held back as unsure changed nothing: the offer of choices, if any, still stands
        }

        if (turn.Uncertain)
        {
            // D-game-9: an uncertain recognition is the microphone's trouble, not the language's: a voice event, no phrase use, no error.
            Log(new VoiceEvent(tick, time, VoiceEventKind.Uncertain, npc, turn.Line));
        }

        if (turn.Communicated && PhraseIds.For(turn.Intent, turn.Slots) is { } phrase)
        {
            var kind = Classify(turn, phrase, previousRepair);
            var detail = turn.Slots.TryGetValue("item", out var item) ? item : turn.Slots.TryGetValue("place", out var place) ? place : null;
            Log(new PhraseUse(tick, time, phrase, kind, npc, turn.Prompt, turn.Line, detail));
        }

        if (turn.FailForward)
        {
            Log(new PhraseUse(tick, time, PhraseIds.RequestWater, PhraseUseKind.FailForward, npc, turn.Prompt, turn.Line, turn.Communicated ? turn.Intent : "no request"));
        }

        if (turn.HostEvents.Count > 0)
        {
            ApplyHostEvents(npc, turn.HostEvents, turn.FailForward ? "fail_forward" : "request");
        }
    }

    // How a phrase was used (D-game-10): the rules are in GameEvents.cs and the remarks of this type.
    private PhraseUseKind Classify(GuideTurn turn, string phrase, RepairKind? previousRepair)
    {
        if (string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal) || string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal))
        {
            return PhraseUseKind.DontKnow;
        }

        if (turn.Choice || previousRepair == RepairKind.OfferChoices)
        {
            return PhraseUseKind.Choice;
        }

        if (ObjectiveComplete && PhraseIds.HomeOf(phrase) is { } home && !string.Equals(home, turn.Npc, StringComparison.Ordinal))
        {
            return PhraseUseKind.Transfer;
        }

        return turn.Prompt is NpcService.StallNeedPrompt or NpcService.StallOfferPrompt or NpcService.GuideOfferPrompt
            ? PhraseUseKind.Supported
            : PhraseUseKind.Unaided;
    }

    private TurnRequest Submit(TurnSource source, string? text, PcmClip? clip, string? wavPath, GuideCell? cursor)
    {
        var worker = _worker ?? throw new InvalidOperationException("The turn worker is not started (StartWorker).");
        var review = Review;
        var request = new TurnRequest(
            _nextTurnId++,
            source,
            text,
            clip,
            wavPath,
            Scene(cursor, review),
            StoredTime,
            StateDigest(),
            review,
            Tick,
            CurrentNpc);
        worker.Submit(request);
        return request;
    }

    // The moment of a turn as plain values: copies of the world around the player and of the inventory, so the worker never reads what the
    // simulation keeps changing.
    private GuideScene Scene(GuideCell? cursor, GuideReview? review)
    {
        var facts = SnapshotFacts();
        var inventory = LiveInventory.Copy();
        return review is null
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: inventory, Facts: facts)
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: inventory, Facts: facts);
    }
}
