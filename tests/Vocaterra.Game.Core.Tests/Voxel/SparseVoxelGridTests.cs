using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using static Vocaterra.Game.Core.Tests.Voxel.StreamedGridSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>B04a: the sparse storage in both modes, the world's limits and the y range below zero.</summary>
public sealed class SparseVoxelGridTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    [Fact]
    public void Given_TheValley_When_AStreamedGridServesItsChunks_Then_EveryBlockAndLevelAgreeWithTheBoundedGrid()
    {
        var valley = RouteScene.Build().Grid;
        var generator = new CountingGenerator(coord =>
            valley.TryGetChunk(coord, out var chunk) && !chunk.IsEmpty
                ? new ChunkContent(chunk.Blocks.ToArray(), chunk.HasWaterLevels ? chunk.WaterLevels.ToArray() : null)
                : null);
        var streamed = new VoxelGrid(valley.Limits, generator);

        for (var cy = 0; cy < valley.Limits.EndChunkY; cy++)
        {
            for (var cz = 0; cz < valley.Limits.EndChunkZ; cz++)
            {
                for (var cx = 0; cx < valley.Limits.EndChunkX; cx++)
                {
                    var coord = new VoxelChunkCoord(cx, cy, cz);
                    var expected = valley.GetChunk(coord);
                    var actual = streamed.GetChunk(coord);
                    Assert.True(expected.Blocks.SequenceEqual(actual.Blocks), $"chunk {coord} differs");
                    Assert.True(expected.WaterLevels.SequenceEqual(actual.WaterLevels), $"chunk {coord} differs in its water levels");
                }
            }
        }

        // Reads through the public cells agree too, on a spread of positions and on every column's highest block.
        var state = 12345u;
        for (var i = 0; i < 20_000; i++)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            var x = (int)((state >> 8) % (uint)valley.SizeX);
            state = unchecked((state * 1664525u) + 1013904223u);
            var y = (int)((state >> 8) % (uint)valley.SizeY);
            state = unchecked((state * 1664525u) + 1013904223u);
            var z = (int)((state >> 8) % (uint)valley.SizeZ);
            Assert.Equal(valley.GetBlock(x, y, z), streamed.GetBlock(x, y, z));
            Assert.Equal(valley.GetWater(x, y, z), streamed.GetWater(x, y, z));
            Assert.Equal(valley.IsSolid(x, y, z), streamed.IsSolid(x, y, z));
        }

        for (var z = 0; z < valley.SizeZ; z += 3)
        {
            for (var x = 0; x < valley.SizeX; x += 3)
            {
                Assert.Equal(valley.HighestBlock(x, z), streamed.HighestBlock(x, z));
            }
        }
    }

    [Fact]
    public void Given_ABoundedGrid_When_ItIsReadEverywhereAndAirIsWritten_Then_NoChunkIsAllocated()
    {
        var grid = new VoxelGrid(64, 32, 64);

        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                Assert.Equal(BlockId.Air, grid.GetBlock(x, 5, z));
                Assert.Equal(-1, grid.HighestBlock(x, z));
            }
        }

        Assert.False(grid.SetBlock(3, 3, 3, BlockId.Air));
        grid.FillColumn(4, 4, 0, 31, BlockId.Air);

        Assert.Equal(0, grid.ResidentChunkCount);
        Assert.Equal(0, grid.EditCount);
    }

    [Fact]
    public void Given_ABoundedGrid_When_OneBlockIsWritten_Then_OnlyItsChunkIsAllocatedAndTheDigestIsTheDenseOne()
    {
        var grid = new VoxelGrid(48, 32, 48);
        var untouched = new VoxelGrid(48, 32, 48);

        grid.SetBlock(20, 17, 33, BlockId.Stone);

        Assert.Equal(1, grid.ResidentChunkCount);
        Assert.NotEqual(untouched.ComputeDigestHex(), grid.ComputeDigestHex());
        Assert.Equal(3 * 2 * 3, grid.Chunks.Count); // the dense view gives every chunk an object
        Assert.Equal(grid.ComputeDigestHex(), grid.ComputeDigestHex());
        grid.SetBlock(20, 17, 33, BlockId.Air);
        Assert.Equal(untouched.ComputeDigestHex(), grid.ComputeDigestHex()); // a block put back is the world it was
    }

    [Fact]
    public void Given_AStreamedGridWithoutAGenerator_When_ItIsRead_Then_EverythingIsAirAndNothingBecomesResident()
    {
        var grid = Default();

        Assert.Equal(BlockId.Air, grid.GetBlock(100, 5, -100));
        Assert.Equal(WaterLevel.Source, grid.GetWater(100, 5, -100));
        Assert.Equal(-65, grid.HighestBlock(100, -100));
        Assert.False(grid.SetBlock(100, 5, -100, BlockId.Air));

        Assert.Equal(0, grid.ResidentChunkCount);
        Assert.Equal(0, grid.ResidencyVersion);
    }

    [Theory]
    [InlineData(-30_001, 0, 0)]
    [InlineData(30_000, 0, 0)]
    [InlineData(0, -65, 0)]
    [InlineData(0, 320, 0)]
    [InlineData(0, 0, -30_001)]
    [InlineData(0, 0, 30_000)]
    [InlineData(int.MinValue, 0, int.MaxValue)]
    public void Given_ACellOutsideTheLimits_When_ItIsRead_Then_ItIsSolidAirAndCannotBeWritten(int x, int y, int z)
    {
        var grid = Default();

        Assert.True(grid.IsSolid(x, y, z));
        Assert.True(grid.BlocksBody(x, y, z));
        Assert.Equal(BlockId.Air, grid.GetBlock(x, y, z));
        Assert.False(grid.InBounds(x, y, z));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetBlock(x, y, z, BlockId.Stone));
    }

    [Theory]
    [InlineData(29_000, -60, -29_000)]
    [InlineData(-29_000, 310, 29_000)]
    [InlineData(-30_000, -64, -30_000)]
    [InlineData(29_999, 319, 29_999)]
    [InlineData(29_001, -1, -29_001)]
    [InlineData(-1, -1, -1)]
    public void Given_ACellFarFromTheOriginOrBelowZero_When_ItIsWritten_Then_ItReadsBackAndItsChunkIsTheRightOne(int x, int y, int z)
    {
        var grid = Default();

        Assert.True(grid.SetBlock(x, y, z, BlockId.Stone));

        Assert.Equal(BlockId.Stone, grid.GetBlock(x, y, z));
        Assert.True(grid.IsSolid(x, y, z));
        Assert.Equal(BlockId.Air, grid.GetBlock(x, y + 1 > 319 ? y - 1 : y + 1, z));
        Assert.Equal(y, grid.HighestBlock(x, z));
        var chunk = grid.GetChunk(VoxelChunkCoord.FromBlock(x, y, z));
        Assert.Equal(1, chunk.NonAirCount);
        Assert.Equal(VoxelChunkCoord.FromBlock(x, y, z), chunk.Coord);
        Assert.Equal(1, grid.ResidentChunkCount);
        Assert.Equal(1, grid.EditCount);
    }

    [Fact]
    public void Given_AColumnFillAcrossZero_When_ItIsRead_Then_EveryChunkOfItIsFilledAndTheHighestBlockIsTheTop()
    {
        var grid = Default();

        grid.FillColumn(-17, 35, -70, 20, BlockId.Dirt); // clipped to the limits: -64 to 20

        Assert.Equal(85, grid.EditCount);
        Assert.Equal(20, grid.HighestBlock(-17, 35));
        Assert.Equal(BlockId.Dirt, grid.GetBlock(-17, -64, 35));
        Assert.Equal(BlockId.Dirt, grid.GetBlock(-17, -1, 35));
        Assert.Equal(BlockId.Dirt, grid.GetBlock(-17, 0, 35));
        Assert.Equal(BlockId.Air, grid.GetBlock(-17, 21, 35));
        Assert.Equal(BlockId.Air, grid.GetBlock(-17, -65, 35));
        Assert.Equal(6, grid.ResidentChunkCount); // chunk layers -4 to 1 of one chunk column
    }

    [Fact]
    public void Given_APlayerAboveAFloorBelowZero_When_ItFalls_Then_ItLandsOnTheFloor()
    {
        var grid = Default();
        for (var x = 28_990; x <= 29_010; x++)
        {
            for (var z = -29_010; z <= -28_990; z++)
            {
                grid.SetBlock(x, -60, z, BlockId.Stone);
            }
        }

        var body = new PlayerBody(grid, (29_000 * U) + (U / 2), -50 * U, (-29_000 * U) + (U / 2), 0);
        for (var tick = 0; tick < 200; tick++)
        {
            body.Step(grid, 0, 0, false);
        }

        Assert.Equal(new BlockPos(29_000, -59, -29_000), body.FeetBlock);
        Assert.True(body.Grounded);
    }

    [Fact]
    public void Given_ABlockBelowZeroUnderTheEye_When_TheRayLooksDown_Then_ItHitsTheTopFace()
    {
        var grid = Default();
        grid.SetBlock(-29_000, -60, 29_000, BlockId.Stone);

        var hit = BlockRay.Cast(grid, (-29_000 * U) + (U / 2), -55 * U, (29_000 * U) + (U / 2), 0, -Angles.One, 0, 10L * U);

        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(-29_000, -60, 29_000), hit.Value.Block);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
    }

    [Fact]
    public void Given_GroundsAtTheBottomAndTheTopOfTheWorld_When_TheFinderLooksForStanding_Then_ItFindsTheCellOverThem()
    {
        var grid = Default();
        grid.FillColumn(29_000, -29_000, -64, -60, BlockId.Stone);
        grid.FillColumn(-29_000, 29_000, 300, 310, BlockId.Stone);

        Assert.Equal(new BlockPos(29_000, -59, -29_000), GroundFinder.StandingAt(grid, 29_000, -29_000));
        Assert.Equal(new BlockPos(-29_000, 311, 29_000), GroundFinder.StandingAt(grid, -29_000, 29_000));
        Assert.Null(GroundFinder.StandingAt(grid, 0, 0)); // nothing there
        Assert.Null(GroundFinder.StandingAt(grid, 30_000, 0)); // outside
    }

    [Fact]
    public void Given_ATwoFloorWorld_When_TheWalkPlannerPlansBelowZero_Then_ItFindsTheWay()
    {
        var grid = Default();
        for (var x = 28_990; x <= 29_020; x++)
        {
            for (var z = -29_010; z <= -28_990; z++)
            {
                grid.SetBlock(x, -61, z, BlockId.Stone);
            }
        }

        var plan = WalkPlanner.Plan(grid, new BlockPos(29_000, -60, -29_000), 29_015, -29_005);

        Assert.NotNull(plan);
        Assert.Equal((29_015, -29_005), plan[^1]);
    }

    [Fact]
    public void Given_ASourceBelowZeroFarFromTheOrigin_When_TheFlowRuns_Then_ItSpreadsOverTheFloor()
    {
        var grid = Default();
        for (var x = 28_980; x <= 29_020; x++)
        {
            for (var z = -29_020; z <= -28_980; z++)
            {
                grid.SetBlock(x, -61, z, BlockId.Stone);
            }
        }

        var tick = 0L;
        var flow = new Vocaterra.Game.Core.Water.Flow.FlowSimulation(grid, () => tick);
        grid.AddListener(flow);
        grid.SetBlock(29_000, -60, -29_000, BlockId.Water);
        while (flow.Queue.Count > 0 && tick < 15 * 200)
        {
            tick++;
            flow.Step(tick);
        }

        Assert.Equal(BlockId.Water, grid.GetBlock(29_003, -60, -29_000));
        Assert.Equal(BlockId.Water, grid.GetBlock(28_997, -60, -29_000));
        Assert.Equal(0, flow.Queue.Count);
    }

    [Fact]
    public void Given_AStreamedSimulation_When_ThePlayerIsTeleportedBelowZero_Then_TheFeetAreThereAndSettleOnTheGround()
    {
        var grid = Default();
        grid.FillColumn(100, 100, -64, -60, BlockId.Stone);
        for (var x = 98; x <= 102; x++)
        {
            for (var z = 98; z <= 102; z++)
            {
                grid.SetBlock(x, -60, z, BlockId.Stone);
            }
        }

        var scene = new Scene("deep", 0, grid, new SpawnPoint(new BlockPos(100, -59, 100), 0), [], new SortedDictionary<string, BlockPos>(StringComparer.Ordinal), 0, 0);
        var sim = VoxelSimulation.Create(scene, TestItems.Catalogue);

        sim.TeleportTo(new BlockPos(100, -57, 100));
        for (var tick = 0; tick < 120; tick++)
        {
            sim.Step();
        }

        Assert.Equal(new BlockPos(100, -59, 100), sim.Player.FeetBlock);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.TeleportTo(new BlockPos(100, -65, 100)));
    }

    [Fact]
    public void Given_AStreamedGrid_When_TheDenseMembersAreUsed_Then_TheyRefuse()
    {
        var grid = Default();

        Assert.Throws<NotSupportedException>(() => grid.Chunks);
        Assert.Throws<NotSupportedException>(() => grid.ChunksX);
        Assert.Throws<NotSupportedException>(() => grid.ChunksY);
        Assert.Throws<NotSupportedException>(() => grid.ChunksZ);
        Assert.Throws<NotSupportedException>(() => grid.ComputeDigest());
        Assert.Throws<NotSupportedException>(() => grid.BlocksOfKind(BlockId.Stone));
    }

    [Theory]
    [InlineData(0, 0, 0, 20, 16, 16)]
    [InlineData(8, 0, 0, 16, 16, 16)]
    [InlineData(0, -60, 0, 16, 16, 16)]
    [InlineData(0, 0, 0, 0, 16, 16)]
    public void Given_LimitsThatAreNotWholeChunks_When_AStreamedGridIsMade_Then_ItRefuses(int minX, int minY, int minZ, int sizeX, int sizeY, int sizeZ) =>
        Assert.Throws<ArgumentException>(() => new VoxelGrid(new WorldLimits(minX, minY, minZ, sizeX, sizeY, sizeZ)));

    [Fact]
    public void Given_TheDefaultLimits_When_TheyAreRead_Then_TheyAreTheWorldsBox()
    {
        var limits = WorldLimits.Default;

        Assert.Equal((-30_000, -64, -30_000), (limits.MinX, limits.MinY, limits.MinZ));
        Assert.Equal((30_000, 320, 30_000), (limits.EndX, limits.EndY, limits.EndZ));
        Assert.Equal((-1875, -4, -1875), (limits.MinChunkX, limits.MinChunkY, limits.MinChunkZ));
        Assert.Equal((1875, 20, 1875), (limits.EndChunkX, limits.EndChunkY, limits.EndChunkZ));
        Assert.Equal(WorldLimits.Bounded(64, 32, 64), new VoxelGrid(64, 32, 64).Limits);
    }

    [Fact]
    public void Given_AStreamedGrid_When_ItIsCloned_Then_TheCopyEditsApartAndKeepsTheLimits()
    {
        var grid = Default();
        grid.SetBlock(-5, -5, -5, BlockId.Stone);

        var copy = grid.Clone();
        copy.SetBlock(-5, -5, -5, BlockId.Air);

        Assert.Equal(BlockId.Stone, grid.GetBlock(-5, -5, -5));
        Assert.Equal(BlockId.Air, copy.GetBlock(-5, -5, -5));
        Assert.Equal(grid.Limits, copy.Limits);
        Assert.True(copy.IsStreamed);
    }
}
