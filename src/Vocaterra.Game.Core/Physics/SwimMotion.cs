namespace Vocaterra.Game.Core.Physics;

/// <summary>
/// The numbers and the velocity rules of moving through water, in the body's fixed-point units (<see cref="VoxelPhysics"/>): pure functions,
/// no state, so the body composes them and each rule is tested alone. Speeds are named in blocks per second and converted to units per tick
/// once, at compile time.
/// </summary>
internal static class SwimMotion
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>The walking speed while wading (water at the feet only), as a share of the walking speed in thousandths (0.8).</summary>
    public const int WadePermille = 800;

    /// <summary>The horizontal speed while swimming (water at the waist or higher), as a share of the walking speed in thousandths (0.6; about 2.6 blocks per second).</summary>
    public const int SwimPermille = 600;

    /// <summary>The gravity while swimming, as a share of <see cref="VoxelPhysics.Gravity"/> in thousandths (0.2).</summary>
    public const int GravityPermille = 200;

    /// <summary>The vertical drag in thousandths of the vertical speed taken off each tick (0.07); with the swimming gravity it makes the sink speed settle at about 1.5 blocks per second.</summary>
    public const int DragPermille = 70;

    /// <summary>The top sink speed in blocks per second; also the most a fall into water keeps of its downward speed.</summary>
    public const double SinkBlocksPerSecond = 1.5;

    /// <summary>The swim-up speed with jump held, in blocks per second (the acceleration stops adding above it; a hop out may exceed it).</summary>
    public const double RiseBlocksPerSecond = 2.5;

    /// <summary>
    /// The upward speed of a hop out onto a bank, in blocks per second (about 12, above a jump's 9.4: the body leaves the swim as soon as its
    /// waist clears the water, and from there ordinary gravity must still lift it over a bank a block above the surface).
    /// </summary>
    public const double HopBlocksPerSecond = 12.0;

    /// <summary>The highest ledge a hop out can reach, in blocks above the feet (a one-block bank seen from a swimmer at the surface is about 2.0 above the feet; a two-block bank is 3.0).</summary>
    public const double HopReachBlocks = 2.25;

    /// <summary>The gravity while swimming, units per tick squared (about 2.6 blocks per second squared).</summary>
    public const int SwimGravity = VoxelPhysics.Gravity * GravityPermille / 1000;

    /// <summary>The top sink speed in units per tick (about 102).</summary>
    public const int SinkSpeed = (int)(SinkBlocksPerSecond * U / VoxelPhysics.TicksPerSecond);

    /// <summary>The swim-up speed in units per tick (about 170).</summary>
    public const int RiseSpeed = (int)(RiseBlocksPerSecond * U / VoxelPhysics.TicksPerSecond);

    /// <summary>The upward push of held jump in units per tick squared; with the drag and gravity it settles at <see cref="RiseSpeed"/> (about 16 blocks per second squared).</summary>
    public const int RiseAcceleration = 18;

    /// <summary>The upward speed of a hop out, units per tick (about 820).</summary>
    public const int HopVelocity = (int)(HopBlocksPerSecond * U / VoxelPhysics.TicksPerSecond);

    /// <summary>The highest ledge a hop out can reach, in units above the feet.</summary>
    public const int HopReach = (int)(HopReachBlocks * U);

    /// <summary>The horizontal speed in units per tick: <see cref="SwimPermille"/> of the walk when swimming, <see cref="WadePermille"/> when wading.</summary>
    public static int HorizontalSpeed(bool swimming) =>
        VoxelPhysics.WalkSpeed * (swimming ? SwimPermille : WadePermille) / 1000;

    /// <summary>
    /// The vertical speed after one swimming tick: the drag and the slight gravity act, held jump pushes up (until <see cref="RiseSpeed"/>),
    /// and a fall faster than <see cref="SinkSpeed"/> is cut to it.
    /// </summary>
    public static int NextVerticalSpeed(int velocityY, bool jumpHeld)
    {
        var next = velocityY - (velocityY * DragPermille / 1000) - SwimGravity;
        if (jumpHeld && velocityY < RiseSpeed)
        {
            next += RiseAcceleration;
        }

        return Math.Max(next, -SinkSpeed);
    }

    /// <summary>The vertical speed of a hop out: <see cref="HopVelocity"/>, or the current one if it is already higher (so held contact with the wall does not add up).</summary>
    public static int HopSpeed(int velocityY) => Math.Max(velocityY, HopVelocity);

    /// <summary>Whether a ledge <paramref name="rise"/> units above the feet is within a hop's reach.</summary>
    public static bool InHopReach(int rise) => rise > 0 && rise <= HopReach;
}
