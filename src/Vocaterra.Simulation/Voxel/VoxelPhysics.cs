namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// The 3D physics constants. Positions are integers in 1/4096 of a block ("units"), velocities are units per tick and accelerations units
/// per tick squared, so a tick is exact integer arithmetic and replays are bit-identical on every platform. Y grows up here (the 2D
/// physics has Y growing down). The box sizes are the nearest whole unit to the design values (0.6 x 1.8 blocks, eye at 1.62).
/// </summary>
public static class VoxelPhysics
{
    /// <summary>The simulation rate: ticks per second.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Units in one block.</summary>
    public const int UnitsPerBlock = 4096;

    /// <summary>The player box width and depth: 0.6 blocks (2 458 units, 0.60010; even, so the box is centred on a whole unit).</summary>
    public const int PlayerWidth = 2458;

    /// <summary>The player box height: 1.8 blocks (7 373 units, 1.80005).</summary>
    public const int PlayerHeight = 7373;

    /// <summary>The eye height above the feet: 1.62 blocks (6 636 units, 1.62012).</summary>
    public const int EyeHeight = 6636;

    /// <summary>Downward acceleration per tick (about 32 blocks per second squared).</summary>
    public const int Gravity = 36;

    /// <summary>The terminal fall speed in units per tick (about 47 blocks per second; the collision sweep does not depend on it being below one block).</summary>
    public const int MaxFallSpeed = 3000;

    /// <summary>The walking speed in units per tick (about 4.3 blocks per second).</summary>
    public const int WalkSpeed = 294;

    /// <summary>The upward speed a jump starts with; with <see cref="Gravity"/> it rises about 1.3 blocks, so one block can be jumped and two cannot.</summary>
    public const int JumpVelocity = 640;

    /// <summary>The most a body can step up while walking on the ground: one block.</summary>
    public const int StepHeight = UnitsPerBlock;

    /// <summary>How far the player reaches to gather or place, in blocks from the eye to the nearest point of the block.</summary>
    public const int ReachInBlocks = 4;

    /// <summary>How near a station block must be for a recipe to count it, in blocks from the middle of the body to the middle of the block.</summary>
    public const int StationReachInBlocks = 6;
}
