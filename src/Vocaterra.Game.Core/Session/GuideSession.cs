using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// An arrow the guide put out: it points at a place until the player gets there or closes it.
/// </summary>
/// <param name="Place">The word the arrow shows: the noun the player asked about (<c>árbol</c>), or the label of a place of the scene (<c>tienda</c>).</param>
/// <param name="Target">The tile or block it points at.</param>
/// <param name="Quiz">Whether it is the guide's review question: the front-end then hides the noun, which is the answer.</param>
/// <param name="Point">The scene's name of the place it points at (<c>stall</c>, village-plan V02), which its arrival is logged with; <see langword="null"/> for an arrow on a noun's block.</param>
public sealed record PointerMarker(string Place, GuideCell Target, bool Quiz = false, string? Point = null);

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
/// <b>Talking to an NPC (S6).</b> <see cref="OpenTalk"/> starts a talk with any NPC of <c>game/npcs.json</c>: the NPC's opening is a core
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
public abstract partial class GuideSession : IDisposable
{
    /// <summary>The line the help button sends (<c>ayuda</c>, <c>game/words.json</c>; the intent <c>ask_help</c> is allowed at every prompt).</summary>
    public string HelpLine => Data.Words.HelpLine;

    /// <summary>The line the "I don't know" button sends (<c>no sé</c>, <c>game/words.json</c>; the intent <c>dont_know</c> is allowed at every prompt).</summary>
    public string DontKnowLine => Data.Words.DontKnowLine;

    /// <summary>The start time used when a caller gives none: 2025-10-09 08:53:20 UTC, the time the console host's one-shot runs use.</summary>
    public const long DefaultStartedAt = 1_760_000_000;

    private readonly NpcService _npcs;
    private readonly IGuideFacts _liveFacts;
    private readonly List<GameEvent> _events = [];
    private readonly Dictionary<string, RepairKind?> _lastRepair = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastLine = new(StringComparer.Ordinal);
    private readonly Dictionary<VoiceEventKind, int> _voiceCounts = [];
    private int _spokenTurns;
    private int _typedTurns;
    private readonly List<(string Npc, string Skill)> _deferredSkills = [];
    private TurnWorker? _worker;
    private long _nextTurnId = 1;
    private IReadOnlyList<WordCard> _keyWordCards = [];
    private bool _keyWordsPending;
    private readonly SortedSet<string> _keyWordsTaught = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _met = new(StringComparer.Ordinal);
    private long _keyWordsStart;
    private NpcOpening? _delayedOpening;
    private bool _talkOpen;
    private PhraseReview? _retry;

    /// <summary>
    /// The shared part of a game, on the game's loaded content.
    /// </summary>
    /// <param name="data">The game's content (<see cref="GameData.Load"/>): the bundle and every game file read from it.</param>
    /// <param name="worldSeed">The world seed: it generates the world and seeds the conversation's random draws.</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="liveFacts">What the guide can say about the world as it is now (read on the simulation's thread).</param>
    /// <param name="playSession">The number of this play session (1 for a new game; the saves of S10 will bump it on each load).</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> (typed half, spoken full) when <see langword="null"/>.</param>
    /// <param name="lessons">The lessons to run instead of the bundle's <c>game/lessons.json</c> (the tests' own), or <see langword="null"/> for the bundle's.</param>
    /// <param name="places">The places of the scene the NPCs' directions can name (village-plan V02), or <see langword="null"/> for none.</param>
    protected GuideSession(
        GameData data,
        ulong worldSeed,
        long startedAt,
        IGuideFacts liveFacts,
        int playSession,
        CreditRules? credit,
        LessonBook? lessons = null,
        ScenePlaces? places = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(liveFacts);
        Data = lessons is null ? data : data.WithLessons(lessons);
        Bundle = data.Bundle;
        Nouns = data.Nouns;
        Credit = credit ?? CreditRules.Default;
        WorldSeed = worldSeed;
        StartedAt = startedAt;
        Recipes = data.Recipes;
        Words = new WordProgress(Nouns, playSession);
        Phrases = data.Phrases;
        Locale = data.Locale;
        Glosses = data.Glosses;
        var book = Data.Lessons;
        PhraseProgress = new PhraseProgress(Phrases, playSession, data.Npcs.ScaffoldPhrases.Concat(book.TaughtPhrases).Concat(data.Procedures.Procedures.SelectMany(static p => p.Rows))); // W06: every instruction row of a procedure is a scaffold
        Procedures = new ProcedureRunner(data.Procedures, data.Stations, Nouns, PhraseProgress, Credit, worldSeed);
        Lessons = new LessonRunner(this, book, Locale);
        Support = new PhraseSupport(Phrases, PhraseProgress, Bundle, data.Npcs, Locale, null); // every scaffold phrase has its card from rung 1, whether or not a class taught it (L01)
        Deck = new ReviewDeck(this);
        Tips = new TipFeed(this, Data.Tips);
        _liveFacts = liveFacts;
        _npcs = new NpcService(Data, liveFacts, worldSeed, new StoredTimestamp(startedAt), places);
    }

    /// <summary>Raised on the caller's thread after each finished turn is applied by <see cref="Collect"/>, once its events are in <see cref="Events"/> (the session log, S11).</summary>
    public event Action<AppliedTurn>? Applied;

    /// <summary>The game's content: the bundle and every game file read from it (the session's own lessons when a test gave some).</summary>
    public GameData Data { get; }

    /// <summary>The content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The credit a correct use earns per kind of input, and the gaps between credited uses (D-game-6).</summary>
    public CreditRules Credit { get; private set; }

    /// <summary>
    /// Changes the credit rules for the turns and acts that follow (the menu's typed-credit test setting, <c>--typed-credit</c> at run time). The records already made are kept:
    /// only the credit a later use earns changes. The rules are not part of a save.
    /// </summary>
    /// <param name="credit">The new rules.</param>
    public void SetCredit(CreditRules credit)
    {
        ArgumentNullException.ThrowIfNull(credit);
        Credit = credit;
        Procedures.SetCredit(credit);
    }

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

    /// <summary>The learner's support language (S16, D-game-13; the locale bundle since C03): the controls, the tips, the bilingual objective, the glosses under the NPCs' lines and the feedback after a turn.</summary>
    public LocaleText Locale { get; }

    /// <summary>The lessons (S16): the data-driven sequences attached to the NPCs, and the runner that walks the learner through them.</summary>
    public LessonRunner Lessons { get; }

    /// <summary>The verbs' English (S20, the book's Verbs page).</summary>
    public VerbGlosses Glosses { get; }

    /// <summary>The definite article of a noun from its gender and lemma (<c>el</c> for the feminine <c>agua</c>), as the bundle's morphology gives it (S20).</summary>
    /// <param name="gender">The lexicon's gender (<c>m</c> or <c>f</c>).</param>
    /// <param name="lemma">The noun.</param>
    public string DefiniteArticle(string gender, string lemma) => Bundle.SpanishMorphology().Article(Definiteness.Definite, gender, "sg", lemma);

    /// <summary>
    /// The learner's record (S19): every line the game took (a dialogue turn, a lesson line, a check answer), in order, and the word record folded from them.
    /// Fed on the caller's thread by <see cref="Apply"/>; the book reads it.
    /// </summary>
    public LearnerRecord Learner { get; } = new();

    /// <summary>
    /// The phrases said "no sé" to that wait for a second try in this session (X04, R02). Each is asked at most once, at the next talk with its NPC.
    /// </summary>
    public RetryQueue Retries { get; } = new();

    /// <summary>
    /// The English note for the second try asked at the talk just opened (<c>Let's try that one again: …</c>), or <see langword="null"/> when none was asked. The
    /// front-end shows it above the guide's request (English, never spoken).
    /// </summary>
    public string? RetryNote { get; private set; }

    /// <summary>The first-time tips (S16): English text shown once each when something happens for the first time.</summary>
    public TipFeed Tips { get; }

    /// <summary>
    /// The support language to draw under an NPC's line, or <see langword="null"/> when there is none or it is not due: the locale's gloss of the line (by its
    /// speech-line id, or the lesson's translation of it), shown while the phrase in play is at the support rung or the help key was pressed, and always for
    /// a line no phrase of the prompt is practising (S16, <see cref="PhraseSupport.EnglishForLines"/>).
    /// </summary>
    /// <param name="npc">The NPC that says the line.</param>
    /// <param name="spanish">The line as the panel shows it.</param>
    public string? SupportForNpcLine(string npc, string spanish)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(spanish);
        var support = Data.SupportForLine(npc, spanish) ?? Lessons.TranslationOf(spanish);
        return support is not null && Support.EnglishForLines ? support : null;
    }

    /// <summary>The guide's review of a due phrase in progress (S8), or <see langword="null"/>.</summary>
    public PhraseReview? PhraseReview { get; private set; }

    /// <summary>What the guide says to start a review: the word review's question, or the request line of the phrase review; <see langword="null"/> when none is open.</summary>
    public string? ReviewLine => Review?.Question ?? PhraseReview?.Request;

    /// <summary>
    /// The day of the village (V12c, V25): the session that lessons, objectives, orders, unlocks and the check battery follow. A save keeps it as it is and a load puts it
    /// back; only a sleep at the player's fire (or the test aids <c>--sleep</c> and <c>--skip-to-session</c>) moves it.
    /// </summary>
    public int Day { get; set; } = 1;

    /// <summary>
    /// The sitting (V25): the play session of the phrase-credit spacing rule, which a launch, a load and a sleep each begin (S10). Setting it sets the words' and the phrases'
    /// session, so a use in a new sitting bypasses the spacing gap. A save keeps it; a save from before V25 continues from its day.
    /// </summary>
    public int Sitting
    {
        get => Words.Session;
        set
        {
            Words.Session = value;
            PhraseProgress.Session = value;
        }
    }

    /// <summary>The learner asked to practise a lesson again from the book (S26): the 3D scene points the beacon at the lesson's NPC. The base game has no beacon.</summary>
    /// <param name="npc">The lesson's NPC.</param>
    internal virtual void LessonReplayAsked(string npc)
    {
    }

    /// <summary>The ticks the stall's key-word cards stand alone before the shopkeeper's first question (two and a half seconds).</summary>
    public const long KeyWordTicks = 150;

    // The prompt the support follows while a lesson step of the villager or the shopkeeper asks for no phrase (S26): the guide's open prompt, which no phrase row uses.
    private const string QuietPrompt = "guide-open";

    /// <summary>
    /// The key words an NPC teaches before its first exchange (brief step 4), from its NPC file (village-plan V01; <c>game/words.json</c> before): the shopkeeper's are the
    /// nouns of the first request and the bread. Empty for an NPC that has none, or no such NPC.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    public IReadOnlyList<string> KeyWordsOf(string npc) => Data.Npcs.Find(npc)?.KeyWords ?? [];

    /// <summary>The key-word cards on show: from the first time the panel of an NPC with key words opens (S8) until that talk ends; empty otherwise.</summary>
    public IReadOnlyList<WordCard> KeyWordCards => _keyWordCards;

    /// <summary>Whether the key-word cards are on show alone, before the NPC's first question (the opening follows by itself after <see cref="KeyWordTicks"/>).</summary>
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

    /// <summary>The dialogue key of the weather now (E07): an NPC's answer to «¿Qué tiempo hace?» takes its pool from it. <see langword="null"/> for a session with no weather.</summary>
    protected virtual string? WeatherKey => null;

    /// <summary>The facts the guide was built with: the world as it is now.</summary>
    protected IGuideFacts LiveFacts => _liveFacts;

    // ---- The save (S10): what a save keeps of this session and a load puts back. Only the save module uses these.

    /// <summary>The NPCs whose key words were taught (the first panel opening on each; never again), in ordinal order.</summary>
    internal IReadOnlyList<string> KeyWordsTaughtBy => [.. _keyWordsTaught];

    /// <summary>Whether the NPC's key words were taught.</summary>
    /// <param name="npc">The NPC's id.</param>
    internal bool KeyWordsTaughtFor(string npc) => _keyWordsTaught.Contains(npc);

    /// <summary>Puts back the NPCs whose key words were taught (a load); the earlier ones are forgotten.</summary>
    /// <param name="npcs">Their ids.</param>
    internal void RestoreKeyWordsTaught(IEnumerable<string> npcs)
    {
        _keyWordsTaught.Clear();
        _keyWordsTaught.UnionWith(npcs);
    }

    /// <summary>The NPCs the player has met (L01): the panel opened on them once. A class opens on having met its teacher; an order is offered by its NPC at a talk (issue #54).</summary>
    public IReadOnlyList<string> MetNpcs => [.. _met];

    /// <summary>Whether the player has met the NPC (L01): talked to them.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    public bool HasMet(string npc) => _met.Contains(npc);

    /// <summary>Marks an NPC as met without a talk (a session skip, a load).</summary>
    /// <param name="npc">The NPC's agent id.</param>
    internal void MarkMet(string npc) => _met.Add(npc);

    /// <summary>Puts back the NPCs met (a load); the earlier ones are forgotten.</summary>
    /// <param name="npcs">Their ids.</param>
    internal void RestoreMet(IEnumerable<string> npcs)
    {
        _met.Clear();
        _met.UnionWith(npcs);
    }

    /// <summary>Marks the NPC's key words as taught without the cards (a session skip).</summary>
    /// <param name="npc">The NPC's id.</param>
    internal void MarkKeyWordsTaught(string npc) => _keyWordsTaught.Add(npc);

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
        _retry = null;
    }

    /// <summary>
    /// The player starts talking to an NPC (S6). The NPC opens with a core host turn when its state has one (the shopkeeper's "¿Qué necesitas?",
    /// the villager's "Hola.", the guide's transfer offer), and with its prompt's text otherwise; the guide may instead open with her review
    /// question (<see cref="BeginTalk"/>, returned in <see cref="Review"/>). What the opening's host events mean in the world is applied here
    /// (the shopkeeper's second visit hands over the bottle). When the player carries the goods of the NPC's order (village-plan V03), the opening is its
    /// thanks instead: the goods are taken, its reward given and <see cref="WorldEvents.OrderDelivered"/> logged. Later typed and spoken lines go to this NPC until
    /// <see cref="EndTalk"/>.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="teachKeyWords">
    /// Whether the front-end wants an NPC's key words taught first (S8): the first time the panel of an NPC with key words (<see cref="KeyWordsOf"/>; the
    /// shopkeeper's <c>agua</c> and <c>pan</c>) opens with this set, its opening waits while the word cards stand alone for <see cref="KeyWordTicks"/> (the
    /// returned opening has no lines; the real one arrives from <see cref="TakeOpening"/>). Headless callers leave it off and get the opening at once.
    /// </param>
    /// <returns>What the NPC says first.</returns>
    public NpcOpening OpenTalk(string npc, bool teachKeyWords = false)
    {
        ArgumentNullException.ThrowIfNull(npc);
        EndReview();
        RetryNote = null;
        CurrentNpc = npc;
        _talkOpen = true;
        _delayedOpening = null;
        _met.Add(npc); // L01: a class opens on having met its teacher
        _deckClaim = Deck.Claims(npc);
        if (string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal))
        {
            NoteGuideMet();
        }

        FlushSkills();
        if (teachKeyWords && PendingTurns == 0 && !_keyWordsTaught.Contains(npc) && KeyWordsOf(npc).Count > 0)
        {
            return BeginKeyWords(npc);
        }

        var opening = OpenNow(npc, logOpened: true);
        if (HandOverOpen || _orderOffer is not null || _landOffer is not null)
        {
            _classAfterHandOver = true; // issues #42 and #54: no class starts, resumes or is offered while the NPC waits for the goods or for the answer to its offer; it follows in this talk
        }
        else
        {
            Lessons.OnTalkOpened(npc); // a lesson attached to this NPC takes up where it stopped, or starts by itself when its file says so (S16)
        }
        OfferSecondTry(npc);
        Deck.OnTalkOpened(npc); // the warm-up offer of a new session, or the review asked for from the book (X06)
        _deckClaim = false;
        return opening;
    }

    // The second try (X04, R02): at the talk with the NPC a "no sé" was said to, the phrase waiting for it is asked once, as the guide's review (the same request
    // line and answer rules as a due phrase). Not while a review is open, a turn is on the worker, or a lesson holds the conversation: a review would then take
    // the lesson's prompt (see RetryQueue). A phrase taken here is never queued again in this session.
    private void OfferSecondTry(string npc)
    {
        if (PhraseReview is not null || Review is not null || PendingTurns > 0 || Lessons.Engaged || HandOverOpen || _orderOffer is not null || _landOffer is not null
            || !Retries.Pending.Any(item => string.Equals(item.Npc, npc, StringComparison.Ordinal)))
        {
            return;
        }

        if (Retries.TakeFor(npc) is not { } item || PhraseReviews.Create(item.PhraseId, Phrases, Bundle) is not { } review)
        {
            return;
        }

        _retry = review;
        PhraseReview = review;
        RetryNote = Locale.Ui("retryAgain", ("english", Phrases.Find(item.PhraseId)?.English ?? item.PhraseId));
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.SecondTryAsked, npc, review.PhraseId));
    }

    // The learner said "no sé" to these phrases at this NPC: the first learner phrase the queue takes is asked again later (X04, R02).
    private void ScheduleSecondTry(string npc, IEnumerable<string> phrases)
    {
        foreach (var id in phrases)
        {
            if (Phrases.IsPractisable(id) && Retries.Schedule(id, npc))
            {
                return;
            }
        }
    }

    // ---- The lessons' hooks (S16): the runner is in LessonRunner.cs; these are the parts of the session it needs.

    /// <summary>
    /// Whether a teacher may offer a class at this talk (L01): nothing else holds the talk (a review, the second try, the warm-up, a turn on the worker) and the NPC does not wait
    /// on a yes or no of its own (the guide's transfer offer, the stall's bread), whose Sí the class would take.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    internal bool MayOfferClass(string npc) =>
        PendingTurns == 0 && PhraseReview is null && Review is null && !_deckClaim && Deck.Status == DeckStatus.Idle && !Lessons.Engaged && !HandOverOpen
        && Bundle.Prompts.TryGetValue(_npcs.PromptOf(npc), out var prompt) && !prompt.AllowedIntentIds.Contains("confirm", StringComparer.Ordinal);

    /// <summary>Whether a conversation is open (Y07: the session's ending waits for it to close).</summary>
    public bool TalkOpen => _talkOpen;

    /// <summary>Whether the panel is open on this NPC (a talk is open and the NPC is the one the lines go to).</summary>
    internal bool TalkOpenOn(string npc) => _talkOpen && string.Equals(CurrentNpc, npc, StringComparison.Ordinal);

    /// <summary>Appends a game event to <see cref="Events"/> (the lesson runner and the tips log through it).</summary>
    internal void LogEvent(GameEvent gameEvent) => Log(gameEvent);

    /// <summary>How many uses of a speech button make its English go (the way the objective line drops a word's English once the word is known, D-game-13): three.</summary>
    public const int ButtonEnglishUses = 3;

    /// <summary>
    /// Whether the English next to a speech button (<c>[R] Repetir (repeat)</c>) or the hold-to-talk hint is drawn now (S17, D-game-13). It is drawn while the learner is
    /// early: until the button has been used (or its phrase said) <see cref="ButtonEnglishUses"/> times; the hint until as many lines have been spoken, the typing
    /// button until as many typed. The English is on-screen help only: the buttons and the keys work the same with or without it (rule 8).
    /// </summary>
    /// <param name="button">One of <see cref="SpeechButtonKeys"/>.</param>
    public bool ButtonEnglishUp(string button)
    {
        ArgumentNullException.ThrowIfNull(button);
        var uses = button switch
        {
            SpeechButtonKeys.Repeat => _voiceCounts.GetValueOrDefault(VoiceEventKind.Replay) + PhraseUsesOf(PhraseIds.AgainPlease),
            SpeechButtonKeys.Slower => _voiceCounts.GetValueOrDefault(VoiceEventKind.Slower) + PhraseUsesOf(PhraseIds.SlowerPlease),
            SpeechButtonKeys.Help => _voiceCounts.GetValueOrDefault(VoiceEventKind.Hint) + PhraseUsesOf(PhraseIds.Help),
            SpeechButtonKeys.DontKnow => _voiceCounts.GetValueOrDefault(VoiceEventKind.NoSe) + PhraseUsesOf(PhraseIds.DontKnow),
            SpeechButtonKeys.Type => _voiceCounts.GetValueOrDefault(VoiceEventKind.TypeFallback) + _typedTurns,
            SpeechButtonKeys.Hold => _spokenTurns,
            _ => ButtonEnglishUses,
        };
        return uses < ButtonEnglishUses;
    }

    // The phrase's credited production in whole uses (a typed use at half credit counts half), rounded down.
    private int PhraseUsesOf(string phraseId)
    {
        var record = PhraseProgress.RecordOf(phraseId);
        return (int)Math.Floor(record.SupportedUses + record.UnaidedUses);
    }

    /// <summary>
    /// What the NPC said last, as one line (S17): the line "Otra vez, por favor" says again. It follows the NPC's opening, the line of a lesson step and the last line of
    /// each turn the lesson did not take.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    public string? LastLineOf(string npc) => _lastLine.TryGetValue(npc, out var line) ? line : null;

    /// <summary>The NPC says a line (a lesson step's line, S17): it becomes the line a request to say it again repeats.</summary>
    internal void NoteNpcLine(string npc, string text) => _lastLine[npc] = text;

    /// <summary>A lesson step sends the beacon to the nearest block of a noun (S17, a <c>do</c> step with a <c>point</c>). The 3D scene does it; the base game has no beacon.</summary>
    internal virtual void LessonPointAt(string npc, string noun)
    {
    }

    /// <summary>A lesson step sends the beacon to a named place of the scene (village-plan V02, a <c>do</c> step with a <c>pointPlace</c>). The 3D scene does it.</summary>
    internal virtual void LessonPointAtPlace(string npc, string place)
    {
    }

    /// <summary>
    /// Whether an order (an objective with <c>deliverTo</c>, village-plan V03) has been delivered: a lesson's <c>do</c> step that waits for it ends at once, since the
    /// goods may be brought before the lesson asks for them (V11: walking up is enough). The 3D scene keeps the objectives.
    /// </summary>
    /// <param name="order">The order's objective id.</param>
    internal virtual bool OrderDelivered(string order) => false;

    /// <summary>
    /// Whether the bag already holds what a lesson's <c>do</c> step waits for a pickup of (V23a): at least <paramref name="needed"/> of the noun's items, so a learner who
    /// gathered the wood before the lesson asked for it is not sent to gather it again. The 3D scene has the bag.
    /// </summary>
    /// <param name="lemma">The noun the step waits to see picked up.</param>
    /// <param name="needed">How many the bag must hold.</param>
    internal virtual bool HoldsPickup(string lemma, int needed) => false;

    /// <summary>
    /// The player reached a beacon the front-end placed itself (the start, <c>--beacon</c>; <see cref="BeaconDirector"/>): logged as
    /// <see cref="WorldEvents.BeaconReached"/> with the point's name (village-plan V02). The session logs the arrival at its own arrow itself.
    /// </summary>
    /// <param name="point">The scene's name of the point.</param>
    public void NoteBeaconReached(string point)
    {
        ArgumentException.ThrowIfNullOrEmpty(point);
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.BeaconReached, null, point));
    }

    /// <summary>The player opened the panel on the guide, or began her lesson: the 3D scene notes that the guide has been met (S17).</summary>
    protected virtual void NoteGuideMet()
    {
    }

    /// <summary>The panel's support follows the lesson step now in play (its card and audio, or none): called when a step begins or the lesson ends.</summary>
    internal void RefreshLessonSupport()
    {
        if (_talkOpen && Support.Npc is { } npc)
        {
            RefreshSupport(npc, _npcs.PromptOf(npc));
        }
    }

    /// <summary>The help path of a lesson step with a phrase: the card and the English go up whatever the rung (the help key shows the English at every rung).</summary>
    internal void RevealLessonSupport() => RevealSupport();

    /// <summary>"No sé" in a lesson step with a phrase: the phrase use ("no sé" with the step's phrase in play), as the button makes in an ordinary exchange.</summary>
    internal void LogLessonDontKnow(string npc, string prompt, string phraseId)
    {
        Log(new PhraseUse(Tick, StoredTime.UnixSeconds, PhraseIds.DontKnow, PhraseUseKind.DontKnow, npc, prompt, DontKnowLine, null, InputKind.Button, true, PhraseProgress.LevelOf(phraseId), [phraseId]));
        ScheduleSecondTry(npc, [phraseId]); // a lesson's "no sé" queues its phrase for a second try too (X04, R02)
    }

    // The first time the panel of an NPC with key words opens (the shopkeeper's agua and pan), they are taught before its question (brief step 4): their word cards
    // stand alone for KeyWordTicks, counted as heard and read (never a quiz), and the real opening follows (FinishKeyWords). The save keeps the NPCs they were taught
    // by (KeyWordsTaughtBy), so each NPC with key words teaches them once a game.
    private NpcOpening BeginKeyWords(string npc)
    {
        _keyWordsTaught.Add(npc);
        _keyWordsPending = true;
        _keyWordsStart = Tick;
        var cards = new List<WordCard>();
        foreach (var lemma in KeyWordsOf(npc))
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
    /// Ends the key-word cards standing alone and runs the NPC's real opening now (the player pressed a key, or sent a line, before the
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

    /// <summary>The NPC's opening that followed the key-word cards (the shopkeeper's question), once; <see langword="null"/> when there is none waiting.</summary>
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
        string? order = null;
        if (PendingTurns > 0)
        {
            // A turn is still on the worker (the player closed the panel while it thought): the NPC's state is about to change, so no host turn now.
            opening = new NpcOpening(npc, _npcs.PromptOf(npc), [_npcs.PromptTextOf(npc)], false, [], false);
        }
        else
        {
            CompleteTeachEnd(npc); // a teacher a load left in its teaching state comes out of it before the talk (W08)
            order = OrderReadyAt(npc); // the player walked up with an order's goods (village-plan V03): the NPC thanks them at once, or asks for them (issue #42)
            var asked = order is null ? null : BeginHandOver(npc, order); // issue #42: the learner says what they hand over; «no sé» or two misses hand it over anyway
            if (asked is not null)
            {
                order = null;
                opening = asked;
            }
            else
            {
                EndStaleHandOver(npc);
                EndStaleOrderOffer(npc);
                EndStaleLandOffer(npc);
                var orderOffer = order is null ? OrderOfferOf(npc) : null; // issue #54: an order not yet given is offered when the talk opens, and taken with «sí»
                var landOffer = order is null && orderOffer is null ? LandOfferOf(npc) : null; // Y11: the mayor offers the stake, or the plot once it is marked
                var offer = orderOffer?.HostEvent ?? landOffer ?? (order is null ? BakeOfferOf(npc, asked: false) : null); // W08: the teacher's offer to bake opens the talk, once a day until the learner has baked
                opening = _npcs.Open(npc, ObjectiveComplete, StoredTime, StateDigest(), order is null ? null : NpcHostEvents.Of(npc, NpcHostEvents.Delivered), offer);
                if (orderOffer is { } made && string.Equals(_npcs.StateOf(npc), NpcHostEvents.OrderOfferState, StringComparison.Ordinal))
                {
                    NoteOrderOffer(npc, made.Order);
                }
                else if (landOffer is not null && IsLandOfferState(_npcs.StateOf(npc)))
                {
                    NoteLandOffer(npc);
                }
                else if (offer is not null)
                {
                    NoteOffer(npc);
                }
            }

            // The question for the goods (issue #42) is said even while a lesson holds the talk, as the walk-up's thanks were: the answer is the learner's next line.
            if (asked is null && string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal) && !opening.HostTurn
                && !string.Equals(_npcs.GuideState.StateId, "offering", StringComparison.Ordinal))
            {
                if (Lessons.OwnsConversation(npc))
                {
                    opening = opening with { Lines = [] }; // her lesson speaks first (S17): no "¿En qué te ayudo?" before the first step
                }
                else if (!_deckClaim)
                {
                    _ = BeginTalk();
                }
            }
            else if (asked is null && !string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal) && Lessons.OwnsConversation(npc) && !opening.Delivered)
            {
                opening = opening with { Lines = [] }; // the villager's or the shopkeeper's lesson speaks first (S26): the host turn's state change stays, its line is not said
            }
        }

        var delivered = opening.Delivered ? order : null;

        if (opening.Lines.Count > 0)
        {
            _lastLine[npc] = opening.Lines[^1];
        }

        if (logOpened)
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkOpened, npc, opening.HostTurn ? "host_turn" : null));
        }

        if (delivered is not null)
        {
            TakeOrder(npc, delivered); // the goods go before the reward comes in
        }

        if (opening.HostEvents.Count > 0)
        {
            ApplyHostEvents(npc, opening.HostEvents, opening.FailForward ? "fail_forward" : delivered is not null ? "order" : "offer", SlotMap.Empty);
            if (opening.FailForward)
            {
                Log(new PhraseUse(Tick, StoredTime.UnixSeconds, PhraseIds.RequestWater, PhraseUseKind.FailForward, npc, NpcService.StallNeedPrompt, string.Empty, "second visit"));
            }
        }

        if (delivered is not null)
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderDelivered, npc, delivered)); // the order's one step waits for it
        }

        if (opening.HostTurn && opening.HostEvents.Contains(NpcHostEvents.GuideOffered, StringComparer.Ordinal))
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TransferOffered, npc, Bundle.Prompts[NpcService.GuideOfferPrompt].Text));
        }

        RefreshSupport(npc, opening.Prompt);
        return opening;
    }

    /// <summary>The panel closed: later lines go to the guide again (the default), and the end of the talk is logged.</summary>
    public void EndTalk()
    {
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.TalkClosed, CurrentNpc));
        Deck.OnTalkClosed(); // an offer or a warm-up left standing is over (X06)
        EndReview();
        Lessons.OnTalkClosed();
        _talkOpen = false;
        _keyWordsPending = false;
        _keyWordCards = [];
        _delayedOpening = null;
        _handOver = null; // the next talk asks again
        _classAfterHandOver = false;
        _orderOffer = null; // issue #54: an offer left unanswered is made again at the next talk
        _landOffer = null; // Y11: so is the mayor's
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
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: LiveInventory, PromptOverride: phraseReview?.Prompt ?? LessonPrompt, Credited: Lessons.CreditedPhrases, OrderHeld: OrderHeldAt(CurrentNpc), ClosedGifts: ClosedGifts, Grant: TeachingNpc is null ? null : PlanGrant(), Offer: BakeOfferOf(CurrentNpc, asked: true), WeatherKey: WeatherKey, Land: PlotFactsNow())
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: LiveInventory, WeatherKey: WeatherKey);
        var turn = _npcs.Say(CurrentNpc, line, scene, StoredTime, StateDigest());
        PhraseReview? answeredReview = null;
        if (review is not null && turn.Communicated)
        {
            EndReview(); // understood: the question is answered (a line the guide did not understand leaves it open for another try)
        }
        else if (phraseReview is not null && turn.Communicated && !IsHelpRequest(turn) && ReferenceEquals(phraseReview, PhraseReview))
        {
            PhraseReview = null; // answered (a request for help leaves the review open: the card goes up and the next line is the answer)
            answeredReview = phraseReview;
        }

        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target, Point: pointer.Point);
        }

        LastWordTurn = Words.RecordTurn(turn, turn.CreditKind, Credit, StoredTime.UnixSeconds);
        RecordTurnLine(turn, StoredTime.UnixSeconds); // the learner's history (S19): the synchronous path records its lines like the worker's
        var afterwards = AfterTurn(turn, Tick, StoredTime.UnixSeconds, phraseReview, support);
        if (answeredReview is not null)
        {
            Deck.AfterAnswer(answeredReview); // the next item of the warm-up is asked, once this answer has been counted (X06)
        }

        return afterwards;
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
        CompleteTeachEnd();
        CompleteHandOver();
        return applied;
    }

    // One finished request, on the caller's thread: a cancelled one is dropped (its turn never happened), a mic check is no turn, and a turn changes
    // the arrow, the review, the words and the events.
    internal AppliedTurn Apply(TurnOutcome outcome)
    {
        WordTurnResult? words = null;
        PhraseReview? answeredReview = null;
        if (!outcome.Cancelled && outcome.Turn is { } turn)
        {
            if (outcome.Request.Review is { } review && turn.Communicated && ReferenceEquals(review, Review))
            {
                EndReview();
            }
            else if (outcome.Request.PhraseReview is { } phraseReview && turn.Communicated && !IsHelpRequest(turn) && ReferenceEquals(phraseReview, PhraseReview))
            {
                PhraseReview = null;
                answeredReview = phraseReview;
            }

            if (turn.Pointer is { Blocks: > 0 } pointer)
            {
                Pointer = new PointerMarker(pointer.Place, pointer.Target, Point: pointer.Point);
            }

            words = Words.RecordTurn(turn, turn.CreditKind, Credit, outcome.Request.Now.UnixSeconds);
            LastWordTurn = words;
            RecordTurnLine(turn, outcome.Request.Now.UnixSeconds);
            var shown = AfterTurn(turn, outcome.Request.Tick, outcome.Request.Now.UnixSeconds, outcome.Request.PhraseReview, outcome.Request.Support);
            if (!ReferenceEquals(shown, turn))
            {
                outcome = outcome with { Turn = shown }; // the lesson took the turn, or the guide says her last line again (S17)
            }

            if (answeredReview is not null)
            {
                Deck.AfterAnswer(answeredReview); // the next item of the warm-up is asked, once this answer has been counted (X06)
            }
        }
        else if (!outcome.Cancelled && outcome.Grade is { } grade && outcome.Request.Check is { } item)
        {
            // A check answer (S11) is a line the learner gave too (S19). It changes nothing else: the battery grades it; the world and the progress do not move.
            Learner.Append(new InputEntry(
                Learner.NextId,
                Day,
                outcome.Request.Now.UnixSeconds,
                SourceOf(grade.Input),
                grade.Transcript,
                null,
                item.Prompt,
                InputPlace.Check,
                item.Id,
                grade.Intent,
                grade.Communicated,
                false,
                grade.Errors ?? [],
                grade.Words ?? []));
        }

        return new AppliedTurn(outcome, words);
    }

    // A turn the game took is a line in the learner's history (S19), numbered and stamped with its session and stored time. Read before the turn can end
    // a lesson's step: the unit is the step the line landed on.
    private void RecordTurnLine(GuideTurn turn, long storedTime)
    {
        var unit = Lessons.UnitFor(turn.Npc);
        Learner.Append(new InputEntry(
            Learner.NextId,
            Day,
            storedTime,
            SourceOf(turn.Input),
            turn.Line,
            turn.Npc,
            turn.Prompt,
            unit is null ? InputPlace.Dialogue : InputPlace.Lesson,
            unit,
            turn.Intent,
            turn.Taken,
            turn.Unsure,
            turn.Errors,
            turn.Words ?? []));
    }

    // How a line arrived, as the learner's record names it (S19).
    private static InputSource SourceOf(InputKind input) => input switch
    {
        InputKind.Spoken => InputSource.Voice,
        InputKind.Button => InputSource.Button,
        _ => InputSource.Typed,
    };

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;

    /// <summary>
    /// Applies what the host events of a turn or an opening mean in the world: an item in the inventory, the arrow on the river. The game's
    /// words for the events are in <see cref="NpcHostEvents"/>; the derived class owns the world they act on. Called on the caller's thread.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="hostEvents">The events, in the order they were raised.</param>
    /// <param name="how">How they came about: <c>request</c>, <c>offer</c> or <c>fail_forward</c> (for the log).</param>
    /// <param name="slots">The slots of the learner's line (village-plan V02: the place a direction names, the number of items asked for); empty for an opening.</param>
    protected virtual void ApplyHostEvents(string npc, IReadOnlyList<string> hostEvents, string how, IReadOnlyDictionary<string, string> slots)
    {
    }

    /// <summary>Appends a game event to <see cref="Events"/>.</summary>
    protected void Log(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        _events.Add(gameEvent);
        if (gameEvent is VoiceEvent voice)
        {
            _voiceCounts[voice.Kind] = _voiceCounts.GetValueOrDefault(voice.Kind) + 1;
        }

        if (gameEvent is PhraseUse use)
        {
            _ = PhraseProgress.Apply(use, Credit); // the phrase progress is fed from the events, in the order they are logged (S8)
        }
        else if (gameEvent is WorldEvent world)
        {
            Tips.Offer(world); // a first-time tip waits for the event it explains (S16), with its filters (C06)
            Lessons.OnEvent(world); // a lesson's do step waits for a world event (S16)
            OnWorldEvent(world);
        }
        else if (gameEvent is LessonEvent { Kind: LessonEventKind.StepStarted } step)
        {
            OnLessonStepStarted(step); // issue #54: the step of a lesson that tells an order gives it
        }
    }

    /// <summary>A lesson step began (issue #54): the 3D scene gives the order whose lesson tells it at this step (<c>lessonStep</c>). The base game has no orders.</summary>
    /// <param name="step">The lesson event.</param>
    protected virtual void OnLessonStepStarted(LessonEvent step)
    {
    }

    /// <summary>
    /// The order of an NPC whose goods the player carries now (village-plan V03: an objective with <c>deliverTo</c> that is shown and not done), or
    /// <see langword="null"/>. The talk then opens with the NPC's thanks (<c>&lt;npc&gt;.delivered</c>). The 3D scene has the objectives; the base game has none.
    /// </summary>
    /// <param name="npc">The NPC the panel opens on.</param>
    protected virtual string? OrderReadyAt(string npc) => null;

    /// <summary>
    /// Whether an NPC has an order whose goods the player carries and that a lesson keeps back until its step (village-plan V13): the NPC then says why a purchase the coins
    /// do not cover waits (V21, <see cref="NpcHostEvents.CantPayWait"/>). The 3D scene has the objectives; the base game has none.
    /// </summary>
    /// <param name="npc">The NPC.</param>
    protected virtual bool OrderHeldAt(string npc) => false;

    /// <summary>
    /// The gifts the NPCs are not to give now (N06, <c>GiftRule.Key</c>): those given already today that no lesson step requires, or <see langword="null"/> for none. An NPC asked
    /// for one answers <c>&lt;npc&gt;.gift_used</c>. The 3D scene has the days; the base game has none.
    /// </summary>
    protected virtual IReadOnlySet<string>? ClosedGifts => null;

    /// <summary>The land's facts for the mayor's answer to a purchase (Y11), taken now for a turn's scene; the 3D scene has the land, the base game has none.</summary>
    protected virtual PlotFacts? PlotFactsNow() => null;

    /// <summary>
    /// The host event of the offer an NPC opens the talk with about the player's land (Y11: <c>&lt;npc&gt;.offer_stake</c> or <c>&lt;npc&gt;.offer_plot</c>), or <see langword="null"/> for none. The 3D scene
    /// knows the land; the base game has none.
    /// </summary>
    /// <param name="npc">The NPC the panel opens on.</param>
    protected virtual string? LandOfferAt(string npc) => null;

    /// <summary>The NPC thanked the player for an order (its opening was <c>&lt;npc&gt;.delivered</c>): the goods leave the inventory. The 3D scene does it.</summary>
    /// <param name="npc">The NPC.</param>
    /// <param name="order">The order's objective id (<see cref="OrderReadyAt"/>).</param>
    protected virtual void TakeOrder(string npc, string order)
    {
    }

    /// <summary>A world event was logged (after the tips and the lessons saw it): the 3D session counts it for its objectives (C07).</summary>
    /// <param name="world">The event.</param>
    protected virtual void OnWorldEvent(WorldEvent world)
    {
    }

    // What follows an applied turn: the phrase it used, the voice event it was, and what its host events do in the world. Returns the turn as the player is to see
    // it: the same turn, or (S17) a copy marked as taken by the lesson, or with the guide's last line added because the learner asked for it again.
    private GuideTurn AfterTurn(GuideTurn turn, long tick, long time, PhraseReview? phraseReview, SupportSnapshot? support)
    {
        support ??= Support.Snapshot();
        var npc = turn.Npc;
        _lastRepair.TryGetValue(npc, out var previousRepair);
        if (!turn.Unsure)
        {
            _lastRepair[npc] = turn.RepairKind; // a line held back as unsure changed nothing: the offer of choices, if any, still stands
        }

        if (turn.HeardOpen)
        {
            // V31: the line was read from the open decode; the playtest counts these. Credited as typed (GuideTurn.CreditKind).
            Log(new VoiceEvent(tick, time, VoiceEventKind.HeardOpen, npc, turn.Line));
        }

        if (turn.Uncertain)
        {
            // D-game-9: an uncertain recognition is the microphone's trouble, not the language's: a voice event, no phrase use, no error.
            Log(new VoiceEvent(tick, time, VoiceEventKind.Uncertain, npc, turn.Line));
        }

        // A line said at the receiving prompt of an NPC that waits for an order's goods (issue #42) is the hand-over's, never a lesson step's answer.
        var handOverTurn = IsHandOverTurn(turn);

        // A lesson step that will count the answer as a miss for the person or the tense of its verb (S26, S27) does not credit the phrase: the learner did not say it.
        var formMiss = !handOverTurn && Lessons.IsFormMiss(turn);
        var retryAnswer = phraseReview is not null && ReferenceEquals(phraseReview, _retry) && turn.Communicated && !IsHelpRequest(turn);
        var recovered = false;
        if (turn.Taken && !formMiss && PhraseOf(turn, support) is { } phrase)
        {
            var kind = Classify(turn, phrase, previousRepair, phraseReview, support);
            var detail = PhraseSheet.DetailOf(turn.Slots);
            var correct = turn.Errors.Count == 0;
            IReadOnlyList<string>? inPlay = kind == PhraseUseKind.DontKnow ? (phraseReview is not null ? [phraseReview.PhraseId] : support.InPlay) : null;
            Log(new PhraseUse(tick, time, phrase, kind, npc, turn.Prompt, turn.Line, detail, turn.CreditKind, correct, PhraseProgress.LevelOf(phrase), inPlay));
            recovered = retryAnswer && correct && string.Equals(phrase, phraseReview?.PhraseId, StringComparison.Ordinal) && kind is PhraseUseKind.Unaided or PhraseUseKind.Supported;
            if (kind == PhraseUseKind.DontKnow)
            {
                RevealSupport(); // the NPC gave the model: its card is up and the next use counts as supported
                if (string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal) && inPlay is not null)
                {
                    ScheduleSecondTry(npc, inPlay); // "no sé" (not "ayuda") queues its phrase for a second try in this session (X04, R02)
                }
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

        if (retryAnswer && phraseReview is { } answered)
        {
            // The answer to the second try (X04, R02): "recovered" when it was the same phrase said correctly, with or without the card. Its use was counted like any
            // review answer, so the spacing gap (D-game-6) still decides any credit.
            Log(new WorldEvent(tick, time, WorldEvents.SecondTryAnswered, npc, $"{answered.PhraseId}, {(recovered ? "recovered" : "not_yet")}"));
            _retry = null;
        }

        if (turn.FailForward)
        {
            Log(new PhraseUse(tick, time, PhraseIds.RequestWater, PhraseUseKind.FailForward, npc, turn.Prompt, turn.Line, turn.Communicated ? turn.Intent : "no request"));
        }

        if (turn.HostEvents.Count > 0)
        {
            ApplyHostEvents(npc, turn.HostEvents, turn.FailForward ? "fail_forward" : "request", NpcEffect.CountedSlots(turn.Slots, turn.Line));
        }

        if (!turn.Unsure && string.Equals(turn.AgentState, NpcHostEvents.OfferingState, StringComparison.Ordinal))
        {
            NoteOffer(npc); // W08: the answer to «¿cómo hago pan?» was the offer
        }

        if (!turn.Unsure && turn.Input == InputKind.Spoken)
        {
            _spokenTurns++;
        }
        else if (turn.Input == InputKind.Typed)
        {
            _typedTurns++;
        }

        var hadLast = _lastLine.TryGetValue(npc, out var last);
        if (turn.Communicated && !turn.Unsure && (turn.HostEvents.Contains(GuideEvents.Repeat, StringComparer.Ordinal) || turn.HostEvents.Contains(GuideEvents.RepeatSlow, StringComparer.Ordinal)))
        {
            Lessons.NoteRepeat(npc); // the learner asked for the line again: the lesson's record counts it (S19)
        }

        var handled = !handOverTurn && (Lessons.OnTurn(turn) || Lessons.OnClassTurn(turn)); // a lesson's repeat or answer step ends, retries or moves on with this turn (S16); it speaks for the guide (S17)
        var shown = turn; // (`last` was read before the turn ended the step: the next step's line is not what the learner asked to hear again)
        if (turn.Communicated && !turn.Unsure && (turn.HostEvents.Contains(GuideEvents.Repeat, StringComparer.Ordinal) || turn.HostEvents.Contains(GuideEvents.RepeatSlow, StringComparer.Ordinal)))
        {
            // "Otra vez, por favor" and "Más despacio, por favor" do what they say (S17): the guide's last line is shown and played again, the second time slowly.
            var slow = turn.HostEvents.Contains(GuideEvents.RepeatSlow, StringComparer.Ordinal);
            shown = turn with
            {
                LessonHandled = handled,
                SlowFollowUp = slow,
                FollowUp = hadLast ? [.. turn.FollowUp ?? [], last!] : turn.FollowUp,
            };
        }
        else
        {
            if (handled)
            {
                // A miss for the person or the tense (S26, S27) shows no correction line of the core: its recast would echo the learner's own words with "¡Eso es!", against the lesson's retry.
                shown = turn with { LessonHandled = true, Feedback = formMiss ? null : turn.Feedback };
            }

            if (!handled && shown.GuideLines.Count > 0)
            {
                _lastLine[npc] = shown.GuideLines[^1];
            }
        }

        var waited = HandOverOpen;
        shown = HandOverAfter(turn, shown); // issue #42: the line that hands an order over, or a miss at the NPC's receiving prompt
        LandOfferAfter(turn); // Y11: the mayor's offer is answered, or asked again
        OrderOfferAfter(turn); // issue #54: «sí» to the NPC's offer gives the order, «no» puts it off until the next day (after the lesson's turn: a class offered now waits for the next line)
        if (_talkOpen && string.Equals(Support.Npc, npc, StringComparison.Ordinal) && !turn.Unsure)
        {
            // The prompt may have moved on (the bread offer, a hand-over done), and a level may have changed.
            RefreshSupport(npc, waited && !HandOverOpen && PendingTurns == 0 ? _npcs.PromptOf(npc) : _npcs.PromptAfter(npc, turn.AgentState));
        }

        return shown;
    }

    // The phrase a communicated turn used: the phrase in play on the prompt whose row has the turn's intent (the NPC's focus there, a lesson step's phrase, S16, or the
    // review's), else the row the prompt teaches for the intent, else the fixed mapping (PhraseSheet.PhraseFor). Of several in play with the intent (the fisher's two
    // directions), the one that names the place asked about (PhraseSheet.OfIntent); in free talk, where the NPC's focus is in play, one row of the intent that names
    // another place gives way to the row practised elsewhere that names it (V14: the villager's river for the fisher's bakery, PhraseSheet.ByPlace). In free talk, a
    // line that is a phrase of a lesson the learner has begun or done (its Spanish or a variant, LineReading.PhraseSaid) is that phrase, at any NPC, before the fixed
    // mapping (V20: only the focus and the prompt's rows counted, so «Mañana compraré pan.» said to Luz after its lesson was no use of it).
    private string? PhraseOf(GuideTurn turn, SupportSnapshot support)
    {
        var inPlay = support.InPlay.Select(Phrases.Find).OfType<PhraseRow>().ToList();
        var freeTalk = support.InPlay.SequenceEqual(Support.FocusAt(turn.Prompt), StringComparer.Ordinal);
        if (turn.OffPromptIntent is not null)
        {
            // V27: a line the NPC understood off its prompt credits only a phrase of a lesson begun or done that it is; the intent it has elsewhere says no phrase of this prompt.
            return freeTalk ? LineReading.PhraseSaid(Phrases, turn.Line, Data.Npcs.Find(turn.Npc)?.Name, Lessons.Credits)?.Id : null;
        }

        if (freeTalk && Phrases.ByPlace(inPlay, turn.Intent, turn.Slots, Data.Npcs.Homes.Keys, out var placed))
        {
            return placed;
        }

        var said = freeTalk ? LineReading.PhraseSaid(Phrases, turn.Line, Data.Npcs.Find(turn.Npc)?.Name, Lessons.Credits)?.Id : null;
        return PhraseSheet.OfIntent(inPlay, turn.Intent, turn.Slots, Data.Nouns)
            ?? Phrases.PhraseFor(turn.Prompt, turn.Intent, turn.Slots, Data.Words.Noun("river"), freeTalk ? Data.Npcs.Homes.Keys : null, said);
    }

    private static bool IsHelpRequest(GuideTurn turn) =>
        string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal) || string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal);

    // The support cards and sounds for the prompt the NPC waits on; a card that newly appears counts as shown and, for the core's learner model, as a
    // phrase skill step (LearnerModel.StepSkills), deferred while a turn is on the worker.
    private void RefreshSupport(string npc, string prompt)
    {
        var inLesson = !HandOverOpen && Lessons.Engaged && string.Equals(npc, Lessons.CurrentNpc, StringComparison.Ordinal); // a hand-over waiting (issue #42) has the card
        if (inLesson && Lessons.Focus is null && !string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal))
        {
            // A lesson step of the villager's or the shopkeeper's that asks for no phrase (S26): the stall's and the villager's own phrase cards stay down, as the guide's do (her
            // own prompt asks for none). The quiet prompt is one no row of the sheet uses.
            prompt = QuietPrompt;
        }

        var change = Support.Show(npc, prompt, PhraseReview?.PhraseId, inLesson ? Lessons.Focus : null);
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

        if (phraseReview is not null && Data.Npcs.FocusAt(phraseReview.Prompt).Contains(phrase, StringComparer.Ordinal))
        {
            // The guide's review of a due phrase (S8): no card is shown, so the answer is unaided unless help was asked for first.
            return support.Scaffolded.Count > 0 ? PhraseUseKind.Supported : PhraseUseKind.Unaided;
        }

        if (ObjectiveComplete && Data.Npcs.HomeOf(phrase) is not null && !Data.Npcs.IsHomeOf(turn.Npc, phrase))
        {
            // Said away from every NPC whose prompt practises it (the smith's and the farmer's "¿Cómo te llamas?" is at home with both, V12b).
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
            ? new GuideScene(Viewpoint, cursor, Pointer is not null, Inventory: inventory, Facts: facts, PromptOverride: PhraseReview?.Prompt ?? LessonPrompt, Credited: Lessons.CreditedPhrases, OrderHeld: OrderHeldAt(CurrentNpc), ClosedGifts: ClosedGifts, Grant: TeachingNpc is null ? null : PlanGrant(), Offer: BakeOfferOf(CurrentNpc, asked: true), WeatherKey: WeatherKey, Land: PlotFactsNow())
            : new GuideScene(Viewpoint, review.Target, Pointer is not null, Review: true, Inventory: inventory, Facts: facts, WeatherKey: WeatherKey);
    }
}
