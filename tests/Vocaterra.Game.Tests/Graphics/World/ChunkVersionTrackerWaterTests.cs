using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>A water level that changes makes the chunk, and a chunk that looks across the edge at it, due for meshing again (water-plan 4.8).</summary>
public sealed class ChunkVersionTrackerWaterTests
{
    private static (VoxelGrid Grid, ChunkVersionTracker Tracker) Meshed()
    {
        var grid = new VoxelGrid(48, 32, 48);
        grid.SetBlock(15, 5, 20, BlockId.Water);
        grid.SetBlock(16, 5, 20, BlockId.Water);
        grid.SetBlock(8, 5, 20, BlockId.Water);
        var tracker = new ChunkVersionTracker(grid);
        foreach (var chunk in grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord);
        }

        Assert.Empty(tracker.Due());
        return (grid, tracker);
    }

    [Fact]
    public void Given_AMeshedChunk_When_AWaterLevelInItChanges_Then_OnlyThatChunkIsDue()
    {
        var (grid, tracker) = Meshed();

        Assert.True(grid.SetWater(8, 5, 20, WaterLevel.Flowing(3)));

        Assert.Equal([new VoxelChunkCoord(0, 0, 1)], tracker.Due());
    }

    [Fact]
    public void Given_AMeshedChunk_When_ALevelIsWrittenAgainUnchanged_Then_NothingIsDue()
    {
        var (grid, tracker) = Meshed();

        Assert.False(grid.SetWater(8, 5, 20, WaterLevel.Source));

        Assert.Empty(tracker.Due());
    }

    [Fact]
    public void Given_AWaterLevelOnTheEdgeOfAChunk_When_ItChanges_Then_TheChunkAcrossTheEdgeIsDueToo()
    {
        var (grid, tracker) = Meshed();

        Assert.True(grid.SetWater(15, 5, 20, WaterLevel.Flowing(1)));

        // The surface corners of the cell at x = 16 average the cell at x = 15, in the chunk to the west.
        Assert.Equal([new VoxelChunkCoord(0, 0, 1), new VoxelChunkCoord(1, 0, 1)], tracker.Due());
    }

    [Fact]
    public void Given_ALevelThatChangedBackAfterRemeshing_When_Asked_Then_TheLaterChangeIsDueAgain()
    {
        var (grid, tracker) = Meshed();
        _ = grid.SetWater(15, 5, 20, WaterLevel.Flowing(1));
        foreach (var coord in tracker.Due())
        {
            tracker.MarkMeshed(coord);
        }

        Assert.Empty(tracker.Due());
        _ = grid.SetWater(15, 5, 20, WaterLevel.Flowing(2));

        Assert.Equal(2, tracker.Due().Count);
    }
}
