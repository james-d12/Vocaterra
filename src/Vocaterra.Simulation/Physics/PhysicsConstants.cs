namespace Vocaterra.Simulation.Physics;

/// <summary>
/// The physics constants. Positions are integers in 1/4096 of a tile ("units"), velocities are units per tick and accelerations units per
/// tick squared, so a tick is exact integer arithmetic and replays are bit-identical on every platform. The y axis grows downwards.
/// </summary>
public static class PhysicsConstants
{
    /// <summary>The simulation rate: ticks per second.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Units in one tile.</summary>
    public const int UnitsPerTile = 4096;

    /// <summary>The player box width: 1.25 tiles.</summary>
    public const int PlayerWidth = UnitsPerTile * 5 / 4;

    /// <summary>The player box height: 2.75 tiles.</summary>
    public const int PlayerHeight = UnitsPerTile * 11 / 4;

    /// <summary>Downward acceleration per tick.</summary>
    public const int Gravity = 100;

    /// <summary>The terminal fall speed (0.625 tiles per tick, always below one tile, though the collision sweep does not rely on it).</summary>
    public const int MaxFallSpeed = 2560;

    /// <summary>The top walking speed (about 9.4 tiles per second).</summary>
    public const int MaxRunSpeed = 640;

    /// <summary>Horizontal acceleration per tick on the ground while a direction is held.</summary>
    public const int GroundAcceleration = 50;

    /// <summary>Horizontal slow-down per tick on the ground with no direction held.</summary>
    public const int GroundFriction = 70;

    /// <summary>Horizontal acceleration per tick in the air while a direction is held.</summary>
    public const int AirAcceleration = 30;

    /// <summary>Horizontal slow-down per tick in the air with no direction held.</summary>
    public const int AirFriction = 10;

    /// <summary>The upward speed a jump starts with (a rise of about 4.9 tiles with the full button hold).</summary>
    public const int JumpVelocity = 1950;

    /// <summary>Releasing the jump while rising caps the upward speed at this value, so a tap gives a short hop.</summary>
    public const int JumpCutVelocity = 600;

    /// <summary>Ticks after leaving the ground in which a jump is still allowed.</summary>
    public const int CoyoteTicks = 6;

    /// <summary>Ticks a jump press is remembered before landing.</summary>
    public const int JumpBufferTicks = 6;
}
