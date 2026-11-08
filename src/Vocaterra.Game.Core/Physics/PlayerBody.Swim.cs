using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.Water.Currents;

namespace Vocaterra.Game.Core.Physics;

public sealed partial class PlayerBody
{
    /// <summary>
    /// One tick with water at the feet. Wading (water below the waist): the walk at <see cref="SwimMotion.WadePermille"/>, ordinary gravity,
    /// jump and step-up. Swimming (water at the waist or higher): the slow horizontal speed, the drag, jump held to swim up, and a hop out
    /// onto a bank when a wall blocks the way. Swimming never reports a jump or a landing.
    /// </summary>
    private (bool Jumped, bool Landed) StepInWater(VoxelGrid grid, int intentX, int intentZ, bool jump, Immersion immersion, RiverCurrents? currents, int speedPermille)
    {
        var wasGrounded = Grounded;
        SteppedUp = false;
        var swimming = immersion.Body;
        var speed = Scaled(SwimMotion.HorizontalSpeed(swimming), speedPermille); // N02's walk factor and sprint apply to the water walk too
        VelocityX = Angles.Multiply(intentX, speed);
        VelocityZ = Angles.Multiply(intentZ, speed);
        var (pushX, pushZ, pull) = SwimMotion.Push(grid, currents, VoxelCollider.FloorDiv(CenterX, VoxelPhysics.UnitsPerBlock), VoxelCollider.FloorDiv(MinY, VoxelPhysics.UnitsPerBlock), VoxelCollider.FloorDiv(CenterZ, VoxelPhysics.UnitsPerBlock));
        VelocityX += pushX;
        VelocityZ += pushZ;

        var jumped = false;
        if (swimming)
        {
            VelocityY = SwimMotion.NextVerticalSpeed(VelocityY, jump);
            if (jump && HasLedgeAhead(grid))
            {
                VelocityY = SwimMotion.HopSpeed(VelocityY);
            }

            VelocityY = Math.Max(VelocityY - pull, -SwimMotion.SinkSpeed);
        }
        else
        {
            if (jump && wasGrounded && VelocityY <= 0)
            {
                VelocityY = VoxelPhysics.JumpVelocity;
                jumped = true;
            }

            VelocityY = Math.Max(VelocityY - VoxelPhysics.Gravity, -VoxelPhysics.MaxFallSpeed);
        }

        MoveWithStepUp(grid, wasGrounded);
        return (jumped, !swimming && Grounded && !wasGrounded);
    }

    /// <summary>
    /// Whether the horizontal move of this tick is blocked by a wall whose top is within <see cref="SwimMotion.HopReach"/> above the feet and
    /// the body fits on top of it: the bank a swimmer can hop out onto.
    /// </summary>
    private bool HasLedgeAhead(VoxelGrid grid) =>
        (VelocityX != 0 && LedgeAt(grid, MinX + VelocityX, MinZ)) || (VelocityZ != 0 && LedgeAt(grid, MinX, MinZ + VelocityZ));

    private bool LedgeAt(VoxelGrid grid, int x, int z)
    {
        if (VoxelCollider.IsFree(grid, x, MinY, z))
        {
            return false;
        }

        // The block boundaries above the feet, nearest first: the first one the body fits at is the wall's top.
        for (var top = (VoxelCollider.FloorDiv(MinY, VoxelPhysics.UnitsPerBlock) + 1) * VoxelPhysics.UnitsPerBlock;
             SwimMotion.InHopReach(top - MinY);
             top += VoxelPhysics.UnitsPerBlock)
        {
            if (VoxelCollider.IsFree(grid, x, top, z))
            {
                return true;
            }
        }

        return false;
    }
}
