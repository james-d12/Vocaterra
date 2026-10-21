using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Lighting;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation;

/// <summary>
/// The game world in motion: a generated <see cref="World"/>, the player body, the inventory and a tick counter. A front-end owns a clock,
/// accumulates frame time and calls <see cref="Step(IReadOnlyList{PlayerCommand})"/> zero or more times per frame, once per 1/60 s of game
/// time; it reads state through <see cref="World"/>, <see cref="Player"/>, <see cref="Inventory"/>, <see cref="Tick"/> and <see cref="Events"/>
/// and writes only commands. Nothing here reads the clock or draws random numbers outside generation, so the same world and the same
/// command lists give the same state on every machine.
/// </summary>
public sealed class GameSimulation
{
    /// <summary>How far the player reaches to mine or place, in tiles from the middle of the body to the middle of the tile.</summary>
    public const int ReachInTiles = 5;

    private const int U = PhysicsConstants.UnitsPerTile;
    private readonly List<SimulationEvent> _events = [];
    private int _x;
    private int _y;
    private int _velocityX;
    private int _velocityY;
    private bool _grounded;
    private int _facing = 1;
    private int _coyote;
    private int _jumpBuffer;
    private bool _jumpHeld;
    private int _selectedSlot;
    private TilePoint? _miningTarget;
    private ItemId? _miningItem;
    private int _miningProgress;
    private IReadOnlyList<Recipe> _recipes = [];
    private LightMap? _light;

    private GameSimulation(World world)
    {
        World = world;
        Inventory = new Inventory();
        Inventory.SetSlot(0, new ItemStack(ItemId.Axe, 1));
        Inventory.SetSlot(1, new ItemStack(ItemId.Pickaxe, 1));
        PlacePlayer(
            (world.Spawn.X * U) + (U / 2) - (PhysicsConstants.PlayerWidth / 2),
            ((world.Spawn.Y + 1) * U) - PhysicsConstants.PlayerHeight);
    }

    /// <summary>The ticks per second of game time (60).</summary>
    public static int TicksPerSecond => PhysicsConstants.TicksPerSecond;

    /// <summary>The world (tiles, spawn, generation record).</summary>
    public World World { get; }

    /// <summary>The player's inventory: 40 slots, the first 10 the hotbar. A front-end reads it; the simulation changes it on mining and placing.</summary>
    public Inventory Inventory { get; }

    /// <summary>The number of ticks stepped so far. The game's time is this divided by <see cref="TicksPerSecond"/>.</summary>
    public long Tick { get; private set; }

    /// <summary>The events of the last <see cref="Step(IReadOnlyList{PlayerCommand})"/>; replaced by the next step.</summary>
    public IReadOnlyList<SimulationEvent> Events => _events;

    /// <summary>A snapshot of the player body.</summary>
    public PlayerState Player => new(_x, _y, _velocityX, _velocityY, _grounded, _facing);

    /// <summary>The selected hotbar slot, 0 to 9. The axe and the pickaxe start in slots 0 and 1, and slot 0 is selected.</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>The stack in the selected slot (empty when the slot holds nothing).</summary>
    public ItemStack SelectedStack => Inventory.Slots[_selectedSlot];

    /// <summary>
    /// The light of the world (see <see cref="LightMap"/>). It is computed on first use and then kept up to date as tiles are mined and
    /// placed, so a host that never reads it pays nothing for it.
    /// </summary>
    public LightMap Light => _light ??= new LightMap(World.Tiles);

    /// <summary>The recipes the player can craft, in the order the front-end shows them; set by the host with <see cref="SetRecipes"/>.</summary>
    public IReadOnlyList<Recipe> Recipes => _recipes;

    /// <summary>The tile being mined, or <see langword="null"/> when no mining is going on.</summary>
    public TilePoint? MiningTarget => _miningTarget;

    /// <summary>The ticks of holding already spent on <see cref="MiningTarget"/>.</summary>
    public int MiningProgress => _miningProgress;

    /// <summary>Generates the world of <paramref name="seed"/> (<see cref="WorldSize.Small"/>) and starts a simulation on it.</summary>
    public static GameSimulation Create(ulong seed) => new(WorldGenerator.Generate(seed));

    /// <summary>Generates the world of <paramref name="seed"/> at the given size and starts a simulation on it.</summary>
    public static GameSimulation Create(ulong seed, WorldSize size) => new(WorldGenerator.Generate(seed, size));

    /// <summary>Starts a simulation on an existing world, with the player standing on the spawn tile.</summary>
    public static GameSimulation Create(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new GameSimulation(world);
    }

    /// <summary>Advances one tick with no input.</summary>
    public void Step() => Step([]);

    /// <summary>
    /// Advances one tick. The commands are what the player does during this tick; movement and jump commands are levels (send the held
    /// ones every tick), and a command kind the simulation does not know is ignored. Commands are applied in a fixed order whatever their
    /// order in the list: the hotbar selection, then mining (the last mine command of the tick), then placing (each place command in list
    /// order), then crafting (each craft command in list order), then movement, jump and physics.
    /// </summary>
    public void Step(IReadOnlyList<PlayerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _events.Clear();
        var left = false;
        var right = false;
        var jump = false;
        TilePoint? mine = null;
        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i].Kind)
            {
                case PlayerCommandKind.MoveLeft:
                    left = true;
                    break;
                case PlayerCommandKind.MoveRight:
                    right = true;
                    break;
                case PlayerCommandKind.Jump:
                    jump = true;
                    break;
                case PlayerCommandKind.SelectSlot:
                    if (Inventory.IsHotbarSlot(commands[i].X))
                    {
                        _selectedSlot = commands[i].X;
                    }

                    break;
                case PlayerCommandKind.Mine:
                    mine = new TilePoint(commands[i].X, commands[i].Y);
                    break;
                default:
                    break;
            }
        }

        Tick++;
        StepMining(mine);
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].Kind == PlayerCommandKind.Place)
            {
                TryPlace(new TilePoint(commands[i].X, commands[i].Y));
            }
        }

        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].Kind == PlayerCommandKind.Craft)
            {
                Craft(commands[i].X);
            }
        }

        StepPlayer(left, right, jump);
    }

    /// <summary>
    /// Gives the player items outside the command stream: the starting kit of a demo or a debug launch option. It is a host action, not a game
    /// rule; a run is deterministic when the host makes the same calls at the same ticks.
    /// </summary>
    /// <returns>How many did not fit.</returns>
    public int GrantItems(ItemId item, int count) => Inventory.Add(item, count);

    /// <summary>
    /// Puts the player's body where it stands on the tile (its feet on the tile's bottom edge, as on the spawn), at rest. A debug action for a
    /// front-end's launch options (a look at a cave); it is not a game rule, and it is not a command.
    /// </summary>
    public void TeleportTo(TilePoint tile)
    {
        if (!World.Tiles.InBounds(tile.X, tile.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(tile), tile, "The tile lies outside the world.");
        }

        PlacePlayer((tile.X * U) + (U / 2) - (PhysicsConstants.PlayerWidth / 2), ((tile.Y + 1) * U) - PhysicsConstants.PlayerHeight);
    }

    /// <summary>
    /// Sets the recipe list (plain values the game layer read from the bundle). Recipes are rules, not state: they are not part of
    /// <see cref="ComputeStateDigest"/>.
    /// </summary>
    public void SetRecipes(IReadOnlyList<Recipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        _recipes = recipes.ToArray();
    }

    /// <summary>Whether a tile of kind <paramref name="station"/> lies within <see cref="CraftingRules.StationReachInTiles"/> of the player.</summary>
    public bool StationInReach(TileId station)
    {
        var radius = CraftingRules.StationReachInTiles;
        var centerX = TileCollider.FloorDiv(_x + (PhysicsConstants.PlayerWidth / 2), U);
        var centerY = TileCollider.FloorDiv(_y + (PhysicsConstants.PlayerHeight / 2), U);
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (World.Tiles.GetTile(x, y) == station && WithinTiles(new TilePoint(x, y), radius))
                {
                    return true;
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
        return CraftingRules.Check(recipe, Inventory, recipe.Station is { } station && StationInReach(station));
    }

    /// <summary>Crafts the recipe at <paramref name="recipeIndex"/> when <see cref="CanCraft"/> says so (the same as a <see cref="PlayerCommand.Craft"/> in a tick).</summary>
    public CraftResult Craft(int recipeIndex)
    {
        var result = CanCraft(recipeIndex);
        if (result != CraftResult.Done)
        {
            return result;
        }

        var recipe = _recipes[recipeIndex];
        CraftingRules.Craft(recipe, Inventory, recipe.Station is null || StationInReach(recipe.Station.Value));
        _events.Add(new SimulationEvent(SimulationEventKind.ItemCrafted, Tick));
        return result;
    }

    /// <summary>
    /// A SHA-256 of the whole simulation state (the world's tile digest, the tick, the player body, its input memory, the hotbar selection,
    /// the mining progress and the inventory) as 64 lowercase hex characters. Two runs that diverge anywhere differ here; the game's state hash
    /// for the language core can be built from it.
    /// </summary>
    public string ComputeStateDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(World.Tiles.ComputeDigest());
        Span<byte> buffer = stackalloc byte[8 + (7 * 4)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, Tick);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], _x);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..], _y);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], _velocityX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[20..], _velocityY);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[24..], (_grounded ? 1 : 0) | (_jumpHeld ? 2 : 0) | (_facing < 0 ? 4 : 0));
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..], _coyote);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[32..], _jumpBuffer);
        hash.AppendData(buffer);
        var rest = new byte[(6 * 4) + (Inventory.SlotCount * 8)];
        BinaryPrimitives.WriteInt32LittleEndian(rest, _selectedSlot);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(4), _miningProgress);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(8), _miningTarget?.X ?? -1);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(12), _miningTarget?.Y ?? -1);
        BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(16), _miningItem is { } held ? (int)held : -1);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            var stack = Inventory.Slots[i];
            BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(24 + (i * 8)), stack.IsEmpty ? -1 : (int)stack.Item);
            BinaryPrimitives.WriteInt32LittleEndian(rest.AsSpan(28 + (i * 8)), stack.Count);
        }

        hash.AppendData(rest);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>Puts the player box with its top-left corner at the position (in units), at rest, and works out whether it stands on ground.</summary>
    internal void PlacePlayer(int x, int y)
    {
        _x = x;
        _y = y;
        _velocityX = 0;
        _velocityY = 0;
        _coyote = 0;
        _jumpBuffer = 0;
        _jumpHeld = false;
        _grounded = IsStandingOnGround();
    }

    private void StepPlayer(bool left, bool right, bool jump)
    {
        var grid = World.Tiles;
        var wasGrounded = _grounded;

        // Horizontal input.
        var direction = (right ? 1 : 0) - (left ? 1 : 0);
        if (direction != 0)
        {
            _facing = direction;
            var acceleration = wasGrounded ? PhysicsConstants.GroundAcceleration : PhysicsConstants.AirAcceleration;
            _velocityX = Math.Clamp(_velocityX + (direction * acceleration), -PhysicsConstants.MaxRunSpeed, PhysicsConstants.MaxRunSpeed);
        }
        else
        {
            var friction = wasGrounded ? PhysicsConstants.GroundFriction : PhysicsConstants.AirFriction;
            _velocityX = _velocityX > 0 ? Math.Max(0, _velocityX - friction) : Math.Min(0, _velocityX + friction);
        }

        // Jump: a press is remembered for a few ticks, the ground is remembered for a few ticks after leaving it.
        _coyote = wasGrounded ? PhysicsConstants.CoyoteTicks : Math.Max(0, _coyote - 1);
        var pressed = jump && !_jumpHeld;
        _jumpHeld = jump;
        _jumpBuffer = pressed ? PhysicsConstants.JumpBufferTicks : Math.Max(0, _jumpBuffer - 1);
        if (_jumpBuffer > 0 && _coyote > 0)
        {
            _velocityY = -PhysicsConstants.JumpVelocity;
            _coyote = 0;
            _jumpBuffer = 0;
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerJumped, Tick));
        }
        else if (!jump && _velocityY < -PhysicsConstants.JumpCutVelocity)
        {
            _velocityY = -PhysicsConstants.JumpCutVelocity;
        }

        _velocityY = Math.Min(_velocityY + PhysicsConstants.Gravity, PhysicsConstants.MaxFallSpeed);

        // Move on X, with a step up onto a one-tile ledge when walking on the ground.
        var startX = _x;
        if (TileCollider.MoveX(grid, ref _x, _y, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight, _velocityX))
        {
            var target = startX + _velocityX;
            if (wasGrounded
                && _velocityY >= 0
                && TileCollider.IsFree(grid, _x, _y - U, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight)
                && TileCollider.IsFree(grid, target, _y - U, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight))
            {
                _y -= U;
                _x = target;
            }
            else
            {
                _velocityX = 0;
            }
        }

        // Move on Y.
        if (TileCollider.MoveY(grid, _x, ref _y, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight, _velocityY))
        {
            _velocityY = 0;
        }

        _grounded = IsStandingOnGround();
        if (_grounded && !wasGrounded)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.PlayerLanded, Tick));
        }
    }

    private ItemDefinition? SelectedDefinition => SelectedStack.IsEmpty ? null : ItemDefinition.Get(SelectedStack.Item);

    /// <summary>Whether the tile lies within reach of the middle of the player's body.</summary>
    private bool InReach(TilePoint tile) => WithinTiles(tile, ReachInTiles);

    private bool WithinTiles(TilePoint tile, int radiusInTiles)
    {
        long dx = (tile.X * U) + (U / 2) - (_x + (PhysicsConstants.PlayerWidth / 2));
        long dy = (tile.Y * U) + (U / 2) - (_y + (PhysicsConstants.PlayerHeight / 2));
        long reach = (long)radiusInTiles * U;
        return (dx * dx) + (dy * dy) <= reach * reach;
    }

    private void ResetMining()
    {
        _miningTarget = null;
        _miningItem = null;
        _miningProgress = 0;
    }

    /// <summary>
    /// One tick of mining. The target must be a tile in reach that is not air, the selected tool must be the one it needs, and its drop must
    /// fit in the inventory (a full inventory holds the mining back; it does not destroy the tile). Progress is kept while the target and the
    /// selected item stay the same; the tile goes when the progress reaches its mining ticks.
    /// </summary>
    private void StepMining(TilePoint? target)
    {
        if (target is not { } tile || !World.Tiles.InBounds(tile.X, tile.Y) || !InReach(tile))
        {
            ResetMining();
            return;
        }

        var kind = World.Tiles.GetTile(tile.X, tile.Y);
        if (kind == TileId.Air)
        {
            ResetMining();
            return;
        }

        var definition = TileDefinition.Get(kind);
        var selected = SelectedDefinition;
        var tool = selected?.Tool ?? ToolKind.None;
        if (definition.RequiredTool != ToolKind.None && tool != definition.RequiredTool)
        {
            ResetMining();
            return;
        }

        var felled = kind == TileId.Trunk ? FellingOf(tile) : [];
        var dropCount = 1 + felled.Count(t => t.Kind == TileId.Trunk);
        if (definition.Drop is { } expected && !Inventory.CanAdd(expected, dropCount))
        {
            ResetMining();
            return;
        }

        var held = selected?.Id;
        if (_miningTarget != tile || _miningItem != held)
        {
            _miningTarget = tile;
            _miningItem = held;
            _miningProgress = 0;
        }

        _miningProgress++;
        if (_miningProgress < definition.MiningTicks)
        {
            return;
        }

        World.Tiles.SetTile(tile.X, tile.Y, TileId.Air);
        foreach (var (point, _) in felled)
        {
            World.Tiles.SetTile(point.X, point.Y, TileId.Air);
        }

        _light?.TilesChanged(felled.Count > 0 ? tile.X - 2 : tile.X, felled.Count > 0 ? tile.X + 2 : tile.X);
        if (definition.Drop is { } gained)
        {
            Inventory.Add(gained, dropCount);
        }

        ResetMining();
        _events.Add(new SimulationEvent(SimulationEventKind.TileMined, Tick));
    }

    /// <summary>
    /// The tiles that fall with a mined trunk tile: the trunk tiles straight above it and the leaves of the canopy around the top (the
    /// shape the forest pass plants: two columns either side, from two rows above the top trunk to one below it). The mined tile itself is
    /// not in the list. Deterministic: it reads the tiles only.
    /// </summary>
    private List<(TilePoint Point, TileId Kind)> FellingOf(TilePoint mined)
    {
        var result = new List<(TilePoint, TileId)>();
        var top = mined.Y;
        while (World.Tiles.GetTile(mined.X, top - 1) == TileId.Trunk)
        {
            top--;
            result.Add((new TilePoint(mined.X, top), TileId.Trunk));
        }

        for (var dy = -2; dy <= 1; dy++)
        {
            var reach = dy is -2 or 1 ? 1 : 2;
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (World.Tiles.GetTile(mined.X + dx, top + dy) == TileId.Leaves)
                {
                    result.Add((new TilePoint(mined.X + dx, top + dy), TileId.Leaves));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Places the selected item's block at the tile when the item places one, the tile is in reach and empty, and a solid block does not
    /// overlap the player's body. One item is used up.
    /// </summary>
    private void TryPlace(TilePoint tile)
    {
        var definition = SelectedDefinition;
        if (definition?.PlacesTile is not { } placed
            || !World.Tiles.InBounds(tile.X, tile.Y)
            || !InReach(tile)
            || World.Tiles.GetTile(tile.X, tile.Y) != TileId.Air)
        {
            return;
        }

        if (TileDefinition.IsSolid(placed) && OverlapsPlayer(tile))
        {
            return;
        }

        World.Tiles.SetTile(tile.X, tile.Y, placed);
        _light?.TilesChanged(tile.X, tile.X);
        Inventory.RemoveFromSlot(_selectedSlot, 1);
        _events.Add(new SimulationEvent(SimulationEventKind.TilePlaced, Tick));
    }

    /// <summary>Whether the tile's square overlaps the player's box (touching an edge does not count).</summary>
    private bool OverlapsPlayer(TilePoint tile) =>
        _x < ((tile.X + 1) * U)
        && _x + PhysicsConstants.PlayerWidth > tile.X * U
        && _y < ((tile.Y + 1) * U)
        && _y + PhysicsConstants.PlayerHeight > tile.Y * U;

    private bool IsStandingOnGround() =>
        !TileCollider.IsFree(World.Tiles, _x, _y + 1, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight);
}
