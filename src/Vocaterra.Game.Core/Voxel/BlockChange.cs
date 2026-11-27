namespace Vocaterra.Game.Core.Voxel;

/// <summary>One change to a cell of the grid: a different block, a different water level in a water cell, or a different state byte.</summary>
/// <param name="Position">The cell.</param>
/// <param name="OldBlock">The block before.</param>
/// <param name="NewBlock">The block after (equal to <paramref name="OldBlock"/> when only the level changed).</param>
/// <param name="OldLevel">The water level before (source for a cell that was not water).</param>
/// <param name="NewLevel">The water level after (source for a cell that is not water).</param>
/// <param name="OldState">The state byte before (K01; 0 for a cell with none).</param>
/// <param name="NewState">The state byte after (equal to <paramref name="OldState"/> when only the block or the level changed and the cell kept it).</param>
public readonly record struct BlockChange(BlockPos Position, BlockId OldBlock, BlockId NewBlock, WaterLevel OldLevel, WaterLevel NewLevel, byte OldState = 0, byte NewState = 0);
