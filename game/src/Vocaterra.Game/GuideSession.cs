using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
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
/// The part of a running game that the guide and the learner share, whatever the world is: the conversation (<see cref="GuideService"/>), the
/// word counts, the arrow, the review question, the stored time and the turn worker. The 3D scene derives from it (<see cref="VoxelSession"/>):
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
/// <b>Turns on a worker (P5).</b> After <see cref="StartWorker"/> a turn is <i>submitted</i> (<see cref="SubmitTyped"/>, <see cref="SubmitClip"/>,
/// <see cref="SubmitWav"/>, <see cref="SubmitSpeak"/>) and the frame goes on: the stored time, the state digest, a copy of the world around the
/// player and a copy of the inventory are captured at the submission; a dedicated thread (<see cref="TurnWorker"/>) evaluates, and
/// <see cref="Collect"/> applies the finished turns on the caller's thread, in the order they were submitted. Nothing here blocks.
/// <see cref="Say"/> is the synchronous path (tests and the console host's sibling): do not mix it with the worker.
/// </para>
/// </remarks>
public abstract class GuideSession : IDisposable
{
    /// <summary>The start time used when a caller gives none: 2025-10-09 08:53:20 UTC, the time the console host's one-shot runs use.</summary>
    public const long DefaultStartedAt = 1_760_000_000;

    private readonly GuideService _guideService;
    private readonly IGuideFacts _liveFacts;
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
        _liveFacts = liveFacts;
        _guideService = new GuideService(bundle, liveFacts, nouns, worldSeed, new StoredTimestamp(startedAt), recipes);
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

    /// <summary>The learner's progress with the catalogue nouns, fed from every turn.</summary>
    public WordProgress Words { get; }

    /// <summary>What the last turn changed in the word counts: the nouns it credited and the nouns it made known (null before the first turn).</summary>
    public WordTurnResult? LastWordTurn { get; private set; }

    /// <summary>The world seed.</summary>
    public ulong WorldSeed { get; }

    /// <summary>The stored time at tick 0, in Unix seconds.</summary>
    public long StartedAt { get; }

    /// <summary>The conversation with the guide: its state, the learner model and the repeat counts.</summary>
    public GuideService Conversation => _guideService;

    /// <summary>The arrow now on screen, or <see langword="null"/>.</summary>
    public PointerMarker? Pointer { get; protected set; }

    /// <summary>The stored seconds added by <see cref="SkipTime"/> on top of the stepped game time.</summary>
    public long SkippedSeconds { get; private set; }

    /// <summary>The stored time now: the start plus the seconds of game time that have been stepped plus the skipped seconds.</summary>
    public StoredTimestamp StoredTime => new(StartedAt + (Tick / VoxelSimulation.TicksPerSecond) + SkippedSeconds);

    /// <summary>The review question in progress, or <see langword="null"/>.</summary>
    public GuideReview? Review { get; private set; }

    /// <summary>The line the guide says when the dialogue opens (<c>PromptSpec.Text</c> of the prompt).</summary>
    public string OpeningLine => _guideService.OpeningLine;

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

            Review = new GuideReview(due.Lemma, found.Cell, _guideService.ReviewQuestion());
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
    /// The player types a line to the guide. Evaluates it, moves the conversation on, and, if the guide points at something, puts the arrow
    /// out (replacing any earlier one). Call it between ticks, never while the worker is running.
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
        var turn = _guideService.Say(line, scene, StoredTime, StateDigest());
        if (review is not null && turn.Communicated)
        {
            EndReview(); // understood: the question is answered (a line the guide did not understand leaves it open for another try)
        }

        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target);
        }

        LastWordTurn = Words.RecordTurn(turn, turn.Input, Credit, StoredTime.UnixSeconds);
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

        _worker = new TurnWorker(_guideService, Bundle, clockMilliseconds, loadSpeech);
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
            WordTurnResult? words = null;
            if (outcome!.Turn is { } turn)
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
            }

            applied.Add(new AppliedTurn(outcome, words));
        }

        return applied;
    }

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;

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
            Tick);
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
