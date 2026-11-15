using Vocaterra.Game.Tests.Streaming;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// What the renderer draws of a streamed world (B07): a chunk is listed, and so meshed, only when all 26 chunks round it are resident (a chunk that is not reads as air, which would draw a wall at the seam of
/// a half-loaded world); the distance is measured across, so a peak stays in view while its foot is; the list is capped; and meshing never generates.
/// </summary>
public sealed class StreamedViewSetTests
{
    private sealed class GeneratingSource(VoxelGrid grid) : IBlockSource
    {
        public BlockId Get(int x, int y, int z) => y < grid.Limits.MinY ? BlockId.Stone : grid.GetBlock(x, y, z); // a read that generates; the floor under the world is stone, as the renderer's source has it

        public WaterLevel GetWater(int x, int y, int z) => grid.GetWater(x, y, z);

        public FlowVector FlowAt(int x, int y, int z) => FlowVector.At(grid, x, y, z);
    }

    private static readonly BandStreamPolicy Small = new(7, 9, 2, 3);

    private static (Vocaterra.Game.Core.World.Scene Scene, ChunkResidency Residency, float X, float Y, float Z) Settled(JobPool pool)
    {
        var scene = StreamedWorlds.Scene();
        var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, Small, pool, scene.Places);
        Assert.True(residency.Settle(x, y, z));
        return (scene, residency, x, y, z);
    }

    private static IEnumerable<VoxelChunkCoord> Neighbours(VoxelChunkCoord c)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0 || dy != 0 || dz != 0)
                    {
                        yield return new VoxelChunkCoord(c.X + dx, c.Y + dy, c.Z + dz);
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_ASettledStreamedWorld_When_TheViewSetIsAsked_Then_EveryChunkListedHasAllItsNeighboursResidentAndIsInViewAcross()
    {
        using var pool = new JobPool(0);
        var (scene, _, x, y, z) = Settled(pool);
        var set = new ChunkViewSet(scene.Grid, 96);

        var wanted = set.Update(x, y, z);

        Assert.True(wanted.Count > 100);
        foreach (var coord in wanted)
        {
            Assert.True(scene.Grid.TryGetChunk(coord, out var chunk) && !chunk.IsEmpty);
            Assert.All(Neighbours(coord).Where(scene.Grid.Limits.Contains), n => Assert.True(scene.Grid.IsResident(n), $"{coord} is listed but its neighbour {n} is not resident"));
            Assert.True(ChunkViewSet.AcrossDistanceSquared(x, z, coord.X, coord.Z) <= 96L * 96);
        }

        Assert.Equal(wanted.Count, wanted.Distinct().Count());
    }

    [Fact]
    public void Given_AHalfLoadedWorld_When_ChunksComeAndGo_Then_AChunkIsListedExactlyWhileItsWholeNeighbourhoodIsResident()
    {
        var scene = StreamedWorlds.Scene();
        var grid = scene.Grid;
        var source = scene.Streaming!.Source;
        var set = new ChunkViewSet(grid, 200);
        var layer = Enumerable.Range(0, 8).Last(y => !source.Generate(new VoxelChunkCoord(12, y, 12)).IsEmpty);
        var centre = new VoxelChunkCoord(12, layer, 12); // the top of the valley's ground at its middle: not empty
        Assert.False(source.Generate(centre).IsEmpty);
        var all = Neighbours(centre).Append(centre).ToList();
        foreach (var coord in all.Where(c => c.X != 13 || c.Y != layer + 1 || c.Z != 13))
        {
            Assert.True(GridResidency.Install(grid, source.Generate(coord)));
        }

        var eye = (X: (12 * 16) + 8f, Y: (layer * 16) + 8f, Z: (12 * 16) + 8f);
        Assert.DoesNotContain(centre, set.Update(eye.X, eye.Y, eye.Z)); // one neighbour is missing
        Assert.False(set.IsReady(centre));

        Assert.True(GridResidency.Install(grid, source.Generate(new VoxelChunkCoord(13, layer + 1, 13)))); // the last one comes
        Assert.Contains(centre, set.Update(eye.X, eye.Y, eye.Z));
        Assert.True(set.IsReady(centre));

        Assert.True(GridResidency.TryEvict(grid, new VoxelChunkCoord(11, layer - 1, 11))); // one goes: the chunk is held back again
        Assert.DoesNotContain(centre, set.Update(eye.X, eye.Y, eye.Z));
        Assert.Equal(set.ReadyChunks, all.Count(set.IsReady));
    }

    [Fact]
    public void Given_ACapOnTheChunks_When_TheViewSetIsAsked_Then_OnlyTheNearestAreListed()
    {
        using var pool = new JobPool(0);
        var (scene, _, x, y, z) = Settled(pool);
        var all = new ChunkViewSet(scene.Grid, 96).Update(x, y, z);
        var capped = new ChunkViewSet(scene.Grid, 96, 40).Update(x, y, z);

        Assert.Equal(40, capped.Count);
        Assert.Equal(all.Take(40), capped);
    }

    [Fact]
    public void Given_APeakHighAboveTheEye_When_ItsFootIsInViewAcross_Then_ItsChunksAreListedWhateverTheirHeight()
    {
        var scene = StreamedWorlds.Scene();
        var stone = new BlockId[VoxelChunk.Volume];
        Array.Fill(stone, BlockId.Stone);
        var peak = new VoxelChunkCoord(21, 17, 12); // 272 blocks up, nine chunks across: a high chunk of a mountain
        foreach (var coord in Neighbours(peak).Append(peak))
        {
            Assert.True(GridResidency.Install(scene.Grid, ChunkData.From(coord, stone, [])));
        }

        var eye = (X: (12 * 16) + 8f, Y: 70f, Z: (12 * 16) + 8f);
        var wanted = new ChunkViewSet(scene.Grid, 200).Update(eye.X, eye.Y, eye.Z);

        Assert.True(ChunkViewSet.DistanceSquared(eye.X, eye.Y, eye.Z, peak.X, peak.Y, peak.Z) > 200L * 200, "in three dimensions the peak is past the view");
        Assert.Contains(peak, wanted);
    }

    [Fact]
    public void Given_ReadyChunksOfAStreamedWorld_When_TheyAreMeshedByPeeking_Then_NothingIsGeneratedAndEachMeshEqualsTheOneInAFullyGeneratedWorld()
    {
        using var pool = new JobPool(0);
        var (scene, _, x, y, z) = Settled(pool);
        var reference = StreamedWorlds.Scene();
        var wanted = new ChunkViewSet(scene.Grid, 96).Update(x, y, z);
        var peeking = new GridBlockSource(scene.Grid);
        var generating = new GeneratingSource(reference.Grid);
        var before = scene.Grid.ResidentChunkCount;
        var compared = 0;

        foreach (var coord in wanted.Where((_, i) => i % 9 == 0))
        {
            var expected = ChunkMesher.Build(generating, coord.X, coord.Y, coord.Z, 0, new MeshOptions(true, 100, null, LightBand.Day));
            var actual = ChunkMesher.Build(peeking, coord.X, coord.Y, coord.Z, 0, new MeshOptions(true, 100, null, LightBand.Day));
            Assert.True(expected.Opaque.Vertices.SequenceEqual(actual.Opaque.Vertices), $"opaque mesh of {coord}");
            Assert.True(expected.Alpha.Vertices.SequenceEqual(actual.Alpha.Vertices), $"alpha mesh of {coord}");
            Assert.True(expected.PlantMesh.Vertices.SequenceEqual(actual.PlantMesh.Vertices), $"plant mesh of {coord}");
            compared++;
        }

        Assert.True(compared > 20);
        Assert.Equal(before, scene.Grid.ResidentChunkCount); // peeking never generates
    }
}
