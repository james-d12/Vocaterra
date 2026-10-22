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
    private readonly List<(string Npc, string Skill)> _deferredSkills = [];
    private TurnWorker? _worker;
    private long _nextTurnId = 1;
    private IReadOnlyList<WordCard> _keyWordCards = [];
    private bool _keyWordsPending;
    private bool _keyWordsTaught;
    private long _keyWordsStart;
    private NpcOpening? _delayedOpening;
    private bool _talkOpen;

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
        PhraseProgress = new PhraseProgress(Phrases, playSession);
        Support = new PhraseSupport(Phrases, PhraseProgress, bundle);
        _liveFacts = liveFacts;
        _npcs = new NpcService(bundle, liveFacts, nouns, worldSeed, new StoredTimestamp(startedAt), recipes);
    }

    /// <summary>Raised on the caller's thread after each finished turn is applied by <see cref="Collect"/>, once its events are in <see cref="Events"/> (the session log, S11).</summary>
    public event Action<AppliedTurn>? Applied;

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

    /// <summary>
    /// The learner's progress with the phrases of the phrase sheet (S8): the counts and the support level of each row, fed from every
    /// <see cref="PhraseUse"/> this session logs.
    /// </summary>
    public PhraseProgress PhraseProgress { get; }

    /// <summary>What the dialogue panel shows by way of support for the prompt it is on: the cards, the audio-only state and the sounds to play (S8).</summary>
    public PhraseSupport Support { get; }

    /// <summary>The guide's review of a due phrase in progress (S8), or <see langword="null"/>.</summary>
    public PhraseReview? PhraseReview { get; private set; }

    /// <summary>What the guide says to start a review: the word review's question, or the request line of the phrase review; <see langword="null"/> when none is open.</summary>
    public string? ReviewLine => Review?.Question ?? PhraseReview?.Request;

    /// <summary>The number of this play session; setting it sets the words' and the phrases' (the saves of S10 bump it on each load, which lifts the spacing gaps).</summary>
    public int PlaySession
    {
        get => Words.Session;
        set
        {
            Words.Session = value;
            PhraseProgress.Session = value;
        }
    }

    /// <summary>The ticks the stall's key-word cards stand alone before the shopkeeper's first question (two and a half seconds).</summary>
    public const long KeyWordTicks = 150;

    /// <summary>The key words of the stall, taught before the first exchange (brief step 4): the nouns of the first request and the bread.</summary>
    public static IReadOnlyList<string> KeyWords { get; } = ["agua", "pan"];

    /// <summary>The key-word cards on show: from the first time the shopkeeper's panel opens (S8) until that talk ends; empty otherwise.</summary>
    public IReadOnlyList<WordCard> KeyWordCards => _keyWordCards;

    /// <summary>Whether the key-word cards are on show alone, before the shopkeeper's first question (the opening follows by itself after <see cref="KeyWordTicks"/>).</summary>
    public bool KeyWordsPending => _keyWordsPending;

    /// <summary>The nouns whose word card has been shown (a pickup, a craft or the key words): a card is shown once per noun.</summary>
    protected ISet<string> CardedNouns { get; } = new HashSet<string>(StringComparer.Ordinal);

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

    // ---- The save (S10): what a save keeps of this session and a load puts back. Only the save module uses these.

    /// <summary>Whether the shopkeeper's key words were taught (the first panel opening on him; never again).</summary>
    internal bool KeyWordsTaught
    {
        get => _keyWordsTaught;
        set => _keyWordsTaught = value;
    }

    /// <summary>
    /// Whether the session can be saved now: no turn is on the worker, the key-word cards are not standing alone and no opening waits. A save
    /// taken in any other state would hold a half-applied turn.
    /// </summary>
    public bool CanSave => PendingTurns == 0 && !_keyWordsPending && _delayedOpening is null;

    /// <summary>The number the next submitted turn gets (turn ids are in the replay and the logs).</summary>
    internal long NextTurnId
    {
        get => _nextTurnId;
        set => _nextTurnId = value;
    }

    /// <summary>The learner-model steps of the cards shown that wait for the next collect (NPC, skill), in order.</summary>
    internal IReadOnlyList<(string Npc, string Skill)> DeferredSkills => _deferredSkills.ToArray();

    /// <summary>Puts the waiting learner-model steps back, in order.</summary>
    internal void RestoreDeferredSkills(IEnumerable<(string Npc, string Skill)> skills)
    {
        _deferredSkills.Clear();
        _deferredSkills.AddRange(skills);
    }

    /// <summary>The repair each NPC's last answered turn ended with (only those that ended with one), in ordinal NPC order.</summary>
    internal IReadOnlyList<(string Npc, RepairKind Repair)> LastRepairs() =>
        _lastRepair.Where(static pair => pair.Value is not null)
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => (pair.Key, pair.Value!.Value))
            .ToArray();

    /// <summary>Puts back the repairs of the last answered turns.</summary>
    internal void RestoreLastRepairs(IEnumerable<(string Npc, RepairKind Repair)> repairs)
    {
        _lastRepair.Clear();
        foreach (var (npc, repair) in repairs)
        {
            _lastRepair[npc] = repair;
        }
    }

    /// <summary>The lemmas of the nouns whose word card has been shown, ordinal.</summary>
    internal IReadOnlyList<string> CardedLemmas() => CardedNouns.OrderBy(static lemma => lemma, StringComparer.Ordinal).ToArray();

    /// <summary>Puts back the nouns whose card was shown; each must be a catalogue noun.</summary>
    /// <exception cref="ArgumentException">A lemma is not a catalogue noun.</exception>
    internal void RestoreCarded(IEnumerable<string> lemmas)
    {
        CardedNouns.Clear();
        foreach (var lemma in lemmas)
        {
            if (Nouns.ForLemma(lemma) is null)
            {
                throw new ArgumentException($"'{lemma}' is not a catalogue noun.", nameof(lemmas));
            }

            _ = CardedNouns.Add(lemma);
        }
    }

    /// <summary>Puts back the stored seconds added by <see cref="SkipTime"/>.</summary>
    internal void RestoreSkippedSeconds(long seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        SkippedSeconds = seconds;
    }

    /// <summary>Puts back the arrow (a save keeps an arrow that is not a review question; the review is never saved).</summary>
    internal void RestorePointer(PointerMarker? pointer) => Pointer = pointer;

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
    /// <see langword="null"/>. An earlier review left open is replaced. A due phrase (<see cref="PhraseProgress.DuePhrases"/>) is asked first, in
    /// context, as a <see cref="PhraseReview"/> (the result is then <see langword="null"/> too); the due word follows the next time.
    /// </summary>
    public GuideReview? BeginTalk()
    {
        EndReview();
        if (PendingTurns > 0)
        {
            return null; // the words and the guide's state are about to change: no review question until the pending turns are in
        }

        if (BeginPhraseReview())
        {
            return null; // a due phrase is asked first (the prototype is about phrases); the due word, if any, is asked the next time
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

    // Ask a due phrase in context (S8). The guide's request line is shown; the answer is graded unaided unless the card was shown again.
    private bool BeginPhraseReview()
    {
        foreach (var due in PhraseProgress.DuePhrases(StoredTime.UnixSeconds, Credit))
        {
            if (PhraseReviews.Create(due.PhraseId, Phrases, Bundle) is { } review)
            {
                PhraseReview = review;
                Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.PhraseReviewAsked, NpcService.GuideAgent, review.PhraseId));
                return true;
            }
        }

        return false;
    }

    /// <summary>Drops the review question and its arrow (the dialogue closed, or the answer was understood).</summary>
    public void EndReview()
    {
        if (Review is not null && Pointer is { Quiz: true })
        {
            Pointer = null;
        }

        Review = null;
        PhraseReview = null;
    }

    /// <summary>
    /// The player starts talking to an NPC (S6). The NPC opens with a core host turn when its state has one (the shopkeeper's "¿Qué necesitas?",
    /// the villager's "Hola.", the guide's transfer offer), and with its prompt's text otherwise; the guide may instead open with her review
    /// question (<see cref="BeginTalk"/>, returned in <see cref="Review"/>). What the opening's host events mean in the world is applied here
    /// (the shopkeeper's second visit hands over the bottle). Later typed and spoken lines go to this NPC until <see cref="EndTalk"/>.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="teachKeyWords">
    /// Whether the front-end wants the key words of the stall taught first (S8): the first time the shopkeeper's panel opens with this set, his opening
    /// waits while the word cards for <c>agua</c> and <c>pan</c> stand alone for <see cref="KeyWordTicks"/> (the returned opening has no lines; the real
    /// one arrives from <see cref="TakeOpening"/>). Headless callers leave it off and get the opening at once.
    /// </param>
    /// <returns>What the NPC says first.</returns>
    public NpcOpening OpenTalk(string npc, bool teachKeyWords = false)
    {
        ArgumentNullException.ThrowIfNull(npc);
        EndReview();
        CurrentNpc = npc;
        _talkOpen = true;
        _delayedOpening = null;
        FlushSkills();
        if (teachKeyWords && PendingTurns == 0 && !_keyWordsTaught && string.Equals(npc, NpcService.ShopkeeperAgent, StringComparison.Ordinal))
        {
            return BeginKeyWords(npc);
        }

        return OpenNow(npc, logOpened: true);
    }

    // The first time the shopkeeper's panel opens, the key words (agua, pan) are taught before his question (brief step 4): their word cards stand alone for
    // KeyWordTicks, counted as heard and read (never a quiz), and the real opening follows (FinishKeyWords).
    private NpcOpening BeginKeyWords(string npc)
    {
        _keyWordsTaught = true;
        _keyWordsPending = true;
        _keyWordsStart = Tick;
        var cards = new List<WordCard>();
        foreach (var lemma in KeyWords)
        {
            if (Nouns.ForLemma(lemma) is not { } noun)
            {
                continue;
            }

            Words.MarkHeard(lemma);
            Words.MarkRead(lemma);
            CardedNouns.Add(lemma);
            cards.Add(new WordCard(lemma, noun.WithDefinite));
            Support.Enqueue(new AudioCue(npc, noun.WithDefinite, null, "key-word"));
        }

        _keyWordCards = cards;
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkOpened, npc, "key_words"));
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.KeyWordsShown, npc, string.Join(", ", cards.Select(static c => c.Lemma))));
        return new NpcOpening(npc, _npcs.PromptOf(npc), [], false, [], false);
    }

    /// <summary>
    /// Ends the key-word cards standing alone and runs the shopkeeper's real opening now (the player pressed a key, or sent a line, before the
    /// time was up). Does nothing when the key words are not pending. The opening is then waiting in <see cref="TakeOpening"/>.
    /// </summary>
    public void FinishKeyWords()
    {
        if (!_keyWordsPending)
        {
            return;
        }

        _keyWordsPending = false;
        _delayedOpening = OpenNow(CurrentNpc, logOpened: false);
    }

    /// <summary>The shopkeeper's opening that followed the key-word cards (his question), once; <see langword="null"/> when there is none waiting.</summary>
    public NpcOpening? TakeOpening()
    {
        var opening = _delayedOpening;
        _delayedOpening = null;
        return opening;
    }

    /// <summary>Counts the key-word cards' time in ticks of the game: the derived class calls it from its <c>Step</c>; after <see cref="KeyWordTicks"/> the opening follows.</summary>
    protected void AdvanceKeyWords()
    {
        if (_keyWordsPending && Tick - _keyWordsStart >= KeyWordTicks)
        {
            FinishKeyWords();
        }
    }

    private NpcOpening OpenNow(string npc, bool logOpened)
    {
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

        if (logOpened)
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkOpened, npc, opening.HostTurn ? "host_turn" : null));
        }

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

        RefreshSupport(npc, opening.Prompt);
        return opening;
    }

    /// <summary>The panel closed: later lines go to the guide again (the default), and the end of the talk is logged.</summary>
    public void EndTalk()
    {
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkClosed, CurrentNpc));
        EndReview();
        _talkOpen = false;
        _keyWordsPending = false;
        _keyWordCards = [];
        _delayedOpening = null;
        Support.Clear();
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
        FinishKeyWords();
        var review = Review;
        var phraseReview = PhraseReview;
        var support = Support.Snapshot();
        var scene = review is null
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: LiveInventory, PromptOverride: phraseReview?.Prompt)
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: LiveInventory);
        var turn = _npcs.Say(CurrentNpc, line, scene, StoredTime, StateDigest());
        if (review is not null && turn.Communicated)
        {
            EndReview(); // understood: the question is answered (a line the guide did not understand leaves it open for another try)
        }
        else if (phraseReview is not null && turn.Communicated && !IsHelpRequest(turn) && ReferenceEquals(phraseReview, PhraseReview))
        {
            PhraseReview = null; // answered (a request for help leaves the review open: the card goes up and the next line is the answer)
        }

        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target);
        }

        LastWordTurn = Words.RecordTurn(turn, turn.Input, Credit, StoredTime.UnixSeconds);
        AfterTurn(turn, Tick, StoredTime.UnixSeconds, phraseReview, support);
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

        _worker = new TurnWorker(_npcs, Bundle, clockMilliseconds, loadSpeech, WorldSeed);
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
        RevealSupport();
        return Submit(TurnSource.Button, HelpLine, null, null, cursor);
    }

    /// <summary>The <c>No sé</c> button: sends "no sé" to the NPC the panel is open on (a <c>dont_know</c> turn, never a failure), logged as a voice event. Returns at once.</summary>
    /// <param name="cursor">The cell under the crosshair.</param>
    public TurnRequest SubmitDontKnow(GuideCell? cursor = null)
    {
        LogVoice(VoiceEventKind.NoSe);
        RevealSupport();
        return Submit(TurnSource.Button, DontKnowLine, null, null, cursor);
    }

    /// <summary>
    /// Submits a check answer (S11): a typed line, the "No sé" button, or a clip, WAV or line to synthesize, for a production item. The worker
    /// grades it through the core in a throwaway context; no conversation, learner model, word or phrase changes (<see cref="TurnOutcome.Grade"/>).
    /// Returns at once. Call it only while the check battery runs.
    /// </summary>
    /// <param name="item">The production item.</param>
    /// <param name="source">Typed, Button (no sé), Clip, Speak or Wav.</param>
    /// <param name="text">The typed line or the "no sé" text, or the line to synthesize.</param>
    /// <param name="clip">A recorded clip (for <see cref="TurnSource.Clip"/>).</param>
    /// <param name="wavPath">A WAV file (for <see cref="TurnSource.Wav"/>).</param>
    public TurnRequest SubmitCheck(CheckItem item, TurnSource source, string? text, PcmClip? clip, string? wavPath)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != CheckItemKind.Production)
        {
            throw new ArgumentException($"The item '{item.Id}' is a comprehension item: its answer is a pick, not a submission.", nameof(item));
        }

        if (source is not (TurnSource.Typed or TurnSource.Button or TurnSource.Clip or TurnSource.Speak or TurnSource.Wav))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "A check answer is typed, a button, a clip, a line to speak or a WAV file.");
        }

        return Submit(source, text, clip, wavPath, null, item);
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
            var one = Apply(outcome!);
            applied.Add(one);
            Applied?.Invoke(one); // after this turn's events are logged: the session log writes the turn with its events (S11)
        }

        FlushSkills();
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
            else if (outcome.Request.PhraseReview is { } phraseReview && turn.Communicated && !IsHelpRequest(turn) && ReferenceEquals(phraseReview, PhraseReview))
            {
                PhraseReview = null;
            }

            if (turn.Pointer is { Blocks: > 0 } pointer)
            {
                Pointer = new PointerMarker(pointer.Place, pointer.Target);
            }

            words = Words.RecordTurn(turn, turn.Input, Credit, outcome.Request.Now.UnixSeconds);
            LastWordTurn = words;
            AfterTurn(turn, outcome.Request.Tick, outcome.Request.Now.UnixSeconds, outcome.Request.PhraseReview, outcome.Request.Support);
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
        if (gameEvent is PhraseUse use)
        {
            _ = PhraseProgress.Apply(use, Credit); // the phrase progress is fed from the events, in the order they are logged (S8)
        }
    }

    // What follows an applied turn: the phrase it used, the voice event it was, and what its host events do in the world.
    private void AfterTurn(GuideTurn turn, long tick, long time, PhraseReview? phraseReview, SupportSnapshot? support)
    {
        support ??= Support.Snapshot();
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
            var kind = Classify(turn, phrase, previousRepair, phraseReview, support);
            var detail = turn.Slots.TryGetValue("item", out var item) ? item : turn.Slots.TryGetValue("place", out var place) ? place : null;
            var correct = turn.Errors.Count == 0;
            IReadOnlyList<string>? inPlay = kind == PhraseUseKind.DontKnow ? (phraseReview is not null ? [phraseReview.PhraseId] : support.InPlay) : null;
            Log(new PhraseUse(tick, time, phrase, kind, npc, turn.Prompt, turn.Line, detail, turn.Input, correct, PhraseProgress.LevelOf(phrase), inPlay));
            if (kind == PhraseUseKind.DontKnow)
            {
                RevealSupport(); // the NPC gave the model: its card is up and the next use counts as supported
            }
            else if (kind is PhraseUseKind.Supported or PhraseUseKind.Unaided or PhraseUseKind.Transfer)
            {
                Support.SpendHelp();
                if (correct && phraseReview is null)
                {
                    foreach (var row in Phrases.Rows.Where(row => string.Equals(row.Says, "hears", StringComparison.Ordinal) && string.Equals(row.Prompt, turn.Prompt, StringComparison.Ordinal)))
                    {
                        PhraseProgress.NoteAnswered(row.Id); // the learner understood the question and answered it
                    }
                }
            }
        }

        if (turn.FailForward)
        {
            Log(new PhraseUse(tick, time, PhraseIds.RequestWater, PhraseUseKind.FailForward, npc, turn.Prompt, turn.Line, turn.Communicated ? turn.Intent : "no request"));
        }

        if (turn.HostEvents.Count > 0)
        {
            ApplyHostEvents(npc, turn.HostEvents, turn.FailForward ? "fail_forward" : "request");
        }

        if (_talkOpen && string.Equals(Support.Npc, npc, StringComparison.Ordinal) && !turn.Unsure)
        {
            RefreshSupport(npc, NpcService.PromptAfter(npc, turn.AgentState)); // the prompt may have moved on (the bread offer), and a level may have changed
        }
    }

    private static bool IsHelpRequest(GuideTurn turn) =>
        string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal) || string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal);

    // The support cards and sounds for the prompt the NPC waits on; a card that newly appears counts as shown and, for the core's learner model, as a
    // phrase skill step (LearnerModel.StepSkills), deferred while a turn is on the worker.
    private void RefreshSupport(string npc, string prompt)
    {
        var change = Support.Show(npc, prompt, PhraseReview?.PhraseId);
        foreach (var id in change.NewCards)
        {
            CardAppeared(npc, id, "level");
        }

        foreach (var id in change.NewHears)
        {
            PhraseProgress.NoteHeard(id);
        }

        FlushSkills();
    }

    // Ayuda or "no sé": the card of the phrases in play goes up whatever their level.
    private void RevealSupport()
    {
        if (Support.Npc is not { } npc)
        {
            return;
        }

        foreach (var id in Support.Reveal())
        {
            CardAppeared(npc, id, "help");
        }

        FlushSkills();
    }

    private void CardAppeared(string npc, string phraseId, string how)
    {
        PhraseProgress.NoteCardShown(phraseId);
        _deferredSkills.Add((npc, NpcService.PhraseSkillPrefix + phraseId));
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.SupportCardShown, npc, $"{phraseId}, {how}"));
    }

    // The learner-model steps of the cards shown. They change an NPC's context, which the worker rewrites at the end of a turn, so they wait until no
    // turn is on the worker (the next collect, or the next time a panel opens).
    private void FlushSkills()
    {
        if (_deferredSkills.Count == 0 || PendingTurns > 0)
        {
            return;
        }

        foreach (var (npc, skill) in _deferredSkills)
        {
            _npcs.StepSkills(npc, [skill], correct: true);
        }

        _deferredSkills.Clear();
    }

    // How a phrase was used (D-game-10): the rules are in GameEvents.cs and the remarks of this type.
    private PhraseUseKind Classify(GuideTurn turn, string phrase, RepairKind? previousRepair, PhraseReview? phraseReview, SupportSnapshot support)
    {
        if (string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal) || string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal))
        {
            return PhraseUseKind.DontKnow;
        }

        if (turn.Choice || previousRepair == RepairKind.OfferChoices)
        {
            return PhraseUseKind.Choice;
        }

        if (phraseReview is not null && PhraseIds.FocusAt(phraseReview.Prompt).Contains(phrase, StringComparer.Ordinal))
        {
            // The guide's review of a due phrase (S8): no card is shown, so the answer is unaided unless help was asked for first.
            return support.Scaffolded.Count > 0 ? PhraseUseKind.Supported : PhraseUseKind.Unaided;
        }

        if (ObjectiveComplete && PhraseIds.HomeOf(phrase) is { } home && !string.Equals(home, turn.Npc, StringComparison.Ordinal))
        {
            return PhraseUseKind.Transfer;
        }

        // Supported while the help is on offer (the card, the audio, or the model just shown after Ayuda or "no sé"), unaided once it has faded to nothing.
        return support.Scaffolded.Contains(phrase, StringComparer.Ordinal) ? PhraseUseKind.Supported : PhraseUseKind.Unaided;
    }

    private TurnRequest Submit(TurnSource source, string? text, PcmClip? clip, string? wavPath, GuideCell? cursor, CheckItem? check = null)
    {
        var worker = _worker ?? throw new InvalidOperationException("The turn worker is not started (StartWorker).");
        FinishKeyWords();
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
            CurrentNpc,
            PhraseReview,
            Support.Snapshot(),
            check);
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
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: inventory, Facts: facts, PromptOverride: PhraseReview?.Prompt)
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: inventory, Facts: facts);
    }
}
