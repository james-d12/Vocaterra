using Vocaterra.Simulation;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Simulation.Tests.Voxel;

/// <summary>The grid: chunks, bounds, the digest and the edit counter.</summary>
public sealed class VoxelGridTests
{
    [Fact]
    public void TheGridIsMadeOfSixteenCubedChunks()
    {
        var grid = new VoxelGrid(48, 32, 64);
        Assert.Equal((3, 2, 4), (grid.ChunksX, grid.ChunksY, grid.ChunksZ));
        Assert.Equal(24, grid.Chunks.Count);
        Assert.Equal(16 * 16 * 16, VoxelChunk.Volume);
        Assert.Equal(new VoxelChunkCoord(2, 1, 3), VoxelChunkCoord.FromBlock(47, 16, 63));
        Assert.Equal(new BlockPos(32, 16, 48), new VoxelChunkCoord(2, 1, 3).Origin);
        Assert.Throws<ArgumentException>(() => new VoxelGrid(40, 32, 32));
    }

    [Fact]
    public void BlocksReadBackAndOutsideReadsAsAirForLookupsAndSolidForCollision()
    {
        var grid = new VoxelGrid(32, 32, 32);
        Assert.True(grid.SetBlock(31, 31, 31, BlockId.Stone));
        Assert.False(grid.SetBlock(31, 31, 31, BlockId.Stone));
        Assert.Equal(BlockId.Stone, grid.GetBlock(31, 31, 31));
        Assert.Equal(BlockId.Air, grid.GetBlock(0, 0, 0));
        Assert.False(grid.IsSolid(0, 0, 0));
        foreach (var (x, y, z) in new[] { (-1, 0, 0), (0, -1, 0), (0, 0, -1), (32, 0, 0), (0, 32, 0), (0, 0, 32), (1000, 1000, 1000) })
        {
            Assert.False(grid.InBounds(x, y, z));
            Assert.Equal(BlockId.Air, grid.GetBlock(x, y, z));
            Assert.True(grid.IsSolid(x, y, z), $"({x}, {y}, {z}) outside the grid must be solid");
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetBlock(32, 0, 0, BlockId.Stone));
    }

    [Fact]
    public void EveryBlockEditChangesTheWorldDigestAndOnlyTheEditedChunk()
    {
        var grid = new VoxelGrid(64, 32, 64);
        var rng = new WorldRng(99);
        var seen = new HashSet<string> { grid.ComputeDigestHex() };
        var kinds = Enum.GetValues<BlockId>();
        for (var i = 0; i < 300; i++)
        {
            var x = rng.NextInt(64);
            var y = rng.NextInt(32);
            var z = rng.NextInt(64);
            var before = grid.GetBlock(x, y, z);
            var next = kinds[rng.NextInt(kinds.Length)];
            if (next == before)
            {
                next = before == BlockId.Air ? BlockId.Stone : BlockId.Air;
            }

            var worldBefore = grid.ComputeDigestHex();
            var chunkDigests = grid.Chunks.Select(c => c.ComputeDigestHex()).ToArray();
            var versions = grid.Chunks.Select(c => c.Version).ToArray();
            var edits = grid.EditCount;

            Assert.True(grid.SetBlock(x, y, z, next));

            Assert.NotEqual(worldBefore, grid.ComputeDigestHex());
            Assert.True(seen.Add(grid.ComputeDigestHex()), "a digest repeated");
            Assert.Equal(edits + 1, grid.EditCount);
            var edited = grid.GetChunk(VoxelChunkCoord.FromBlock(x, y, z));
            for (var c = 0; c < grid.Chunks.Count; c++)
            {
                var same = ReferenceEquals(grid.Chunks[c], edited);
                Assert.Equal(same, chunkDigests[c] != grid.Chunks[c].ComputeDigestHex());
                Assert.Equal(same ? versions[c] + 1 : versions[c], grid.Chunks[c].Version);
            }
        }
    }

    [Fact]
    public void RevertingAnEditRestoresTheDigestAndWritingTheSameBlockChangesNothing()
    {
        var grid = new VoxelGrid(32, 32, 32);
        grid.SetBlock(3, 4, 5, BlockId.Dirt);
        var original = grid.ComputeDigestHex();
        var edits = grid.EditCount;
        grid.SetBlock(3, 4, 5, BlockId.Dirt);
        Assert.Equal(original, grid.ComputeDigestHex());
        Assert.Equal(edits, grid.EditCount);

        grid.SetBlock(3, 4, 5, BlockId.Sand);
        Assert.NotEqual(original, grid.ComputeDigestHex());
        grid.SetBlock(3, 4, 5, BlockId.Dirt);
        Assert.Equal(original, grid.ComputeDigestHex());
    }

    [Fact]
    public void TheDigestDependsOnTheSizeAndOnTheBlockPositionNotJustTheCounts()
    {
        var a = new VoxelGrid(32, 32, 32);
        var b = new VoxelGrid(32, 32, 32);
        a.SetBlock(1, 2, 3, BlockId.Stone);
        b.SetBlock(3, 2, 1, BlockId.Stone);
        Assert.NotEqual(a.ComputeDigestHex(), b.ComputeDigestHex());
        Assert.NotEqual(new VoxelGrid(32, 32, 32).ComputeDigestHex(), new VoxelGrid(16, 32, 64).ComputeDigestHex());
        Assert.Equal(64, new VoxelGrid(16, 16, 16).ComputeDigestHex().Length);
    }

    [Fact]
    public void HighestBlockSkipsAir()
    {
        var grid = new VoxelGrid(16, 16, 16);
        Assert.Equal(-1, grid.HighestBlock(3, 3));
        grid.SetBlock(3, 2, 3, BlockId.Stone);
        grid.SetBlock(3, 9, 3, BlockId.Leaves);
        Assert.Equal(9, grid.HighestBlock(3, 3));
        Assert.Equal(-1, grid.HighestBlock(-1, 3));
    }

    [Fact]
    public void TheBlockRegistryIsAppendOnlyAndComplete()
    {
        for (var i = 0; i < BlockDefinition.All.Count; i++)
        {
            Assert.Equal((BlockId)i, BlockDefinition.All[i].Id);
        }

        Assert.Equal(Enum.GetValues<BlockId>().Length, BlockDefinition.All.Count);
        Assert.Equal(52, BlockDefinition.All.Count);

        // The values are hashed into every golden: pinned here so a reorder cannot go unnoticed.
        Assert.Equal(0, (int)BlockId.Air);
        Assert.Equal(1, (int)BlockId.Grass);
        Assert.Equal(5, (int)BlockId.Trunk);
        Assert.Equal(8, (int)BlockId.BerryBush);
        Assert.Equal(9, (int)BlockId.Water);
        Assert.Equal(13, (int)BlockId.Table);
        Assert.Equal(18, (int)BlockId.Wall);
        Assert.Equal(21, (int)BlockId.Bridge);
        Assert.Equal(22, (int)BlockId.Snow);
        Assert.Equal(23, (int)BlockId.SpruceTrunk); // S14 appended its kinds after the snow, in this order
        Assert.Equal(27, (int)BlockId.TallGrass);
        Assert.Equal(31, (int)BlockId.WhiteFlower);
        Assert.Equal(33, (int)BlockId.Cobble);
        Assert.Equal(34, (int)BlockId.Timber); // S15 appended its kinds after the cobble, in this order
        Assert.Equal(35, (int)BlockId.Lantern);
        Assert.Equal(45, (int)BlockId.Bottles);
        Assert.Equal(50, (int)BlockId.SignMontanaB);
        Assert.Equal(51, (int)BlockId.PostTorch);
        Assert.Equal(BlockDefinition.Get(BlockId.FireRing), BlockDefinition.FromName("fire-ring"));
        Assert.Null(BlockDefinition.FromName("lava"));
        Assert.Equal(BlockDefinition.All.Count, BlockDefinition.All.Select(d => d.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SolidityFollowsTheDesign()
    {
        foreach (var solid in new[] { BlockId.Grass, BlockId.Dirt, BlockId.Stone, BlockId.Sand, BlockId.Trunk, BlockId.Leaves, BlockId.Path, BlockId.Planks, BlockId.Table, BlockId.Stall, BlockId.Roof, BlockId.Fence, BlockId.Wall, BlockId.FireRing, BlockId.Sign, BlockId.Bridge })
        {
            Assert.True(BlockDefinition.Get(solid).Solid, solid.ToString());
        }

        foreach (var open in new[] { BlockId.Air, BlockId.Bush, BlockId.BerryBush, BlockId.Water, BlockId.Door, BlockId.Torch })
        {
            Assert.False(BlockDefinition.Get(open).Solid, open.ToString());
        }

        Assert.True(BlockDefinition.Get(BlockId.Water).Liquid);
        Assert.True(BlockDefinition.Get(BlockId.Water).Transparent);
        Assert.True(BlockDefinition.Get(BlockId.Leaves).Transparent);
        Assert.False(BlockDefinition.Get(BlockId.Stone).Transparent);
        Assert.True(BlockDefinition.Get(BlockId.Trunk).Gatherable);
        Assert.True(BlockDefinition.Get(BlockId.BerryBush).Gatherable);
        Assert.False(BlockDefinition.Get(BlockId.Stone).Gatherable);
    }
}
