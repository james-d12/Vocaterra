using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>
/// What a chunk knows about itself so that a large world can skip it (S13): how many blocks it holds, which kinds, and whether it is empty. In the
/// timing collection: the cached digest's test has a CPU-time budget (B29d; it was a 100 ms wall-clock bound).
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
public sealed class ChunkIndexTests(ITestOutputHelper output)
{
    private const int Attempts = 3;

    // B29d: the Release CPU cost of one edit and its digest (the mean of 200 edits, fastest of three) and the budget, twice the cost, in local milliseconds.
    // Measured 0.283 ms (timing run 1) and 0.264 ms (run 2); budget 0.6 ms, twice the higher, rounded up. Was 100 ms wall.
    private static readonly double DigestBudget = CpuTime.Budget(0.6);

    [Fact]
    public void AChunkCountsItsBlocksThroughEveryEditAndKnowsWhenItIsEmpty()
    {
        var grid = new VoxelGrid(32, 32, 32);
        var chunk = grid.Chunks[0];
        Assert.True(chunk.IsEmpty);
        Assert.Equal(0, chunk.NonAirCount);
        grid.SetBlock(3, 4, 5, BlockId.Stone);
        grid.SetBlock(4, 4, 5, BlockId.Stone);
        grid.SetBlock(3, 4, 5, BlockId.Dirt); // a change of kind is not a new block
        Assert.Equal(2, chunk.NonAirCount);
        Assert.False(chunk.IsEmpty);
        grid.SetBlock(3, 4, 5, BlockId.Air);
        grid.SetBlock(4, 4, 5, BlockId.Air);
        Assert.Equal(0, chunk.NonAirCount);
        Assert.True(chunk.IsEmpty);
        Assert.True(grid.Chunks[7].IsEmpty); // another chunk, untouched
    }

    [Fact]
    public void TheKindMaskFollowsTheEditsAndIsCachedBetweenThem()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var chunk = grid.Chunks[0];
        Assert.Equal(1UL, chunk.KindMask); // air only
        grid.SetBlock(1, 1, 1, BlockId.Trunk);
        grid.SetBlock(2, 2, 2, BlockId.Water);
        var mask = chunk.KindMask;
        Assert.Equal(1UL | (1UL << (int)BlockId.Trunk) | (1UL << (int)BlockId.Water), mask);
        Assert.Equal(mask, chunk.KindMask);
        grid.SetBlock(1, 1, 1, BlockId.Air);
        Assert.Equal(1UL | (1UL << (int)BlockId.Water), chunk.KindMask); // a kind that is gone leaves the mask
    }

    [Fact]
    public void BlocksOfKindFindsEveryBlockAndOnlyReadsChunksThatHoldTheKind()
    {
        var grid = new VoxelGrid(48, 32, 48);
        var expected = new List<BlockPos> { new(1, 1, 1), new(47, 31, 47), new(20, 17, 33), new(20, 17, 34) };
        foreach (var p in expected)
        {
            grid.SetBlock(p.X, p.Y, p.Z, BlockId.BerryBush);
        }

        grid.SetBlock(5, 5, 5, BlockId.Bush);
        var found = grid.BlocksOfKind(BlockId.BerryBush);
        Assert.Equal(expected.OrderBy(p => (p.Y, p.Z, p.X)).Count(), found.Count);
        Assert.All(expected, p => Assert.Contains(p, found));
        Assert.Empty(grid.BlocksOfKind(BlockId.Torch));
        Assert.Single(grid.BlocksOfKind(BlockId.Bush));
    }

    [Fact]
    public void FillingAColumnWritesTheBlocksChunkByChunkAndCountsTheEdits()
    {
        var grid = new VoxelGrid(32, 48, 32);
        grid.FillColumn(17, 3, 5, 40, BlockId.Stone);
        for (var y = 0; y < 48; y++)
        {
            Assert.Equal(y is >= 5 and <= 40 ? BlockId.Stone : BlockId.Air, grid.GetBlock(17, y, 3));
        }

        Assert.Equal(36, grid.EditCount);
        Assert.Equal(11, grid.Chunks[(0 * 2 * 2) + (0 * 2) + 1].NonAirCount); // y 5 to 15 of the lowest chunk of the column
        Assert.Equal(16, grid.Chunks[(1 * 2 * 2) + (0 * 2) + 1].NonAirCount);
        Assert.Equal(9, grid.Chunks[(2 * 2 * 2) + (0 * 2) + 1].NonAirCount); // y 32 to 40
        grid.FillColumn(17, 3, 5, 40, BlockId.Stone); // nothing changes the second time
        Assert.Equal(36, grid.EditCount);
        grid.FillColumn(17, 3, -5, 100, BlockId.Dirt); // clipped to the grid
        Assert.Equal(BlockId.Dirt, grid.GetBlock(17, 0, 3));
        Assert.Equal(BlockId.Dirt, grid.GetBlock(17, 47, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.FillColumn(32, 0, 0, 1, BlockId.Stone));
    }

    [Fact]
    public void TheHighestBlockSkipsEmptyChunksAndReadsOnlyTheOnesWithBlocks()
    {
        var grid = new VoxelGrid(32, 128, 32);
        Assert.Equal(-1, grid.HighestBlock(3, 3));
        grid.SetBlock(3, 77, 3, BlockId.Trunk);
        grid.SetBlock(3, 12, 3, BlockId.Stone);
        Assert.Equal(77, grid.HighestBlock(3, 3));
        grid.SetBlock(3, 77, 3, BlockId.Air);
        Assert.Equal(12, grid.HighestBlock(3, 3));
        Assert.Equal(-1, grid.HighestBlock(-1, 3));
        Assert.Equal(-1, grid.HighestBlock(3, 99));
    }

    [Fact]
    public void ADigestOfALargeWorldIsCachedUntilTheNextEdit()
    {
        var grid = new VoxelGrid(384, 128, 384);
        var first = grid.ComputeDigestHex();
        Assert.Same(grid.ComputeDigest(), grid.ComputeDigest()); // the very same array: nothing was rehashed
        grid.SetBlock(5, 5, 5, BlockId.Stone);
        Assert.NotEqual(first, grid.ComputeDigestHex());
        grid.SetBlock(5, 5, 5, BlockId.Air);
        Assert.Equal(first, grid.ComputeDigestHex());

        // After one edit the digest rehashes one chunk and the 32-byte digests of the rest: far from the cost of hashing the world again.
        // B29d: CPU time, not a wall clock. One edit and digest is below the process's CPU clock (ticks of about 10 ms), so the test times
        // 200 edits, each one a change of kind (so each digest rehashes its chunk), and takes the mean; the fastest of three attempts.
        const int Edits = 200;
        var toggle = 0;
        var perEdit = CpuTime.Fastest(Attempts, () =>
        {
            for (var edit = 0; edit < Edits; edit++)
            {
                grid.SetBlock(200, 60, 200, toggle++ % 2 == 0 ? BlockId.Stone : BlockId.Air);
                _ = grid.ComputeDigest();
            }
        }) / Edits;
        output.WriteLine($"an edit and its digest: {perEdit:F3} ms of CPU time, the mean of {Edits} edits, fastest of {Attempts} (budget {DigestBudget:F2} ms, was 100 ms wall)");
        Assert.True(perEdit < DigestBudget, $"a digest after one edit takes {perEdit:F3} ms of CPU time (budget {DigestBudget:F2})");
    }
}
