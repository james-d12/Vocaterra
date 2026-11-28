using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Simulation;

/// <summary>
/// The 3D game world in motion: a built <see cref="Scene"/>, the player body, the inventory and a tick counter. A front-end owns a clock,
/// accumulates frame time and calls <see cref="Step(IReadOnlyList{VoxelCommand})"/> zero or more times per frame, once per 1/60 s of game
/// time; it reads state through <see cref="Grid"/>, <see cref="Player"/>, <see cref="Inventory"/>, <see cref="Tick"/> and
/// <see cref="Events"/> and writes only commands. Nothing here reads the clock or draws random numbers (the scene builder already used the
/// seed), so the same scene and the same command lists give the same state on every machine. The simulation edits the scene's grid in place.
/// </summary>
public sealed partial class VoxelSimulation
{
    /// <summary>The ticks per second of game time (60).</summary>
    public const int TicksPerSecond = VoxelPhysics.TicksPerSecond;

    /// <summary>The ticks a player holds <see cref="VoxelCommandKind.Interact"/> on water with a bottle to fill it (half a second).</summary>
    public const int BottleFillTicks = 30;

    /// <summary>The fixed-point factor that keeps a diagonal walk as fast as a straight one: 1 / sqrt(2) in units of 1/65 536.</summary>
    private const int DiagonalScale = 46341;

    private const int U = VoxelPhysics.UnitsPerBlock;
    private readonly List<SimulationEvent> _events = [];
    private int _selectedSlot;
    private BlockPos? _gatherTarget;
    private BlockId _gatherBlock;
    private int _gatherProgress;
    private IReadOnlyList<Recipe> _recipes = [];
    private BlockPos? _noBottleHint;

    private VoxelSimulation(Scene scene, IItemCatalogue items, EquipmentData equipment)
    {
        Scene = scene;
        Inventory = new Inventory(items, equipment.BagSlots);
        StartEquipment(items, equipment);
        StartInteraction(items);
        var spawn = scene.Spawn.Feet;
        Player = new PlayerBody(scene.Grid, (spawn.X * U) + (U / 2), spawn.Y * U, (spawn.Z * U) + (U / 2), scene.Spawn.Yaw);
        StartFlow();
        Fires = new FireRings(scene); // made with the simulation, so it hears of every stone set from the first tick (B06)
    }

    /// <summary>The scene (grid, spawn, NPCs, named points).</summary>
    public Scene Scene { get; }

    /// <summary>The blocks (the scene's grid; the simulation changes it when blocks are gathered or placed).</summary>
    public VoxelGrid Grid => Scene.Grid;

    /// <summary>The player's body: position, velocity, facing and the eye. Read only for a front-end.</summary>
    public PlayerBody Player { get; }

    /// <summary>The player's inventory: the base slots of the bundle's <c>equipment.json</c> (30) and the worn bag's capacity, the first 10 the hotbar (I03b). It starts empty.</summary>
    public Inventory Inventory { get; }

    /// <summary>The number of ticks stepped so far. The game's time is this divided by <see cref="TicksPerSecond"/>.</summary>
    public long Tick { get; private set; }

    /// <summary>The events of the last step; replaced by the next step.</summary>
    public IReadOnlyList<SimulationEvent> Events => _events;

    /// <summary>
    /// How fast the player walks, in thousandths of the walking speed: 1000 walks, 1600 sprints, a stage's walk factor lowers it (N02). A host setting the session sets before every
    /// tick, not state: it is not in the digest, and a simulation nobody sets walks at 1000.
    /// </summary>
    public int WalkPermille { get; set; } = 1000;

    /// <summary>
    /// Whether the player flies (the fly cheat): no gravity, the jump command rises and <see cref="VoxelCommandKind.Descend"/> sinks (<see cref="PlayerBody.StepFlying"/>). A host setting like
    /// <see cref="WalkPermille"/>, set from the player's settings: not state, not in the digest; a simulation nobody sets walks.
    /// </summary>
    public bool Flying { get; set; }

    /// <summary>The horizontal flying speed in units per tick (<see cref="VoxelPhysics.FlySpeedOf"/>; the settings' fly speed slider). A host setting like <see cref="Flying"/>.</summary>
    public int FlySpeed { get; set; } = VoxelPhysics.FlySpeed;

    /// <summary>The ticks of holding it takes to break a block under the break-all cheat when the block is not gathered (a sixth of a second).</summary>
    public const int BreakAllTicks = 10;

    /// <summary>
    /// Whether the break-all cheat is on: holding <see cref="VoxelCommandKind.Interact"/> breaks any block <see cref="BreaksUnderCheat"/> allows. A block that gathers as usual (its drop
    /// fits) still gathers; any other goes to air after <see cref="BreakAllTicks"/> and gives nothing, and the land rules do not refuse a plank. A host setting like <see cref="Flying"/>.
    /// </summary>
    public bool BreakAll { get; set; }

    /// <summary>Whether the break-all cheat breaks blocks of kind <paramref name="id"/>: every block but air, the liquids (water, lava), undergrowth and bedrock (<see cref="BlockDefinition.Indestructible"/>: the floor of the world stays).</summary>
    /// <param name="id">The block kind.</param>
    public static bool BreaksUnderCheat(BlockId id)
    {
        var definition = BlockDefinition.Get(id);
        return id != BlockId.Air && !definition.Liquid && !definition.Decor && !definition.Indestructible;
    }

    /// <summary>
    /// How fast gathering a block goes, in thousandths: 1000 is full speed, a smaller number lets the progress grow only on the ticks that cross a whole step of it (a half: every
    /// other tick), so gathering slows and never stops (N02, at zero stamina). A host setting like <see cref="WalkPermille"/>; filling a bottle is not slowed.
    /// </summary>
    public int GatherPermille { get; set; } = 1000;

    /// <summary>The selected hotbar slot, 0 to 9.</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>The stack in the selected slot (empty when the slot holds nothing).</summary>
    public ItemStack SelectedStack => Inventory.Slots[_selectedSlot];

    /// <summary>The block being gathered, or <see langword="null"/> when no gathering is going on.</summary>
    public BlockPos? GatherTarget => _gatherTarget;

    /// <summary>The ticks of holding already spent on <see cref="GatherTarget"/>.</summary>
    public int GatherProgress => _gatherProgress;

    /// <summary>
    /// The ticks of holding it takes to gather a block now: the block's own gather ticks, divided by the selected hotbar slot's tool speed when that
    /// tool works on the block's material (W02; the axe cuts a trunk four times as fast, village-plan V03). Hands are speed 1. The gathering, its progress
    /// events and the front-end's bar all read this number.
    /// </summary>
    /// <param name="block">The block kind.</param>
    public int GatherTicksOf(BlockId block)
    {
        var definition = GatherDefinition(block);
        if (!definition.Gatherable && BreakAll && BreaksUnderCheat(block))
        {
            return BreakAllTicks;
        }

        if (definition.Gatherable && ToolOn(definition) is { } tool)
        {
            return Math.Max(1, definition.GatherTicks / tool.Speed);
        }

        return definition.GatherTicks;
    }

    // The selected hotbar slot's tool when it works on the block's material and is not worn out (I07: a worn-out tool works at bare hands' speed), else null.
    private ToolSpec? ToolOn(BlockDefinition definition)
    {
        if (definition.Material != Material.None && SelectedStack is { IsEmpty: false } held
            && Inventory.Items.Get(held.Item) is { Tool: { } tool } item && tool.Class == definition.Material && !item.IsWornOut(held.Wear))
        {
            return tool;
        }

        return null;
    }

    /// <summary>The recipes the player can craft; set by the host with <see cref="SetRecipes"/>.</summary>
    public IReadOnlyList<Recipe> Recipes => _recipes;

    /// <summary>What the player's eye is looking at within the reach (a block, the face and the cell in front of it), or <see langword="null"/>. Water is looked through (H05).</summary>
    public BlockRayHit? Target => BlockRay.Cast(Grid, Player, throughWater: true);

    /// <summary>H05: <see cref="Target"/> for the interact button, where water counts as a hit (a bottle is filled from the water cell in front).</summary>
    public BlockRayHit? InteractTarget => BlockRay.Cast(Grid, Player);

    /// <summary>Starts a simulation on a scene, with the player standing at its spawn.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="items">The item catalogue (<c>game/items.json</c>): the bag's stack limits and the tools' specs are read from it.</param>
    /// <param name="day">The bundle's day and night cycle (E01), or <see langword="null"/> for the default values (a test's simulation built without game data).</param>
    /// <param name="weather">The bundle's weather (E04), or <see langword="null"/> for the default data.</param>
    /// <param name="worldSeed">The world seed the weather's draws are keyed by.</param>
    /// <param name="equipment">The bundle's slot groups (<c>game/equipment.json</c>, I03a), or <see langword="null"/> for none (a test's simulation built without game data: nothing can be worn).</param>
    /// <param name="stateHeader">What the state digest starts with (B05: the seed, the region template, the generator version and the world-generation data), or <see langword="null"/> for the scene's seed with no data (a test's simulation).</param>
    public static VoxelSimulation Create(Scene scene, IItemCatalogue items, DayData? day = null, WeatherData? weather = null, ulong worldSeed = 0, EquipmentData? equipment = null, WorldStateHeader? stateHeader = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(items);
        var simulation = new VoxelSimulation(scene, items, equipment ?? EquipmentData.Empty) { StateHeader = stateHeader ?? WorldStateHeader.Unkeyed(scene.Seed) };
        simulation.StartAtmosphere(day, weather, worldSeed);
        return simulation;
    }

    /// <summary>What the world's state digest starts with (<see cref="WorldStateDigest"/>).</summary>
    public WorldStateHeader StateHeader { get; private init; } = null!;

    /// <summary>Advances one tick with no input.</summary>
    public void Step() => Step([]);

    /// <summary>
    /// Advances one tick. Walking and jumping are levels (send the held ones every tick); <see cref="VoxelCommandKind.Interact"/> is held;
    /// the other commands are presses. A command kind the simulation does not know is ignored. Commands are applied in a fixed order
    /// whatever their order in the list: the hotbar selection and the facing (the last of each wins), then gathering (the last interact of
    /// the tick), then placing and crafting (each in list order), then the walk (all move commands add up) with the jump and the physics, then the water (the flow step on every 15th tick, then the breath).
    /// </summary>
    public void Step(IReadOnlyList<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _events.Clear();
        var jump = false;
        var descend = false;
        BlockPos? interact = null;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            switch (command.Kind)
            {
                case VoxelCommandKind.Look:
                    Player.SetLook(command.A, command.B);
                    break;
                case VoxelCommandKind.Jump:
                    jump = true;
                    break;
                case VoxelCommandKind.Descend:
                    descend = true;
                    break;
                case VoxelCommandKind.SelectSlot:
                    if (Inventory.IsHotbarSlot(command.A))
                    {
                        _selectedSlot = command.A;
                    }

                    break;
                case VoxelCommandKind.Interact:
                    interact = new BlockPos(command.A, command.B, command.C);
                    break;
                default:
                    break;
            }
        }

        Tick++;
        StepGather(interact);
        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i].Kind)
            {
                case VoxelCommandKind.Place:
                    TryPlace((ItemId)commands[i].A, new BlockPos(commands[i].B, commands[i].C, commands[i].D));
                    break;
                case VoxelCommandKind.Craft:
                    Craft(commands[i].A);
                    break;
                case VoxelCommandKind.Equip:
                    Equip(commands[i].A, new EquipSlot(commands[i].B, commands[i].C));
                    break;
                case VoxelCommandKind.Unequip:
                    Unequip(new EquipSlot(commands[i].A, commands[i].B), commands[i].C);
                    break;
                case VoxelCommandKind.SlotMove:
                    ApplySlotMove(commands[i]);
                    break;
                default:
                    break;
            }
        }

        var (intentX, intentZ) = WalkIntent(commands);
        var (jumped, landed) = Flying
            ? Player.StepFlying(Grid, intentX, intentZ, (jump ? 1 : 0) - (descend ? 1 : 0), FlySpeed, WalkPermille)
            : Player.Step(Grid, intentX, intentZ, jump, Scene.Currents, WalkPermille);
        if (jumped)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerJumped, Tick));
        }

        if (landed)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerLanded, Tick));
        }

        StepWater();
        StepAtmosphere();
        StepWear();
        RefreshStats(); // the sheet of the end of this tick: the temperature and the walk read it on the next one
    }

    /// <summary>
    /// Gives the player items outside the command stream: the starting kit of a demo or a test. It is a host action, not a game rule; a run
    /// is deterministic when the host makes the same calls at the same ticks.
    /// </summary>
    /// <returns>How many did not fit.</returns>
    public int GrantItems(ItemId item, int count) => Inventory.Add(item, count);

    /// <summary>
    /// Takes items from the player outside the command stream, all of them or none: an NPC's purchase or order (village-plan V03). A host action like
    /// <see cref="GrantItems"/>.
    /// </summary>
    /// <returns>Whether the player had that many and they were taken.</returns>
    public bool TakeItems(ItemId item, int count) => Inventory.Remove(item, count);

    /// <summary>
    /// Puts the player's feet in the middle of a cell, at rest. A debug action for a front-end's launch options; it is not a game rule and
    /// not a command. The caller makes sure the body fits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell lies outside the grid.</exception>
    public void TeleportTo(BlockPos feet)
    {
        if (!Grid.InBounds(feet.X, feet.Y, feet.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(feet), feet, "The cell lies outside the grid.");
        }

        Player.PlaceAt(Grid, (feet.X * U) + (U / 2), feet.Y * U, (feet.Z * U) + (U / 2));
    }

    /// <summary>Sets the recipe list (plain values the game layer read from the bundle). Recipes are rules, not state: they are not in the digest.</summary>
    public void SetRecipes(IReadOnlyList<Recipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        _recipes = recipes.ToArray();
    }

    /// <summary>
    /// Whether a block of kind <paramref name="station"/> lies within <see cref="VoxelPhysics.StationReachInBlocks"/> of the middle of the
    /// player's body (the distance from the body's middle to the middle of the block, squared integer arithmetic).
    /// </summary>
    public bool StationInReach(BlockId station)
    {
        var radius = VoxelPhysics.StationReachInBlocks;
        var cx = Player.CenterX;
        var cy = Player.MinY + (VoxelPhysics.PlayerHeight / 2);
        var cz = Player.CenterZ;
        var bx = VoxelCollider.FloorDiv(cx, U);
        var by = VoxelCollider.FloorDiv(cy, U);
        var bz = VoxelCollider.FloorDiv(cz, U);
        long reach = (long)radius * U;
        for (var y = by - radius; y <= by + radius; y++)
        {
            for (var z = bz - radius; z <= bz + radius; z++)
            {
                for (var x = bx - radius; x <= bx + radius; x++)
                {
                    if (Grid.GetBlock(x, y, z) != station || (station == BlockId.FireRing && !Fires.StoneBurns(new BlockPos(x, y, z))))
                    {
                        continue;
                    }

                    long dx = (x * U) + (U / 2) - cx;
                    long dy = (y * U) + (U / 2) - cy;
                    long dz = (z * U) + (U / 2) - cz;
                    if ((dx * dx) + (dy * dy) + (dz * dz) <= reach * reach)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>The fire rings of the world, the one finder the session shares (N07, Y01): a fire-ring stone is a station (<c>fuego</c>) only while its ring burns.</summary>
    public FireRings Fires { get; }

    /// <summary>What crafting the recipe at <paramref name="recipeIndex"/> would do now (nothing is changed).</summary>
    public CraftResult CanCraft(int recipeIndex)
    {
        if ((uint)recipeIndex >= (uint)_recipes.Count)
        {
            return CraftResult.NoSuchRecipe;
        }

        var recipe = _recipes[recipeIndex];
        return recipe.Repairs
            ? CraftingRules.CheckRepair(recipe, Inventory, RecipeStationInReach(recipe), Wear.NeedsRepair(recipe.Output))
            : CraftingRules.Check(recipe, Inventory, RecipeStationInReach(recipe));
    }

    /// <summary>Crafts the recipe at <paramref name="recipeIndex"/> when <see cref="CanCraft"/> says so (the same as a <see cref="VoxelCommandKind.Craft"/> in a tick).</summary>
    public CraftResult Craft(int recipeIndex)
    {
        var result = CanCraft(recipeIndex);
        if (result != CraftResult.Done)
        {
            return result;
        }

        var recipe = _recipes[recipeIndex];
        if (recipe.Repairs)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                Inventory.Remove(ingredient.Item, ingredient.Count);
            }

            _ = Wear.Repair(recipe.Output);
            _events.Add(new SimulationEvent(SimulationEventKind.ItemRepaired, Tick, Detail: (int)recipe.Output));
            RefreshStats(); // a repaired piece gives its stats again at once
            return result;
        }

        CraftingRules.Craft(recipe, Inventory, RecipeStationInReach(recipe));
        _events.Add(new SimulationEvent(SimulationEventKind.ItemCrafted, Tick, Detail: (int)recipe.Output, Amount: recipe.OutputCount));
        return result;
    }

    /// <summary>
    /// The simulation's state apart from its grid, as plain values (a save writes it). Nothing here changes the simulation.
    /// </summary>
    public VoxelSimulationState CaptureState()
    {
        var slots = new ItemStack[Inventory.SlotCount]; // the active slots only: the state carries the active count as its length
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            slots[i] = Inventory.Slots[i];
        }

        return new VoxelSimulationState(Tick, _selectedSlot, _gatherTarget, _gatherBlock, _gatherProgress, _noBottleHint, Player.State(), slots, _breath.Air, _flow.Queue.Snapshot(), CaptureAtmosphere() is { IsDefault: false } atmosphere ? atmosphere : null, Equipment.IsEmpty ? null : Equipment.Capture());
    }

    /// <summary>
    /// Puts back a state that <see cref="CaptureState"/> gave (the grid is not touched: the scene diff of a save does that before the simulation is
    /// made). The events of the last step are cleared. The state is checked: an unknown slot, a count above a stack limit or a negative tick throws.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The state holds a value the simulation cannot have.</exception>
    public void Restore(VoxelSimulationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(state.Tick);
        ArgumentOutOfRangeException.ThrowIfNegative(state.GatherProgress);
        if (!Inventory.IsHotbarSlot(state.SelectedSlot))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state.SelectedSlot, "The selected slot is not a hotbar slot.");
        }

        var active = Equipment.ActiveSlotsFor(Inventory, state.Worn); // the worn bag decides the size (I03b)
        if (state.Slots.Count != active)
        {
            throw new ArgumentException($"The inventory has {state.Slots.Count} slots, not the {active} its worn items give.", nameof(state));
        }

        Tick = state.Tick;
        _selectedSlot = state.SelectedSlot;
        _gatherTarget = state.GatherTarget;
        _gatherBlock = state.GatherBlock;
        _gatherProgress = state.GatherProgress;
        _noBottleHint = state.NoBottleHint;
        Player.Restore(state.Player);
        _breath.Restore(state.Air);
        _flow.Queue.Restore(state.Flow ?? []);
        RestoreAtmosphere(state.Atmosphere);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            Inventory.SetSlot(i, ItemStack.Empty); // the bag that stands now may be larger than the one restored
        }

        Held.Clear(); // a save holds nothing and keeps no trash (I02)
        Trash.Clear();
        Equipment.Restore(state.Worn);
        Equipment.SyncBag(Inventory);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            Inventory.SetSlot(i, state.Slots[i]);
        }

        RefreshStats();
        _events.Clear();
    }

    /// <summary>
    /// A SHA-256 of the whole simulation state (the world's state digest, see <see cref="WorldStateDigest"/>: the header and the changed chunks; the tick, the body, the hotbar selection, the gathering progress, the
    /// inventory's active slots and the worn items) as 64 lowercase hex characters. Two runs that diverge anywhere differ here.
    /// </summary>
    public string ComputeStateDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(WorldStateDigest.Compute(Grid, StateHeader));
        var body = Player;
        Span<byte> buffer = stackalloc byte[8 + (11 * 4)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, Tick);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], body.MinX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..], body.MinY);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], body.MinZ);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[20..], body.VelocityX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[24..], body.VelocityY);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..], body.VelocityZ);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[32..], body.Grounded ? 1 : 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[36..], body.Yaw);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[40..], body.Pitch);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[44..], _selectedSlot);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[48..], _gatherProgress);
        hash.AppendData(buffer);
        var rest = new byte[(4 * 4) + (Inventory.SlotCount * 8)];
        BinaryPrimitives.WriteInt32LittleEndian(rest, _gatherTarget?.X ?? -1);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(4), _gatherTarget?.Y ?? -1);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(8), _gatherTarget?.Z ?? -1);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(12), (int)_gatherBlock);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            var stack = Inventory.Slots[i];
            BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(16 + (i * 8)), stack.IsEmpty ? -1 : (int)stack.Item);
            BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(20 + (i * 8)), stack.Count);
        }

        hash.AppendData(rest);
        if (Inventory.SlotCount != Inventory.BaseSlots)
        {
            Span<byte> active = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(active, Inventory.SlotCount); // the active count, said outright once a bag has changed it (I03b); with no bag worn it adds nothing
            hash.AppendData(active);
        }

        if (!_breath.IsFull)
        {
            Span<byte> air = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(air, _breath.Air); // a full breath adds nothing, so the digests of dry runs are as they were
            hash.AppendData(air);
        }

        AppendFlowDigest(hash); // a run with no water waiting adds nothing
        AppendAtmosphereDigest(hash); // a clock never moved by a sleep or held, and a weather in its first spell, add nothing
        AppendWearDigest(hash); // nothing worn out or scuffed adds nothing (I07)
        AppendEquipmentDigest(hash); // nothing worn adds nothing (the active count is in the inventory's slots above: its length follows the worn bag)
        AppendInteractionDigest(hash); // nothing held and an empty trash add nothing (I02)

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private bool RecipeStationInReach(Recipe recipe) =>
        recipe.Station is { } station && StationInReach(station);

    /// <summary>The summed walking intent of the tick's move commands, each component within +-1.0 (fixed point).</summary>
    private (int X, int Z) WalkIntent(IReadOnlyList<VoxelCommand> commands)
    {
        var x = 0;
        var z = 0;
        var yaw = Player.Yaw;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (command.Kind == VoxelCommandKind.Move)
            {
                var dx = Math.Sign(command.A);
                var dz = Math.Sign(command.B);
                var scale = dx != 0 && dz != 0 ? DiagonalScale : Angles.One;
                x += dx * scale;
                z += dz * scale;
            }
            else if (command.Kind == VoxelCommandKind.MoveRelative)
            {
                var strafe = Math.Sign(command.A);
                var forward = Math.Sign(command.B);
                var scale = strafe != 0 && forward != 0 ? DiagonalScale : Angles.One;
                x += Angles.Multiply((forward * Angles.Sin(yaw)) + (strafe * Angles.Cos(yaw)), scale);
                z += Angles.Multiply((-forward * Angles.Cos(yaw)) + (strafe * Angles.Sin(yaw)), scale);
            }
        }

        return (Math.Clamp(x, -Angles.One, Angles.One), Math.Clamp(z, -Angles.One, Angles.One));
    }

    private bool InReach(BlockPos block)
    {
        long dx = Gap(Player.EyeX, block.X);
        long dy = Gap(Player.EyeY, block.Y);
        long dz = Gap(Player.EyeZ, block.Z);
        long reach = (long)VoxelPhysics.ReachInBlocks * U;
        return (dx * dx) + (dy * dy) + (dz * dz) <= reach * reach;
    }

    /// <summary>The distance in units from the coordinate to the block's extent [block, block + 1) on one axis (0 when inside it).</summary>
    private static long Gap(int coordinate, int block)
    {
        var low = (long)block * U;
        var high = low + U;
        return coordinate < low ? low - coordinate : coordinate > high ? coordinate - high : 0;
    }

    // Whether this tick adds a step of progress: always at full speed, else on the ticks where the running total of permille crosses a thousand (no state kept).
    private bool GatherStepsNow()
    {
        var permille = Math.Clamp(GatherPermille, 1, 1000);
        return permille >= 1000 || (Tick * permille / 1000) > ((Tick - 1) * permille / 1000);
    }

    private void ResetGather()
    {
        _gatherTarget = null;
        _gatherBlock = BlockId.Air;
        _gatherProgress = 0;
    }

    /// <summary>
    /// One tick of gathering. The target must be a gatherable block in reach whose drop fits in the inventory (a full inventory holds the
    /// gathering back; it does not destroy the block). Progress is kept while the target stays the same and the block goes when the progress
    /// reaches its gather ticks.
    /// </summary>
    private void StepGather(BlockPos? target)
    {
        if (target != _takeBackRefusedAt)
        {
            _takeBackRefusedAt = null; // a new hold may say again why a plank stays (Y09)
        }

        if (target is not { } cell || !Grid.InBounds(cell.X, cell.Y, cell.Z) || !InReach(cell))
        {
            _noBottleHint = null;
            ResetGather();
            return;
        }

        var id = Grid.GetBlock(cell.X, cell.Y, cell.Z);
        if (id == BlockId.Water)
        {
            StepFill(cell);
            return;
        }

        _noBottleHint = null;
        if (BuildRules?.IsCampBlock(cell) == true)
        {
            StepPackCamp(cell, id); // Y12: a block of the player's camp is not gathered one by one: holding on it packs the whole camp
            return;
        }

        var definition = GatherDefinition(id);
        var gathers = definition.Gatherable && Inventory.CanAdd(definition.Drop!.Value, definition.DropCount);
        var breaks = !gathers && BreakAll && BreaksUnderCheat(id); // the break-all cheat: what does not gather breaks, and gives nothing
        if ((!BreakAll && TakeBackRefused(id, cell)) || (!gathers && !breaks))
        {
            ResetGather();
            return;
        }

        if (_gatherTarget != cell || _gatherBlock != id)
        {
            _gatherTarget = cell;
            _gatherBlock = id;
            _gatherProgress = 0;
        }

        if (GatherStepsNow())
        {
            _gatherProgress++;
        }

        var ticks = breaks ? BreakAllTicks : GatherTicksOf(id); // the held tool's speed on its own material (W02; the axe on a trunk, V03)
        _events.Add(new SimulationEvent(SimulationEventKind.GatherProgress, Tick, cell.X, cell.Y, cell.Z, _gatherProgress, ticks));
        if (_gatherProgress < ticks)
        {
            return;
        }

        if (breaks)
        {
            Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air);
            ResetGather();
            _events.Add(new SimulationEvent(SimulationEventKind.BlockRemoved, Tick, cell.X, cell.Y, cell.Z, (int)id));
            return;
        }

        var drop = definition.Drop!.Value; // gathers: the block is gatherable
        var usedTool = ToolOn(definition) is not null;
        Grid.SetBlock(cell.X, cell.Y, cell.Z, definition.BecomesWhenGathered);
        Inventory.Add(drop, definition.DropCount);
        ResetGather();
        if (usedTool)
        {
            Wear.Apply(Items.Wearing.UseWearSource.SourceId, 1); // W13: each block the tool gathered wears it once
        }

        _events.Add(new SimulationEvent(SimulationEventKind.BlockRemoved, Tick, cell.X, cell.Y, cell.Z, (int)id));
        _events.Add(new SimulationEvent(SimulationEventKind.ItemPickedUp, Tick, cell.X, cell.Y, cell.Z, (int)drop, definition.DropCount));
    }

    /// <summary>
    /// One tick of working on a water block (S6). With an empty bottle in the inventory, holding builds progress like a gather and, after
    /// <see cref="BottleFillTicks"/>, the bottle becomes a bottle of water (the water block stays: the river is not used up). Without a bottle
    /// nothing is gathered and nothing blocks: one <see cref="SimulationEventKind.WaterWithoutBottle"/> per hold lets the front-end say why.
    /// </summary>
    private void StepFill(BlockPos cell)
    {
        if (Inventory.Count(ItemId.Bottle) < 1)
        {
            ResetGather();
            if (_noBottleHint != cell)
            {
                _noBottleHint = cell;
                _events.Add(new SimulationEvent(SimulationEventKind.WaterWithoutBottle, Tick, cell.X, cell.Y, cell.Z));
            }

            return;
        }

        _noBottleHint = null;
        if (_gatherTarget != cell || _gatherBlock != BlockId.Water)
        {
            _gatherTarget = cell;
            _gatherBlock = BlockId.Water;
            _gatherProgress = 0;
        }

        _gatherProgress++;
        _events.Add(new SimulationEvent(SimulationEventKind.GatherProgress, Tick, cell.X, cell.Y, cell.Z, _gatherProgress, BottleFillTicks));
        if (_gatherProgress < BottleFillTicks)
        {
            return;
        }

        ResetGather();
        Inventory.Remove(ItemId.Bottle, 1);
        Inventory.Add(ItemId.WaterBottle, 1);
        _events.Add(new SimulationEvent(SimulationEventKind.BottleFilled, Tick, cell.X, cell.Y, cell.Z, (int)ItemId.WaterBottle));
        _events.Add(new SimulationEvent(SimulationEventKind.ItemPickedUp, Tick, cell.X, cell.Y, cell.Z, (int)ItemId.WaterBottle, 1));
    }

    /// <summary>
    /// Places one <paramref name="item"/> as a block in the cell when the item makes a block, the player has one, the cell is empty air in
    /// reach with a block (of any kind) next to it, and a solid block does not overlap the player's box. One item is used up.
    /// </summary>
    private void TryPlace(ItemId item, BlockPos cell)
    {
        if (!Grid.InBounds(cell.X, cell.Y, cell.Z)
            || !IsEmptyOrDecor(Grid.GetBlock(cell.X, cell.Y, cell.Z))
            || !InReach(cell)
            || BlockDefinition.PlacedBy(item) is not { } placed
            || Inventory.Count(item) < 1
            || !HasNeighbour(cell))
        {
            return;
        }

        if (BlockDefinition.IsSolid(placed) && OverlapsPlayer(cell, placed))
        {
            return;
        }

        if (PlaceRefusedByLand(placed, cell))
        {
            return; // Y09: not on the village, the roads or Luna's camp (a torch is always allowed)
        }

        Grid.SetBlock(cell.X, cell.Y, cell.Z, placed);
        Inventory.Remove(item, 1);
        _events.Add(new SimulationEvent(SimulationEventKind.BlockPlaced, Tick, cell.X, cell.Y, cell.Z, (int)placed, (int)item));
    }

    // Air, undergrowth (a placed block takes the place of the grass) or water (a placed block displaces it, H06: planks dam a stream).
    private static bool IsEmptyOrDecor(BlockId id) => id == BlockId.Air || BlockDefinition.IsDecor(id) || id == BlockId.Water;

    private bool HasNeighbour(BlockPos cell)
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var next = cell.Offset(face);
            var around = Grid.GetBlock(next.X, next.Y, next.Z);
            if (around != BlockId.Air && !BlockDefinition.IsDecor(around)) // water still counts as something to build against
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the cell's cube overlaps the player's box (touching an edge does not count).</summary>
    /// <param name="cell">The cell.</param>
    public bool OverlapsPlayer(BlockPos cell) => OverlapsPlayer(cell, BlockId.Stone);

    /// <summary>
    /// Whether the collision of the block <paramref name="id"/> placed at the cell would overlap the player's box (K03: the true shape, so a slab placed under the player's feet is allowed while a fence's 24-sixteenth post
    /// is not; touching does not count).
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="id">The block that would stand there.</param>
    /// <param name="state">Its state byte (0 until placement writes states, K06).</param>
    public bool OverlapsPlayer(BlockPos cell, BlockId id, byte state = 0) =>
        BodyCollision.Overlaps(id, state, cell, Player.MinX, Player.MinY, Player.MinZ, Player.MaxX, Player.MaxY, Player.MaxZ);
}
