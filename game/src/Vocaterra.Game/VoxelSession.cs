using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// One running game of the 3D scene (the prototype's only world since S4): the voxel simulation over the bundle's scene, and the guide, the
/// word counts, the arrow, the camp objective, the word cards and the turn worker around it (the shared part is <see cref="GuideSession"/>).
/// The guide's facts are the blocks of the scene: the nearest block of a noun and its direction from where the player faces
/// (<see cref="WorldFacts3D"/>).
/// </summary>
/// <remarks>
/// The front-end owns the clock and the input: it calls <see cref="Step"/> once per tick with the body's commands, and it reads the simulation,
/// the arrow, the objective and the words. Nothing here reads the clock or draws a random number of its own.
/// </remarks>
public sealed class VoxelSession : GuideSession
{
    /// <summary>The arrow is taken down when the player comes within this many blocks of its target (the beacon's own reach).</summary>
    public const int ReachBlocks = 3;

    private readonly HashSet<string> _carded = new(StringComparer.Ordinal);
    private readonly List<WordCard> _newCards = [];
    private readonly List<BlockPos> _fireRing = [];
    private IReadOnlyList<ObjectiveStep> _completed = [];

    /// <summary>
    /// Starts a 3D game on a loaded bundle and a built scene.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: the guide's random draws are keyed by it (the scene itself is built by the caller from the same seed).</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle.</param>
    /// <param name="scene">The built scene; the simulation starts with the player at its spawn.</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> when <see langword="null"/>.</param>
    public VoxelSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns, Scene scene, CreditRules? credit = null)
        : base(
            bundle,
            worldSeed,
            startedAt,
            nouns,
            WorldFacts3D.Live(scene?.Grid ?? throw new ArgumentNullException(nameof(scene)), nouns),
            RecipeBook.Load(bundle, nouns),
            playSession: 1,
            credit)
    {
        Scene = scene;
        Simulation = VoxelSimulation.Create(scene);
        Simulation.SetRecipes(Recipes.Rules);
        Objective = Objective.Camp();
        ScanFireRing(scene.Grid);
    }

    /// <summary>The built scene (the world the facts read).</summary>
    public Scene Scene { get; }

    /// <summary>The 3D simulation: the body, the inventory, the tick counter and the events.</summary>
    public VoxelSimulation Simulation { get; }

    /// <summary>The camp objective: the checklist the player completes (gathering, the torch at the fire ring, the water).</summary>
    public Objective Objective { get; }

    /// <summary>The objective steps the last <see cref="Step"/> or <see cref="HostEvent"/> completed (empty when none).</summary>
    public IReadOnlyList<ObjectiveStep> Completed => _completed;

    /// <summary>The block the crosshair is on (the nearest solid or gatherable block in reach), or <see langword="null"/>.</summary>
    public GuideCell? AimedCell => Simulation.Target is { } hit ? hit.Block : null;

    /// <inheritdoc />
    protected override long Tick => Simulation.Tick;

    /// <inheritdoc />
    protected override GuideViewpoint Viewpoint => new(Simulation.Player.FeetBlock, Simulation.Player.CenterX, Simulation.Player.CenterZ, Simulation.Player.Yaw);

    /// <inheritdoc />
    protected override Inventory LiveInventory => Simulation.Inventory;

    /// <inheritdoc />
    protected override IGuideFacts SnapshotFacts() => WorldFacts3D.Snapshot(Scene.Grid, Nouns);

    /// <inheritdoc />
    protected override string StateDigest() => Simulation.ComputeStateDigest();

    /// <summary>
    /// Advances the simulation one tick with the body's commands, then feeds the objective and the word cards from the tick's events, and takes
    /// the arrow down when the player is within <see cref="ReachBlocks"/> of its target. The conversation does not run here (see
    /// <see cref="GuideSession.Say"/>).
    /// </summary>
    public void Step(IReadOnlyList<VoxelCommand> commands)
    {
        Simulation.Step(commands);
        var completed = new List<ObjectiveStep>();
        foreach (var e in Simulation.Events)
        {
            switch (e.Kind)
            {
                case SimulationEventKind.ItemPickedUp:
                    completed.AddRange(Objective.RecordPickup((ItemId)e.Detail, e.Amount));
                    LearnItem((ItemId)e.Detail);
                    break;
                case SimulationEventKind.ItemCrafted:
                    LearnItem((ItemId)e.Detail);
                    break;
                case SimulationEventKind.BlockPlaced:
                    if ((BlockId)e.Detail == BlockId.Torch)
                    {
                        completed.AddRange(Objective.RecordTorchPlaced(AtFireRing(new BlockPos(e.X, e.Y, e.Z))));
                    }

                    break;
                default:
                    break;
            }
        }

        _completed = completed;
        if (Pointer is { } pointer)
        {
            const long Unit = VoxelPhysics.UnitsPerBlock;
            var dx = Math.Abs((pointer.Target.X * Unit) + (Unit / 2) - Simulation.Player.CenterX);
            var dz = Math.Abs((pointer.Target.Z * Unit) + (Unit / 2) - Simulation.Player.CenterZ);
            if (dx <= ReachBlocks * Unit && dz <= ReachBlocks * Unit)
            {
                Pointer = null;
            }
        }
    }

    /// <summary>
    /// A named host event for the objective (<see cref="Objective.WaterEvent"/> when the bottle is filled, in S6). Returns the steps it completed.
    /// </summary>
    public IReadOnlyList<ObjectiveStep> HostEvent(string name)
    {
        _completed = Objective.RecordHostEvent(name);
        return _completed;
    }

    /// <summary>
    /// The word cards of the first pickups and crafts since the last call, in order, and clears them. Each card has been counted as heard and
    /// read in <see cref="GuideSession.Words"/> already.
    /// </summary>
    public IReadOnlyList<WordCard> TakeWordCards()
    {
        var cards = _newCards.ToArray();
        _newCards.Clear();
        return cards;
    }

    /// <summary>
    /// Whether a cell lies at the camp's fire ring: a cell in the three-by-three square of the ring, on the layer of its stones (the empty
    /// centre) or on top of a stone (the rim). The torch is set on the rim, where a one-block ring does not hide its flame.
    /// </summary>
    public bool AtFireRing(BlockPos cell)
    {
        foreach (var stone in _fireRing)
        {
            if ((stone.Y == cell.Y || stone.Y + 1 == cell.Y) && Math.Abs(stone.X - cell.X) <= 1 && Math.Abs(stone.Z - cell.Z) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    // The first time an item enters the inventory the noun is counted as heard and read, and its card waits for the front-end.
    private void LearnItem(ItemId item)
    {
        var noun = Nouns.ForItem(item);
        if (noun is null || !_carded.Add(noun.Lemma))
        {
            return;
        }

        Words.MarkHeard(noun.Lemma);
        Words.MarkRead(noun.Lemma);
        _newCards.Add(new WordCard(noun.Lemma, noun.WithDefinite));
    }

    private void ScanFireRing(VoxelGrid grid)
    {
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    if (grid.GetBlock(x, y, z) == BlockId.FireRing)
                    {
                        _fireRing.Add(new BlockPos(x, y, z));
                    }
                }
            }
        }
    }
}
