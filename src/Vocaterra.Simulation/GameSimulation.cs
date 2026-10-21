using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Physics;

namespace Vocaterra.Simulation;

/// <summary>
/// The game world in motion: a generated <see cref="World"/>, the player body and a tick counter. A front-end owns a clock, accumulates frame
/// time and calls <see cref="Step(IReadOnlyList{PlayerCommand})"/> zero or more times per frame, once per 1/60 s of game time; it reads
/// state through <see cref="World"/>, <see cref="Player"/>, <see cref="Tick"/> and <see cref="Events"/> and writes only commands.
/// Nothing here reads the clock or draws random numbers outside generation, so the same world and the same command lists give the same
/// state on every machine.
/// </summary>
public sealed class GameSimulation
{
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

    private GameSimulation(World world)
    {
        World = world;
        PlacePlayer(
            (world.Spawn.X * U) + (U / 2) - (PhysicsConstants.PlayerWidth / 2),
            ((world.Spawn.Y + 1) * U) - PhysicsConstants.PlayerHeight);
    }

    /// <summary>The ticks per second of game time (60).</summary>
    public static int TicksPerSecond => PhysicsConstants.TicksPerSecond;

    /// <summary>The world (tiles, spawn, generation record).</summary>
    public World World { get; }

    /// <summary>The number of ticks stepped so far. The game's time is this divided by <see cref="TicksPerSecond"/>.</summary>
    public long Tick { get; private set; }

    /// <summary>The events of the last <see cref="Step(IReadOnlyList{PlayerCommand})"/>; replaced by the next step.</summary>
    public IReadOnlyList<SimulationEvent> Events => _events;

    /// <summary>A snapshot of the player body.</summary>
    public PlayerState Player => new(_x, _y, _velocityX, _velocityY, _grounded, _facing);

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
    /// order in the list: movement, then jump, then physics.
    /// </summary>
    public void Step(IReadOnlyList<PlayerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _events.Clear();
        var left = false;
        var right = false;
        var jump = false;
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
                default:
                    break;
            }
        }

        Tick++;
        StepPlayer(left, right, jump);
    }

    /// <summary>
    /// A SHA-256 of the whole simulation state (the world's tile digest, the tick, the player body and its input memory) as 64 lowercase hex
    /// characters. Two runs that diverge anywhere differ here; the game's state hash for the language core can be built from it.
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

    private bool IsStandingOnGround() =>
        !TileCollider.IsFree(World.Tiles, _x, _y + 1, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight);
}
