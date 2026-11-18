using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water;

/// <summary>
/// Where the water is, as a volume: a point is in the water when it lies in a water cell and below that cell's drawn surface (<see cref="WaterSurface"/>,
/// with the water-above rule, so a column of water has no gaps). One rule for <see cref="Immersion"/> (the eye) and for the view under water, so the
/// physics and the picture cannot disagree about whether a point is wet.
/// </summary>
public static class WaterVolume
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>
    /// Whether a point is inside the water. The point is in <see cref="VoxelPhysics"/> fixed-point units (<see cref="VoxelPhysics.UnitsPerBlock"/> to a block).
    /// A point outside the grid is dry.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="x">East, in units.</param>
    /// <param name="y">Up, in units.</param>
    /// <param name="z">South, in units.</param>
    public static bool Contains(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var top = SurfaceTop(grid, VoxelCollider.FloorDiv(x, U), VoxelCollider.FloorDiv(y, U), VoxelCollider.FloorDiv(z, U));
        return top is { } surface && y < surface;
    }

    /// <summary>The world height (in units) of the water's top in the cell, or null when the cell is not water (or lies outside the grid).</summary>
    internal static int? SurfaceTop(IVoxelReader grid, int cellX, int cellY, int cellZ)
    {
        if (grid.GetBlock(cellX, cellY, cellZ) != BlockId.Water)
        {
            return null;
        }

        var aboveIsWater = grid.GetBlock(cellX, cellY + 1, cellZ) == BlockId.Water;
        return (cellY * U) + WaterSurface.Units(grid.GetWater(cellX, cellY, cellZ), aboveIsWater);
    }
}
