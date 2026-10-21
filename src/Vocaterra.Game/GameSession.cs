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
public sealed record PointerMarker(string Place, TilePoint Target);

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
/// <see cref="CreditRules.Default"/> credit for typed input.
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
    public GameSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns)
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
        Words = new WordProgress(nouns);
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

    /// <summary>The stored time now: the start plus the seconds of game time that have been stepped.</summary>
    public StoredTimestamp StoredTime => new(StartedAt + (Simulation.Tick / GameSimulation.TicksPerSecond));

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
    /// <param name="cursor">The tile under the mouse pointer, for "¿qué es esto?".</param>
    public GuideTurn Say(string line, TilePoint? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        var scene = new GuideScene(PlayerTile, cursor, Pointer is not null);
        var turn = _guideService.Say(line, scene, StoredTime, Simulation.ComputeStateDigest());
        if (turn.Pointer is { Blocks: > 0 } pointer)
        {
            Pointer = new PointerMarker(pointer.Place, pointer.Target);
        }

        LastWordTurn = Words.RecordTurn(turn, InputKind.Typed, CreditRules.Default);
        return turn;
    }

    /// <summary>Takes the arrow off the screen (the player closed the dialogue or asked for it to go).</summary>
    public void DismissPointer() => Pointer = null;
}
