using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>
/// B09: the kind mask holds block ids 0 to 127. The guard fails when the block id table outgrows it; the other tests put kinds above 63 (ids no table
/// names, so they stay test-only) through every user of the mask: the chunk, the grid's search, the accumulator and a union.
/// </summary>
public sealed class KindMaskWidthTests
{
    private static readonly BlockId High = (BlockId)100;
    private static readonly BlockId Edge = (BlockId)127;
    private static readonly BlockId JustAbove = (BlockId)64;

    [Fact]
    public void TheBlockIdTableFitsTheKindMask()
    {
        var highest = Enum.GetValues<BlockId>().Max(static id => (int)id);
        Assert.True(highest < BlockKinds.Capacity, $"Block id {highest} does not fit the {BlockKinds.Capacity}-bit kind mask: widen it (BlockKinds, B09).");
        Assert.Equal(128, BlockKinds.Capacity);
    }

    [Fact]
    public void BitsAboveSixtyThreeAreDistinctAndOrdered()
    {
        for (var n = 0; n < BlockKinds.Capacity; n++)
        {
            Assert.Equal(UInt128.One << n, BlockKinds.Bit((BlockId)n));
            Assert.Equal(UInt128.One << n, BlockKinds.Bit(n));
        }

        var union = BlockKinds.Bit(JustAbove) | BlockKinds.Bit(Edge) | BlockKinds.Bit(BlockId.Stone);
        Assert.NotEqual(UInt128.Zero, union & BlockKinds.Bit(Edge));
        Assert.Equal(UInt128.Zero, union & BlockKinds.Bit(High)); // "any of" is false for a kind that is not in the set
        Assert.NotEqual(UInt128.Zero, union & (BlockKinds.Bit(High) | BlockKinds.Bit(JustAbove))); // "any of" is true when one is
        var members = Enumerable.Range(0, BlockKinds.Capacity).Where(n => (union & BlockKinds.Bit(n)) != UInt128.Zero).ToArray();
        Assert.Equal([(int)BlockId.Stone, 64, 127], members); // iterated in id order
    }

    [Fact]
    public void TheAccumulatorBuildsTheSameMaskAsTheUnionOfBits()
    {
        var accumulator = default(BlockKinds.Accumulator);
        foreach (var id in new[] { BlockId.Air, BlockId.Stone, (BlockId)63, JustAbove, High, Edge, High })
        {
            accumulator.Add(id);
        }

        var expected = BlockKinds.Bit(BlockId.Air) | BlockKinds.Bit(BlockId.Stone) | BlockKinds.Bit(63) | BlockKinds.Bit(64) | BlockKinds.Bit(100) | BlockKinds.Bit(127);
        Assert.Equal(expected, accumulator.Mask);
    }

    [Fact]
    public void AChunkHoldingKindsAboveSixtyThreeReportsThemAndDropsThemWhenTheyAreGone()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var chunk = grid.Chunks[0];
        grid.SetBlock(1, 1, 1, High);
        grid.SetBlock(2, 2, 2, Edge);
        grid.SetBlock(3, 3, 3, BlockId.Stone);
        var mask = chunk.KindMask;
        Assert.Equal(BlockKinds.Bit(BlockId.Air) | BlockKinds.Bit(BlockId.Stone) | BlockKinds.Bit(High) | BlockKinds.Bit(Edge), mask);
        grid.SetBlock(1, 1, 1, BlockId.Air);
        Assert.Equal(UInt128.Zero, chunk.KindMask & BlockKinds.Bit(High));
        Assert.NotEqual(UInt128.Zero, chunk.KindMask & BlockKinds.Bit(Edge));
    }

    [Fact]
    public void BlocksOfKindFindsAKindAboveSixtyThreeAndSkipsChunksWithoutIt()
    {
        var grid = new VoxelGrid(48, 32, 48);
        grid.SetBlock(40, 20, 33, High);
        grid.SetBlock(2, 3, 4, Edge);
        grid.SetBlock(5, 5, 5, BlockId.Stone);
        Assert.Equal([new BlockPos(40, 20, 33)], grid.BlocksOfKind(High));
        Assert.Equal([new BlockPos(2, 3, 4)], grid.BlocksOfKind(Edge));
        Assert.Empty(grid.BlocksOfKind(JustAbove));
    }
}
