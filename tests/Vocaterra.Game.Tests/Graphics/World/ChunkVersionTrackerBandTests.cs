using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>A mesh remembers the light band it was built in; a change of band makes the meshes of the other bands stale (not due) until each is built again (E02).</summary>
public sealed class ChunkVersionTrackerBandTests
{
    private static (VoxelGrid Grid, ChunkVersionTracker Tracker) Meshed(LightBand band)
    {
        var grid = new VoxelGrid(48, 32, 48);
        grid.SetBlock(5, 5, 5, BlockId.Stone);
        var tracker = new ChunkVersionTracker(grid);
        foreach (var chunk in grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord, 0, band);
        }

        return (grid, tracker);
    }

    [Fact]
    public void Given_AMeshBuiltInABand_When_TheBandIsAsked_Then_ItIsTheOneItWasBuiltIn()
    {
        var (_, tracker) = Meshed(LightBand.Dusk);

        Assert.Equal(LightBand.Dusk, tracker.BandOf(new VoxelChunkCoord(0, 0, 0)));
    }

    [Fact]
    public void Given_MeshesBuiltInTheDayBand_When_TheBandInForceIsTheSame_Then_NothingIsStale()
    {
        var (grid, tracker) = Meshed(LightBand.Day);

        Assert.All(grid.Chunks, chunk => Assert.False(tracker.IsBandStale(chunk.Coord, LightBand.Day)));
    }

    [Fact]
    public void Given_MeshesBuiltInTheDayBand_When_TheBandChanges_Then_EveryMeshIsStaleButNoneIsDue()
    {
        var (grid, tracker) = Meshed(LightBand.Day);

        Assert.All(grid.Chunks, chunk => Assert.True(tracker.IsBandStale(chunk.Coord, LightBand.Night)));
        Assert.Empty(tracker.Due()); // the blocks did not change: the old mesh is still right for them and stays on screen
        Assert.All(grid.Chunks, chunk => Assert.True(tracker.HasMesh(chunk.Coord)));
    }

    [Fact]
    public void Given_AStaleMesh_When_ItIsBuiltAgainInTheNewBand_Then_ItIsNoLongerStale()
    {
        var (_, tracker) = Meshed(LightBand.Day);
        var coord = new VoxelChunkCoord(0, 0, 0);
        var other = new VoxelChunkCoord(1, 0, 0);
        Assert.True(tracker.IsBandStale(coord, LightBand.Dusk));

        tracker.MarkMeshed(coord, 0, LightBand.Dusk);

        Assert.False(tracker.IsBandStale(coord, LightBand.Dusk));
        Assert.True(tracker.IsBandStale(other, LightBand.Dusk), "the others wait for their turn");
        Assert.Equal(LightBand.Dusk, tracker.BandOf(coord));
        Assert.Equal(LightBand.Day, tracker.BandOf(other)); // drawn in their own band's tint meanwhile
    }

    [Fact]
    public void Given_AChunkWithNoMesh_When_TheBandChanges_Then_ItIsDueAndNotStale()
    {
        var grid = new VoxelGrid(32, 32, 32);
        var tracker = new ChunkVersionTracker(grid);
        var coord = new VoxelChunkCoord(0, 0, 0);

        Assert.False(tracker.IsBandStale(coord, LightBand.Night));
        Assert.Contains(coord, tracker.Due());
    }

    [Fact]
    public void Given_AChunkLetGo_When_TheBandChanges_Then_ItIsDueAndNotStale()
    {
        var (_, tracker) = Meshed(LightBand.Day);
        var coord = new VoxelChunkCoord(0, 0, 0);

        tracker.Discard(coord);

        Assert.False(tracker.IsBandStale(coord, LightBand.Night));
        Assert.Contains(coord, tracker.Due());
    }

    [Fact]
    public void Given_AStaleMeshAndAnEdit_When_TheChunkIsChecked_Then_ItIsBothDueAndStale()
    {
        var (grid, tracker) = Meshed(LightBand.Day);
        var coord = new VoxelChunkCoord(0, 0, 0);

        grid.SetBlock(6, 5, 5, BlockId.Stone);

        Assert.True(tracker.IsDue(coord));
        Assert.True(tracker.IsBandStale(coord, LightBand.Night));
    }
}
