namespace Vocaterra.Game.Core.Voxel;

/// <summary>One change to a cell of the grid: a different block, or a different water level in a water cell.</summary>
/// <param name="Position">The cell.</param>
/// <param name="OldBlock">The block before.</param>
/// <param name="NewBlock">The block after (equal to <paramref name="OldBlock"/> when only the level changed).</param>
/// <param name="OldLevel">The water level before (source for a cell that was not water).</param>
/// <param name="NewLevel">The water level after (source for a cell that is not water).</param>
public readonly record struct BlockChange(BlockPos Position, BlockId OldBlock, BlockId NewBlock, WaterLevel OldLevel, WaterLevel NewLevel);
