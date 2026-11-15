using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Flow;

/// <summary>
/// The part of a cell the flow rules read and write: its block and, for a water cell, its level. A cell that is not water always has the level
/// <see cref="WaterLevel.Source"/> here, so two cells that are alike compare equal.
/// </summary>
/// <param name="Block">The block.</param>
/// <param name="Level">The water level (source when the block is not water).</param>
public readonly record struct FlowCell(BlockId Block, WaterLevel Level)
{
    /// <summary>Reads the cell at the position (air outside the grid).</summary>
    public static FlowCell Read(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var block = grid.GetBlock(x, y, z);
        return new FlowCell(block, block == BlockId.Water ? grid.GetWater(x, y, z) : WaterLevel.Source);
    }
}
