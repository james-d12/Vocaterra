using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water;

/// <summary>Where the lava is, as a volume (B10b): a point is in lava when it lies in a lava cell (a lava cell is a full block). The view tints orange by it.</summary>
public static class LavaVolume
{
    /// <summary>Whether the point, in <see cref="VoxelPhysics"/> units, is inside lava. A point outside the grid is not.</summary>
    public static bool Contains(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        const int U = VoxelPhysics.UnitsPerBlock;
        return grid.GetBlock(VoxelCollider.FloorDiv(x, U), VoxelCollider.FloorDiv(y, U), VoxelCollider.FloorDiv(z, U)) == BlockId.Lava;
    }
}
