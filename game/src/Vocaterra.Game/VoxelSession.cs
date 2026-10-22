using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// One running game of the 3D scene (the prototype's only world since S4): the voxel simulation over the bundle's scene, and the NPCs (the guide,
/// the shopkeeper and the villager), the word counts, the arrow, the camp objective, the word cards and the turn worker around it (the shared part
/// is <see cref="GuideSession"/>). The guide's facts are the blocks of the scene: the nearest block of a noun and its direction from where the
/// player faces (<see cref="WorldFacts3D"/>). Since S6 the NPCs' host events act on this world (<see cref="NpcHostEvents"/>): the shopkeeper hands
/// over the bottle, which the player fills at the river (the objective's water step), the villager points the beacon at the river.
/// </summary>
/// <remarks>
/// The front-end owns the clock and the input: it calls <see cref="Step"/> once per tick with the body's commands, and it reads the simulation,
/// the arrow, the objective and the words. Nothing here reads the clock or draws a random number of its own.
/// </remarks>
public sealed class VoxelSession : GuideSession
{
    /// <summary>The arrow is taken down when the player comes within this many blocks of its target (the beacon's own reach).</summary>
    public const int ReachBlocks = 3;

    private readonly List<WordCard> _newCards = [];
    private readonly List<BlockPos> _fireRing = [];
    private readonly List<string> _hints = [];
    private IReadOnlyList<ObjectiveStep> _completed = [];
    private bool _riverPointed;

    /// <summary>
    /// Starts a 3D game on a loaded bundle and a built scene.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: the guide's random draws are keyed by it (the scene itself is built by the caller from the same seed).</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle.</param>
    /// <param name="scene">The built scene; the simulation starts with the player at its spawn.</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> when <see langword="null"/>.</param>
    /// <param name="lessons">The lessons to run instead of the bundle's <c>game/lessons.json</c> (the tests' own lessons), or <see langword="null"/> for the bundle's.</param>
    public VoxelSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns, Scene scene, CreditRules? credit = null, LessonBook? lessons = null)
        : base(
            bundle,
            worldSeed,
            startedAt,
            nouns,
            WorldFacts3D.Live(scene?.Grid ?? throw new ArgumentNullException(nameof(scene)), nouns),
            RecipeBook.Load(bundle, nouns),
            playSession: 1,
            credit,
            lessons)
    {
        Scene = scene;
        Simulation = VoxelSimulation.Create(scene);
        Simulation.SetRecipes(Recipes.Rules);
        Objective = Objective.Camp();
        ScanFireRing(scene);
        Tips.Offer("start"); // the opening tips (S16): looking, walking, jumping, collecting
    }

    /// <summary>The built scene (the world the facts read).</summary>
    public Scene Scene { get; }

    /// <summary>The 3D simulation: the body, the inventory, the tick counter and the events.</summary>
    public VoxelSimulation Simulation { get; }

    /// <summary>The camp objective: the checklist the player completes (gathering, the torch at the fire ring, the water).</summary>
    public Objective Objective { get; }

    /// <summary>The objective steps the last <see cref="Step"/> or <see cref="HostEvent"/> completed (empty when none).</summary>
    public IReadOnlyList<ObjectiveStep> Completed => _completed;

    /// <summary>The hint shown when the player works on water with no bottle (a toast; nothing is blocked).</summary>
    public const string NeedBottleHint = "Necesitas una botella.";

    /// <summary>
    /// Whether an NPC has pointed the player at the river (the villager's answer, or the guide's) at any time so far. A player who fills the
    /// bottle without it found the river without Spanish, which the log notes (<see cref="WorldEvents.FoundWithoutSpanish"/>).
    /// </summary>
    public bool RiverPointedOut => _riverPointed;

    /// <summary>Whether an NPC has pointed the player at the river so far (the save keeps it; see <see cref="RiverPointedOut"/>).</summary>
    internal bool RiverPointedState
    {
        get => _riverPointed;
        set => _riverPointed = value;
    }

    /// <inheritdoc />
    protected override bool ObjectiveComplete => Objective.Complete;

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
        if (Pointer is { Quiz: false, Place: "río" })
        {
            _riverPointed = true; // an NPC (the guide, or the villager through the table) has sent the player to the river
        }

        Simulation.Step(commands);
        AdvanceKeyWords();
        Tips.Step(Simulation.Tick);
        var completed = new List<ObjectiveStep>();
        foreach (var e in Simulation.Events)
        {
            switch (e.Kind)
            {
                case SimulationEventKind.ItemPickedUp:
                    completed.AddRange(Objective.RecordPickup((ItemId)e.Detail, e.Amount));
                    LearnItem((ItemId)e.Detail);
                    LogWorld(WorldEvents.ItemPickedUp, null, Nouns.ForItem((ItemId)e.Detail)?.Lemma ?? ItemDefinition.Get((ItemId)e.Detail).Name);
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
                case SimulationEventKind.BottleFilled:
                    LogWorld(WorldEvents.BottleFilled, null, "agua");
                    if (!_riverPointed)
                    {
                        LogWorld(WorldEvents.FoundWithoutSpanish, null, "río");
                    }

                    completed.AddRange(Objective.RecordHostEvent(Objective.WaterEvent));
                    break;
                case SimulationEventKind.WaterWithoutBottle:
                    LogWorld(WorldEvents.WaterWithoutBottle, null, null);
                    _hints.Add(NeedBottleHint);
                    break;
                default:
                    break;
            }
        }

        _completed = completed;
        LogSteps(completed);
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
        LogSteps(_completed);
        return _completed;
    }

    /// <summary>The short hints the world gave since the last call (water with no bottle), in order, and clears them.</summary>
    public IReadOnlyList<string> TakeHints()
    {
        var hints = _hints.ToArray();
        _hints.Clear();
        return hints;
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

    /// <inheritdoc />
    protected override void ApplyHostEvents(string npc, IReadOnlyList<string> hostEvents, string how)
    {
        foreach (var hostEvent in hostEvents)
        {
            var effect = NpcHostEvents.Of(npc, hostEvent);
            switch (effect.Kind)
            {
                case NpcEffectKind.GiveItem:
                    Give(npc, effect.Item, how);
                    break;
                case NpcEffectKind.PointAtNoun:
                    PointAt(npc, effect.Noun);
                    break;
                default:
                    break;
            }
        }
    }

    // An NPC puts one item in the inventory (a pickup the player did not make: the word card shows the first time). The bottle is handed over once:
    // a player who holds a bottle, empty or full, is not given a second one.
    private void Give(string npc, ItemId item, string how)
    {
        if (item == ItemId.Bottle && Simulation.Inventory.Count(ItemId.Bottle) + Simulation.Inventory.Count(ItemId.WaterBottle) > 0)
        {
            return;
        }

        if (Simulation.GrantItems(item, 1) > 0)
        {
            return; // the inventory is full: nothing is lost, the NPC keeps it
        }

        LearnItem(item);
        LogWorld(WorldEvents.ItemGiven, npc, $"{Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name}, {how}");
    }

    // The beacon goes to the nearest block of a noun: the villager's answer to "¿dónde está el río?".
    private void PointAt(string npc, string noun)
    {
        var found = LiveFacts.NearestOfNoun(Viewpoint, noun);
        if (found is null)
        {
            return;
        }

        Pointer = new PointerMarker(noun, found.Cell);
        if (string.Equals(noun, "río", StringComparison.Ordinal))
        {
            _riverPointed = true;
        }

        LogWorld(WorldEvents.BeaconSet, npc, $"{noun} at {found.Cell.X},{found.Cell.Y},{found.Cell.Z}");
    }

    private void LogWorld(string name, string? npc, string? detail) => Log(new WorldEvent(Simulation.Tick, StoredTime.UnixSeconds, name, npc, detail));

    private void LogSteps(IReadOnlyList<ObjectiveStep> steps)
    {
        foreach (var step in steps)
        {
            LogWorld(WorldEvents.ObjectiveStep, null, step.Text);
        }

        if (steps.Count > 0 && Objective.Complete)
        {
            LogWorld(WorldEvents.ObjectiveComplete, null, Objective.Summary);
        }
    }

    // The first time an item enters the inventory the noun is counted as heard and read, and its card waits for the front-end.
    private void LearnItem(ItemId item)
    {
        var noun = Nouns.ForItem(item);
        if (noun is null || !CardedNouns.Add(noun.Lemma))
        {
            return;
        }

        Words.MarkHeard(noun.Lemma);
        Words.MarkRead(noun.Lemma);
        _newCards.Add(new WordCard(noun.Lemma, noun.WithDefinite));
    }

    // The objective's ring is the one at the guide's camp (the scene's point "firering"). The cold ring at the player's own camp (the point
    // "firering.player") is for the end of the session: a torch set on it does not complete the camp's step. A scene without the point counts every ring.
    private void ScanFireRing(Scene scene)
    {
        var hasCampRing = scene.Points.TryGetValue("firering", out var centre);
        foreach (var stone in scene.Grid.BlocksOfKind(BlockId.FireRing))
        {
            if (!hasCampRing || (Math.Abs(stone.X - centre.X) <= 1 && Math.Abs(stone.Z - centre.Z) <= 1))
            {
                _fireRing.Add(stone);
            }
        }
    }
}
