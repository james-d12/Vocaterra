using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;
using static Vocaterra.Game.Core.Tests.Voxel.StreamedGridSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>B04a: installing, generating on read and evicting chunks of a streamed grid, and what the pristine digest says about a chunk.</summary>
public sealed class ChunkResidencyTests
{
    private static readonly VoxelChunkCoord Origin = new(0, 0, 0);

    [Fact]
    public void Given_AStreamedGrid_When_AChunkIsInstalled_Then_ItIsNotAnEditAndNoBlockListenerIsTold()
    {
        var grid = Default();
        var changes = new ChangeRecorder();
        var residency = new ResidencyRecorder();
        grid.AddListener(changes);
        grid.AddResidencyListener(residency);

        var installed = grid.InstallChunk(Origin, Floor(BlockId.Stone));

        Assert.True(installed);
        Assert.Equal(0, grid.EditCount);
        Assert.Empty(changes.Changes);
        Assert.Equal(1, grid.ResidencyVersion);
        Assert.Equal([Origin], residency.Installed);
        Assert.Equal(BlockId.Stone, grid.GetBlock(3, 0, 3));
        Assert.Equal(BlockId.Air, grid.GetBlock(3, 1, 3));
        Assert.False(grid.GetChunk(Origin).IsChanged);
    }

    [Fact]
    public void Given_AResidentChunk_When_AnotherIsInstalledOverIt_Then_TheDuplicateIsDropped()
    {
        var grid = Default();
        grid.InstallChunk(Origin, Floor(BlockId.Stone));

        var again = grid.InstallChunk(Origin, Floor(BlockId.Dirt));

        Assert.False(again);
        Assert.Equal(1, grid.ResidencyVersion);
        Assert.Equal(BlockId.Stone, grid.GetBlock(0, 0, 0));
    }

    [Fact]
    public void Given_WaterInstalledBySource_When_TheFlowIsListening_Then_ItIsNotWoken()
    {
        var grid = Default();
        var tick = 0L;
        var flow = new FlowSimulation(grid, () => tick);
        grid.AddListener(flow);
        var blocks = new BlockId[VoxelChunk.Volume];
        Array.Fill(blocks, BlockId.Water, 0, 256);

        grid.InstallChunk(Origin, new ChunkContent(blocks));

        Assert.Equal(0, flow.Queue.Count);
        Assert.Equal(0, grid.EditCount);
    }

    [Fact]
    public void Given_AGenerator_When_AMissingChunkIsRead_Then_ItIsGeneratedOnceInstalledAndNotAnEdit()
    {
        var generator = new CountingGenerator(coord => coord == Origin ? Floor(BlockId.Stone, 3) : null);
        var grid = new VoxelGrid(WorldLimits.Default, generator);
        var residency = new ResidencyRecorder();
        grid.AddResidencyListener(residency);

        Assert.Equal(BlockId.Stone, grid.GetBlock(5, 3, 5));
        Assert.Equal(BlockId.Air, grid.GetBlock(5, 4, 5));
        Assert.Equal(WaterLevel.Source, grid.GetWater(5, 3, 5));
        Assert.True(grid.IsSolid(5, 3, 5));
        Assert.Equal(3, grid.HighestBlock(5, 5));

        Assert.Equal(1, generator.Calls.Count(c => c == Origin));
        Assert.Equal(0, grid.EditCount);
        Assert.Equal(grid.ResidentChunkCount, grid.ResidencyVersion);
        Assert.Equal(grid.ResidentChunkCount, residency.Installed.Count);
    }

    [Fact]
    public void Given_AGeneratorThatReturnsAir_When_TheChunkIsReadTwice_Then_ItIsNotAskedAgain()
    {
        var generator = new CountingGenerator(_ => null);
        var grid = new VoxelGrid(WorldLimits.Default, generator);

        Assert.Equal(BlockId.Air, grid.GetBlock(1, 1, 1));
        Assert.Equal(BlockId.Air, grid.GetBlock(2, 2, 2));

        Assert.Single(generator.Calls);
        Assert.Equal(1, grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_AGeneratedChunk_When_ABlockIsWritten_Then_TheRestOfTheChunkIsWhatWasGenerated()
    {
        var generator = new CountingGenerator(_ => Floor(BlockId.Stone, 2));
        var grid = new VoxelGrid(WorldLimits.Default, generator);

        Assert.True(grid.SetBlock(4, 10, 4, BlockId.Planks));

        Assert.Equal(BlockId.Stone, grid.GetBlock(4, 2, 4));
        Assert.Equal(BlockId.Planks, grid.GetBlock(4, 10, 4));
        Assert.Equal(1, grid.EditCount);
        Assert.True(grid.GetChunk(new VoxelChunkCoord(0, 0, 0)).IsChanged);
    }

    [Fact]
    public void Given_AGeneratedChunk_When_AirIsWrittenOverStone_Then_ItIsAnEditEvenThoughTheChunkWasNotResident()
    {
        var generator = new CountingGenerator(_ => Floor(BlockId.Stone, 2));
        var grid = new VoxelGrid(WorldLimits.Default, generator);

        Assert.True(grid.SetBlock(4, 1, 4, BlockId.Air));

        Assert.Equal(1, grid.EditCount);
        Assert.Equal(BlockId.Air, grid.GetBlock(4, 1, 4));
    }

    [Fact]
    public void Given_ResidentChunks_When_TheyAreEvicted_Then_OnlyTheUnchangedOnesGo()
    {
        var grid = Default();
        var edited = new VoxelChunkCoord(1, 0, 0);
        var untouched = new VoxelChunkCoord(2, 0, 0);
        grid.InstallChunk(edited, Floor(BlockId.Stone));
        grid.InstallChunk(untouched, Floor(BlockId.Stone));
        grid.SetBlock(edited.Origin.X + 2, 5, 2, BlockId.Planks);
        var residency = new ResidencyRecorder();
        grid.AddResidencyListener(residency);
        var versionBefore = grid.ResidencyVersion;
        var editsBefore = grid.EditCount;

        Assert.False(grid.TryEvictChunk(edited));
        Assert.True(grid.TryEvictChunk(untouched));
        Assert.False(grid.TryEvictChunk(untouched)); // not resident any more

        Assert.True(grid.IsResident(edited));
        Assert.False(grid.IsResident(untouched));
        Assert.Equal(BlockId.Air, grid.GetBlock(untouched.Origin.X, 0, 0)); // no generator: gone reads as air
        Assert.Equal([untouched], residency.Evicted);
        Assert.Equal(versionBefore + 1, grid.ResidencyVersion);
        Assert.Equal(editsBefore, grid.EditCount);
    }

    [Fact]
    public void Given_AChunkWhoseEditsWereUndone_When_ItIsEvicted_Then_ItCountsAsUnchanged()
    {
        var grid = Default();
        grid.InstallChunk(Origin, Floor(BlockId.Stone));
        grid.SetBlock(3, 5, 3, BlockId.Planks);
        grid.SetBlock(3, 0, 3, BlockId.Air);
        Assert.True(grid.GetChunk(Origin).IsChanged);

        grid.SetBlock(3, 5, 3, BlockId.Air);
        grid.SetBlock(3, 0, 3, BlockId.Stone);

        Assert.False(grid.GetChunk(Origin).IsChanged);
        Assert.True(grid.TryEvictChunk(Origin));
    }

    [Fact]
    public void Given_AnEvictedChunk_When_ItIsReadAgain_Then_ItIsGeneratedAgainAsItWas()
    {
        var generator = new CountingGenerator(_ => Floor(BlockId.Stone, 1));
        var grid = new VoxelGrid(WorldLimits.Default, generator);
        var first = grid.GetChunk(Origin).ComputeDigestHex();
        Assert.Equal(BlockId.Stone, grid.GetBlock(0, 0, 0)); // fills the one-chunk cache

        Assert.True(grid.TryEvictChunk(Origin));
        Assert.Equal(BlockId.Stone, grid.GetBlock(0, 0, 0));

        Assert.Equal(2, generator.Calls.Count(c => c == Origin));
        Assert.Equal(first, grid.GetChunk(Origin).ComputeDigestHex());
    }

    [Fact]
    public void Given_ABoundedGrid_When_AChunkIsInstalledOrEvicted_Then_ItRefuses()
    {
        var grid = new VoxelGrid(32, 32, 32);

        Assert.Throws<InvalidOperationException>(() => grid.InstallChunk(Origin, null));
        Assert.Throws<InvalidOperationException>(() => grid.TryEvictChunk(Origin));
    }

    [Fact]
    public void Given_AnInstallOutsideTheLimits_When_ItIsMade_Then_ItRefuses() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Default().InstallChunk(new VoxelChunkCoord(1875, 0, 0), null));

    [Fact]
    public void Given_ContentOfTheWrongSize_When_ItIsWrapped_Then_ItRefuses()
    {
        Assert.Throws<ArgumentException>(() => new ChunkContent(new BlockId[10]));
        Assert.Throws<ArgumentException>(() => new ChunkContent(new BlockId[VoxelChunk.Volume], new byte[VoxelChunk.Volume].Select((_, i) => i == 0 ? (byte)1 : (byte)0).ToArray())); // a level on air
    }

    [Fact]
    public void Given_InstalledWaterLevels_When_TheChunkIsRead_Then_TheyAreThere()
    {
        var grid = Default();
        var blocks = new BlockId[VoxelChunk.Volume];
        var levels = new byte[VoxelChunk.Volume];
        blocks[0] = BlockId.Water;
        blocks[1] = BlockId.Water;
        levels[1] = WaterLevel.Flowing(3).Encoded;

        grid.InstallChunk(Origin, new ChunkContent(blocks, levels));

        Assert.Equal(WaterLevel.Source, grid.GetWater(0, 0, 0));
        Assert.Equal(WaterLevel.Flowing(3), grid.GetWater(1, 0, 0));
        Assert.True(grid.GetChunk(Origin).HasWaterLevels);
    }

    [Fact]
    public void Given_AChunkNobodyWrote_When_ThePristineDigestIsAsked_Then_ItIsTheCurrentOne()
    {
        var grid = Default();
        grid.InstallChunk(Origin, Floor(BlockId.Stone));
        var chunk = grid.GetChunk(Origin);

        Assert.Equal(chunk.ComputeDigestHex(), Convert.ToHexString(chunk.PristineDigest).ToLowerInvariant());
        Assert.False(chunk.IsChanged);
    }

    [Fact]
    public void Given_AChunkWritten_When_ThePristineDigestIsAsked_Then_ItIsWhatTheChunkHeldBeforeTheFirstWrite()
    {
        var grid = Default();
        grid.InstallChunk(Origin, Floor(BlockId.Stone));
        var chunk = grid.GetChunk(Origin);
        var before = chunk.ComputeDigestHex();

        grid.SetBlock(1, 1, 1, BlockId.Planks);
        grid.SetBlock(2, 2, 2, BlockId.Planks); // a second write does not move it

        Assert.Equal(before, Convert.ToHexString(chunk.PristineDigest).ToLowerInvariant());
        Assert.NotEqual(before, chunk.ComputeDigestHex());
        Assert.True(chunk.IsChanged);
    }

    [Fact]
    public void Given_AWaterLevelEditThatIsUndone_When_TheChunkIsAsked_Then_ItIsUnchanged()
    {
        var grid = Default();
        var blocks = new BlockId[VoxelChunk.Volume];
        blocks[0] = BlockId.Water;
        grid.InstallChunk(Origin, new ChunkContent(blocks));
        var chunk = grid.GetChunk(Origin);

        grid.SetWater(0, 0, 0, WaterLevel.Flowing(2));
        Assert.True(chunk.IsChanged);
        grid.SetWater(0, 0, 0, WaterLevel.Source);

        Assert.False(chunk.IsChanged);
    }

    [Fact]
    public void Given_AChunkBuiltByWriting_When_ThePristineStateIsSealed_Then_ItCountsAsUnchangedUntilTheNextWrite()
    {
        var grid = new VoxelGrid(32, 32, 32);
        grid.SetBlock(1, 1, 1, BlockId.Stone);
        var chunk = grid.GetChunk(Origin);
        Assert.True(chunk.IsChanged);

        grid.SealPristine();

        Assert.False(chunk.IsChanged);
        grid.SetBlock(2, 2, 2, BlockId.Stone);
        Assert.True(chunk.IsChanged);
    }

    [Fact]
    public void Given_AChunkWithAPristineDigest_When_ItIsCloned_Then_TheCopyKeepsIt()
    {
        var grid = Default();
        grid.InstallChunk(Origin, Floor(BlockId.Stone));
        grid.SetBlock(1, 5, 1, BlockId.Planks);

        var copy = grid.Clone();

        Assert.True(copy.GetChunk(Origin).IsChanged);
        copy.SetBlock(1, 5, 1, BlockId.Air);
        Assert.False(copy.GetChunk(Origin).IsChanged);
        Assert.True(grid.GetChunk(Origin).IsChanged);
    }

    [Fact]
    public void Given_ChunksOfAir_When_TheirDigestsAreAsked_Then_TheyAllShareTheAirDigest()
    {
        var grid = Default();
        grid.InstallChunk(Origin, null);
        var written = new VoxelGrid(32, 32, 32);
        written.SetBlock(1, 1, 1, BlockId.Stone);
        written.SetBlock(1, 1, 1, BlockId.Air);

        Assert.Equal(grid.GetChunk(Origin).ComputeDigestHex(), written.GetChunk(Origin).ComputeDigestHex());
        Assert.Equal(grid.GetChunk(Origin).ComputeDigestHex(), new VoxelGrid(16, 16, 16).GetChunk(Origin).ComputeDigestHex());
    }
}
