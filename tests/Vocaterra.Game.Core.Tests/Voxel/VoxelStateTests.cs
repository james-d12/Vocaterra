using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The per-cell state byte (K01): a lazy layer in the chunk like water's, read and written through the grid, kept out of the digest while it holds nothing.</summary>
public sealed class VoxelStateTests
{
    /// <summary>The digest of the hand-built chunk, the one <see cref="VoxelChunkWaterLayerTests"/> pins: captured before any layer existed.</summary>
    private const string PinnedDigest = "859772d58315ea59f48cbf04121ddb4bc5c582e4f1300b661a5781191314f417";

    private static VoxelGrid HandBuilt()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(0, 0, 0, BlockId.Stone);
        grid.SetBlock(1, 0, 0, BlockId.Water);
        grid.SetBlock(5, 7, 9, BlockId.Water);
        grid.SetBlock(15, 15, 15, BlockId.Stone);
        grid.FillColumn(3, 4, 0, 6, BlockId.Water);
        return grid;
    }

    [Fact]
    public void Given_AChunkWithNoState_When_Digested_Then_TheDigestIsTheOneCapturedBeforeStatesExisted()
    {
        var chunk = HandBuilt().Chunks[0];

        Assert.False(chunk.HasStates);
        Assert.Equal(PinnedDigest, chunk.ComputeDigestHex());
    }

    [Fact]
    public void Given_ANewChunk_When_Inspected_Then_ItHoldsNoStateArrayAndEveryStateReadsZero()
    {
        var grid = HandBuilt();
        var chunk = grid.Chunks[0];

        Assert.False(chunk.HasStates);
        Assert.True(chunk.States.IsEmpty);
        Assert.Null(chunk.StateArray);
        Assert.Equal(0, chunk.GetState(0, 0, 0));
        Assert.Equal(0, grid.GetState(0, 0, 0));
    }

    [Fact]
    public void Given_ABlock_When_ItIsWrittenWithAState_Then_TheStateReadsBackAndTheVersionAndDigestMove()
    {
        var grid = HandBuilt();
        var chunk = grid.Chunks[0];
        var (version, digest, edits) = (chunk.Version, chunk.ComputeDigestHex(), grid.EditCount);

        Assert.True(grid.SetBlock(2, 3, 4, BlockId.Planks, 5));

        Assert.Equal(5, grid.GetState(2, 3, 4));
        Assert.Equal(5, chunk.GetState(2, 3, 4));
        Assert.True(chunk.HasStates);
        Assert.Equal(version + 1, chunk.Version);
        Assert.Equal(edits + 1, grid.EditCount);
        Assert.NotEqual(digest, chunk.ComputeDigestHex());
        Assert.NotNull(chunk.StateArray);
        Assert.Equal(5, chunk.States[(((3 * 16) + 4) * 16) + 2]);
    }

    [Fact]
    public void Given_ABlockWithAState_When_OnlyTheStateChanges_Then_ItIsAnEditTheListenerHearsAbout()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);
        var recorder = new StreamedGridSupport.ChangeRecorder();
        grid.AddListener(recorder);
        var version = grid.Chunks[0].Version;

        Assert.True(grid.SetBlock(2, 3, 4, BlockId.Planks, 6));

        var change = Assert.Single(recorder.Changes);
        Assert.Equal((BlockId.Planks, BlockId.Planks, (byte)5, (byte)6), (change.OldBlock, change.NewBlock, change.OldState, change.NewState));
        Assert.Equal(version + 1, grid.Chunks[0].Version);
        Assert.False(grid.SetBlock(2, 3, 4, BlockId.Planks, 6)); // nothing changes: no edit, no event
        Assert.Single(recorder.Changes);
    }

    [Fact]
    public void Given_ABlockWithAState_When_ItIsReplacedWithoutAState_Then_TheNewBlockHoldsZero()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);

        Assert.True(grid.SetBlock(2, 3, 4, BlockId.Stone));

        Assert.Equal(0, grid.GetState(2, 3, 4));
        Assert.False(grid.Chunks[0].HasStates);
    }

    [Fact]
    public void Given_ABlockWithAState_When_TheSameBlockIsWrittenWithoutAState_Then_TheStateIsCleared()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);

        Assert.True(grid.SetBlock(2, 3, 4, BlockId.Planks));

        Assert.Equal(0, grid.GetState(2, 3, 4));
    }

    [Fact]
    public void Given_ABlockWithAState_When_ItIsBrokenToAir_Then_NoStateIsLeftBehind()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);

        grid.SetBlock(2, 3, 4, BlockId.Air);
        grid.SetBlock(2, 3, 4, BlockId.Stone);

        Assert.Equal(0, grid.GetState(2, 3, 4));
    }

    [Fact]
    public void Given_Air_When_ItIsWrittenWithAState_Then_ItHoldsStateZeroAndNothingChanges()
    {
        var grid = HandBuilt();

        Assert.False(grid.SetBlock(2, 3, 4, BlockId.Air, 7));

        Assert.Equal(0, grid.GetState(2, 3, 4));
        Assert.False(grid.Chunks[0].HasStates);
    }

    [Fact]
    public void Given_ABlockWithAState_When_FilledOverByAColumnFill_Then_TheStateGoesWithTheBlock()
    {
        var grid = HandBuilt();
        grid.SetBlock(8, 2, 8, BlockId.Planks, 3);
        grid.SetBlock(8, 3, 8, BlockId.Planks, 3);

        grid.FillColumn(8, 8, 2, 2, BlockId.Stone); // a different block
        grid.FillColumn(8, 8, 3, 3, BlockId.Planks); // the same block: written with state 0, like a placement without one

        Assert.Equal(0, grid.GetState(8, 2, 8));
        Assert.Equal(0, grid.GetState(8, 3, 8));
        Assert.False(grid.Chunks[0].HasStates);
    }

    [Fact]
    public void Given_ThatEveryStateIsCleared_When_TheChunkIsDigested_Then_TheLayerIsGoneAndTheDigestIsTheOriginalOne()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);
        grid.SetBlock(9, 3, 4, BlockId.Planks, 6);
        grid.SetBlock(2, 3, 4, BlockId.Air);
        grid.SetBlock(9, 3, 4, BlockId.Air);

        var chunk = grid.Chunks[0];

        Assert.False(chunk.HasStates); // the array is dropped again when the last state is cleared (back to no layer, as if never set)
        Assert.Null(chunk.StateArray);
        Assert.Equal(PinnedDigest, chunk.ComputeDigestHex());
    }

    [Fact]
    public void Given_TwoChunksOfOneBlockEach_When_OneHoldsAStateAndTheOtherALevelOfTheSameNumber_Then_TheirDigestsDiffer()
    {
        var withState = new VoxelGrid(16, 16, 16);
        withState.SetBlock(1, 1, 1, BlockId.Water);
        withState.SetBlock(1, 1, 1, BlockId.Water, 3);
        var withLevel = new VoxelGrid(16, 16, 16);
        withLevel.SetBlock(1, 1, 1, BlockId.Water);
        withLevel.SetWater(1, 1, 1, WaterLevel.FromEncoded(3));

        Assert.NotEqual(withLevel.Chunks[0].ComputeDigestHex(), withState.Chunks[0].ComputeDigestHex());
    }

    [Fact]
    public void Given_ACloneOfAGridWithStates_When_TheCloneIsEdited_Then_TheOriginalKeepsItsStates()
    {
        var grid = HandBuilt();
        grid.SetBlock(2, 3, 4, BlockId.Planks, 5);
        var copy = grid.Clone();

        Assert.Equal(5, copy.GetState(2, 3, 4));
        Assert.Equal(grid.ComputeDigestHex(), copy.ComputeDigestHex());
        copy.SetBlock(2, 3, 4, BlockId.Planks, 9);

        Assert.Equal(5, grid.GetState(2, 3, 4));
        Assert.NotEqual(grid.ComputeDigestHex(), copy.ComputeDigestHex());
    }

    [Fact]
    public void Given_PositionsOutsideTheGrid_When_TheStateIsRead_Then_ItIsZeroAndAWriteThrows()
    {
        var grid = HandBuilt();

        Assert.Equal(0, grid.GetState(-1, 0, 0));
        Assert.Equal(0, grid.GetState(0, 16, 0));
        Assert.Equal(0, grid.GetState(0, 0, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetBlock(-1, 0, 0, BlockId.Planks, 1));
    }

    [Fact]
    public void Given_ABedrockCell_When_ItIsWrittenWithAState_Then_NothingChanges()
    {
        var grid = HandBuilt();
        grid.SetBlock(4, 4, 4, BlockId.Bedrock);

        Assert.False(grid.SetBlock(4, 4, 4, BlockId.Bedrock, 2));

        Assert.Equal(0, grid.GetState(4, 4, 4));
    }

    [Fact]
    public void Given_StatesAcrossChunkBordersAndNegativeCoordinates_When_TheyAreRead_Then_EachCellKeepsItsOwn()
    {
        var grid = StreamedGridSupport.Default();
        var cells = new (int X, int Y, int Z, byte State)[]
        {
            (0, 0, 0, 1),
            (15, 0, 0, 2),
            (16, 0, 0, 3), // the next chunk across the east border
            (-1, 0, 0, 4), // the chunk to the west, at negative x
            (-16, -1, -1, 5), // negative on every axis, a chunk corner
            (-17, -64, -30000, 6), // beyond a border, at the bottom of the world
            (29999, 319, 29999, 7), // the far corner
        };

        foreach (var (x, y, z, state) in cells)
        {
            Assert.True(grid.SetBlock(x, y, z, BlockId.Planks, state));
        }

        foreach (var (x, y, z, state) in cells)
        {
            Assert.Equal(state, grid.GetState(x, y, z));
            Assert.Equal(BlockId.Planks, grid.GetBlock(x, y, z));
        }

        Assert.Equal(0, grid.GetState(1, 0, 0)); // a neighbour of a stated cell holds nothing
        Assert.Equal(0, grid.GetState(-2, 0, 0));
        Assert.Equal(7, cells.Length);
    }

    [Fact]
    public void Given_AStreamedGrid_When_TheStateOfAChunkThatIsNotLoadedIsRead_Then_ItIsZero()
    {
        var grid = StreamedGridSupport.Default(); // no generator: a chunk nobody wrote holds nothing

        Assert.Equal(0, grid.GetState(100, 5, 100));
        Assert.Equal(0, grid.GetState(-100, -5, -100));
    }

    [Fact]
    public void Given_ContentWithStates_When_ItIsInstalled_Then_TheChunkHoldsThemAndRefusesAStateOnAir()
    {
        var blocks = new BlockId[VoxelChunk.Volume];
        blocks[10] = BlockId.Planks;
        var states = new byte[VoxelChunk.Volume];
        states[10] = 4;

        var generator = new StreamedGridSupport.CountingGenerator(_ => new ChunkContent(blocks, null, states));
        var grid = new VoxelGrid(WorldLimits.Default, generator);

        Assert.Equal(4, grid.GetState(10, 0, 0));
        Assert.True(grid.TryGetChunk(new VoxelChunkCoord(0, 0, 0), out var chunk));
        Assert.True(chunk!.HasStates);

        var onAir = new byte[VoxelChunk.Volume];
        onAir[11] = 1;
        Assert.Throws<ArgumentException>(() => new ChunkContent(blocks, null, onAir));
        Assert.Throws<ArgumentException>(() => new ChunkContent(blocks, null, new byte[3]));
    }
}
