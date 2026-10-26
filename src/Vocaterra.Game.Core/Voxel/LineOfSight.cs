using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>The line of sight through the grid (S3): a wall between the eye and an NPC hides the NPC's name and keeps the crosshair off it.</summary>
public static class LineOfSight
{
    /// <summary>Whether a solid block lies between the eye and the point (both in blocks), measured with the simulation's own ray.</summary>
    /// <param name="grid">The blocks.</param>
    /// <param name="eye">The eye, in blocks.</param>
    /// <param name="target">The point looked at, in blocks.</param>
    /// <param name="distance">The distance from the eye to the point, in blocks.</param>
    public static bool Blocked(VoxelGrid grid, (float X, float Y, float Z) eye, (float X, float Y, float Z) target, double distance)
    {
        if (distance < 0.5)
        {
            return false;
        }

        const double U = VoxelPhysics.UnitsPerBlock;
        var dx = (int)Math.Round((target.X - eye.X) / distance * Angles.One);
        var dy = (int)Math.Round((target.Y - eye.Y) / distance * Angles.One);
        var dz = (int)Math.Round((target.Z - eye.Z) / distance * Angles.One);
        var hit = BlockRay.Cast(grid, (int)(eye.X * U), (int)(eye.Y * U), (int)(eye.Z * U), dx, dy, dz, (long)((distance - 0.3) * U));
        return hit is { } h && BlockDefinition.Get(h.Id).Solid;
    }
}
