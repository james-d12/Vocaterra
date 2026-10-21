using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// One running game of the 2D slice, engine-free: the simulation (the world and the player's body), the content bundle, the guide and the
/// conversation with it, the world seed and the stored time. A front-end calls <see cref="Step"/> once per tick with the player's commands and
/// <see cref="GuideSession.Say"/> for each line the player types; it reads everything else. The shared part (the conversation, the words, the
/// arrow, the review and the worker) is <see cref="GuideSession"/>; the 3D scene's counterpart is <see cref="VoxelSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the guide lives.</b> In this layer, not in the simulation: it is a pure function of the generated world
/// (<see cref="GuideNpc.Place"/>), it does not move or collide yet, and its conversation state belongs to the agent state machine
/// (<see cref="GuideService.GuideState"/>). Putting it in the simulation would add a record to the state digest and the saves for nothing
/// that ticks. When NPCs walk, they move into the simulation.
/// </para>
/// </remarks>
public sealed class GameSession : GuideSession
{
    /// <summary>The player has reached the arrow's target within this many tiles on each axis.</summary>
    public const int ReachTiles = 2;

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
        : this(bundle, worldSeed, startedAt, nouns, playSession, credit, GameSimulation.Create(worldSeed), RecipeBook.Load(bundle ?? throw new ArgumentNullException(nameof(bundle)), nouns))
    {
    }

    private GameSession(
        ContentBundle bundle,
        ulong worldSeed,
        long startedAt,
        NounCatalog nouns,
        int playSession,
        CreditRules? credit,
        GameSimulation simulation,
        RecipeBook recipes)
        : base(bundle, worldSeed, startedAt, nouns, new WorldFacts(simulation.World), recipes, playSession, credit)
    {
        Simulation = simulation;
        Simulation.SetRecipes(recipes.Rules);
        Facts = new WorldFacts(simulation.World);
        Guide = GuideNpc.Place(simulation.World);
    }

    /// <summary>The simulation: the world, the player body and the tick counter.</summary>
    public GameSimulation Simulation { get; }

    /// <summary>What the guide can say about the world (the same world as <see cref="Simulation"/>).</summary>
    public WorldFacts Facts { get; }

    /// <summary>Where the guide stands.</summary>
    public GuideNpc Guide { get; }

    /// <summary>The tile under the middle of the player's body.</summary>
    public TilePoint PlayerTile => WorldFacts.CenterTile(Simulation.Player);

    /// <summary>Whether the player is close enough to the guide to talk.</summary>
    public bool IsNearGuide => Guide.IsNear(Simulation.Player);

    /// <inheritdoc />
    protected override long Tick => Simulation.Tick;

    /// <inheritdoc />
    protected override GuideViewpoint Viewpoint => PlayerTile;

    /// <inheritdoc />
    protected override Vocaterra.Simulation.Items.Inventory LiveInventory => Simulation.Inventory;

    /// <inheritdoc />
    protected override IGuideFacts SnapshotFacts() => WorldFacts.Snapshot(Simulation.World, PlayerTile);

    /// <inheritdoc />
    protected override string StateDigest() => Simulation.ComputeStateDigest();

    /// <summary>
    /// Advances the simulation one tick with the player's commands, then clears the arrow if the player has reached its target. The
    /// conversation does not run here: a turn happens in <see cref="GuideSession.Say"/>, between ticks.
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
}
