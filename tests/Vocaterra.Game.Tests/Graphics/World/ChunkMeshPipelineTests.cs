using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// Meshing off the game thread (B21): the meshes the workers build are the meshes of the blocks, an edit during a build is never shown from the old blocks, an edit near the eye shows in its own frame, and an edit costs
/// the view a handful of lookups, not a pass over it.
/// </summary>
public sealed class ChunkMeshPipelineTests
{
    private static VoxelChunkCoord ChunkOf(Vector3 eye) => VoxelChunkCoord.FromBlock((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z));

    // Flips a cell between air and planks (or stone where it holds planks), the way a player breaks and places.
    private static void Flip(VoxelGrid grid, int x, int y, int z) => Assert.True(grid.SetBlock(x, y, z, grid.GetBlock(x, y, z) == BlockId.Air ? BlockId.Planks : BlockId.Air));

    private static (int X, int Y, int Z) Middle(VoxelChunkCoord c) => ((c.X * 16) + 8, (c.Y * 16) + 8, (c.Z * 16) + 8);

    [Fact]
    public void Given_TheValleyView_When_ItIsFilledOnTwoWorkers_Then_EveryChunkHasTheMeshOfItsBlocksAndNoJobFailed()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool);

        var built = rig.Settle();

        Assert.True(built > 100, $"{built} meshes");
        Assert.Null(rig.Pipeline.Failure);
        Assert.Null(pool.Failure);
        Assert.Null(rig.FirstMismatch());
        Assert.Equal(0, rig.Pipeline.Pending);
        Assert.True(rig.Wanted.All(rig.Pipeline.IsMeshed));
    }

    [Fact]
    public void Given_NoWorkers_When_TheViewIsFilledInsideABudget_Then_EveryFrameBuildsAtLeastOneAndTheEndStateIsTheSame()
    {
        using var rig = MeshingRig.Valley(null);

        var frames = 0;
        do
        {
            var built = rig.Frame(0); // a budget no build fits in: still one a frame
            Assert.True(built <= 2, $"{built} meshes in a frame of no budget");
            frames++;
        }
        while (rig.Pipeline.Busy && frames < 20_000);

        Assert.True(frames > 50);
        Assert.Null(rig.FirstMismatch());
        Assert.False(rig.Pipeline.IsAsync);
    }

    [Fact]
    public void Given_AMeshInFlight_When_ABlockOfItsChunkChangesBeforeItIsTaken_Then_TheResultIsRefusedTheChunkIsBuiltAgainAndNeverShownFromTheOldBlocks()
    {
        using var pool = new JobPool(1);
        using var gate = new ManualResetEventSlim();
        pool.Submit(JobLane.Mesh, long.MinValue, static () => false, () => gate.Wait(TimeSpan.FromSeconds(60))); // holds the only worker, so the jobs the frame hands out wait
        using var rig = MeshingRig.Valley(pool);
        rig.Frame();
        Assert.True(rig.Pipeline.InFlight > 4, "jobs are waiting for the worker");
        var edited = rig.Wanted.Take(8).First(c => rig.Grid.GetChunk(c).NonAirCount < 4000);
        var stamp = rig.Pipeline.StampOf(edited);
        var (x, y, z) = Middle(edited);

        Flip(rig.Grid, x, y, z);
        Assert.NotEqual(stamp, rig.Pipeline.StampOf(edited));
        gate.Set();
        rig.Settle();

        Assert.True(rig.Pipeline.Refused >= 1, "the result made from the old blocks was refused");
        Assert.True(rig.Meshes.ContainsKey(edited) || rig.Grid.GetChunk(edited).IsEmpty);
        Assert.Null(rig.FirstMismatch());
        Assert.Null(rig.Pipeline.Failure);
    }

    [Fact]
    public void Given_ManyEditsAtCellsAcrossBordersAndCornersWhileJobsFly_When_TheViewSettles_Then_EveryChunksMeshMatchesItsBlocks()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool, 80);
        rig.Settle();
        var random = new Random(2026);
        var wanted = rig.Wanted.ToList();

        for (var round = 0; round < 90; round++)
        {
            var coord = wanted[random.Next(Math.Min(wanted.Count, 120))]; // near the eye, where the pipeline hurries
            // A cell: anywhere in the chunk, or on a face, an edge or a corner of it (the neighbours read those).
            int Pick() => random.Next(3) switch { 0 => 0, 1 => 15, _ => random.Next(16) };
            var (x, y, z) = ((coord.X * 16) + Pick(), (coord.Y * 16) + Pick(), (coord.Z * 16) + Pick());
            var block = random.Next(5) switch { 0 => BlockId.Water, 1 => BlockId.Torch, 2 => BlockId.Stone, 3 => BlockId.Leaves, _ => BlockId.Air };
            _ = rig.Grid.SetBlock(x, y, z, block);
            if (round % 3 == 0)
            {
                rig.Frame(2);
                Thread.Sleep(random.Next(3));
            }
        }

        rig.Settle();

        Assert.Null(rig.FirstMismatch());
        Assert.Null(rig.Pipeline.Failure);
        Assert.True(rig.Pipeline.Refused + rig.Pipeline.InlineEdits > 0, "the edits reached the pipeline");
    }

    [Fact]
    public void Given_ASettledViewAndAWorkerPool_When_ABlockIsBrokenNearTheEye_Then_TheNextFrameHoldsTheNewMeshOfItsChunkMadeOnTheGameThread()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool);
        rig.Settle();
        var here = ChunkOf(rig.Eye);
        var coord = rig.Wanted.Where(c => c.Y == here.Y).OrderBy(c => ChunkViewSet.DistanceSquared(rig.Eye.X, rig.Eye.Y, rig.Eye.Z, c.X, c.Y, c.Z)).First();
        var (x, y, z) = Middle(coord);
        var before = rig.Meshes[coord];

        Flip(rig.Grid, x, y, z);
        var inline = rig.Pipeline.InlineEdits;
        var built = rig.Frame(4);

        Assert.True(built >= 1);
        Assert.Equal(inline + 1, rig.Pipeline.InlineEdits);
        Assert.NotSame(before, rig.Meshes[coord]);
        Assert.Null(rig.FirstMismatch());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_ChunksWithGrassBlocks_When_TheirLayersAreBuiltThroughThePipeline_Then_EachIsTheLayerTheGrassMesherMakesOfTheGrid(int workers)
    {
        using var pool = new JobPool(workers);
        using var rig = MeshingRig.Valley(workers == 0 ? null : pool, 96);
        rig.Settle();
        var biomes = GrassBiomes.Shipped;
        var options = new GrassLayerOptions(1f, biomes, null, null, LightBand.Day, false);
        var chunks = rig.Wanted.Where(c => rig.Grid.TryGetChunk(c, out var chunk) && (chunk.KindMask & biomes.TopMask) != UInt128.Zero).Take(40).ToList();
        Assert.True(chunks.Count > 10, $"{chunks.Count} chunks with grass");

        foreach (var coord in chunks)
        {
            Assert.True(rig.Pipeline.SubmitGrass(coord, options, rig));
        }

        var started = Environment.TickCount64;
        while (rig.Grass.Count < chunks.Count)
        {
            _ = rig.Frame();
            Thread.Sleep(1);
            Assert.True(Environment.TickCount64 - started < 60_000, "the layers did not come");
        }

        foreach (var coord in chunks)
        {
            var direct = GrassMesher.Build(new GridBlockSource(rig.Grid, rig.Currents), coord.X, coord.Y, coord.Z, options);
            if (direct is null)
            {
                Assert.False(rig.Grass.ContainsKey(coord) && rig.Grass[coord].GrassStart is not null);
                continue;
            }

            var held = rig.Grass[coord];
            Assert.Equal(direct.Mesh.FaceCount, held.GrassMesh.Quads);
            Assert.True(held.GrassMesh.Vertices.AsSpan(0, direct.Mesh.VertexCount).SequenceEqual(direct.Mesh.Vertices), $"the grass of {coord} differs");
            Assert.Equal(direct.QuadrantStart, held.GrassStart);
            Assert.Equal(direct.Cumulative, held.GrassCumulative);
        }

        Assert.Null(rig.Pipeline.Failure);
    }

    [Fact]
    public void Given_ASettledView_When_ABlockInTheMiddleOfAChunkChanges_Then_ThereIsNoSortOfTheViewAndNoPassOverIt()
    {
        using var rig = MeshingRig.Valley(null);
        rig.Settle();
        Assert.True(rig.Wanted.Count > 200);
        var quietKeys = rig.Tracker.KeysComputed;
        var quietScanned = rig.Pipeline.Scanned;
        var rebuilds = rig.View.Rebuilds;
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(0, rig.Frame());
        }

        Assert.Equal(quietScanned, rig.Pipeline.Scanned); // a frame with nothing to do looks at nothing
        Assert.Equal(quietKeys, rig.Tracker.KeysComputed);
        Assert.Equal(rebuilds, rig.View.Rebuilds);

        var coords = rig.Wanted.Where(c => rig.Grid.GetChunk(c).NonAirCount is > 100 and < 3500).Take(30).ToList();
        foreach (var coord in coords)
        {
            var (x, y, z) = Middle(coord);
            Flip(rig.Grid, x, y, z);
            Assert.True(rig.Frame() >= 1);
        }

        Assert.Equal(rebuilds, rig.View.Rebuilds); // the list of what is in view was not made again
        Assert.True(rig.Tracker.KeysComputed - quietKeys <= coords.Count * 3, $"{rig.Tracker.KeysComputed - quietKeys} keys for {coords.Count} edits");
        Assert.True(rig.Pipeline.Scanned - quietScanned <= coords.Count * 8, $"{rig.Pipeline.Scanned - quietScanned} entries looked at for {coords.Count} edits");
        Assert.Null(rig.FirstMismatch());
    }

    [Fact]
    public void Given_AnEmptyChunkInView_When_ItsFirstBlockIsPlacedAndThenTaken_Then_TheViewListsItAndThenDropsIt()
    {
        using var rig = MeshingRig.Valley(null);
        rig.Settle();
        var here = ChunkOf(rig.Eye);
        var sky = new VoxelChunkCoord(here.X, here.Y + 2, here.Z);
        Assert.True(rig.Grid.TryGetChunk(sky, out var air) ? air.IsEmpty : true);
        Assert.DoesNotContain(sky, rig.Wanted);
        var rebuilds = rig.View.Rebuilds;

        Assert.True(rig.Grid.SetBlock((sky.X * 16) + 4, (sky.Y * 16) + 4, (sky.Z * 16) + 4, BlockId.Planks));
        rig.Settle();

        Assert.Equal(rebuilds + 1, rig.View.Rebuilds);
        Assert.Contains(sky, rig.Wanted);
        Assert.True(rig.Meshes.ContainsKey(sky));

        Assert.True(rig.Grid.SetBlock((sky.X * 16) + 4, (sky.Y * 16) + 4, (sky.Z * 16) + 4, BlockId.Air));
        rig.Settle();

        Assert.DoesNotContain(sky, rig.Wanted);
        Assert.False(rig.Meshes.ContainsKey(sky));
        Assert.Null(rig.FirstMismatch());
    }

    [Fact]
    public void Given_ASettledView_When_TheLightBandChanges_Then_EveryChunkIsBuiltAgainInTheNewBandNearestFirstWhileTheOldMeshesStayUntilReplaced()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool, 64);
        rig.Settle();
        var old = rig.Meshes.ToDictionary(p => p.Key, p => p.Value);
        rig.InstallOrder.Clear();

        rig.Band = LightBand.Night;
        rig.Pipeline.Band = LightBand.Night;
        rig.Pipeline.BandChanged();
        rig.Frame();
        Assert.True(rig.Meshes.Count >= old.Count, "nothing leaves the screen while its replacement is made");
        rig.Settle();

        Assert.All(rig.Wanted.Where(rig.Meshes.ContainsKey), c => Assert.Equal(LightBand.Night, rig.Meshes[c].Band));
        Assert.Null(rig.FirstMismatch());
        var distances = rig.InstallOrder.Where(c => rig.Wanted.Contains(c)).Select(c => ChunkViewSet.DistanceSquared(rig.Eye.X, rig.Eye.Y, rig.Eye.Z, c.X, c.Y, c.Z)).ToList();
        Assert.True(distances.Count > 20);
        Assert.True(distances.Take(10).Max() < distances.Skip(distances.Count - 10).Min(), "the nearest chunks come first");
    }

    [Fact]
    public void Given_AStreamedWorldSettled_When_ItsViewIsMeshedOnWorkers_Then_EveryChunkMatchesItsBlocksAndNothingWasGeneratedByTheMeshing()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, new BandStreamPolicy(8, 10, 2, 3), pool, scene.Places);
        var eye = new Vector3(scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        Assert.True(residency.Settle(eye.X, eye.Y, eye.Z));
        using var rig = new MeshingRig(scene.Grid, scene.Currents, pool, eye, 80);
        var generated = scene.Grid.ChunksGeneratedByReads;

        rig.Settle();

        Assert.True(rig.Meshes.Count > 50);
        Assert.Null(rig.FirstMismatch());
        Assert.Equal(generated, scene.Grid.ChunksGeneratedByReads);
        Assert.Null(rig.Pipeline.Failure);
    }

    [Fact]
    public void Given_ASettledView_When_FramesRunWithNothingToDo_Then_TheyAllocateNothing()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool, 64);
        rig.Settle();
        for (var i = 0; i < 50; i++)
        {
            _ = rig.Frame(4); // warm
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 300; i++)
        {
            _ = rig.Frame(4);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
