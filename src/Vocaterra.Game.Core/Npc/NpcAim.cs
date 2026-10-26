using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// Whether the crosshair is on an NPC (S3): the ray from the eye along the look direction passes within a radius of the figure's body
/// centre, within reach. Plain maths over the simulation's own look vector (<see cref="Angles.Direction"/>), so it is tested without a GPU; the
/// front-end also checks that no solid block is in the way (<see cref="LineOfSight.Blocked"/>).
/// </summary>
public static class NpcAim
{
    /// <summary>How far the talk reaches, in blocks from the eye to the figure's centre.</summary>
    public const double ReachBlocks = 8.0;

    /// <summary>How far from the ray the figure's aim point may be, in blocks: the figure is about 0.6 blocks wide, and the margin lets the crosshair rest on the head.</summary>
    public const double RadiusBlocks = 0.8;

    /// <summary>The point an NPC is aimed at: the chest, 1.2 blocks above its feet (a figure is about 1.8 blocks tall).</summary>
    /// <param name="feet">The cell the NPC's feet are in.</param>
    public static (double X, double Y, double Z) BodyOf(BlockPos feet) => (feet.X + 0.5, feet.Y + 1.2, feet.Z + 0.5);

    /// <summary>
    /// Whether the ray from <paramref name="eye"/> along the look (<paramref name="yaw"/>, <paramref name="pitch"/> in angle steps) passes
    /// within <paramref name="radius"/> of <paramref name="point"/>, at a distance from the eye between zero and <paramref name="reach"/>.
    /// </summary>
    public static bool Hits(
        (double X, double Y, double Z) eye,
        int yaw,
        int pitch,
        (double X, double Y, double Z) point,
        double reach = ReachBlocks,
        double radius = RadiusBlocks)
    {
        var (lx, ly, lz) = Angles.Direction(yaw, pitch);
        const double One = Angles.One;
        var ux = lx / One;
        var uy = ly / One;
        var uz = lz / One;
        var vx = point.X - eye.X;
        var vy = point.Y - eye.Y;
        var vz = point.Z - eye.Z;
        var along = (vx * ux) + (vy * uy) + (vz * uz);
        if (along < 0 || along > reach)
        {
            return false;
        }

        var across = (vx * vx) + (vy * vy) + (vz * vz) - (along * along);
        return across <= radius * radius;
    }

    /// <summary>The distance in blocks from the eye to a point.</summary>
    public static double Distance((double X, double Y, double Z) eye, (double X, double Y, double Z) point)
    {
        var dx = point.X - eye.X;
        var dy = point.Y - eye.Y;
        var dz = point.Z - eye.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
