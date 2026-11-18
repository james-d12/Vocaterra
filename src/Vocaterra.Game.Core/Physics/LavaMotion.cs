using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Physics;

/// <summary>
/// Moving through lava (B10b): a thick liquid. The body enters it, sinks slowly, moves slowly and swims up slowly, and climbs out at an edge like from
/// water (<see cref="SwimMotion"/>'s rules with these numbers). It does no harm. A lava cell is a full block; there are no levels.
/// </summary>
internal static class LavaMotion
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>The walking speed while wading in lava (lava at the feet only), in thousandths of the walk (0.35; water 0.8).</summary>
    public const int WadePermille = 350;

    /// <summary>The horizontal speed while swimming in lava, in thousandths of the walk (0.25, about 1.1 blocks per second; water 0.6).</summary>
    public const int SwimPermille = 250;

    /// <summary>The vertical drag in thousandths (0.15; water 0.07): thick, so the speeds settle low.</summary>
    public const int DragPermille = 150;

    /// <summary>The top sink speed in blocks per second (water 1.5).</summary>
    public const double SinkBlocksPerSecond = 0.5;

    /// <summary>The swim-up speed with jump held, in blocks per second (water 2.5); the acceleration stops adding above it.</summary>
    public const double RiseBlocksPerSecond = 1.2;

    /// <summary>The top sink speed in units per tick.</summary>
    public const int SinkSpeed = (int)(SinkBlocksPerSecond * U / VoxelPhysics.TicksPerSecond);

    /// <summary>The swim-up speed in units per tick.</summary>
    public const int RiseSpeed = (int)(RiseBlocksPerSecond * U / VoxelPhysics.TicksPerSecond);

    /// <summary>The horizontal speed in units per tick: <see cref="SwimPermille"/> of the walk when swimming, <see cref="WadePermille"/> when wading.</summary>
    public static int HorizontalSpeed(bool swimming) => VoxelPhysics.WalkSpeed * (swimming ? SwimPermille : WadePermille) / 1000;

    /// <summary>How deep in lava a body is, read as <see cref="Water.Immersion"/> reads water: feet in a lava cell, the lava in the centre column reaching the waist, the eye inside.</summary>
    public static Immersion Immersion(IVoxelReader grid, PlayerBody body)
    {
        var cx = VoxelCollider.FloorDiv(body.CenterX, U);
        var cz = VoxelCollider.FloorDiv(body.CenterZ, U);
        var cy = VoxelCollider.FloorDiv(body.MinY, U);
        if (grid.GetBlock(cx, cy, cz) != BlockId.Lava)
        {
            return default;
        }

        while (grid.GetBlock(cx, cy, cz) == BlockId.Lava)
        {
            cy++;
        }

        var reach = cy * U;
        return new Immersion(true, reach >= body.MinY + (VoxelPhysics.PlayerHeight / 2), LavaVolume.Contains(grid, body.EyeX, body.EyeY, body.EyeZ));
    }
}
