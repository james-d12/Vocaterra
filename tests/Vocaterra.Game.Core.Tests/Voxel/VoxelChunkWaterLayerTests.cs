using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The lazy water level layer of a chunk, and the promise that a chunk without one digests as it always did.</summary>
public sealed class VoxelChunkWaterLayerTests
{
    /// <summary>The digest of <see cref="HandBuilt"/> as captured from the code before the level layer existed.</summary>
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
    public void Given_AChunkWithOnlySourceWater_When_Digested_Then_TheDigestIsTheOneCapturedBeforeTheLayerExisted()
    {
        Assert.Equal(PinnedDigest, HandBuilt().Chunks[0].ComputeDigestHex());
    }

    private static VoxelGrid GridWithWater()
    {
        var grid = new VoxelGrid(32, 32, 32);
        grid.SetBlock(2, 2, 2, BlockId.Water);
        return grid;
    }

    [Fact]
    public void Given_ANewChunk_When_Inspected_Then_ItHasNoLevelsAndReadsSource()
    {
        var grid = HandBuilt();
        Assert.False(grid.Chunks[0].HasWaterLevels);
        Assert.Equal(WaterLevel.Source, grid.Chunks[0].GetWater(1, 0, 0));
    }

    [Fact]
    public void Given_AWaterCell_When_ALevelIsSet_Then_ItReadsBackAndVersionAndDigestMove()
    {
        var grid = GridWithWater();
        var chunk = grid.Chunks[0];
        var version = chunk.Version;
        var digest = chunk.ComputeDigestHex();

        Assert.True(grid.SetWater(2, 2, 2, WaterLevel.Flowing(3)));

        Assert.Equal(WaterLevel.Flowing(3), grid.GetWater(2, 2, 2));
        Assert.True(chunk.HasWaterLevels);
        Assert.Equal(version + 1, chunk.Version);
        Assert.NotEqual(digest, chunk.ComputeDigestHex());
    }

    [Fact]
    public void Given_ALevel_When_TheSameLevelIsSetAgain_Then_VersionAndEditCountStay()
    {
        var grid = GridWithWater();
        grid.SetWater(2, 2, 2, WaterLevel.Flowing(3));
        var version = grid.Chunks[0].Version;
        var edits = grid.EditCount;

        Assert.False(grid.SetWater(2, 2, 2, WaterLevel.Flowing(3)));

        Assert.Equal(version, grid.Chunks[0].Version);
        Assert.Equal(edits, grid.EditCount);
    }

    [Fact]
    public void Given_ASourceWaterCell_When_SourceIsSet_Then_NothingChangesAndNoLayerIsAllocated()
    {
        var grid = GridWithWater();
        var version = grid.Chunks[0].Version;

        Assert.False(grid.SetWater(2, 2, 2, WaterLevel.Source));

        Assert.Equal(version, grid.Chunks[0].Version);
        Assert.False(grid.Chunks[0].HasWaterLevels);
    }

    [Fact]
    public void Given_ALevelBackToSource_When_Digested_Then_TheDigestIsTheLevelFreeOne()
    {
        var grid = GridWithWater();
        var plain = grid.Chunks[0].ComputeDigestHex();
        grid.SetWater(2, 2, 2, WaterLevel.Flowing(1));
        grid.SetWater(2, 2, 2, WaterLevel.Source);

        Assert.False(grid.Chunks[0].HasWaterLevels);
        Assert.Equal(plain, grid.Chunks[0].ComputeDigestHex());
    }

    [Fact]
    public void Given_ALevel_When_TheBlockBecomesSomethingElse_Then_TheLevelIsCleared()
    {
        var grid = GridWithWater();
        grid.SetWater(2, 2, 2, WaterLevel.Flowing(5, falling: true));
        grid.SetBlock(2, 2, 2, BlockId.Stone);
        grid.SetBlock(2, 2, 2, BlockId.Water);

        Assert.Equal(WaterLevel.Source, grid.GetWater(2, 2, 2));
        Assert.False(grid.Chunks[0].HasWaterLevels);
    }

    [Fact]
    public void Given_ALevel_When_FillColumnWritesWater_Then_TheCellsAreSource()
    {
        var grid = GridWithWater();
        grid.SetWater(2, 2, 2, WaterLevel.Flowing(2));
        grid.FillColumn(2, 2, 0, 5, BlockId.Water);

        Assert.Equal(WaterLevel.Source, grid.GetWater(2, 2, 2));
        Assert.False(grid.Chunks[0].HasWaterLevels);
    }

    [Fact]
    public void Given_ALevel_When_TheGridIsCloned_Then_TheCopyKeepsItAndEditsApart()
    {
        var grid = GridWithWater();
        grid.SetWater(2, 2, 2, WaterLevel.Flowing(4));
        var copy = grid.Clone();

        Assert.Equal(WaterLevel.Flowing(4), copy.GetWater(2, 2, 2));
        Assert.Equal(grid.ComputeDigestHex(), copy.ComputeDigestHex());

        copy.SetWater(2, 2, 2, WaterLevel.Flowing(6));
        Assert.Equal(WaterLevel.Flowing(4), grid.GetWater(2, 2, 2));
        Assert.NotEqual(grid.ComputeDigestHex(), copy.ComputeDigestHex());
    }

    [Fact]
    public void Given_ANonWaterCellOrOutside_When_Read_Then_ItIsSource()
    {
        var grid = GridWithWater();
        Assert.Equal(WaterLevel.Source, grid.GetWater(0, 0, 0));
        Assert.Equal(WaterLevel.Source, grid.GetWater(-1, 0, 99));
    }

    [Fact]
    public void Given_ANonWaterCellOrOutside_When_ALevelIsSet_Then_ItThrows()
    {
        var grid = GridWithWater();
        Assert.Throws<InvalidOperationException>(() => grid.SetWater(0, 0, 0, WaterLevel.Flowing(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWater(32, 0, 0, WaterLevel.Flowing(1)));
    }

    [Fact]
    public void Given_TheBuiltRouteScene_When_Inspected_Then_OnlyTheChunksOfTheFallsAndTheRapidsHoldALevel()
    {
        var chunks = VoxelTestSupport.BuildRoute(1).Grid.Chunks;

        Assert.Contains(chunks, chunk => chunk.HasWaterLevels);
        Assert.True(chunks.Count(chunk => chunk.HasWaterLevels) < chunks.Count / 10, "falling water is local: most chunks hold only sources");
    }
}
