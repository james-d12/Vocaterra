using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Graphics.Weather;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B04b: the tracker, the view set, the light index and the rain cover are keyed by chunk coordinate; on the valley they agree with what the dense versions gave, and on a streamed grid installs and evictions are followed.</summary>
public sealed class KeyedChunkConsumersTests
{
    private sealed class DenseSource(VoxelGrid grid) : IBlockSource
    {
        public BlockId Get(int x, int y, int z) => y < 0 ? BlockId.Stone : grid.GetBlock(x, y, z);

        public WaterLevel GetWater(int x, int y, int z) => grid.GetWater(x, y, z);

        public FlowVector FlowAt(int x, int y, int z) => FlowVector.At(grid, x, y, z);
    }

    private sealed class ValleyGenerator(VoxelGrid valley) : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord) =>
            valley.TryGetChunk(coord, out var chunk) && !chunk.IsEmpty
                ? new ChunkContent(chunk.Blocks.ToArray(), chunk.HasWaterLevels ? chunk.WaterLevels.ToArray() : null)
                : null;
    }

    // The scan of the whole grid the renderer made before B04b: every lantern and post torch, chunk by chunk.
    private static List<BlockPos> DenseLights(VoxelGrid grid)
    {
        var found = new List<BlockPos>();
        foreach (var chunk in grid.Chunks)
        {
            var origin = chunk.Coord.Origin;
            for (var y = 0; y < VoxelChunk.Size; y++)
            {
                for (var z = 0; z < VoxelChunk.Size; z++)
                {
                    for (var x = 0; x < VoxelChunk.Size; x++)
                    {
                        if (chunk[x, y, z] is BlockId.Lantern or BlockId.PostTorch)
                        {
                            found.Add(new BlockPos(origin.X + x, origin.Y + y, origin.Z + z));
                        }
                    }
                }
            }
        }

        return found;
    }

    [Fact]
    public void Given_TheValley_When_TheLightIndexIsBuilt_Then_ItHoldsTheLightsTheDenseScanFoundInTheSameOrder()
    {
        var grid = RouteScene.Build().Grid;
        var index = new ChunkLightIndex(grid);

        var dense = DenseLights(grid);

        Assert.NotEmpty(dense);
        Assert.Equal(dense, index.All);
        Assert.Equal(dense.Count, index.Count);
        Assert.True(index.ColumnCount <= dense.Count);
    }

    [Fact]
    public void Given_TheValley_When_ChunksAreMeshedWithTheKeyedSourceAndLights_Then_EveryMeshIsTheDenseMesh()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var eye = scene.Spawn.Feet;
        var index = new ChunkLightIndex(grid);
        var lights = DenseLights(grid);
        var dense = new DenseSource(grid);
        var peeking = new GridBlockSource(grid);
        var wanted = new ChunkViewSet(grid, 80).Update(eye.X, eye.Y + 1.6f, eye.Z);

        Assert.NotEmpty(wanted);
        foreach (var coord in wanted)
        {
            var expected = ChunkMesher.Build(dense, coord.X, coord.Y, coord.Z, 0, new MeshOptions(true, 100, lights, LightBand.Day));
            var actual = ChunkMesher.Build(peeking, coord.X, coord.Y, coord.Z, 0, new MeshOptions(true, 100, index.Near(coord), LightBand.Day));
            Assert.True(expected.Opaque.Vertices.SequenceEqual(actual.Opaque.Vertices), $"opaque mesh of {coord}");
            Assert.True(expected.Alpha.Vertices.SequenceEqual(actual.Alpha.Vertices), $"alpha mesh of {coord}");
            Assert.True(expected.PlantMesh.Vertices.SequenceEqual(actual.PlantMesh.Vertices), $"plant mesh of {coord}");
            Assert.True(expected.LitMesh.Vertices.SequenceEqual(actual.LitMesh.Vertices), $"lit mesh of {coord}");
            Assert.True(expected.GlowMesh.Vertices.SequenceEqual(actual.GlowMesh.Vertices), $"glow mesh of {coord}");
        }

        Assert.Contains(wanted, c => ChunkMesher.Build(peeking, c.X, c.Y, c.Z, 0, new MeshOptions(true, 100, index.Near(c), LightBand.Day)).GlowMesh.FaceCount > 0);
    }

    [Fact]
    public void Given_TheValley_When_TheViewSetIsAsked_Then_ItIsTheDenseListNearestFirst()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var eye = scene.Spawn.Feet;
        var set = new ChunkViewSet(grid, 96);

        var wanted = set.Update(eye.X + 0.5f, eye.Y + 1.6f, eye.Z + 0.5f);

        var expected = grid.Chunks
            .Where(c => !c.IsEmpty)
            .Select(c => (Distance: ChunkViewSet.DistanceSquared(eye.X + 0.5f, eye.Y + 1.6f, eye.Z + 0.5f, c.Coord.X, c.Coord.Y, c.Coord.Z), c.Coord))
            .Where(c => c.Distance <= 96L * 96)
            .OrderBy(c => c.Distance).ThenBy(c => c.Coord.Y).ThenBy(c => c.Coord.Z).ThenBy(c => c.Coord.X)
            .Select(c => c.Coord)
            .ToList();
        Assert.Equal(expected, wanted);
    }

    [Fact]
    public void Given_AChunkThatIsNotResident_When_ItsKeyIsRead_Then_ItIsTheKeyOfAnEmptyChunkAndNothingIsInstalled()
    {
        var grid = new VoxelGrid(64, 64, 64);
        grid.SetBlock(20, 20, 20, BlockId.Stone);
        var tracker = new ChunkVersionTracker(grid);
        var far = new VoxelChunkCoord(3, 3, 3);
        var residentBefore = grid.ResidentChunkCount;

        var peeked = tracker.KeyOf(far);

        Assert.Equal(residentBefore, grid.ResidentChunkCount);
        Assert.False(grid.IsResident(far));
        _ = grid.GetChunk(far); // now it is an empty chunk object
        Assert.Equal(peeked, tracker.KeyOf(far));
    }

    [Fact]
    public void Given_TheValley_When_TheRainCoverIsRefreshed_Then_EveryColumnReadsAsTheDenseHighestBlockAndOnlyTheReachedColumnsHaveATile()
    {
        var grid = RouteScene.Build().Grid;
        var cover = new ColumnCover(grid);

        cover.Refresh(150, 110, 24);
        var reached = cover.TileCount;

        Assert.InRange(reached, 1, 16); // the square spans at most four chunk columns each way, nothing like the whole valley
        for (var z = 0; z < grid.SizeZ; z += 5)
        {
            for (var x = 0; x < grid.SizeX; x += 5)
            {
                Assert.Equal(OverheadCover.Top(grid.HighestBlock(x, z)), cover.TopAt(x, z));
            }
        }
    }

    [Fact]
    public void Given_AStreamedGridWithAGenerator_When_ChunksAreInstalledAndEvicted_Then_TheViewSetAndTheTrackerFollowThem()
    {
        var valley = RouteScene.Build().Grid;
        var grid = new VoxelGrid(valley.Limits, new ValleyGenerator(valley));
        var tracker = new ChunkVersionTracker(grid);
        grid.AddResidencyListener(tracker);
        var set = new ChunkViewSet(grid, 64);
        var eye = (X: 152f, Y: 75f, Z: 111f);
        Assert.Empty(set.Update(eye.X, eye.Y, eye.Z)); // nothing is resident: nothing to mesh, and nothing was generated
        Assert.Equal(0, grid.ResidentChunkCount);

        var ground = new VoxelChunkCoord(9, 4, 6);
        Assert.True(valley.TryGetChunk(ground, out var source) && !source.IsEmpty);
        _ = grid.GetChunk(ground); // installs it
        var wanted = set.Update(eye.X, eye.Y, eye.Z);

        Assert.Contains(ground, wanted); // a newly installed chunk is wanted, and due
        Assert.True(tracker.IsDue(ground));
        tracker.MarkMeshed(ground);
        Assert.False(tracker.IsDue(ground));
        Assert.True(tracker.HasMesh(ground));

        Assert.True(grid.TryEvictChunk(ground));

        Assert.DoesNotContain(ground, set.Update(eye.X, eye.Y, eye.Z)); // an evicted chunk is no longer wanted, and its mesh is forgotten
        Assert.False(tracker.HasMesh(ground));
        Assert.Equal(0, grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_ANeighbourThatIsInstalledLater_When_ItHoldsBlocksOnTheSharedEdge_Then_TheMeshedChunkBecomesDue()
    {
        var grid = new VoxelGrid(new WorldLimits(0, 0, 0, 64, 64, 64), new EdgeGenerator());
        var tracker = new ChunkVersionTracker(grid);
        var left = new VoxelChunkCoord(0, 0, 0);
        _ = grid.GetChunk(left);
        tracker.MarkMeshed(left);
        Assert.False(tracker.IsDue(left));

        _ = grid.GetChunk(new VoxelChunkCoord(1, 0, 0)); // a full chunk of stone beside it

        Assert.True(tracker.IsDue(left));
    }

    private sealed class EdgeGenerator : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord)
        {
            if (coord.X == 0)
            {
                return null;
            }

            var blocks = new BlockId[VoxelChunk.Volume];
            Array.Fill(blocks, BlockId.Stone);
            return new ChunkContent(blocks);
        }
    }

    [Fact]
    public void Given_ALightIndex_When_APostTorchIsPlacedAndRemoved_Then_TheIndexAndTheChunksItReachesChange()
    {
        var grid = new VoxelGrid(64, 64, 64);
        var index = new ChunkLightIndex(grid);
        Assert.Empty(index.All);

        grid.SetBlock(17, 5, 17, BlockId.PostTorch);

        Assert.Equal([new BlockPos(17, 5, 17)], index.All);
        Assert.Equal([new BlockPos(17, 5, 17)], index.Near(new VoxelChunkCoord(0, 0, 0))); // a light near the corner reaches the chunks beside its own
        Assert.Equal([new BlockPos(17, 5, 17)], index.TakeChanged());
        Assert.Empty(index.TakeChanged());
        Assert.Contains(new VoxelChunkCoord(0, 0, 0), ChunkLightIndex.ChunksReachedBy(new BlockPos(17, 5, 17)));
        Assert.Contains(new VoxelChunkCoord(1, 0, 1), ChunkLightIndex.ChunksReachedBy(new BlockPos(17, 5, 17)));
        Assert.DoesNotContain(new VoxelChunkCoord(3, 0, 3), ChunkLightIndex.ChunksReachedBy(new BlockPos(17, 5, 17)));

        grid.SetBlock(17, 5, 17, BlockId.Air);

        Assert.Empty(index.All);
        Assert.Equal(0, index.Count);
        Assert.Equal(0, index.ColumnCount);
        Assert.Equal([new BlockPos(17, 5, 17)], index.TakeChanged());
    }

    [Fact]
    public void Given_AStreamedGrid_When_AChunkWithALanternIsInstalledAndEvicted_Then_TheIndexHoldsItsLightOnlyWhileItIsResident()
    {
        var generator = new LanternGenerator();
        var grid = new VoxelGrid(new WorldLimits(0, 0, 0, 64, 64, 64), generator);
        var index = new ChunkLightIndex(grid);
        Assert.Empty(index.All);

        _ = grid.GetChunk(new VoxelChunkCoord(2, 1, 2));

        Assert.Equal([new BlockPos(32 + 3, 16 + 4, 32 + 5)], index.All);
        Assert.Single(index.TakeChanged());

        Assert.True(grid.TryEvictChunk(new VoxelChunkCoord(2, 1, 2)));

        Assert.Empty(index.All);
        Assert.Equal([new BlockPos(35, 20, 37)], index.TakeChanged());
    }

    private sealed class LanternGenerator : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord)
        {
            var blocks = new BlockId[VoxelChunk.Volume];
            blocks[(((4 * VoxelChunk.Size) + 5) * VoxelChunk.Size) + 3] = BlockId.Lantern; // x 3, y 4, z 5
            return new ChunkContent(blocks);
        }
    }

    [Fact]
    public void Given_ARainCoverOnAStreamedGrid_When_AChunkIsNotResident_Then_NothingIsGeneratedAndTheColumnIsOpen()
    {
        var generator = new LanternGenerator();
        var grid = new VoxelGrid(new WorldLimits(0, 0, 0, 64, 64, 64), generator);
        var cover = new ColumnCover(grid);

        cover.Refresh(32, 32, 20);

        Assert.Equal(0, grid.ResidentChunkCount);
        Assert.Equal(0, cover.TopAt(35, 37));

        _ = grid.GetChunk(new VoxelChunkCoord(2, 1, 2));
        cover.Refresh(32, 32, 20);

        Assert.Equal(16 + 4 + 1, cover.TopAt(35, 37)); // the lantern stands in the chunk's column now
    }
}
