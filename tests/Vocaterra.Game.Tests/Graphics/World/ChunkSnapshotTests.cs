using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// The snapshot a worker meshes from (B21): everything the mesher reads of a chunk, copied on the game loop's thread. A mesh made from it is the mesh made from the grid, vertex for vertex, wherever it is made.
/// </summary>
public sealed class ChunkSnapshotTests
{
    private static readonly UInt128 Interesting = InterestingKinds();

    // The kinds that are not plain cubes (water, torches, lanterns, doors, fences, plants, beds, piles), so the sample holds the shapes that read beyond their cell.
    private static UInt128 InterestingKinds()
    {
        UInt128 mask = 0;
        foreach (var kind in Enum.GetValues<BlockId>())
        {
            if (ChunkMesher.ShapeOf(kind) != BlockShape.Cube && kind != BlockId.Air)
            {
                mask |= ChunkMesher.KindBit(kind);
            }
        }

        return mask;
    }

    private static List<VoxelChunkCoord> Sample(VoxelGrid grid)
    {
        var plain = 0;
        var chosen = new List<VoxelChunkCoord>();
        foreach (var chunk in grid.Chunks)
        {
            if (chunk.IsEmpty)
            {
                continue;
            }

            if ((chunk.KindMask & Interesting) != UInt128.Zero ? chosen.Count < 160 : plain++ % 25 == 0)
            {
                chosen.Add(chunk.Coord);
            }
        }

        return chosen;
    }

    [Fact]
    public void Given_ChunksOfTheValleyIncludingWaterLightsAndSmallShapes_When_MeshedFromASnapshotAndFromTheGrid_Then_TheMeshesAreTheSameVertexForVertex()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var lights = new ChunkLightIndex(grid);
        var source = new GridBlockSource(grid, scene.Currents);
        var sample = Sample(grid);
        Assert.True(sample.Count > 60, "the sample holds the shapes");
        Assert.Contains(sample, c => (grid.GetChunk(c).KindMask & ChunkMesher.KindBit(BlockId.Water)) != UInt128.Zero);

        foreach (var coord in sample)
        {
            var options = new MeshOptions(true, 100, lights.Near(coord), LightBand.Dusk);
            var direct = ChunkMesher.Build(source, coord.X, coord.Y, coord.Z, default, options);
            var snapshot = ChunkSnapshot.Capture(grid, coord, scene.Currents, options.Lights)!;
            var fromSnapshot = ChunkMesher.Build(snapshot, coord.X, coord.Y, coord.Z, default, options);

            Assert.True(Same(direct, fromSnapshot), $"the mesh of {coord} differs");
            Assert.Equal(ChunkMesher.IsBuried(source, grid.GetChunk(coord)), ChunkMesher.IsBuried(snapshot, coord, snapshot.NonAirCount, snapshot.KindMask));
        }
    }

    [Fact]
    public void Given_SnapshotsOfTheValley_When_MeshedOnTheWorkersAndOnTheGameThread_Then_TheMeshesAreByteIdentical()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var lights = new ChunkLightIndex(grid);
        var sample = Sample(grid).Take(80).ToList();
        var snapshots = sample.Select(c => (Coord: c, Snapshot: ChunkSnapshot.Capture(grid, c, scene.Currents, lights.Near(c))!, Options: new MeshOptions(true, 100, lights.Near(c)))).ToList();
        var inline = snapshots.Select(s => ChunkMesher.Build(s.Snapshot, s.Coord.X, s.Coord.Y, s.Coord.Z, default, s.Options)).ToList();
        var onWorkers = new ChunkMeshes[snapshots.Count];
        using var pool = new JobPool(2);
        var done = new CountdownEvent(snapshots.Count);
        for (var i = 0; i < snapshots.Count; i++)
        {
            var (coord, snapshot, options) = snapshots[i];
            var index = i;
            pool.Submit(JobLane.Mesh, i, static () => false, () =>
            {
                onWorkers[index] = ChunkMesher.Build(snapshot, coord.X, coord.Y, coord.Z, default, options);
                done.Signal();
            });
        }

        Assert.True(done.Wait(TimeSpan.FromMinutes(2)));
        Assert.Null(pool.Failure);
        for (var i = 0; i < snapshots.Count; i++)
        {
            Assert.True(Same(inline[i], onWorkers[i]), $"the mesh of {snapshots[i].Coord} differs between a worker and the game thread");
        }
    }

    [Fact]
    public void Given_ASnapshotOfAChunk_When_TheGridIsEditedAfterwards_Then_TheSnapshotStillHoldsTheBlocksOfItsMoment()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var coord = Sample(grid).First(c => grid.GetChunk(c).NonAirCount is > 500 and < 4000);
        var (x, y, z) = (coord.X * 16, coord.Y * 16, coord.Z * 16);
        var before = grid.GetBlock(x, y, z);
        var snapshot = ChunkSnapshot.Capture(grid, coord)!;

        Assert.True(grid.SetBlock(x, y, z, before == BlockId.Planks ? BlockId.Stone : BlockId.Planks));

        Assert.Equal(before, snapshot.Get(x, y, z));
        Assert.NotEqual(before, grid.GetBlock(x, y, z));
    }

    [Fact]
    public void Given_ABottomChunkOfTheValley_When_Snapshotted_Then_TheFloorUnderTheWorldReadsAsStoneAsTheRenderersSourceReadsIt()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var source = new GridBlockSource(grid, scene.Currents);
        var coord = grid.Chunks.First(c => c.Coord.Y == grid.Limits.MinChunkY && !c.IsEmpty).Coord;
        var snapshot = ChunkSnapshot.Capture(grid, coord)!;

        for (var z = coord.Z * 16; z < (coord.Z * 16) + 16; z++)
        {
            for (var x = coord.X * 16; x < (coord.X * 16) + 16; x++)
            {
                Assert.Equal(source.Get(x, -1, z), snapshot.Get(x, -1, z));
                Assert.Equal(BlockId.Stone, snapshot.Get(x, -1, z));
            }
        }
    }

    [Fact]
    public void Given_AStreamedWorldHalfResident_When_AChunkAtTheEdgeIsSnapshotted_Then_ItIsIncompleteAndAMissingNeighbourReadsAsAirAndNothingIsGenerated()
    {
        var scene = StreamedWorlds.Scene();
        var grid = scene.Grid;
        using var pool = new JobPool(0);
        var residency = new ChunkResidency(grid, scene.Streaming!, new BandStreamPolicy(5, 7, 2, 3), pool, scene.Places);
        var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        Assert.True(residency.Settle(x, y, z));
        var source = new GridBlockSource(grid, scene.Currents);
        var resident = grid.ResidentChunks.ToList();
        var inside = resident.First(c => !grid.GetChunk(c).IsEmpty && Neighbours(c).All(n => !grid.Limits.Contains(n) || grid.IsResident(n)));
        var edge = resident.First(c => !grid.GetChunk(c).IsEmpty && Neighbours(c).Any(n => grid.Limits.Contains(n) && !grid.IsResident(n)));
        var count = grid.ResidentChunkCount;
        var generated = grid.ChunksGeneratedByReads;

        var whole = ChunkSnapshot.Capture(grid, inside)!;
        var partial = ChunkSnapshot.Capture(grid, edge)!;

        Assert.True(whole.Complete);
        Assert.False(partial.Complete);
        Assert.Equal(count, grid.ResidentChunkCount);
        Assert.Equal(generated, grid.ChunksGeneratedByReads);
        var missing = Neighbours(edge).First(n => grid.Limits.Contains(n) && !grid.IsResident(n));
        // The cell of the missing neighbour next to the edge chunk reads as air, as the peek of the renderer's source reads it.
        var (cx, cy, cz) = ((missing.X * 16) + (missing.X < edge.X ? 15 : 0), (missing.Y * 16) + (missing.Y < edge.Y ? 15 : 0), (missing.Z * 16) + (missing.Z < edge.Z ? 15 : 0));
        Assert.Equal(BlockId.Air, partial.Get(cx, cy, cz));
        Assert.Equal(source.Get(cx, cy, cz), partial.Get(cx, cy, cz));
        Assert.Equal(source.Get(edge.X * 16, edge.Y * 16, edge.Z * 16), partial.Get(edge.X * 16, edge.Y * 16, edge.Z * 16));
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

    private static bool Same(ChunkMeshes a, ChunkMeshes b) =>
        a.Kinds == b.Kinds && Same(a.Opaque, b.Opaque) && Same(a.Alpha, b.Alpha) && Same(a.PlantMesh, b.PlantMesh) && Same(a.LitMesh, b.LitMesh) && Same(a.GlowMesh, b.GlowMesh);

    private static bool Same(MeshBuffer a, MeshBuffer b) => a.Vertices.SequenceEqual(b.Vertices);
}
