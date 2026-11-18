using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Physics;

public sealed partial class PlayerBody
{
    /// <summary>
    /// One tick of flying (the fly cheat): no gravity, no water or lava drag, no step-up. The walk moves the body at <paramref name="flySpeed"/>, scaled by the host's
    /// permille as walking is (the sprint key flies faster); <paramref name="vertical"/> rises (+1, the jump key) or sinks (-1, the descend key) at two thirds of that speed,
    /// and with neither the body hovers. Blocks still stop it. Flying never reports a jump; it reports a landing when the body comes down onto the ground.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="intentX">The walk's east component.</param>
    /// <param name="intentZ">The walk's south component.</param>
    /// <param name="vertical">+1 to rise, -1 to sink, 0 to hover.</param>
    /// <param name="flySpeed">The horizontal flying speed in units per tick (<see cref="VoxelPhysics.FlySpeedOf"/>).</param>
    /// <param name="speedPermille">The speed in thousandths of <paramref name="flySpeed"/>.</param>
    internal (bool Jumped, bool Landed) StepFlying(IVoxelReader grid, int intentX, int intentZ, int vertical, int flySpeed = VoxelPhysics.FlySpeed, int speedPermille = 1000)
    {
        var wasGrounded = Grounded;
        SteppedUp = false;
        var speed = Scaled(Math.Max(1, flySpeed), speedPermille);
        VelocityX = Angles.Multiply(intentX, speed);
        VelocityZ = Angles.Multiply(intentZ, speed);
        VelocityY = Math.Sign(vertical) * (speed * 2 / 3);

        var x = MinX;
        var y = MinY;
        var z = MinZ;
        if (VoxelCollider.MoveX(grid, ref x, y, z, VelocityX))
        {
            VelocityX = 0;
        }

        if (VoxelCollider.MoveZ(grid, x, y, ref z, VelocityZ))
        {
            VelocityZ = 0;
        }

        if (VoxelCollider.MoveY(grid, x, ref y, z, VelocityY))
        {
            VelocityY = 0;
        }

        MinX = x;
        MinY = y;
        MinZ = z;
        Grounded = !VoxelCollider.IsFree(grid, x, y - 1, z);
        return (false, Grounded && !wasGrounded);
    }
}
