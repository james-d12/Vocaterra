using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>
/// The residency with packages in flight (B21, review C4 and C5): a jump leaves the old place's jobs running, a quality change leaves the old radii's, and neither may lose a column, make one twice, install what the plan does not
/// hold or leave a chunk half installed; and the settled driver's frames, and the grid's reads, make no garbage and no chunk.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ResidencyInFlightTests
{
    private static (float X, float Y, float Z) Eye(Scene scene) => (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);

    private static HashSet<VoxelChunkCoord> Resident(Scene scene) => [.. GridResidency.Resident(scene.Grid)];

    private static int ResidentColumns(Scene scene) => Resident(scene).Select(c => (c.X, c.Z)).Distinct().Count();

    // Holds the pool's only worker until the gate opens, so every column job submitted meanwhile waits in the queue.
    private static ManualResetEventSlim Hold(JobPool pool)
    {
        var gate = new ManualResetEventSlim();
        pool.Submit(JobLane.Residency, long.MinValue, static () => false, () => gate.Wait(HangGuard.Scale(TimeSpan.FromSeconds(60))));
        return gate;
    }

    // Makes the bands of the columns round a chunk column (the plan reads them) so no band job waits behind the held worker: the plan is made at once and the column jobs are what is in flight.
    private static void Warm(ChunkResidency residency, float x, float z, int radius)
    {
        var (cx, cz) = ((int)MathF.Floor(x / 16), (int)MathF.Floor(z / 16));
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                _ = residency.Bands.Get(cx + dx, cz + dz);
            }
        }
    }

    [Fact]
    public void Given_ColumnJobsInFlightAtOnePlace_When_ThePlayerJumpsFarAndTheOldJobsRun_Then_NoOldColumnIsMadeNoChunkIsDoubleCountedAndTheNewPlacesPlanIsWhatIsResident()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(1);
        var policy = new BandStreamPolicy(11, 13, 2, 3);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, policy, pool, scene.Places);
        var (x, y, z) = Eye(scene);
        var jumpedX = x + (9 * 16); // nine columns on: a jump, and near enough that the new place wants the old place's columns too
        Warm(residency, x, z, 16);
        Warm(residency, jumpedX, z, 16);
        using var gate = Hold(pool);

        residency.Update(x, y, z, null);
        residency.Update(x, y, z, null);
        var flyingBefore = residency.Flying;
        Assert.True(flyingBefore > 4, $"{flyingBefore} column jobs are in flight at the first place");
        residency.Update(jumpedX, y, z, null);
        residency.Update(jumpedX, y, z, null);
        Assert.True(residency.Flying > flyingBefore, "the new place's jobs are in flight beside the old place's");
        Assert.Equal(residency.Stats.InFlight, residency.Flying - flyingBefore);
        gate.Set();

        HangGuard.Settled(limit => residency.Settle(jumpedX, y, z, limit));

        var plan = residency.Plan!;
        Assert.Equal(0, residency.Flying);
        Assert.Equal(0, residency.Stats.InFlight);
        Assert.All(plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
        Assert.All(Resident(scene), chunk => Assert.True(plan.Holds(chunk)));
        Assert.Equal(ResidentColumns(scene), residency.ColumnsGenerated); // each column was made once: the old place's jobs did nothing
        Assert.Equal(residency.Stats.Installed - residency.Stats.Evicted, scene.Grid.ResidentChunkCount); // and no chunk was counted twice
        Assert.Equal(0, scene.Grid.ChunksGeneratedByReads);
        Assert.Null(pool.Failure);
    }

    [Fact]
    public void Given_ColumnJobsInFlight_When_TheRadiiChangeToAnotherQuality_Then_WhatWasMadeIsUsedNothingIsMadeTwiceAndTheNewPlanIsResident()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(1);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, new BandStreamPolicy(6, 8, 2, 3), pool, scene.Places);
        var (x, y, z) = Eye(scene);
        Warm(residency, x, z, 16);
        using var gate = Hold(pool);
        residency.Update(x, y, z, null);
        residency.Update(x, y, z, null);
        Assert.True(residency.Flying > 4);
        var flying = residency.Flying;

        residency.Reconfigure(new BandStreamPolicy(9, 11, 3, 4), int.MaxValue);
        gate.Set();
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));

        Assert.Equal(new BandStreamPolicy(9, 11, 3, 4), residency.Policy);
        var plan = residency.Plan!;
        Assert.All(plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
        Assert.All(Resident(scene), chunk => Assert.True(plan.Holds(chunk)));
        Assert.Equal(ResidentColumns(scene), residency.ColumnsGenerated);
        Assert.Equal(0, residency.Stats.Dropped);
        Assert.True(flying > 4);

        // Down again: what was made for the wider radii is let go by the narrower plan, and nothing in flight is lost.
        residency.Reconfigure(new BandStreamPolicy(5, 7, 2, 3), int.MaxValue);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.All(Resident(scene), chunk => Assert.True(residency.Plan!.Holds(chunk)));
        Assert.All(residency.Plan!.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
        Assert.Equal(residency.Stats.Installed - residency.Stats.Evicted, scene.Grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_ColumnJobsInFlight_When_TheCapDropsBelowWhatTheRadiiHold_Then_NothingMoreIsAskedForTheNearestLandIsKeptAndNoChunkIsHalfInstalled()
    {
        // B24 (review T1 and C8): a quality change can lower the cap while jobs are in flight. The jobs already made are installed whole (a few chunks past the cap at most), nothing new is asked for, and the
        // chunks round the eye are the ones that stay.
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(1);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, new BandStreamPolicy(8, 10, 2, 3), pool, scene.Places);
        var (x, y, z) = Eye(scene);
        Warm(residency, x, z, 16);
        using var gate = Hold(pool);
        residency.Update(x, y, z, null);
        residency.Update(x, y, z, null);
        Assert.True(residency.Flying > 4);

        var flying = residency.Flying;
        const int Cap = 200;
        residency.Reconfigure(new BandStreamPolicy(8, 10, 2, 3), Cap);
        gate.Set();
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));

        var plan = residency.Plan!;
        var count = scene.Grid.ResidentChunkCount;
        Assert.InRange(count, 1, Cap + (flying * 12)); // what was in flight is installed whole, so the count may pass the cap by the chunks of those columns, no more
        Assert.All(Resident(scene), chunk => Assert.True(plan.Holds(chunk)));
        Assert.Equal(residency.Stats.Installed - residency.Stats.Evicted, count);
        var (cx, cz) = ((int)MathF.Floor(x / 16), (int)MathF.Floor(z / 16));
        Assert.Contains(Resident(scene), c => c.X == cx && c.Z == cz); // the eye's own column is resident

        var made = residency.ColumnsGenerated;
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.Equal(made, residency.ColumnsGenerated); // over the cap, nothing more is asked for
        Assert.Equal(count, scene.Grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_AHostWithJobsInFlight_When_TheQualityChanges_Then_TheSameResidencyGoesOnAndSettlesAtTheNewDistance()
    {
        var scene = StreamedWorlds.Scene();
        var low = QualityPreset.For(QualityLevel.Low).WithViewDistance(64).WithFarRange(0);
        var wider = low.WithViewDistance(112);
        using var host = StreamedWorldHost.For(scene, low, 2)!;
        var (x, y, z) = Eye(scene);
        var residency = host.Residency;
        host.Update(x, y, z, _ => { });
        host.Update(x, y, z, _ => { });

        host.ApplyPreset(wider, _ => { });

        Assert.Same(residency, host.Residency);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.Equal(StreamedWorldHost.PolicyFor(112), residency.Policy);
        Assert.All(residency.Plan!.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
        Assert.All(Resident(scene), chunk => Assert.True(residency.Plan!.Holds(chunk)));
        Assert.Equal(residency.Stats.Installed - residency.Stats.Evicted, scene.Grid.ResidentChunkCount);
        Assert.Equal(0, scene.Grid.ChunksGeneratedByReads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_ASettledResidency_When_FramesRunAtTheSameEye_Then_TheyAllocateNothingOnTheGameThread(int workers)
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(workers);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, new BandStreamPolicy(6, 8, 2, 3), pool, scene.Places);
        var (x, y, z) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        for (var i = 0; i < 60; i++)
        {
            residency.Update(x, y, z, 1.5); // warm
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 400; i++)
        {
            residency.Update(x, y, z, 1.5);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Given_AStreamedGridWithSomeChunksResident_When_TheHighestBlockOfColumnsIsAsked_Then_NothingIsInstalledNothingIsGeneratedByTheReadAndTheAnswerIsTheGeneratorsOrTheEdits()
    {
        var scene = StreamedWorlds.Scene();
        var grid = scene.Grid;
        using var pool = new JobPool(0);
        var residency = new ChunkResidency(grid, scene.Streaming!, new BandStreamPolicy(5, 7, 2, 3), pool, scene.Places);
        var (ex, ey, ez) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(ex, ey, ez, limit));
        var (cx, cz) = ((int)MathF.Floor(ex), (int)MathF.Floor(ez));
        var count = grid.ResidentChunkCount;
        var version = grid.ResidencyVersion;
        var generated = grid.ChunksGeneratedByReads;
        var columns = new List<(int X, int Z)>();
        for (var i = 0; i < 12; i++)
        {
            columns.Add((cx + (i * 7) - 40, cz + (i * 5) - 30)); // some resident, some far past the residency
            columns.Add((cx + 400 + (i * 31), cz - 300 + (i * 23))); // none resident
        }

        foreach (var (x, z) in columns)
        {
            Assert.Equal(Generated(scene, x, z), grid.HighestBlock(x, z));
            Assert.Equal(Generated(scene, x, z), grid.HighestBlock(x, z)); // and again: what the generator made as air is remembered
        }

        Assert.Equal(count, grid.ResidentChunkCount);
        Assert.Equal(version, grid.ResidencyVersion);
        Assert.Equal(generated, grid.ChunksGeneratedByReads);

        // A block a player placed high in a resident column is the highest block there.
        var top = grid.HighestBlock(cx, cz);
        Assert.True(grid.IsResident(VoxelChunkCoord.FromBlock(cx, top + 3, cz)));
        Assert.True(grid.SetBlock(cx, top + 3, cz, BlockId.Planks));
        Assert.Equal(top + 3, grid.HighestBlock(cx, cz));
        Assert.Equal(generated, grid.ChunksGeneratedByReads);
    }

    // The highest block of a column in what the generator makes, layer by layer from the top (nothing installed).
    private static int Generated(Scene scene, int x, int z)
    {
        var source = scene.Streaming!.Source;
        var limits = scene.Grid.Limits;
        for (var cy = limits.EndChunkY - 1; cy >= limits.MinChunkY; cy--)
        {
            var chunk = source.Generate(new VoxelChunkCoord(x >> 4, cy, z >> 4));
            if (chunk.IsEmpty)
            {
                continue;
            }

            for (var ly = 15; ly >= 0; ly--)
            {
                if (chunk[x & 15, ly, z & 15] != BlockId.Air)
                {
                    return (cy * 16) + ly;
                }
            }
        }

        return limits.MinY - 1;
    }
}
