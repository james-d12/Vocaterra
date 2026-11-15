using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The residency policy (B06): what is wanted round the player nearest first with a fixed tie-break, and what may be dropped (beyond the keep distance and one chunk of slack).</summary>
public sealed class ChunkStreamerTests
{
    private static readonly WorldLimits Limits = WorldLimits.Default;

    [Fact]
    public void Given_APolicy_When_TheWantedChunksAreAsked_Then_TheyAreTheDiscOfColumnsAndLayersNearestFirst()
    {
        var streamer = new ChunkStreamer(new StreamPolicy(2, 3, 1, 1), Limits);
        var player = new VoxelChunkCoord(100, 3, -50);

        var wanted = streamer.Wanted(player);

        Assert.Equal(player, wanted[0]);
        Assert.Equal(13 * 3, wanted.Count); // a disc of radius 2 holds 13 columns, over 3 layers
        Assert.Equal(wanted.Count, wanted.Distinct().Count());
        var distances = wanted.Select(c => (long)((c.X - 100) * (c.X - 100)) + ((c.Z + 50) * (c.Z + 50)) + ((c.Y - 3) * (c.Y - 3))).ToList();
        Assert.Equal(distances.Order().ToList(), distances);
        Assert.All(wanted, c => Assert.InRange(c.Y, 2, 4));
    }

    [Fact]
    public void Given_ChunksAtTheSameDistance_When_TheyAreOrdered_Then_TheTieBreaksByLayerThenRowThenColumn()
    {
        var streamer = new ChunkStreamer(new StreamPolicy(1, 1, 0, 0), Limits);

        var wanted = streamer.Wanted(new VoxelChunkCoord(0, 0, 0));

        Assert.Equal(
            [new VoxelChunkCoord(0, 0, 0), new VoxelChunkCoord(0, 0, -1), new VoxelChunkCoord(-1, 0, 0), new VoxelChunkCoord(1, 0, 0), new VoxelChunkCoord(0, 0, 1)],
            wanted);
    }

    [Fact]
    public void Given_TheSameAskTwice_When_ItIsRepeated_Then_TheOrderIsTheSameAndTheEvictableOrderIsTheSameWhateverTheInputOrder()
    {
        var streamer = new ChunkStreamer(new StreamPolicy(3, 4, 2, 2), Limits);
        var player = new VoxelChunkCoord(-7, 5, 9);

        Assert.Equal(streamer.Wanted(player), streamer.Wanted(player));

        var resident = streamer.Wanted(new VoxelChunkCoord(10, 5, 9)).ToList();
        var shuffled = new List<VoxelChunkCoord>(resident);
        shuffled.Reverse();
        Assert.Equal(streamer.Evictable(resident, player), streamer.Evictable(shuffled, player));
        Assert.Equal(streamer.Evictable(resident, player), streamer.Evictable(resident, player).OrderBy(static c => c.Y).ThenBy(static c => c.Z).ThenBy(static c => c.X));
    }

    [Fact]
    public void Given_ThePlayerNearTheWorldsLimits_When_TheWantedChunksAreAsked_Then_NothingOutsideIsWanted()
    {
        var streamer = new ChunkStreamer(new StreamPolicy(4, 5, 30, 30), Limits);

        var corner = streamer.Wanted(new VoxelChunkCoord(Limits.EndChunkX - 1, Limits.MinChunkY, Limits.MinChunkZ));

        Assert.All(corner, c => Assert.True(Limits.Contains(c)));
        Assert.Equal(corner.Count, corner.Distinct().Count());
        Assert.Equal(Limits.EndChunkY - Limits.MinChunkY, corner.Select(static c => c.Y).Distinct().Count());
    }

    [Fact]
    public void Given_TheKeepDistance_When_ChunksAreTestedForEviction_Then_OneChunkOfSlackIsKeptBeyondItAcrossAndUpAndDown()
    {
        var streamer = new ChunkStreamer(new StreamPolicy(3, 5, 2, 1), Limits);
        var player = new VoxelChunkCoord(0, 0, 0);

        Assert.False(streamer.MayEvict(new VoxelChunkCoord(5, 0, 0), player)); // the keep distance
        Assert.False(streamer.MayEvict(new VoxelChunkCoord(6, 0, 0), player)); // one chunk of slack beyond it
        Assert.True(streamer.MayEvict(new VoxelChunkCoord(7, 0, 0), player));
        Assert.False(streamer.MayEvict(new VoxelChunkCoord(0, 0, -6), player));
        Assert.True(streamer.MayEvict(new VoxelChunkCoord(0, 0, -7), player));
        Assert.False(streamer.MayEvict(new VoxelChunkCoord(4, 0, 4), player)); // across is a distance, not a square: 32 is within 36
        Assert.True(streamer.MayEvict(new VoxelChunkCoord(5, 0, 5), player));
        Assert.False(streamer.MayEvict(new VoxelChunkCoord(0, 3, 0), player)); // up: 2 wanted plus one of slack
        Assert.True(streamer.MayEvict(new VoxelChunkCoord(0, 4, 0), player));
        Assert.False(streamer.MayEvict(new VoxelChunkCoord(0, -2, 0), player));
        Assert.True(streamer.MayEvict(new VoxelChunkCoord(0, -3, 0), player));
    }

    [Fact]
    public void Given_EveryWantedChunk_When_TheyAreTestedForEviction_Then_NoneMayBeEvictedSoTheLoadedAreKept()
    {
        var policy = new StreamPolicy(6, 6, 2, 2);
        var streamer = new ChunkStreamer(policy, Limits);
        var player = new VoxelChunkCoord(3, 4, 5);

        Assert.Empty(streamer.Evictable(streamer.Wanted(player), player));
    }

    [Fact]
    public void Given_ABadPolicy_When_TheStreamerIsMade_Then_ItIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ChunkStreamer(new StreamPolicy(5, 4, 1, 1), Limits));
        Assert.Throws<ArgumentException>(() => new ChunkStreamer(new StreamPolicy(-1, 4, 1, 1), Limits));
        Assert.Throws<ArgumentException>(() => new ChunkStreamer(new StreamPolicy(2, 4, -1, 1), Limits));
    }
}
