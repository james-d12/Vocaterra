using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The snapshot carries the per-cell state byte (K01) so a worker can read it: its own chunk and the layer round it, nothing at all while no cell holds one.</summary>
public sealed class ChunkSnapshotStateTests
{
    private static VoxelGrid Grid()
    {
        var grid = new VoxelGrid(48, 48, 48);
        grid.SetBlock(20, 20, 20, BlockId.Stone);
        return grid;
    }

    [Fact]
    public void Given_AGridWithNoState_When_ASnapshotIsTaken_Then_ItHoldsNoStateAndReadsZero()
    {
        var snapshot = ChunkSnapshot.Capture(Grid(), new VoxelChunkCoord(1, 1, 1))!;

        Assert.False(snapshot.HasStates);
        Assert.Equal(0, snapshot.GetState(20, 20, 20));
    }

    [Fact]
    public void Given_AStatedBlock_When_ASnapshotIsTaken_Then_TheStateReadsBackFromTheSnapshot()
    {
        var grid = Grid();
        grid.SetBlock(20, 21, 22, BlockId.Planks, 6);

        var snapshot = ChunkSnapshot.Capture(grid, new VoxelChunkCoord(1, 1, 1))!;

        Assert.True(snapshot.HasStates);
        Assert.Equal(6, snapshot.GetState(20, 21, 22));
        Assert.Equal(0, snapshot.GetState(20, 20, 20)); // a cell with none
        Assert.Equal(0, snapshot.GetState(0, 0, 0)); // outside the block the snapshot copies
    }

    [Fact]
    public void Given_AStateInANeighbourChunkAtTheBorder_When_TheSnapshotIsTaken_Then_TheLayerRoundTheChunkHoldsIt()
    {
        var grid = Grid();
        grid.SetBlock(15, 20, 20, BlockId.Planks, 2); // chunk (0, 1, 1), one cell west of chunk (1, 1, 1)
        grid.SetBlock(32, 20, 20, BlockId.Planks, 3); // chunk (2, 1, 1), one cell east
        grid.SetBlock(14, 20, 20, BlockId.Planks, 4); // two cells west: outside the layer

        var snapshot = ChunkSnapshot.Capture(grid, new VoxelChunkCoord(1, 1, 1))!;

        Assert.Equal(2, snapshot.GetState(15, 20, 20));
        Assert.Equal(3, snapshot.GetState(32, 20, 20));
        Assert.Equal(0, snapshot.GetState(14, 20, 20));
    }

    [Fact]
    public void Given_ASnapshotThatHeldStates_When_ItIsFilledFromAChunkWithNone_Then_NoStaleStateRemains()
    {
        var withStates = Grid();
        withStates.SetBlock(20, 21, 22, BlockId.Planks, 6);
        var without = Grid();
        var snapshot = new ChunkSnapshot();
        Assert.True(snapshot.Fill(withStates, new VoxelChunkCoord(1, 1, 1)));
        Assert.Equal(6, snapshot.GetState(20, 21, 22));

        Assert.True(snapshot.Fill(without, new VoxelChunkCoord(1, 1, 1)));

        Assert.False(snapshot.HasStates);
        Assert.Equal(0, snapshot.GetState(20, 21, 22));
    }

    [Fact]
    public void Given_ASnapshotThatHeldAStateElsewhere_When_ItIsFilledFromAnotherStatedChunk_Then_OnlyThatChunksStatesRead()
    {
        var grid = Grid();
        grid.SetBlock(20, 21, 22, BlockId.Planks, 6);
        grid.SetBlock(40, 40, 40, BlockId.Planks, 7); // chunk (2, 2, 2)
        var snapshot = new ChunkSnapshot();
        Assert.True(snapshot.Fill(grid, new VoxelChunkCoord(1, 1, 1)));

        Assert.True(snapshot.Fill(grid, new VoxelChunkCoord(2, 2, 2)));

        Assert.Equal(7, snapshot.GetState(40, 40, 40));
        Assert.Equal(0, snapshot.GetState(20, 21, 22)); // lies in the chunk's own neighbour but outside the layer round (31..48)
    }
}
