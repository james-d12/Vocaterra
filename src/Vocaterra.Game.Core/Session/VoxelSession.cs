using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// One running game of the 3D scene (the prototype's only world since S4): the voxel simulation over the bundle's scene, and the NPCs (the guide,
/// the shopkeeper and the villager), the word counts, the arrow, the camp objective, the word cards and the turn worker around it (the shared part
/// is <see cref="GuideSession"/>). The guide's facts are the blocks of the scene: the nearest block of a noun and its direction from where the
/// player faces (<see cref="WorldFacts"/>). Since S6 the NPCs' host events act on this world (<see cref="NpcHostEvents"/>): the shopkeeper hands
/// over the bottle, which the player fills at the river (the objective's water step), the villager points the beacon at the place the learner asked
/// for (since village-plan V02 through the scene's labels, <see cref="ScenePlaces"/>).
/// </summary>
/// <remarks>
/// The front-end owns the clock and the input: it calls <see cref="Step"/> once per tick with the body's commands, and it reads the simulation,
/// the arrow, the objective and the words. Nothing here reads the clock or draws a random number of its own.
/// </remarks>
public sealed partial class VoxelSession : GuideSession
{
    /// <summary>The arrow is taken down when the player comes within this many blocks of its target (the beacon's own reach).</summary>
    public const int ReachBlocks = 3;

    private readonly List<WordCard> _newCards = [];
    private readonly List<BlockPos> _fireRing = [];
    private readonly List<BlockPos> _playerFireRing = [];
    private readonly List<string> _hints = [];
    private readonly List<(Objective Objective, ObjectiveStep Step)> _pendingSteps = [];
    private IReadOnlyList<ObjectiveStep> _completed = [];
    private int _eventAmount = 1;
    private bool _riverPointed;
    private bool _guideMet;
    private bool _fireLit;
    private bool _sessionComplete;

    /// <summary>
    /// Starts a 3D game on the game's loaded content and a built scene.
    /// </summary>
    /// <param name="data">The game's content (<see cref="GameData.Load"/>).</param>
    /// <param name="worldSeed">The world seed: the guide's random draws are keyed by it (the scene itself is built by the caller from the same seed).</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="scene">The built scene; the simulation starts with the player at its spawn.</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> when <see langword="null"/>.</param>
    /// <param name="lessons">The lessons to run instead of the bundle's <c>game/lessons.json</c> (the tests' own lessons), or <see langword="null"/> for the bundle's.</param>
    public VoxelSession(GameData data, ulong worldSeed, long startedAt, Scene scene, CreditRules? credit = null, LessonBook? lessons = null)
        : base(
            data,
            worldSeed,
            startedAt,
            WorldFacts.Live(scene?.Grid ?? throw new ArgumentNullException(nameof(scene)), (data ?? throw new ArgumentNullException(nameof(data))).Nouns, data.Words.Directions),
            playSession: 1,
            credit,
            lessons,
            ScenePlaces.Of(scene))
    {
        Scene = scene;
        Places = ScenePlaces.Of(scene);
        Simulation = VoxelSimulation.Create(scene);
        foreach (var item in data.Objectives.Grants)
        {
            Simulation.GrantItems(item, 1); // the learner starts with the book (S20, D-game-16; game/session.json, C07); a loaded save puts its own slots back
        }

        Simulation.SetRecipes(Recipes.Rules);
        Objectives = data.Objectives.Objectives.Select(static spec => new Objective(spec)).ToList();
        Objective = Objectives.First(static o => o.IsChecklist);
        ScanFireRing(scene);
        Tips.Offer("start"); // the opening tips (S16): looking, walking, jumping, collecting
    }

    /// <summary>The built scene (the world the facts read).</summary>
    public Scene Scene { get; }

    /// <summary>The places of the scene the NPCs' directions and the lessons name (village-plan V02).</summary>
    public ScenePlaces Places { get; }

    /// <summary>The 3D simulation: the body, the inventory, the tick counter and the events.</summary>
    public VoxelSimulation Simulation { get; }

    /// <summary>The session's objectives in the HUD's order (<c>game/session.json</c>, C07): meeting the guide, the camp checklist, the player's own fire.</summary>
    public IReadOnlyList<Objective> Objectives { get; }

    /// <summary>The camp checklist: the session's first checklist objective (gathering, the torch at the fire ring, the water).</summary>
    public Objective Objective { get; }

    /// <summary>
    /// The first objective in the HUD's order that is not done and is shown in this play session (an order of a later session is hidden, village-plan V03), or
    /// <see langword="null"/> when every one is done. It gates nothing.
    /// </summary>
    public Objective? CurrentObjective => Objectives.FirstOrDefault(o => !o.Complete && Shown(o.Spec));

    /// <summary>
    /// Whether an objective is shown and counted now: an order once the player has taken it on (issue #54: a «sí» to its NPC's offer, or its lesson's step; before, on
    /// meeting its NPC, L01), any other objective from its own <see cref="ObjectiveSpec.Session"/> on.
    /// </summary>
    /// <param name="spec">The objective.</param>
    public bool Shown(ObjectiveSpec spec) => spec.IsOrder ? HasOrder(spec.Id) : spec.ShownIn(Day);

    /// <summary>The step a save names (<c>camp.wood</c>), or <see langword="null"/> for a key the session's objectives do not have.</summary>
    /// <param name="key">The step's save key, <c>&lt;objective&gt;.&lt;step&gt;</c>.</param>
    public ObjectiveStep? ObjectiveStep(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var dot = key.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? null : Objectives.FirstOrDefault(o => string.Equals(o.Id, key[..dot], StringComparison.Ordinal))?.Step(key[(dot + 1)..]);
    }

    /// <summary>The counts a save keeps, by <c>&lt;objective&gt;.&lt;step&gt;</c>, in the session's order: every step without a restore hook (the others are worked out again from the world, <see cref="RestoreStage"/>).</summary>
    public IReadOnlyList<(string Key, int Count)> ObjectiveCounts() =>
        Objectives.SelectMany(o => o.Spec.Steps.Where(static s => s.Restore is null).Select(s => (o.Id + "." + s.Id, o.Step(s.Id)!.Count))).ToList();

    /// <summary>Puts back the counts a save keeps (S10): each key must be a step of an objective and appear once; the steps not named keep their count.</summary>
    /// <param name="counts">The counts by <c>&lt;objective&gt;.&lt;step&gt;</c>.</param>
    /// <exception cref="ArgumentException">A key is not a step of the session's objectives, or is given twice.</exception>
    public void RestoreObjectiveCounts(IEnumerable<(string Key, int Count)> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var byObjective = new Dictionary<Objective, List<(string, int)>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, count) in counts)
        {
            var dot = key.IndexOf('.', StringComparison.Ordinal);
            var objective = dot < 0 ? null : Objectives.FirstOrDefault(o => string.Equals(o.Id, key[..dot], StringComparison.Ordinal));
            if (objective is null || !seen.Add(key))
            {
                throw new ArgumentException($"'{key}' is not a step of the session's objectives, or is given twice.", nameof(counts));
            }

            if (!byObjective.TryGetValue(objective, out var list))
            {
                byObjective[objective] = list = [];
            }

            list.Add((key[(dot + 1)..], count));
        }

        foreach (var (objective, list) in byObjective)
        {
            objective.Restore(list);
        }
    }

    /// <summary>The objective steps the last <see cref="Step"/> or <see cref="HostEvent"/> completed (empty when none).</summary>
    public IReadOnlyList<ObjectiveStep> Completed => _completed;

    /// <summary>The hint shown when the player works on water with no bottle (a toast; nothing is blocked).</summary>
    public const string NeedBottleHint = "needBottle";

    /// <summary>
    /// What the objective area (top right) says now, as coloured pieces (S17): a lesson's short line while its <c>do</c> step waits ("Pick a fruit from a berry bush, then go
    /// back to Luna"); before the guide is met "Go down the trail and find the guide"; the camp checklist while it is the objective (<c>Campamento (camp): madera (wood)
    /// 0/5 ...</c>, the English going as the nouns become known, D-game-13); once it is complete "Go back up the trail and light your own fire"; and, with the fire lit,
    /// "Session complete". Since V12c, once the play session's work is done, "Session complete: sleep at your fire to start session 2" (or, when no later session has a lesson,
    /// that every lesson so far is done), and from session 2 on, while a lesson is left, the lesson to go on with. Every line but the checklist is English only.
    /// </summary>
    public IReadOnlyList<ObjectivePiece> ObjectiveHud()
    {
        if (Lessons.HudLine is { } hud)
        {
            return [new ObjectivePiece(hud, ObjectivePieceKind.Support)];
        }

        if (Lessons.ReplayHud is { } replay)
        {
            return [new ObjectivePiece(replay, ObjectivePieceKind.Support)]; // the learner asked to practise a lesson again from the book (S26)
        }

        if (Deck.PendingHud is { } review)
        {
            return [new ObjectivePiece(review, ObjectivePieceKind.Support)]; // the learner asked for a review from the book (X06)
        }

        var current = CurrentObjective;
        if ((current is null || current.Spec.IsOrder) && RestLine() is { } rest)
        {
            return [rest]; // the session's work is done: sleep at the fire for the next session, or every lesson is done; or the lesson to go on with (V12c)
        }

        return current switch
        {
            null => [new ObjectivePiece(Locale.Ui("objectiveComplete"), ObjectivePieceKind.Support, Done: true)],
            { IsChecklist: true } => Locale.ObjectivePieces(current, lemma => Words.RecordOf(lemma).Known),
            _ => [new ObjectivePiece(Locale.ObjectiveHudLine(current.Id), ObjectivePieceKind.Support)],
        };
    }

    private int _advances;

    /// <summary>
    /// The objective tracker (U01): one line per objective that is shown and not done, the most recently advanced first, three at most and the rest counted for <c>+n</c>.
    /// The objectives are the first of the session's sequence that is not done (the guide, the camp, the fire) and every order the player has taken on. A lesson's <c>do</c> line
    /// (or the replay or review line the learner asked for) comes first, unless the lesson strip shows it (<paramref name="lessonStripShown"/>): it is not shown twice. On day 1's
    /// rest the rest line stands alone in the tracker's place, as <see cref="ObjectiveHud"/> says it. Nothing is saved: after a load the order is the session's own.
    /// </summary>
    /// <param name="lessonStripShown">Whether the lesson strip is on the canvas this frame (the dialogue is open at the lesson's NPC).</param>
    public TrackerView ObjectiveTracker(bool lessonStripShown)
    {
        var lines = new List<TrackerLine>();
        var lessonLine = Lessons.HudLine ?? Lessons.ReplayHud ?? Deck.PendingHud;
        if (lessonLine is not null && !(lessonStripShown && Lessons.HudLine is not null))
        {
            lines.Add(Single(new ObjectivePiece(lessonLine, ObjectivePieceKind.Support)));
        }

        var active = Objectives.Select((o, i) => (Objective: o, Index: i)).Where(x => !x.Objective.Complete && Shown(x.Objective.Spec)).ToList();
        var hasMain = active.Any(static x => !x.Objective.Spec.IsOrder);
        if (!hasMain && lessonLine is null && RestLine() is { } rest)
        {
            return new TrackerView([Single(rest)], 0);
        }

        var main = active.FirstOrDefault(static x => !x.Objective.Spec.IsOrder); // the sequence's first undone objective; the later ones are not reached yet
        var tracked = active.Where(x => x.Objective.Spec.IsOrder || x.Index == main.Index && main.Objective is not null);
        foreach (var (objective, _) in tracked.OrderByDescending(static x => x.Objective.AdvancedAt).ThenBy(static x => x.Index))
        {
            lines.Add(LineOf(objective));
        }

        if (lines.Count == 0 && lessonLine is null)
        {
            lines.Add(Single(new ObjectivePiece(Locale.Ui("objectiveComplete"), ObjectivePieceKind.Support, Done: true)));
        }

        return new TrackerView(lines.Take(TrackerView.MaxLines).ToList(), Math.Max(0, lines.Count - TrackerView.MaxLines));
    }

    private static TrackerLine Single(ObjectivePiece piece) => new([piece], [piece]);

    private TrackerLine LineOf(Objective objective)
    {
        if (!objective.IsChecklist)
        {
            return Single(new ObjectivePiece(Locale.ObjectiveHudLine(objective.Id), ObjectivePieceKind.Support));
        }

        var done = objective.Steps.Count(static s => s.Done);
        var shortLine = new List<ObjectivePiece>
        {
            new(objective.Title, ObjectivePieceKind.Title),
            new(": " + done.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + objective.Steps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ObjectivePieceKind.Count, Done: done == objective.Steps.Count),
        };
        return new TrackerLine(Locale.ObjectivePieces(objective, lemma => Words.RecordOf(lemma).Known), shortLine);
    }

    /// <summary>Whether the player has met the guide (the panel opened on her, or the first step of the checklist was done).</summary>
    public bool GuideMet => _guideMet;

    /// <summary>Whether a torch stands on the rim of the fire ring at the player's own camp (<c>firering.player</c>): the fire is lit.</summary>
    public bool FireLit => _fireLit;

    /// <summary>
    /// Whether the session is complete: the camp checklist is done and the player's own fire is lit (every objective the ending waits for, <see cref="ObjectiveSpec.EndsSession"/>:
    /// not the orders, village-plan V03). The front-end then shows the closing moment.
    /// </summary>
    public bool SessionComplete => _sessionComplete;

    /// <summary>The scene's name for the fire ring at the guide's camp, where the checklist's torch goes (S5).</summary>
    public const string CampFireRing = "firering";

    /// <summary>The scene's name for the fire ring at the player's own camp, where the closing fire is lit (S17).</summary>
    public const string PlayerFireRing = "firering.player";

    /// <summary>The guide's closing line in Spanish (<c>¡Muy bien! Ya tienes fuego.</c>), the first of <c>game/session.json</c>'s closing pool, or an empty text when the bundle has none.</summary>
    public string ClosingLine => Bundle.Dialogue.TryGetPoolById(Data.Objectives.ClosingPool, out var pool) && pool.Variants.Count > 0 ? pool.Variants[0].Text : string.Empty;

    /// <summary>The speech-line id of <see cref="ClosingLine"/> (its gloss is the locale's <c>line.&lt;id&gt;</c>), or <see langword="null"/> when the bundle has none.</summary>
    public string? ClosingLineId => Bundle.Dialogue.TryGetPoolById(Data.Objectives.ClosingPool, out var pool) && pool.Variants.Count > 0 ? pool.Variants[0].LineId : null;

    /// <summary>
    /// Whether the scene has a fire ring at the player's own camp, where the closing fire is lit (the flat test clearing has none, and its sessions never reach
    /// <see cref="SessionComplete"/>).
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
    internal override void LessonPointAtPlace(string npc, string place) => PointAtPlace(npc, place);

    /// <summary>
    /// The scene's name of the place an arrow a save kept points at (the save keeps its word and its block, village-plan V02): a named place with that label on that
    /// block, or an NPC standing there; <see langword="null"/> for an arrow on a noun's block.
    /// </summary>
    /// <param name="target">The arrow's block.</param>
    /// <param name="label">The word it shows.</param>
    internal string? PointNameAt(GuideCell target, string label) =>
        Places.NameAt(target, label) ?? Scene.Npcs.FirstOrDefault(npc => npc.Feet == target.ToBlock() && string.Equals(Data.Npcs.Find(npc.Name)?.Called ?? npc.Name, label, StringComparison.Ordinal))?.Name;

    /// <inheritdoc />
    internal override void LessonReplayAsked(string npc) => PointAtNpc(npc);

    /// <summary>What the load of a save made from another content bundle did (X01): what was kept and what was dropped. <see langword="null"/> for a new game and for a save of this very bundle.</summary>
    public SaveMigrationReport? Migration { get; internal set; }

    /// <summary>
    /// A new play session begins (S26): a save was loaded into it, the player slept at their fire (V12c), or a run was started at a given session. When the session has a lesson
    /// that is unlocked and not begun, the beacon points at its NPC, and the objective line names the lesson (<see cref="ObjectiveHud"/>); when its work is already done and a
    /// later session has a lesson, the beacon points at the player's fire (unless they stand there).
    /// </summary>
    public void BeginPlaySession()
    {
        if (RestDue && !AtOwnFire)
        {
            PointAtFire();
        }

        SettleRest();
    }

    // The beacon goes to an NPC, where a lesson starts (S26). The label is the NPC's name (Luna) or label (Tendero, Aldeano); before C08 it was the raw id for any but the guide.
    private void PointAtNpc(string npc)
    {
        if (!Scene.Npcs.Any(placed => string.Equals(placed.Name, npc, StringComparison.Ordinal)))
        {
            return;
        }

        var feet = Scene.Npc(npc).Feet;
        var label = Data.Npcs.Find(npc)?.Called ?? npc; // C08: the NPC's name (Luna) or label (Tendero), from game/npcs/<id>.json
        Pointer = new PointerMarker(label, new GuideCell(feet.X, feet.Y, feet.Z), Point: npc);
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
    protected override IGuideFacts SnapshotFacts() => WorldFacts.Snapshot(Scene.Grid, Nouns, Data.Words.Directions);

    /// <inheritdoc />
    protected override string StateDigest() => Simulation.ComputeStateDigest();

    /// <summary>
    /// Advances the simulation one tick with the body's commands, then feeds the objective and the word cards from the tick's events, and takes
    /// the arrow down when the player is within <see cref="ReachBlocks"/> of its target, which logs <see cref="WorldEvents.BeaconReached"/> with the
    /// place's name (village-plan V02; not for the guide's review question). The conversation does not run here (see <see cref="GuideSession.Say"/>).
    /// </summary>
    public void Step(IReadOnlyList<VoxelCommand> commands)
    {
        if (Pointer is { Quiz: false } pointed && string.Equals(pointed.Place, Data.Words.Noun("river"), StringComparison.Ordinal))
        {
            _riverPointed = true; // an NPC (the guide, or the villager through the table) has sent the player to the river
        }

        var needsFromX = Simulation.Player.CenterX;
        var needsFromZ = Simulation.Player.CenterZ;
        var sprinting = PrepareTick(commands); // N02: the walking and gathering speed of this tick
        Simulation.Step(commands);
        StepNeeds(needsFromX, needsFromZ, sprinting); // N01: the needs move only in active play
        StepWaterEvents();
        AdvanceKeyWords();
        Tips.Step(Simulation.Tick);
        foreach (var e in Simulation.Events)
        {
            switch (e.Kind)
            {
                case SimulationEventKind.ItemPickedUp:
                    LearnItem((ItemId)e.Detail);
                    _eventAmount = e.Amount; // the objectives count a pickup's items (each gatherable block drops one today)
                    LogWorld(WorldEvents.ItemPickedUp, null, Nouns.ForItem((ItemId)e.Detail)?.Lemma ?? ItemDefinition.Get((ItemId)e.Detail).Name);
                    _eventAmount = 1;
                    break;
                case SimulationEventKind.ItemCrafted:
                    LearnItem((ItemId)e.Detail);
                    _eventAmount = e.Amount; // W01: a craft is a world event, counted as a pickup is (the output's count)
                    LogWorld(WorldEvents.ItemCrafted, null, Nouns.ForItem((ItemId)e.Detail)?.Lemma ?? ItemDefinition.Get((ItemId)e.Detail).Name);
                    _eventAmount = 1;
                    break;
                case SimulationEventKind.BlockRemoved:
                    NoteGathered((BlockId)e.Detail, e.X, e.Y, e.Z); // N06: a bush picked by hand grows back after the next night
                    break;
                case SimulationEventKind.BlockPlaced:
                    if ((BlockId)e.Detail == BlockId.Torch)
                    {
                        var cell = new BlockPos(e.X, e.Y, e.Z);
                        if (AtFireRing(cell))
                        {
                            LogWorld(WorldEvents.TorchAtRing, null, CampFireRing); // C06: the camp's ring (or, in a scene that names none, any ring but the player's)
                        }

                        if (AtPlayerFireRing(cell))
                        {
                            LogWorld(WorldEvents.TorchAtRing, null, PlayerFireRing);
                        }

                        if (AtPlayerFireRing(cell))
                        {
                            LightFire(); // the closing fire (S17): a torch on the rim of the ring at the player's own camp
                        }
                    }

                    break;
                case SimulationEventKind.BottleFilled:
                    LogWorld(WorldEvents.BottleFilled, null, Nouns.ForItem(ItemId.WaterBottle)?.Lemma);
                    if (!_riverPointed)
                    {
                        LogWorld(WorldEvents.FoundWithoutSpanish, null, Data.Words.Noun("river"));
                    }

                    break;
                case SimulationEventKind.WaterWithoutBottle:
                    LogWorld(WorldEvents.WaterWithoutBottle, null, null);
                    _hints.Add(Locale.Ui(NeedBottleHint));
                    break;
                default:
                    break;
            }
        }

        if (Pointer is { } pointer)
        {
            const long Unit = VoxelPhysics.UnitsPerBlock;
            var dx = Math.Abs((pointer.Target.X * Unit) + (Unit / 2) - Simulation.Player.CenterX);
            var dz = Math.Abs((pointer.Target.Z * Unit) + (Unit / 2) - Simulation.Player.CenterZ);
            if (dx <= ReachBlocks * Unit && dz <= ReachBlocks * Unit)
            {
                Pointer = null;
                if (!pointer.Quiz)
                {
                    LogWorld(WorldEvents.BeaconReached, null, pointer.Point ?? pointer.Place); // before the objectives' flush, so a step waiting for it is done this tick
                }
            }
        }

        _completed = FlushObjectives();
        NoteRestDue(); // the session's work is done: the arrow goes back to the fire (V12c)
    }

    /// <summary>
    /// Counts a world event for the objectives without the world raising it or the log showing it (the tests' way to the water: <see cref="WorldEvents.BottleFilled"/>),
    /// then logs the steps it completed as a tick does. Returns those steps.
    /// </summary>
    /// <param name="worldEvent">The event's name (<see cref="WorldEvents"/>).</param>
    /// <param name="detail">Its detail, or <see langword="null"/>.</param>
    public IReadOnlyList<ObjectiveStep> HostEvent(string worldEvent, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        RecordObjectives(new WorldEvent(Simulation.Tick, StoredTime.UnixSeconds, worldEvent, null, detail));
        _completed = FlushObjectives();
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
    /// Works the objectives out again from the world and the progress a save puts back (S17, C07): each step that names a restore hook asks it (<c>lesson-begun</c>: the
    /// guide is met when a lesson was begun; <c>scan-fire-ring</c>: the fire is lit when a torch stands at the player's own ring), an objective another one's progress
    /// completes is completed (the guide is met once the checklist moved), each done objective's hooks put their state back without acting, and the session is complete
    /// when every objective is done. The save holds only the counts of the steps without a restore hook.
    /// </summary>
    internal void RestoreStage()
    {
        _guideMet = false;
        _fireLit = false;
        foreach (var objective in Objectives)
        {
            foreach (var step in objective.Spec.Steps.Where(static s => s.Restore is not null))
            {
                objective.Set(step.Id, RestoreHook(step.Restore!) ? step.Target : 0);
            }
        }

        foreach (var objective in Objectives.Where(o => !o.Complete && AlsoDone(o)))
        {
            objective.Finish();
        }

        foreach (var objective in Objectives.Where(static o => o.Complete))
        {
            foreach (var hook in objective.Spec.OnComplete)
            {
                RunHook(hook, restoring: true);
            }
        }

        _sessionComplete = Objectives.Where(static o => o.Spec.EndsSession).All(static o => o.Complete);
    }

    // A restore hook: whether its step is done in the loaded world (ObjectiveBook.RestoreHooks).
    private bool RestoreHook(string hook) => hook switch
    {
        "lesson-begun" => Lessons.Book.Lessons.Any(l => Lessons.StatusOf(l.Id) != LessonStatus.NotStarted),
        "scan-fire-ring" => ScanPlayerFireRing(),
        _ => throw new InvalidOperationException($"Unknown restore hook '{hook}' (the validator lists the known ones)."),
    };

    // A completion hook (ObjectiveBook.CompleteHooks). Restoring, it puts its state back without acting.
    private void RunHook(string hook, bool restoring)
    {
        switch (hook)
        {
            case "guide-met":
                if (restoring)
                {
                    _guideMet = true;
                }
                else
                {
                    NoteGuideMet(); // the player started on the checklist without the guide: the objective line moves on all the same (nothing is gated)
                }

                break;
            case "spare-torch":
                if (!restoring)
                {
                    GiveSpareTorch();
                }

                break;
            case "point-own-camp":
                if (!restoring)
                {
                    PointAtOwnCamp();
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown completion hook '{hook}' (the validator lists the known ones).");
        }
    }

    // Whether another objective's progress completes this one (alsoDoneBy).
    private bool AlsoDone(Objective objective) =>
        objective.Spec.AlsoDoneBy.Any(id => Objectives.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal)) is { Started: true });

    // Whether a torch stands on the rim of the player's own fire ring; the fire is lit if so.
    private bool ScanPlayerFireRing()
    {
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

        return _fireLit;
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
        LogWorld(WorldEvents.FireLit, null, PlayerFireRing); // the light-fire objective waits for it, and the session may complete with it
    }

    // The arrow sends the player up the trail to their own camp, where the cold ring waits (S17).
    private void PointAtOwnCamp()
    {
        if (_fireLit || !Scene.Points.TryGetValue(PlayerFireRing, out var ring))
        {
            return;
        }

        var label = Scene.LabelOf(PlayerFireRing);
        Pointer = new PointerMarker(label, new GuideCell(ring.X, ring.Y, ring.Z), Point: PlayerFireRing);
        LogWorld(WorldEvents.BeaconSet, null, $"{label} at {ring.X},{ring.Y},{ring.Z}");
    }

    private void CheckSessionComplete()
    {
        if (_sessionComplete || !Objectives.Where(static o => o.Spec.EndsSession).All(static o => o.Complete))
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

        LogWorld(WorldEvents.SpareTorch, null, Nouns.ForItem(ItemId.Torch)?.Lemma);
        _hints.Add(Locale.Ui("spareTorch"));
    }

    /// <inheritdoc />
    protected override void ApplyHostEvents(string npc, IReadOnlyList<string> hostEvents, string how, IReadOnlyDictionary<string, string> slots)
    {
        foreach (var hostEvent in hostEvents)
        {
            var effect = Data.HostEvents.Of(npc, hostEvent).For(slots, Nouns); // W04: a price list comes to the item the line names
            switch (effect.Kind)
            {
                case NpcEffectKind.GiveItem when GiftClosed(npc, effect.Item): // N06: a gift given today is not made again (the worker answered gift_used)
                    break;
                case NpcEffectKind.GiveItem:
                    if (Give(npc, effect.Item, effect.CountFor(slots), how))
                    {
                        CountGift(npc, effect.Item);
                    }

                    break;
                case NpcEffectKind.PointAtNoun:
                    PointAt(npc, effect.Noun);
                    break;
                case NpcEffectKind.PointAtPlace:
                    PointAtPlace(npc, effect.Place);
                    break;
                case NpcEffectKind.PointAtSlot:
                    PointAtSlot(npc, slots.TryGetValue(effect.Slot, out var word) ? word : null);
                    break;
                case NpcEffectKind.TakeItem:
                    Take(npc, effect.Item, effect.CountFor(slots), how);
                    break;
                case NpcEffectKind.Sell when effect.ExchangeMiss(slots, Simulation.Inventory, Nouns) is null: // a miss was answered on the worker (wont_sell, cant_pay, …)
                    _ = Sell(npc, effect.Item, effect.CountFor(slots), effect.CostFor(slots), how);
                    break;
                case NpcEffectKind.Buy when effect.ExchangeMiss(slots, Simulation.Inventory, Nouns) is null: // a miss: wont_buy, cant_sell, none_offered, no_room
                    Buy(npc, effect, effect.SaleCountFor(slots, Simulation.Inventory.Count(effect.Item)), how);
                    break;
                default:
                    ApplyTeacherEffect(npc, effect, how); // W08: the teacher's kinds (VoxelSession.Teaching.cs)
                    break;
            }
        }
    }

    /// <inheritdoc />
    internal override bool OrderDelivered(string order) =>
        Objectives.Any(o => o.Spec.IsOrder && o.Complete && string.Equals(o.Id, order, StringComparison.Ordinal));

    /// <inheritdoc />
    internal override bool HoldsPickup(string lemma, int needed) =>
        Nouns.ForLemma(lemma) is { } noun && noun.Items.Sum(Simulation.Inventory.Count) >= needed;

    /// <inheritdoc />
    protected override string? OrderReadyAt(string npc) =>
        GoodsHeldOrders(npc).FirstOrDefault()?.Id; // no lesson holds a delivery (L01)

    /// <inheritdoc />
    protected override IReadOnlyList<string>? HandOverNounsOf(string order) =>
        Objectives.FirstOrDefault(o => string.Equals(o.Id, order, StringComparison.Ordinal))?.Spec.HandOverNouns;

    /// <inheritdoc />
    protected override bool OrderHeldAt(string npc) => false; // no lesson holds an order back (L01)

    /// <inheritdoc />
    protected override string? OrderToOfferAt(string npc) =>
        Objectives.FirstOrDefault(o => o.Spec.IsOrder && !o.Complete && !HasOrder(o.Id) && string.Equals(o.Spec.DeliverTo, npc, StringComparison.Ordinal))?.Id;

    /// <inheritdoc />
    protected override string? LessonGivingOrder(string order) =>
        Objectives.FirstOrDefault(o => string.Equals(o.Id, order, StringComparison.Ordinal))?.Spec is { LessonStep: not null } spec ? spec.Lesson : null;

    /// <inheritdoc />
    protected override void OnLessonStepStarted(LessonEvent step)
    {
        foreach (var order in Objectives?.Where(o => o.Spec.IsOrder && !o.Complete && string.Equals(o.Spec.Lesson, step.Lesson, StringComparison.Ordinal)
            && string.Equals(o.Spec.LessonStep, step.StepId, StringComparison.Ordinal)) ?? [])
        {
            GiveOrder(order.Spec.DeliverTo!, order.Id, "lesson");
        }
    }

    // The shown, undone orders of an NPC whose goods the player carries.
    private IEnumerable<Objective> GoodsHeldOrders(string npc) =>
        Objectives.Where(o => o.Spec.IsOrder && !o.Complete && Shown(o.Spec) && string.Equals(o.Spec.DeliverTo, npc, StringComparison.Ordinal)
            && o.Spec.Goods.All(goods => Simulation.Inventory.Count(goods.Item) >= goods.Count));

    /// <inheritdoc />
    protected override void TakeOrder(string npc, string order)
    {
        foreach (var goods in Objectives.First(o => string.Equals(o.Id, order, StringComparison.Ordinal)).Spec.Goods)
        {
            Take(npc, goods.Item, goods.Count, "order");
        }
    }

    // Whether an NPC can put items in the inventory: all of them fit, and a bottle goes only to a player who holds none (SecondBottle).
    private bool CanGive(ItemId item, int count) => !SecondBottle(item) && Simulation.Inventory.CanAdd(item, count);

    // Whether the item is a bottle and the player holds one, empty or full: the stall's rule (S6) gives none then.
    private bool SecondBottle(ItemId item) =>
        item == ItemId.Bottle && Simulation.Inventory.Count(ItemId.Bottle) + Simulation.Inventory.Count(ItemId.WaterBottle) > 0;

    // An NPC puts items in the inventory, all of them or none (a pickup the player did not make: the word card shows the first time). The bottle is handed over
    // once, whatever the count. None (a gift by the number said, cero) gives nothing.
    private bool Give(string npc, ItemId item, int count, string how)
    {
        if (count <= 0 || !CanGive(item, count))
        {
            return false; // a bottle held already, or the inventory is full: nothing is lost, the NPC keeps them
        }

        _ = Simulation.GrantItems(item, count);
        LearnItem(item);
        _eventAmount = count; // an objective counting what was given counts every item, as a pickup's
        LogWorld(WorldEvents.ItemGiven, npc, $"{Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name}, {how}");
        _eventAmount = 1;
        return true;
    }

    // An NPC takes items (village-plan V03: take_item, the goods of an order), all of them or none: a player who has fewer keeps what they have. None takes nothing.
    private void Take(string npc, ItemId item, int count, string how)
    {
        if (count <= 0 || !Simulation.TakeItems(item, count))
        {
            return;
        }

        _eventAmount = count; // an objective counting what was taken counts every item
        LogWorld(WorldEvents.ItemTaken, npc, $"{Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name}, {how}");
        _eventAmount = 1;
    }

    // A purchase (village-plan V03, sell): the coins for the items asked for go and the items come, or nothing changes hands (false). None asked for, too few coins
    // or no room for the items once the coins are paid (the NPC answered none_asked, cant_pay or no_room on the worker, NpcEffect.ExchangeMiss) keep the coins and the
    // items where they were, as the stall's bottle rule keeps a second bottle.
    private bool Sell(string npc, ItemId item, int count, int cost, string how)
    {
        if (count <= 0 || SecondBottle(item) || !Simulation.Inventory.CanExchange(ItemId.Coin, cost, item, count))
        {
            return false;
        }

        Take(npc, ItemId.Coin, cost, how);
        _ = Give(npc, item, count, how);
        return true;
    }

    // A sale (village-plan V15, buy): the NPC takes the items the learner offers and pays the price for each, or nothing changes hands. No items to sell, none
    // offered or no room for the coins once the items are taken (cant_sell, none_offered, no_room) keep the items and the coins where they were, as a purchase does.
    private void Buy(string npc, NpcEffect sale, int count, string how)
    {
        if (count <= 0 || !Simulation.Inventory.CanExchange(sale.Item, count, ItemId.Coin, sale.PriceOf(count)))
        {
            return;
        }

        Take(npc, sale.Item, count, how);
        _ = Give(npc, ItemId.Coin, sale.PriceOf(count), how);
    }

    // The beacon goes to a named place of the scene (village-plan V02): a host event's point_at_place, or a lesson's do step with a pointPlace. It shows the place's label.
    private void PointAtPlace(string npc, string place)
    {
        if (Places.Point(place) is not { } point)
        {
            return; // the validator checks the places the data names against the scene
        }

        var label = Places.LabelOf(place);
        Pointer = new PointerMarker(label, point, Point: place);
        LogWorld(WorldEvents.BeaconSet, npc, $"{label} at {point.X},{point.Y},{point.Z}");
    }

    // The beacon goes to the place the learner asked for (village-plan V02, point_at_slot): a label of the scene, else a catalogue noun's nearest block
    // (ScenePlaces.Resolve). The answer is logged as place_asked; a place the NPC does not know points nowhere, and the NPC said so on the worker (unknown_place).
    private void PointAtSlot(string npc, string? word)
    {
        var found = word is null ? null : Places.Resolve(word, Viewpoint, LiveFacts);
        LogWorld(WorldEvents.PlaceAsked, npc, found?.Name ?? WorldEvents.UnknownPlace);
        if (found is null)
        {
            return;
        }

        // The arrival names the place when the arrow stands on its point; the nearest block of the river or of a noun is named by the word, as a save would name it.
        Pointer = new PointerMarker(found.Label, found.Cell, Point: Places.Point(found.Name) == found.Cell.ToBlock() ? found.Name : null);
        if (string.Equals(found.Label, Data.Words.Noun("river"), StringComparison.Ordinal))
        {
            _riverPointed = true;
        }

        LogWorld(WorldEvents.BeaconSet, npc, $"{found.Label} at {found.Cell.X},{found.Cell.Y},{found.Cell.Z}");
    }

    // The beacon goes to the nearest block of a noun: the answer to "¿dónde está el río?" before V02, and a lesson's do step with a point.
    private void PointAt(string npc, string noun)
    {
        var found = LiveFacts.NearestOfNoun(Viewpoint, noun);
        if (found is null)
        {
            return;
        }

        Pointer = new PointerMarker(noun, found.Cell);
        if (string.Equals(noun, Data.Words.Noun("river"), StringComparison.Ordinal))
        {
            _riverPointed = true;
        }

        LogWorld(WorldEvents.BeaconSet, npc, $"{noun} at {found.Cell.X},{found.Cell.Y},{found.Cell.Z}");
    }

    private void LogWorld(string name, string? npc, string? detail) => Log(new WorldEvent(Simulation.Tick, StoredTime.UnixSeconds, name, npc, detail));

    /// <inheritdoc />
    protected override void OnWorldEvent(WorldEvent world)
    {
        if (Objectives is not null) // the base constructor logs nothing before the objectives exist, but a world event then would count for none
        {
            RecordObjectives(world);
        }
    }

    // Counts a world event for every objective not yet done that is shown in this play session (C07; an order of a later session counts nothing yet, V03). A
    // checklist's completed steps wait for FlushObjectives, which logs them after the tick's events; an objective that is no checklist is completed at once (the fire
    // is lit, the guide is met, an order is delivered), so its effects follow its event in the log.
    private void RecordObjectives(WorldEvent world)
    {
        foreach (var objective in Objectives.Where(o => !o.Complete && Shown(o.Spec)))
        {
            var before = objective.Progress;
            var done = objective.Record(world, _eventAmount);
            if (objective.Progress != before)
            {
                objective.AdvancedAt = ++_advances; // the tracker lists the objective that moved last first (U01)
            }

            if (objective.IsChecklist)
            {
                _pendingSteps.AddRange(done.Select(step => (objective, step)));
            }
            else if (objective.Complete)
            {
                CompleteObjective(objective);
            }
        }
    }

    // Logs the checklist steps completed since the last call and completes their checklists, then the objectives another one's progress completes. Returns the steps.
    private IReadOnlyList<ObjectiveStep> FlushObjectives()
    {
        var pending = _pendingSteps.ToList();
        _pendingSteps.Clear();
        foreach (var (_, step) in pending)
        {
            LogWorld(WorldEvents.ObjectiveStep, null, step.Text);
        }

        foreach (var objective in pending.Select(static p => p.Objective).Distinct().Where(static o => o.Complete))
        {
            CompleteObjective(objective);
        }

        foreach (var objective in Objectives.Where(o => !o.Complete && AlsoDone(o)).ToList())
        {
            objective.Finish();
            CompleteObjective(objective);
        }

        return pending.Select(static p => p.Step).ToList();
    }

    // An objective is done: a checklist logs its line, its hooks run (data order), and the session may be complete (the player lit their own fire first, or the
    // checklist was the last thing missing).
    private void CompleteObjective(Objective objective)
    {
        if (objective.IsChecklist)
        {
            LogWorld(WorldEvents.ObjectiveComplete, null, objective.Summary);
        }

        foreach (var hook in objective.Spec.OnComplete)
        {
            RunHook(hook, restoring: false);
        }

        CheckSessionComplete();
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
        var hasCampRing = scene.Points.TryGetValue(CampFireRing, out var centre);
        var hasOwnRing = scene.Points.TryGetValue(PlayerFireRing, out var own);
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
