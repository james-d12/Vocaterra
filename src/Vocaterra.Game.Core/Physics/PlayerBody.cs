using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.Water.Currents;

namespace Vocaterra.Game.Core.Physics;

/// <summary>
/// The player's body in the 3D world: an axis-aligned box of 0.6 x 1.8 x 0.6 blocks (<see cref="VoxelPhysics"/>), integer positions in
/// 1/4096 block, gravity, walking, jumping, a step-up of up to one block onto the top of the box met (a slab, a stair, a block) while walking on the ground, and the facing (yaw and pitch, see
/// <see cref="Angles"/>). A front-end reads it; <see cref="VoxelSimulation"/> steps it. All arithmetic is integer, so replays are
/// bit-identical.
/// </summary>
public sealed partial class PlayerBody
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int W = VoxelPhysics.PlayerWidth;
    private const int H = VoxelPhysics.PlayerHeight;

    internal PlayerBody(IVoxelReader grid, int centerX, int feetY, int centerZ, int yaw)
    {
        MinX = centerX - (W / 2);
        MinY = feetY;
        MinZ = centerZ - (W / 2);
        Yaw = Angles.Wrap(yaw);
        Grounded = !VoxelCollider.IsFree(grid, MinX, MinY - 1, MinZ);
    }

    /// <summary>The west edge of the box in units.</summary>
    public int MinX { get; private set; }

    /// <summary>The bottom of the box (the feet) in units.</summary>
    public int MinY { get; private set; }

    /// <summary>The north edge of the box in units.</summary>
    public int MinZ { get; private set; }

    /// <summary>The east edge of the box in units (exclusive).</summary>
    public int MaxX => MinX + W;

    /// <summary>The top of the box in units (exclusive).</summary>
    public int MaxY => MinY + H;

    /// <summary>The south edge of the box in units (exclusive).</summary>
    public int MaxZ => MinZ + W;

    /// <summary>The east-west position of the middle of the feet, in units.</summary>
    public int CenterX => MinX + (W / 2);

    /// <summary>The height of the feet, in units (a multiple of 4 096 when standing on a block).</summary>
    public int FeetY => MinY;

    /// <summary>The north-south position of the middle of the feet, in units.</summary>
    public int CenterZ => MinZ + (W / 2);

    /// <summary>The eye's east-west position in units (the camera).</summary>
    public int EyeX => CenterX;

    /// <summary>The eye's height in units: the feet plus <see cref="VoxelPhysics.EyeHeight"/> (the camera).</summary>
    public int EyeY => MinY + VoxelPhysics.EyeHeight;

    /// <summary>The eye's north-south position in units (the camera).</summary>
    public int EyeZ => CenterZ;

    /// <summary>The block cell the middle of the feet is in (for a body standing on a block, the air cell just above it).</summary>
    public BlockPos FeetBlock => new(VoxelCollider.FloorDiv(CenterX, U), VoxelCollider.FloorDiv(MinY, U), VoxelCollider.FloorDiv(CenterZ, U));

    /// <summary>Horizontal speed towards the east, in units per tick.</summary>
    public int VelocityX { get; private set; }

    /// <summary>Vertical speed in units per tick (positive is up).</summary>
    public int VelocityY { get; private set; }

    /// <summary>Horizontal speed towards the south, in units per tick.</summary>
    public int VelocityZ { get; private set; }

    /// <summary>Whether a solid block is directly under the feet.</summary>
    public bool Grounded { get; private set; }

    /// <summary>Whether the last step lifted the body one block onto a ledge. A renderer eases the camera over a few frames to hide the jump.</summary>
    public bool SteppedUp { get; private set; }

    /// <summary>The facing, in 1/4096 turns: 0 north, 1 024 east, 2 048 south, 3 072 west (see <see cref="Angles"/>).</summary>
    public int Yaw { get; private set; }

    /// <summary>The look angle in 1/4096 turns, positive up, within +-<see cref="Angles.MaxPitch"/>.</summary>
    public int Pitch { get; private set; }

    internal void SetLook(int yaw, int pitch)
    {
        Yaw = Angles.Wrap(yaw);
        Pitch = Angles.ClampPitch(pitch);
    }

    /// <summary>The body as a plain value (a save reads it; <see cref="Restore"/> puts it back).</summary>
    internal PlayerBodyState State() => new(MinX, MinY, MinZ, VelocityX, VelocityY, VelocityZ, Grounded, SteppedUp, Yaw, Pitch);

    /// <summary>Puts the body back as a save recorded it. The look is taken as the save has it (the angles are wrapped and clamped as always).</summary>
    internal void Restore(PlayerBodyState state)
    {
        MinX = state.MinX;
        MinY = state.MinY;
        MinZ = state.MinZ;
        VelocityX = state.VelocityX;
        VelocityY = state.VelocityY;
        VelocityZ = state.VelocityZ;
        Grounded = state.Grounded;
        SteppedUp = state.SteppedUp;
        Yaw = Angles.Wrap(state.Yaw);
        Pitch = Angles.ClampPitch(state.Pitch);
    }

    /// <summary>Puts the body with the middle of its feet at the point (in units), at rest. A debug action; the caller makes sure the box is free.</summary>
    internal void PlaceAt(IVoxelReader grid, int centerX, int feetY, int centerZ)
    {
        MinX = centerX - (W / 2);
        MinY = feetY;
        MinZ = centerZ - (W / 2);
        VelocityX = 0;
        VelocityY = 0;
        VelocityZ = 0;
        SteppedUp = false;
        Grounded = !VoxelCollider.IsFree(grid, MinX, MinY - 1, MinZ);
    }

    /// <summary>
    /// One tick: walking at the given intent (fixed point, <see cref="Angles.One"/> = 1.0, each component within +-1.0), an optional jump,
    /// gravity, then the move on X, Z and Y with the step-up. In water the optional <paramref name="currents"/> (the scene's river) adds its drift. Returns whether the body left the ground by jumping and whether it landed.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="intentX">The walk's east component.</param>
    /// <param name="intentZ">The walk's south component.</param>
    /// <param name="jump">Whether the jump key is down.</param>
    /// <param name="currents">The scene's river current, which pushes a body in water; <see langword="null"/> for none.</param>
    /// <param name="speedPermille">The walking speed in thousandths of <see cref="VoxelPhysics.WalkSpeed"/> (1000 walks, 1600 sprints, N02); in water it scales the wading or swimming speed the same way.</param>
    internal (bool Jumped, bool Landed) Step(IVoxelReader grid, int intentX, int intentZ, bool jump, RiverCurrents? currents = null, int speedPermille = 1000)
    {
        var wet = Immersion.Of(grid, this);
        if (wet.Feet) { return StepInWater(grid, intentX, intentZ, jump, wet, currents, speedPermille); }

        var molten = LavaMotion.Immersion(grid, this); // lava is a thicker liquid than water (B10b): a body sinks into it
        if (molten.Feet) { return StepInWater(grid, intentX, intentZ, jump, molten, null, speedPermille, lava: true); }

        var speed = Scaled(VoxelPhysics.WalkSpeed, speedPermille);
        var wasGrounded = Grounded;
        SteppedUp = false;
        VelocityX = Angles.Multiply(intentX, speed);
        VelocityZ = Angles.Multiply(intentZ, speed);

        var jumped = false;
        if (jump && wasGrounded && VelocityY <= 0)
        {
            VelocityY = VoxelPhysics.JumpVelocity;
            jumped = true;
        }

        VelocityY = Math.Max(VelocityY - VoxelPhysics.Gravity, -VoxelPhysics.MaxFallSpeed);

        MoveWithStepUp(grid, wasGrounded);
        return (jumped, Grounded && !wasGrounded);
    }

    // A speed scaled by the host's permille (1000 leaves it as it is, bit for bit).
    private static int Scaled(int speed, int speedPermille) => speedPermille == 1000 ? speed : (int)((long)speed * speedPermille / 1000);

    /// <summary>
    /// The lift of a step-up (K03, building plan 3.3): the body at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) was stopped moving to (<paramref name="targetX"/>, <paramref name="targetZ"/>). The lift is the height of
    /// the top of the highest box it would overlap there that reaches down to its feet (a slab, a stair step, a block, a trapdoor on the floor), when that is at most one block and the body fits on it, both where it stopped and
    /// at the target; otherwise 0. A box that starts above the feet (a ceiling, an upside-down stair, a top slab) is never climbed.
    /// </summary>
    private static int StepLift(IVoxelReader grid, int x, int y, int z, int targetX, int targetZ)
    {
        var lift = VoxelCollider.StepLift(grid, targetX, y, targetZ);
        return lift > 0 && lift <= VoxelPhysics.StepHeight
            && VoxelCollider.IsFree(grid, x, y + lift, z)
            && VoxelCollider.IsFree(grid, targetX, y + lift, targetZ)
            ? lift
            : 0;
    }

    /// <summary>The move of one tick on X, Z and then Y with the step-up (up to one block, onto the top of the box met) (only for a body that was grounded and is not rising); sets the new position, the stopped velocities and <see cref="Grounded"/>.</summary>
    private void MoveWithStepUp(IVoxelReader grid, bool wasGrounded)
    {
        var x = MinX;
        var y = MinY;
        var z = MinZ;
        var steppingAllowed = wasGrounded && VelocityY <= 0; // at most one step-up per tick

        var startX = x;
        if (VoxelCollider.MoveX(grid, ref x, y, z, VelocityX))
        {
            var target = startX + VelocityX;
            var lift = steppingAllowed && !SteppedUp ? StepLift(grid, x, y, z, target, z) : 0;
            if (lift > 0)
            {
                y += lift;
                x = target;
                SteppedUp = true;
            }
            else
            {
                VelocityX = 0;
            }
        }

        var startZ = z;
        if (VoxelCollider.MoveZ(grid, x, y, ref z, VelocityZ))
        {
            var target = startZ + VelocityZ;
            var lift = steppingAllowed && !SteppedUp ? StepLift(grid, x, y, z, x, target) : 0;
            if (lift > 0)
            {
                y += lift;
                z = target;
                SteppedUp = true;
            }
            else
            {
                VelocityZ = 0;
            }
        }

        if (VoxelCollider.MoveY(grid, x, ref y, z, VelocityY))
        {
            VelocityY = 0;
        }

        MinX = x;
        MinY = y;
        MinZ = z;
        Grounded = !VoxelCollider.IsFree(grid, x, y - 1, z);
    }
}


