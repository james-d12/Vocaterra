using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The grid's change observer.</summary>
public sealed class VoxelGridListenerTests
{
    private sealed class Recorder : IBlockChangeListener
    {
        public List<BlockChange> Changes { get; } = [];

        public void OnBlockChanged(BlockChange change) => Changes.Add(change);
    }

    [Fact]
    public void Given_AListener_When_ABlockChanges_Then_ItIsToldWithOldAndNew()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.SetBlock(1, 2, 3, BlockId.Water);

        var change = Assert.Single(recorder.Changes);
        Assert.Equal(new BlockChange(new BlockPos(1, 2, 3), BlockId.Air, BlockId.Water, WaterLevel.Source, WaterLevel.Source), change);
    }

    [Fact]
    public void Given_AListener_When_TheSameBlockIsWritten_Then_ItIsNotTold()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(1, 2, 3, BlockId.Stone);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.SetBlock(1, 2, 3, BlockId.Stone);

        Assert.Empty(recorder.Changes);
    }

    [Fact]
    public void Given_AListener_When_ALevelChanges_Then_ItIsToldWithBothLevels()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(1, 2, 3, BlockId.Water);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.SetWater(1, 2, 3, WaterLevel.Flowing(2));

        var change = Assert.Single(recorder.Changes);
        Assert.Equal(BlockId.Water, change.OldBlock);
        Assert.Equal(BlockId.Water, change.NewBlock);
        Assert.Equal(WaterLevel.Source, change.OldLevel);
        Assert.Equal(WaterLevel.Flowing(2), change.NewLevel);
    }

    [Fact]
    public void Given_AListener_When_TheSameLevelIsWritten_Then_ItIsNotTold()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(1, 2, 3, BlockId.Water);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.SetWater(1, 2, 3, WaterLevel.Source);

        Assert.Empty(recorder.Changes);
    }

    [Fact]
    public void Given_AFlowingCell_When_ItIsReplacedBySomethingElse_Then_TheEventCarriesTheOldLevel()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(1, 2, 3, BlockId.Water);
        grid.SetWater(1, 2, 3, WaterLevel.Flowing(4));
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.SetBlock(1, 2, 3, BlockId.Stone);

        var change = Assert.Single(recorder.Changes);
        Assert.Equal(WaterLevel.Flowing(4), change.OldLevel);
        Assert.Equal(WaterLevel.Source, change.NewLevel);
    }

    [Fact]
    public void Given_AListener_When_AColumnIsFilled_Then_ItIsNotTold()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        grid.FillColumn(4, 4, 0, 9, BlockId.Water);

        Assert.Empty(recorder.Changes);
        Assert.Equal(10, grid.EditCount);
    }

    [Fact]
    public void Given_ARemovedListener_When_ABlockChanges_Then_ItIsNotTold()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var recorder = new Recorder();
        grid.AddListener(recorder);
        grid.AddListener(recorder);

        grid.SetBlock(0, 0, 0, BlockId.Stone);
        Assert.True(grid.RemoveListener(recorder));
        grid.SetBlock(1, 0, 0, BlockId.Stone);

        Assert.Single(recorder.Changes);
        Assert.False(grid.RemoveListener(recorder));
    }

    [Fact]
    public void Given_AClonedGrid_When_ItChanges_Then_TheOriginalsListenersAreNotTold()
    {
        var grid = new VoxelGrid(16, 16, 16);
        var recorder = new Recorder();
        grid.AddListener(recorder);

        var copy = grid.Clone();
        copy.SetBlock(0, 0, 0, BlockId.Stone);

        Assert.Empty(recorder.Changes);
    }
}
