using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// A lazy scan of a grid by block kind. <see cref="VoxelGrid.BlocksOfKind"/> returns a list of every position, which on the large valley is millions of
/// entries for stone or dirt (and the list doubles as it grows): tests that only walk the blocks use this instead, so a run's memory does not depend on how
/// many blocks a kind has (H08a, 2026-11-07, test hosts hit the 6 GB cap).
/// </summary>
internal static class GridScan
{
    /// <summary>The positions of every block of <paramref name="kind"/>, in the same order as <see cref="VoxelGrid.BlocksOfKind"/>, one at a time.</summary>
    public static IEnumerable<BlockPos> EachOfKind(this VoxelGrid grid, BlockId kind)
    {
        var bit = 1UL << (int)kind;
        foreach (var chunk in grid.Chunks)
        {
            if ((chunk.KindMask & bit) == 0)
            {
                continue;
            }

            var origin = chunk.Coord.Origin;
            var blocks = chunk.Blocks.ToArray(); // one chunk (8 KB) at a time; a span cannot live across a yield
            for (var i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == kind)
                {
                    yield return new BlockPos(
                        origin.X + (i & (VoxelChunk.Size - 1)),
                        origin.Y + (i >> (2 * VoxelChunk.Shift)),
                        origin.Z + ((i >> VoxelChunk.Shift) & (VoxelChunk.Size - 1)));
                }
            }
        }
    }
}
