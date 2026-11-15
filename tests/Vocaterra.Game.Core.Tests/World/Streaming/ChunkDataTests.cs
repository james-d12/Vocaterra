using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>The immutable chunk of the streaming folder (B03): its order and encodings are a <see cref="VoxelChunk"/>'s, so a grid can adopt it without translating.</summary>
public sealed class ChunkDataTests
{
    private static readonly VoxelChunkCoord Coord = new(3, -2, 5);

    [Fact]
    public void Given_AChunkOfAir_When_ItIsRead_Then_ItIsEmptyAndHoldsNoArray()
    {
        var chunk = ChunkData.Empty(Coord);

        Assert.True(chunk.IsEmpty);
        Assert.Equal(0, chunk.NonAirCount);
        Assert.Equal(ChunkData.Volume, chunk.Blocks.Length);
        Assert.True(chunk.Blocks.ToArray().All(static b => b == BlockId.Air));
        Assert.False(chunk.HasWaterLevels);
        Assert.True(chunk.WaterLevels.IsEmpty);
        Assert.Equal(Coord, chunk.Coord);
    }

    [Fact]
    public void Given_BlocksAndLevels_When_ACopyIsMade_Then_LaterWritesToTheSourceDoNotReachTheChunk()
    {
        var blocks = new BlockId[ChunkData.Volume];
        var levels = new byte[ChunkData.Volume];
        blocks[ChunkData.Index(1, 2, 3)] = BlockId.Water;
        levels[ChunkData.Index(1, 2, 3)] = WaterLevel.Flowing(2, falling: true).Encoded;
        var chunk = ChunkData.From(Coord, blocks, levels);

        blocks[ChunkData.Index(1, 2, 3)] = BlockId.Stone;
        levels[ChunkData.Index(1, 2, 3)] = 0;

        Assert.Equal(BlockId.Water, chunk[1, 2, 3]);
        Assert.Equal(1, chunk.NonAirCount);
        Assert.True(chunk.HasWaterLevels);
        Assert.Equal(WaterLevel.Flowing(2, falling: true).Encoded, chunk.WaterLevels[ChunkData.Index(1, 2, 3)]);
    }

    [Fact]
    public void Given_ALengthThatIsNotAChunks_When_AChunkIsMade_Then_ItIsRefused()
    {
        Assert.Throws<ArgumentException>(() => ChunkData.From(Coord, new BlockId[10], default));
        Assert.Throws<ArgumentException>(() => ChunkData.From(Coord, new BlockId[ChunkData.Volume], new byte[7]));
    }

    [Fact]
    public void Given_LevelsThatAreAllSource_When_AChunkIsMade_Then_ItKeepsNoLevelLayer()
    {
        var chunk = ChunkData.From(Coord, new BlockId[ChunkData.Volume], new byte[ChunkData.Volume]);

        Assert.False(chunk.HasWaterLevels);
    }

    [Fact]
    public void Given_ChunkDataAndAVoxelChunkWithTheSameContent_When_TheyAreDigested_Then_TheDigestsAreEqualWithAndWithoutLevels()
    {
        // The same content in a grid's chunk (written the way a scene is) gives the same bytes: blocks as 16-bit little-endian, then the levels when there are any.
        var grid = new VoxelGrid(32, 32, 32);
        var blocks = new BlockId[ChunkData.Volume];
        foreach (var (x, y, z) in new[] { (0, 0, 0), (5, 1, 7), (15, 15, 15) })
        {
            _ = grid.SetBlock(x, y, z, BlockId.Water);
            blocks[ChunkData.Index(x, y, z)] = BlockId.Water;
        }

        _ = grid.SetBlock(2, 2, 2, BlockId.Stone);
        blocks[ChunkData.Index(2, 2, 2)] = BlockId.Stone;
        var source = grid.GetChunk(new VoxelChunkCoord(0, 0, 0));
        Assert.Equal(source.ComputeDigestHex(), ChunkData.From(new VoxelChunkCoord(0, 0, 0), blocks, default).ComputeDigestHex());

        _ = grid.SetWater(5, 1, 7, WaterLevel.Flowing(3));
        var levels = new byte[ChunkData.Volume];
        levels[ChunkData.Index(5, 1, 7)] = WaterLevel.Flowing(3).Encoded;
        Assert.Equal(grid.GetChunk(new VoxelChunkCoord(0, 0, 0)).ComputeDigestHex(), ChunkData.From(new VoxelChunkCoord(0, 0, 0), blocks, levels).ComputeDigestHex());
    }
}
