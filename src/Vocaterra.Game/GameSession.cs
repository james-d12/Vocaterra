using Vocaterra.Nlp.Content;
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
/// <see cref="CreditRules.Default"/> credit for typed input, with the stored time of the turn (spaced uses, D-game-6 as amended).
/// </para>
/// <para>
/// <b>Skipped time.</b> <see cref="SkipTime"/> moves the stored time on without stepping the world (the scripted <c>--skip</c> and the
/// tests); a save (P9) will keep the total. <b>Review.</b> <see cref="BeginTalk"/> is called when the player opens the dialogue: if a word is
/// due and an example of it is in range, the guide points at the nearest one and asks what it is (<see cref="Review"/>), and the next
/// understood answer is graded against that tile.
/// </para>
/// <para>Evaluation is synchronous in P3 and never runs inside <see cref="Step"/>; P5 moves it to a worker thread.</para>
/// </remarks>
public sealed class GameSession
{
    /// <summary>The start time used when a caller gives none: 2025-10-09 08:53:20 UTC, the time the console host's one-shot runs use.</summary>
    public const long DefaultStartedAt = 1_760_000_000;

    /// <summary>The player has reached the arrow's target within this many tiles on each axis.</summary>
    public const int ReachTiles = 2;

    private readonly GuideService _guideService;

    /// <summary>
    /// Starts a game on an already loaded bundle.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: it generates the world and seeds the conversation's random draws.</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle (<see cref="NounCatalog.Load"/>).</param>
    /// <param name="playSession">The number of this play session (1 for a new game; the saves of P9 will bump it on each load).</param>
    public GameSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns, int playSession = 1)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        Bundle = bundle;
        Nouns = nouns;
        WorldSeed = worldSeed;
        StartedAt = startedAt;
        Simulation = GameSimulation.Create(worldSeed);
        Facts = new WorldFacts(Simulation.World);
        Guide = GuideNpc.Place(Simulation.World);
        Words = new WordProgress(nouns, playSession);
        _guideService = new GuideService(bundle, Facts, worldSeed, new StoredTimestamp(startedAt));
    }

    /// <summary>The content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The noun catalogue: which tile is which noun, and each noun's article and gender.</summary>
    public NounCatalog Nouns { get; }

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
        foreach (var due in Words.DueWords(StoredTime.UnixSeconds, CreditRules.Default))
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
            ? new GuideScene(PlayerTile, cursor, Pointer is not null)
            : new GuideScene(PlayerTile, review.Target, Pointer is not null, Review: true);
        var turn = _guideService.Say(line, scene, StoredTime, Simulation.ComputeStateDigest());
        if (review is not null && turn.Communicated)
        {
            EndReview(); // understood: the question is answered (a line the guide did not understand leaves it open for another try)
        }

        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target);
        }

        LastWordTurn = Words.RecordTurn(turn, InputKind.Typed, CreditRules.Default, StoredTime.UnixSeconds);
        return turn;
    }

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;
}
