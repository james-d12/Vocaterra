using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// An arrow the guide put out: it points at a tile until the player gets there or closes it.
/// </summary>
/// <param name="Place">The noun the player asked about (<c>árbol</c>).</param>
/// <param name="Target">The tile it points at.</param>
/// <param name="Quiz">Whether it is the guide's review question: the front-end then hides the noun, which is the answer.</param>
public sealed record PointerMarker(string Place, TilePoint Target, bool Quiz = false);

/// <summary>
/// The guide's review question in progress: she pointed at an example of a due word and asked what it is. The player's answer is graded
/// against <see cref="Target"/> (the pointed tile), not against the mouse cursor.
/// </summary>
/// <param name="Lemma">The due word whose example she pointed at.</param>
/// <param name="Target">The tile of the example (the nearest to the player when the dialogue opened).</param>
/// <param name="Question">What she asks (<c>¿Qué es esto?</c>).</param>
public sealed record GuideReview(string Lemma, TilePoint Target, string Question);

/// <summary>A turn the worker finished and <see cref="GameSession.Collect"/> applied.</summary>
/// <param name="Outcome">The request, the turn (or why there is none) and the timings.</param>
/// <param name="Words">What the turn did to the learner's word counts, or <see langword="null"/> when there was no turn.</param>
public sealed record AppliedTurn(TurnOutcome Outcome, WordTurnResult? Words)
{
    /// <summary>The turn, or <see langword="null"/> when the request failed.</summary>
    public GuideTurn? Turn => Outcome.Turn;
}

/// <summary>
/// One running game, engine-free: the simulation (the world and the player's body), the content bundle, the guide and the conversation with
/// it, the world seed and the stored time. A front-end calls <see cref="Step"/> once per tick with the player's commands and
/// <see cref="Say"/> for each line the player types; it reads everything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time.</b> The stored time the core sees is a fixed start (<see cref="StartedAt"/>, Unix seconds, passed in) plus
/// <c>Tick / 60</c> seconds of game time. Nothing here reads the clock, so a scripted run gives the same turns on every machine. The save
/// file (P9) will own the start time.
/// </para>
/// <para>
/// <b>Where the guide lives.</b> In this layer, not in the simulation: it is a pure function of the generated world
/// (<see cref="GuideNpc.Place"/>), it does not move or collide yet, and its conversation state belongs to the agent state machine
/// (<see cref="GuideService.GuideState"/>). Putting it in the simulation would add a record to the state digest and the saves for nothing
/// that ticks. When NPCs walk, they move into the simulation.
/// </para>
/// <para>
/// <b>Words.</b> Every turn is counted in <see cref="Words"/> (the learner's progress with the catalogue nouns, D-game-6) with the
/// <see cref="Credit"/> rules for the kind of input of the turn (typed input earns half by default, spoken input full: D-game-6) and the stored
/// time of the turn (spaced uses, D-game-6 as amended).
/// </para>
/// <para>
/// <b>Skipped time.</b> <see cref="SkipTime"/> moves the stored time on without stepping the world (the scripted <c>--skip</c> and the
/// tests); a save (P9) will keep the total. <b>Review.</b> <see cref="BeginTalk"/> is called when the player opens the dialogue: if a word is
/// due and an example of it is in range, the guide points at the nearest one and asks what it is (<see cref="Review"/>), and the next
/// understood answer is graded against that tile.
/// </para>
/// <para>
/// <b>Turns on a worker (P5).</b> After <see cref="StartWorker"/> a turn is <i>submitted</i> (<see cref="SubmitTyped"/>,
/// <see cref="SubmitClip"/>, <see cref="SubmitWav"/>, <see cref="SubmitSpeak"/>) and the frame goes on: the stored time, the state digest, a
/// copy of the world around the player and a copy of the inventory are captured at the submission, a dedicated thread (<see cref="TurnWorker"/>)
/// evaluates, and <see cref="Collect"/> applies the finished turns on the caller's thread, in the order they were submitted. Nothing here
/// blocks. <see cref="Say"/> is the synchronous path (tests and the console host's sibling): do not mix it with the worker.
/// </para>
/// </remarks>
public sealed class GameSession : IDisposable
{
    /// <summary>The start time used when a caller gives none: 2025-10-09 08:53:20 UTC, the time the console host's one-shot runs use.</summary>
    public const long DefaultStartedAt = 1_760_000_000;

    /// <summary>The player has reached the arrow's target within this many tiles on each axis.</summary>
    public const int ReachTiles = 2;

    private readonly GuideService _guideService;
    private TurnWorker? _worker;
    private long _nextTurnId = 1;

    /// <summary>
    /// Starts a game on an already loaded bundle.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: it generates the world and seeds the conversation's random draws.</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle (<see cref="NounCatalog.Load"/>).</param>
    /// <param name="playSession">The number of this play session (1 for a new game; the saves of P9 will bump it on each load).</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> (typed half, spoken full) when <see langword="null"/>.</param>
    public GameSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns, int playSession = 1, CreditRules? credit = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        Bundle = bundle;
        Nouns = nouns;
        Credit = credit ?? CreditRules.Default;
        WorldSeed = worldSeed;
        StartedAt = startedAt;
        Recipes = RecipeBook.Load(bundle, nouns);
        Simulation = GameSimulation.Create(worldSeed);
        Simulation.SetRecipes(Recipes.Rules);
        Facts = new WorldFacts(Simulation.World);
        Guide = GuideNpc.Place(Simulation.World);
        Words = new WordProgress(nouns, playSession);
        _guideService = new GuideService(bundle, Facts, nouns, worldSeed, new StoredTimestamp(startedAt), Recipes);
    }

    /// <summary>The content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The credit a correct use earns per kind of input, and the gaps between credited uses (D-game-6).</summary>
    public CreditRules Credit { get; }

    /// <summary>The worker that evaluates submitted turns, or <see langword="null"/> before <see cref="StartWorker"/>.</summary>
    public TurnWorker? Worker => _worker;

    /// <summary>Submitted turns not yet applied by <see cref="Collect"/> (the guide is thinking).</summary>
    public int PendingTurns => _worker?.Pending ?? 0;

    /// <summary>The noun catalogue: which tile is which noun, and each noun's article and gender.</summary>
    public NounCatalog Nouns { get; }

    /// <summary>The crafting recipes of the bundle (the simulation holds the same recipes as plain values, in the same order).</summary>
    public RecipeBook Recipes { get; }

    /// <summary>The learner's progress with the catalogue nouns, fed from every turn.</summary>
    public WordProgress Words { get; }

    /// <summary>What the last turn changed in the word counts: the nouns it credited and the nouns it made known (null before the first turn).</summary>
    public WordTurnResult? LastWordTurn { get; private set; }

    /// <summary>The world seed.</summary>
    public ulong WorldSeed { get; }

    /// <summary>The stored time at tick 0, in Unix seconds.</summary>
    public long StartedAt { get; }

    /// <summary>The simulation: the world, the player body and the tick counter.</summary>
    public GameSimulation Simulation { get; }

    /// <summary>What the guide can say about the world.</summary>
    public WorldFacts Facts { get; }

    /// <summary>Where the guide stands.</summary>
    public GuideNpc Guide { get; }

    /// <summary>The conversation with the guide: its state, the learner model and the repeat counts.</summary>
    public GuideService Conversation => _guideService;

    /// <summary>The arrow now on screen, or <see langword="null"/>.</summary>
    public PointerMarker? Pointer { get; private set; }

    /// <summary>The stored seconds added by <see cref="SkipTime"/> on top of the stepped game time.</summary>
    public long SkippedSeconds { get; private set; }

    /// <summary>The stored time now: the start plus the seconds of game time that have been stepped plus the skipped seconds.</summary>
    public StoredTimestamp StoredTime => new(StartedAt + (Simulation.Tick / GameSimulation.TicksPerSecond) + SkippedSeconds);

    /// <summary>The review question in progress, or <see langword="null"/>.</summary>
    public GuideReview? Review { get; private set; }

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
            var found = Facts.NearestOfNoun(PlayerTile, due.Lemma);
            if (found is null)
            {
                continue;
            }

            Review = new GuideReview(due.Lemma, found.Tile, _guideService.ReviewQuestion());
            Pointer = new PointerMarker(due.Lemma, found.Tile, Quiz: true);
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

    /// <summary>The tile under the middle of the player's body.</summary>
    public TilePoint PlayerTile => WorldFacts.CenterTile(Simulation.Player);

    /// <summary>Whether the player is close enough to the guide to talk.</summary>
    public bool IsNearGuide => Guide.IsNear(Simulation.Player);

    /// <summary>The line the guide says when the dialogue opens.</summary>
    public string OpeningLine => _guideService.OpeningLine;

    /// <summary>
    /// Advances the simulation one tick with the player's commands, then clears the arrow if the player has reached its target. The
    /// conversation does not run here: a turn happens in <see cref="Say"/>, between ticks.
    /// </summary>
    public void Step(IReadOnlyList<PlayerCommand> commands)
    {
        Simulation.Step(commands);
        if (Pointer is { } pointer)
        {
            var player = PlayerTile;
            if (Math.Abs(player.X - pointer.Target.X) <= ReachTiles && Math.Abs(player.Y - pointer.Target.Y) <= ReachTiles)
            {
                Pointer = null;
            }
        }
    }

    /// <summary>
    /// The player types a line to the guide. Evaluates it, moves the conversation on, and, if the guide points at something, puts the arrow
    /// out (replacing any earlier one). Call it between ticks, never from inside <see cref="Step"/>.
    /// </summary>
    /// <param name="line">The typed line.</param>
    /// <param name="cursor">The tile under the mouse pointer, for "¿qué es esto?" (ignored while a <see cref="Review"/> is open: the pointed tile counts).</param>
    public GuideTurn Say(string line, TilePoint? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        var review = Review;
        var scene = review is null
            ? new GuideScene(PlayerTile, cursor, Pointer is not null, Inventory: Simulation.Inventory)
            : new GuideScene(PlayerTile, review.Target, Pointer is not null, Review: true, Inventory: Simulation.Inventory);
        var turn = _guideService.Say(line, scene, StoredTime, Simulation.ComputeStateDigest());
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
    /// <param name="cursor">The tile under the mouse pointer (ignored while a review is open: the pointed tile counts).</param>
    public TurnRequest SubmitTyped(string line, TilePoint? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Submit(TurnSource.Typed, line, null, null, cursor);
    }

    /// <summary>Submits a recorded clip (16 kHz, from <see cref="AudioClips.Prepare"/>). Returns at once.</summary>
    public TurnRequest SubmitClip(PcmClip clip, TilePoint? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Submit(TurnSource.Clip, null, clip, null, cursor);
    }

    /// <summary>Submits a WAV file for the worker to read, convert and recognize (<c>--wav</c>). Returns at once.</summary>
    public TurnRequest SubmitWav(string path, TilePoint? cursor = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Submit(TurnSource.Wav, null, null, path, cursor);
    }

    /// <summary>Submits a line for the worker to synthesize with Kokoro and then recognize as spoken input (<c>--speak</c>). Returns at once.</summary>
    public TurnRequest SubmitSpeak(string text, TilePoint? cursor = null)
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

    private TurnRequest Submit(TurnSource source, string? text, PcmClip? clip, string? wavPath, TilePoint? cursor)
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
            Simulation.ComputeStateDigest(),
            review,
            Simulation.Tick);
        worker.Submit(request);
        return request;
    }

    // The moment of a turn as plain values: copies of the world around the player and of the inventory, so the worker never reads what the
    // simulation keeps changing.
    private GuideScene Scene(TilePoint? cursor, GuideReview? review)
    {
        var facts = WorldFacts.Snapshot(Simulation.World, PlayerTile);
        var inventory = Simulation.Inventory.Copy();
        return review is null
            ? new GuideScene(PlayerTile, cursor, Pointer is not null, Inventory: inventory, Facts: facts)
            : new GuideScene(PlayerTile, review.Target, Pointer is not null, Review: true, Inventory: inventory, Facts: facts);
    }

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;
}
