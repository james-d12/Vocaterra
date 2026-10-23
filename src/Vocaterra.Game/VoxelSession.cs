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
    private readonly List<BlockPos> _playerFireRing = [];
    private readonly List<string> _hints = [];
    private IReadOnlyList<ObjectiveStep> _completed = [];
    private bool _riverPointed;
    private bool _guideMet;
    private bool _fireLit;
    private bool _sessionComplete;

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
        Simulation.GrantItems(ItemId.Book, 1); // the learner starts with the book (S20, D-game-16); a loaded save puts its own slots back
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
    public const string NeedBottleHint = "You need a bottle.";

    /// <summary>
    /// Where the session stands for the objective area (S17): sent down the trail to the guide, the camp checklist, sent back up to light the player's own fire,
    /// or complete. It only picks a line; nothing is gated by it.
    /// </summary>
    public SessionStage Stage =>
        Objective.Complete ? (_fireLit ? SessionStage.Complete : SessionStage.LightFire) : _guideMet ? SessionStage.Camp : SessionStage.FindGuide;

    /// <summary>
    /// What the objective area (top right) says now, as coloured pieces (S17): a lesson's short line while its <c>do</c> step waits ("Pick a fruit from a berry bush, then go
    /// back to Luna"); before the guide is met "Go down the trail and find the guide"; the camp checklist while it is the objective (<c>Campamento (camp): madera (wood)
    /// 0/5 ...</c>, the English going as the nouns become known, D-game-13); once it is complete "Go back up the trail and light your own fire"; and, with the fire lit,
    /// "Session complete". Every line but the checklist is English only.
    /// </summary>
    public IReadOnlyList<ObjectivePiece> ObjectiveHud()
    {
        if (Lessons.HudLine is { } hud)
        {
            return [new ObjectivePiece(hud, ObjectivePieceKind.English)];
        }

        if (Lessons.ReplayHud is { } replay)
        {
            return [new ObjectivePiece(replay, ObjectivePieceKind.English)]; // the learner asked to practise a lesson again from the book (S26)
        }

        if (Stage is SessionStage.Complete or SessionStage.FindGuide && Lessons.NewLesson is { Objective: { } objective })
        {
            return [new ObjectivePiece(objective, ObjectivePieceKind.English)]; // a new session's lesson is unlocked and not begun: the objective names it (S26)
        }

        return English.StageLine(Stage) is { } line
            ? [new ObjectivePiece(line, ObjectivePieceKind.English, Stage == SessionStage.Complete)]
            : English.ObjectivePieces(Objective, lemma => Words.RecordOf(lemma).Known);
    }

    /// <summary>Whether the player has met the guide (the panel opened on her, or the first step of the checklist was done).</summary>
    public bool GuideMet => _guideMet;

    /// <summary>Whether a torch stands on the rim of the fire ring at the player's own camp (<c>firering.player</c>): the fire is lit.</summary>
    public bool FireLit => _fireLit;

    /// <summary>Whether the session is complete: the camp checklist is done and the player's own fire is lit. The front-end then shows the closing moment.</summary>
    public bool SessionComplete => _sessionComplete;

    /// <summary>The bundle pool that holds the guide's closing line, said when the player's own fire is lit (S17).</summary>
    public const string ClosingPool = "guide.lesson.end";

    /// <summary>The guide's closing line in Spanish (<c>¡Muy bien! Ya tienes fuego.</c>), or an empty text when the bundle has none.</summary>
    public string ClosingLine => Bundle.Dialogue.TryGetPoolById(ClosingPool, out var pool) && pool.Variants.Count > 0 ? pool.Variants[0].Text : string.Empty;

    /// <summary>
    /// Whether the scene has a fire ring at the player's own camp, where the closing fire is lit (the flat test clearing has none, and its sessions never reach
    /// <see cref="SessionStage.Complete"/>).
    /// </summary>
    public bool HasPlayerFireRing => _playerFireRing.Count > 0;

    /// <summary>Whether a cell lies at the fire ring of the player's own camp (the same square as <see cref="AtFireRing"/>, around <c>firering.player</c>).</summary>
    /// <param name="cell">The cell.</param>
    public bool AtPlayerFireRing(BlockPos cell) => AtRing(_playerFireRing, cell);

    /// <inheritdoc />
    protected override void NoteGuideMet()
    {
        if (!_guideMet)
        {
            _guideMet = true;
            LogWorld(WorldEvents.GuideMet, NpcService.GuideAgent, null);
        }
    }

    /// <inheritdoc />
    internal override void LessonPointAt(string npc, string noun) => PointAt(npc, noun);

    /// <inheritdoc />
    internal override void LessonReplayAsked(string npc) => PointAtNpc(npc);

    /// <summary>What the load of a save made from another content bundle did (X01): what was kept and what was dropped. <see langword="null"/> for a new game and for a save of this very bundle.</summary>
    public SaveMigrationReport? Migration { get; internal set; }

    /// <summary>
    /// A new play session begins (S26): a save was loaded into it, or a run was started at a given session. When the session has a lesson that is unlocked and not begun, the beacon
    /// points at its NPC, and the objective line names the lesson (<see cref="ObjectiveHud"/>).
    /// </summary>
    public void BeginPlaySession()
    {
        if (Lessons.NewLesson is { } lesson)
        {
            PointAtNpc(lesson.Npc);
        }
    }

    // The beacon goes to an NPC, where a lesson starts (S26). The label is the NPC's name: Luna, or the villager's or the shopkeeper's role.
    private void PointAtNpc(string npc)
    {
        if (!Scene.Npcs.Any(placed => string.Equals(placed.Name, npc, StringComparison.Ordinal)))
        {
            return;
        }

        var feet = Scene.Npc(npc).Feet;
        var label = string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal) ? "Luna" : npc;
        Pointer = new PointerMarker(label, new GuideCell(feet.X, feet.Y, feet.Z));
        LogWorld(WorldEvents.BeaconSet, npc, $"{label} at {feet.X},{feet.Y},{feet.Z}");
    }

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
                        var cell = new BlockPos(e.X, e.Y, e.Z);
                        completed.AddRange(Objective.RecordTorchPlaced(AtFireRing(cell)));
                        if (AtPlayerFireRing(cell))
                        {
                            LightFire(); // the closing fire (S17): a torch on the rim of the ring at the player's own camp
                        }
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
        if (!_guideMet && Objective.Steps.Any(static s => s.Count > 0))
        {
            NoteGuideMet(); // the player started on the checklist without the guide: the objective line moves on all the same (nothing is gated)
        }

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
    public bool AtFireRing(BlockPos cell) => AtRing(_fireRing, cell);

    private static bool AtRing(List<BlockPos> ring, BlockPos cell)
    {
        foreach (var stone in ring)
        {
            if ((stone.Y == cell.Y || stone.Y + 1 == cell.Y) && Math.Abs(stone.X - cell.X) <= 1 && Math.Abs(stone.Z - cell.Z) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Works the session's stage out again from the world and the progress a save puts back (S17): the guide is met when a lesson was begun or the checklist moved, the
    /// fire is lit when a torch stands at the player's own ring, and the session is complete when both that and the checklist are done. The save needs no new field.
    /// </summary>
    internal void RestoreStage()
    {
        _guideMet = Lessons.Book.Lessons.Any(l => Lessons.StatusOf(l.Id) != LessonStatus.NotStarted) || Objective.Steps.Any(static s => s.Count > 0);
        _fireLit = false;
        foreach (var stone in _playerFireRing)
        {
            for (var dx = -2; dx <= 2 && !_fireLit; dx++)
            {
                for (var dz = -2; dz <= 2 && !_fireLit; dz++)
                {
                    foreach (var y in new[] { stone.Y, stone.Y + 1 })
                    {
                        var x = stone.X + dx;
                        var z = stone.Z + dz;
                        if (Scene.Grid.InBounds(x, y, z) && Scene.Grid.GetBlock(x, y, z) == BlockId.Torch && AtPlayerFireRing(new BlockPos(x, y, z)))
                        {
                            _fireLit = true;
                        }
                    }
                }
            }
        }

        _sessionComplete = _fireLit && Objective.Complete;
    }

    // The player's own fire is lit (S17). The session is complete once the checklist is too, in either order.
    private void LightFire()
    {
        if (_fireLit)
        {
            return;
        }

        _fireLit = true;
        Pointer = null; // the way back is walked: the arrow to the player's camp (or any earlier one) goes
        LogWorld(WorldEvents.FireLit, null, "firering.player");
        CheckSessionComplete();
    }

    // The arrow sends the player up the trail to their own camp, where the cold ring waits (S17).
    private void PointAtOwnCamp()
    {
        if (_fireLit || !Scene.Points.TryGetValue("firering.player", out var ring))
        {
            return;
        }

        Pointer = new PointerMarker("fuego", new GuideCell(ring.X, ring.Y, ring.Z));
        LogWorld(WorldEvents.BeaconSet, null, $"fuego at {ring.X},{ring.Y},{ring.Z}");
    }

    private void CheckSessionComplete()
    {
        if (_sessionComplete || !_fireLit || !Objective.Complete)
        {
            return;
        }

        _sessionComplete = true;
        LogWorld(WorldEvents.SessionComplete, null, Objective.Summary);
    }

    // The checklist is complete (S17): the player is sent back up the trail with a spare torch for their own fire, unless they hold one already or the fire is lit.
    private void GiveSpareTorch()
    {
        if (_fireLit || Simulation.Inventory.Count(ItemId.Torch) > 0 || !HasPlayerFireRing)
        {
            return;
        }

        if (Simulation.GrantItems(ItemId.Torch, 1) > 0)
        {
            LogWorld(WorldEvents.SpareTorch, null, "bag full");
            return;
        }

        LogWorld(WorldEvents.SpareTorch, null, "antorcha");
        _hints.Add(English.Ui("spareTorch"));
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
            GiveSpareTorch();
            PointAtOwnCamp();
            CheckSessionComplete(); // the player lit their own fire first: the checklist was the last thing missing
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
        var hasOwnRing = scene.Points.TryGetValue("firering.player", out var own);
        foreach (var stone in scene.Grid.BlocksOfKind(BlockId.FireRing))
        {
            if (hasOwnRing && Math.Abs(stone.X - own.X) <= 1 && Math.Abs(stone.Z - own.Z) <= 1)
            {
                _playerFireRing.Add(stone);
            }
            else if (!hasCampRing || (Math.Abs(stone.X - centre.X) <= 1 && Math.Abs(stone.Z - centre.Z) <= 1))
            {
                _fireRing.Add(stone);
            }
        }
    }
}
