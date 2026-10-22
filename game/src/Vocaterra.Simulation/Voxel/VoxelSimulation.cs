using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;

namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// The 3D game world in motion: a built <see cref="Scene"/>, the player body, the inventory and a tick counter. A front-end owns a clock,
/// accumulates frame time and calls <see cref="Step(IReadOnlyList{VoxelCommand})"/> zero or more times per frame, once per 1/60 s of game
/// time; it reads state through <see cref="Grid"/>, <see cref="Player"/>, <see cref="Inventory"/>, <see cref="Tick"/> and
/// <see cref="Events"/> and writes only commands. Nothing here reads the clock or draws random numbers (the scene builder already used the
/// seed), so the same scene and the same command lists give the same state on every machine. The simulation edits the scene's grid in place.
/// </summary>
public sealed class VoxelSimulation
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

    private VoxelSimulation(Scene scene)
    {
        Scene = scene;
        Inventory = new Inventory();
        var spawn = scene.Spawn.Feet;
        Player = new PlayerBody3D(scene.Grid, (spawn.X * U) + (U / 2), spawn.Y * U, (spawn.Z * U) + (U / 2), scene.Spawn.Yaw);
    }

    /// <summary>The scene (grid, spawn, NPCs, named points).</summary>
    public Scene Scene { get; }

    /// <summary>The blocks (the scene's grid; the simulation changes it when blocks are gathered or placed).</summary>
    public VoxelGrid Grid => Scene.Grid;

    /// <summary>The player's body: position, velocity, facing and the eye. Read only for a front-end.</summary>
    public PlayerBody3D Player { get; }

    /// <summary>The player's inventory: 40 slots, the first 10 the hotbar. It starts empty.</summary>
    public Inventory Inventory { get; }

    /// <summary>The number of ticks stepped so far. The game's time is this divided by <see cref="TicksPerSecond"/>.</summary>
    public long Tick { get; private set; }

    /// <summary>The events of the last step; replaced by the next step.</summary>
    public IReadOnlyList<SimulationEvent> Events => _events;

    /// <summary>The selected hotbar slot, 0 to 9.</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>The stack in the selected slot (empty when the slot holds nothing).</summary>
    public ItemStack SelectedStack => Inventory.Slots[_selectedSlot];

    /// <summary>The block being gathered, or <see langword="null"/> when no gathering is going on.</summary>
    public BlockPos? GatherTarget => _gatherTarget;

    /// <summary>The ticks of holding already spent on <see cref="GatherTarget"/>.</summary>
    public int GatherProgress => _gatherProgress;

    /// <summary>The recipes the player can craft; set by the host with <see cref="SetRecipes"/>.</summary>
    public IReadOnlyList<Recipe> Recipes => _recipes;

    /// <summary>What the player's eye is looking at within the reach (a block, the face and the cell in front of it), or <see langword="null"/>.</summary>
    public BlockRayHit? Target => BlockRay.Cast(Grid, Player);

    /// <summary>Starts a simulation on a scene, with the player standing at its spawn.</summary>
    public static VoxelSimulation Create(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new VoxelSimulation(scene);
    }

    /// <summary>Advances one tick with no input.</summary>
    public void Step() => Step([]);

    /// <summary>
    /// Advances one tick. Walking and jumping are levels (send the held ones every tick); <see cref="VoxelCommandKind.Interact"/> is held;
    /// the other commands are presses. A command kind the simulation does not know is ignored. Commands are applied in a fixed order
    /// whatever their order in the list: the hotbar selection and the facing (the last of each wins), then gathering (the last interact of
    /// the tick), then placing and crafting (each in list order), then the walk (all move commands add up) with the jump and the physics.
    /// </summary>
    public void Step(IReadOnlyList<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _events.Clear();
        var jump = false;
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
                default:
                    break;
            }
        }

        var (intentX, intentZ) = WalkIntent(commands);
        var (jumped, landed) = Player.Step(Grid, intentX, intentZ, jump);
        if (jumped)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerJumped, Tick));
        }

        if (landed)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerLanded, Tick));
        }
    }

    /// <summary>
    /// Gives the player items outside the command stream: the starting kit of a demo or a test. It is a host action, not a game rule; a run
    /// is deterministic when the host makes the same calls at the same ticks.
    /// </summary>
    /// <returns>How many did not fit.</returns>
    public int GrantItems(ItemId item, int count) => Inventory.Add(item, count);

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
                    if (Grid.GetBlock(x, y, z) != station)
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

    /// <summary>What crafting the recipe at <paramref name="recipeIndex"/> would do now (nothing is changed).</summary>
    public CraftResult CanCraft(int recipeIndex)
    {
        if ((uint)recipeIndex >= (uint)_recipes.Count)
        {
            return CraftResult.NoSuchRecipe;
        }

        var recipe = _recipes[recipeIndex];
        return CraftingRules.Check(recipe, Inventory, RecipeStationInReach(recipe));
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
        CraftingRules.Craft(recipe, Inventory, RecipeStationInReach(recipe));
        _events.Add(new SimulationEvent(SimulationEventKind.ItemCrafted, Tick, Detail: (int)recipe.Output, Amount: recipe.OutputCount));
        return result;
    }

    /// <summary>
    /// The simulation's state apart from its grid, as plain values (a save writes it). Nothing here changes the simulation.
    /// </summary>
    public VoxelSimulationState CaptureState()
    {
        var slots = new ItemStack[Inventory.SlotCount];
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            slots[i] = Inventory.Slots[i];
        }

        return new VoxelSimulationState(Tick, _selectedSlot, _gatherTarget, _gatherBlock, _gatherProgress, _noBottleHint, Player.State(), slots);
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

        if (state.Slots.Count != Inventory.SlotCount)
        {
            throw new ArgumentException($"The inventory has {state.Slots.Count} slots, not {Inventory.SlotCount}.", nameof(state));
        }

        Tick = state.Tick;
        _selectedSlot = state.SelectedSlot;
        _gatherTarget = state.GatherTarget;
        _gatherBlock = state.GatherBlock;
        _gatherProgress = state.GatherProgress;
        _noBottleHint = state.NoBottleHint;
        Player.Restore(state.Player);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            Inventory.SetSlot(i, state.Slots[i]);
        }

        _events.Clear();
    }

    /// <summary>
    /// A SHA-256 of the whole simulation state (the grid's digest, the tick, the body, the hotbar selection, the gathering progress and the
    /// inventory) as 64 lowercase hex characters. Two runs that diverge anywhere differ here.
    /// </summary>
    public string ComputeStateDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Grid.ComputeDigest());
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
        var definition = BlockDefinition.Get(id);
        if (!definition.Gatherable || !Inventory.CanAdd(definition.Drop!.Value, definition.DropCount))
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

        _gatherProgress++;
        _events.Add(new SimulationEvent(SimulationEventKind.GatherProgress, Tick, cell.X, cell.Y, cell.Z, _gatherProgress, definition.GatherTicks));
        if (_gatherProgress < definition.GatherTicks)
        {
            return;
        }

        var drop = definition.Drop.Value;
        Grid.SetBlock(cell.X, cell.Y, cell.Z, definition.BecomesWhenGathered);
        Inventory.Add(drop, definition.DropCount);
        ResetGather();
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
            || Grid.GetBlock(cell.X, cell.Y, cell.Z) != BlockId.Air
            || !InReach(cell)
            || BlockDefinition.PlacedBy(item) is not { } placed
            || Inventory.Count(item) < 1
            || !HasNeighbour(cell))
        {
            return;
        }

        if (BlockDefinition.IsSolid(placed) && OverlapsPlayer(cell))
        {
            return;
        }

        Grid.SetBlock(cell.X, cell.Y, cell.Z, placed);
        Inventory.Remove(item, 1);
        _events.Add(new SimulationEvent(SimulationEventKind.BlockPlaced, Tick, cell.X, cell.Y, cell.Z, (int)placed, (int)item));
    }

    private bool HasNeighbour(BlockPos cell)
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var next = cell.Offset(face);
            if (Grid.GetBlock(next.X, next.Y, next.Z) != BlockId.Air)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the cell's cube overlaps the player's box (touching an edge does not count).</summary>
    private bool OverlapsPlayer(BlockPos cell) =>
        Player.MinX < (cell.X + 1) * U && Player.MaxX > cell.X * U
        && Player.MinY < (cell.Y + 1) * U && Player.MaxY > cell.Y * U
        && Player.MinZ < (cell.Z + 1) * U && Player.MaxZ > cell.Z * U;
}
