using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Diagnostics;

/// <summary>B43: the voxel zone's stage counters that <c>--stream-stats</c> prints (generated, resident, meshed, on the card, drawn).</summary>
public sealed class PipelineCountersTests
{
    [Fact]
    public void Given_ChunksReachingAStageOverTime_When_TheTimesAreAsked_Then_HalfMostAndAllAreTheBucketsTheyWereReachedIn()
    {
        var counters = new PipelineCounters();
        Assert.True(counters.Track(0, 0, 1000));
        for (var i = 0; i < 10; i++)
        {
            counters.Add(PipelineStage.Meshed, 1, 1000 + (i * 1000) + 10); // one chunk a second, from 10 ms after the start
        }

        Assert.Equal(10, counters.Count(PipelineStage.Meshed));
        Assert.Equal(4050, counters.TimeTo(PipelineStage.Meshed, 0.5)); // the fifth chunk, at 4010 ms, is in the bucket that ends at 4050
        Assert.Equal(8050, counters.TimeTo(PipelineStage.Meshed, 0.9));
        Assert.Equal(9050, counters.TimeTo(PipelineStage.Meshed, 1));
        Assert.Null(counters.TimeTo(PipelineStage.Visible, 0.5));
    }

    [Fact]
    public void Given_ChunksCountedBeforeTheFirstFrame_When_TheStartBegins_Then_TheyAreKeptInItsFirstBucket()
    {
        var counters = new PipelineCounters();
        counters.Add(PipelineStage.Generated, 5, 10); // the near zone, made before the first frame

        Assert.True(counters.Track(0, 0, 5000));
        counters.Add(PipelineStage.Generated, 5, 5000 + 2000);

        Assert.Equal(10, counters.Count(PipelineStage.Generated));
        Assert.Equal(50, counters.TimeTo(PipelineStage.Generated, 0.5));
        Assert.Equal(2050, counters.TimeTo(PipelineStage.Generated, 1));
    }

    [Fact]
    public void Given_AStart_When_TheEyeWalksAndThenJumps_Then_OnlyTheJumpBeginsAnEpisodeAndTheTotalsKeepCounting()
    {
        var counters = new PipelineCounters();
        Assert.True(counters.Track(100, 100, 0));
        counters.Add(PipelineStage.Resident, 40, 500);
        Assert.False(counters.Track(130, 100, 16)); // a walk
        Assert.True(counters.Track(3000, 100, 32)); // a jump
        counters.Add(PipelineStage.Resident, 7, 1032);

        Assert.Equal(2, counters.Episodes);
        Assert.Equal(7, counters.Count(PipelineStage.Resident));
        Assert.Equal(47, counters.Total(PipelineStage.Resident));
        Assert.Equal(1050, counters.TimeTo(PipelineStage.Resident, 1)); // 1000 ms after the jump's frame: the bucket from 1000 to 1050
    }

    [Fact]
    public void Given_AnEpisode_When_ItsLineIsMade_Then_ItSaysEachStagesCountTimesAndRatesAndTheTotals()
    {
        var counters = new PipelineCounters();
        _ = counters.Track(0, 0, 0);
        _ = counters.Track(500, 0, 0); // a jump to 500,0
        for (var i = 0; i < 10; i++)
        {
            counters.Add(PipelineStage.Generated, 10, (i * 100) + 60); // 100 chunks over the first second
        }

        var line = counters.Line(2000);

        Assert.Equal(
            "pipeline: jump to 500,0, 2.0 s in: generated 100 (50/90/100% at 0.5/0.9/1.0 s, 100/s, 10-90% 100/s); resident 0; meshed 0; gpu-ready 0; visible 0; since the first frame (2.0 s): 100/0/0/0/0",
            line);
    }

    [Fact]
    public void Given_WarmCounters_When_ChunksAreAddedAndFramesTracked_Then_NothingIsAllocated()
    {
        var counters = new PipelineCounters();
        _ = counters.Track(0, 0, 0);
        counters.Add(PipelineStage.Visible, 1, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            _ = counters.Track(i * 0.01f, 0, i * 16);
            counters.Add((PipelineStage)(i % PipelineCounters.Stages), 3, i * 16);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Given_ManyThreadsAdding_When_TheyAreDone_Then_NoChunkIsLost()
    {
        var counters = new PipelineCounters();
        _ = counters.Track(0, 0, 0);

        _ = Parallel.For(0, 8, t =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                counters.Add(PipelineStage.Generated, 1, i % 3000);
            }
        });

        Assert.Equal(80_000, counters.Count(PipelineStage.Generated));
        Assert.Equal(80_000, counters.Total(PipelineStage.Generated));
        Assert.Equal(3000, counters.TimeTo(PipelineStage.Generated, 1));
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_AResidencyWithCounters_When_ItSettles_Then_EveryInstalledChunkIsResidentInTheCountersAndEveryGeneratedOneIsCounted()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, new BandStreamPolicy(5, 7, 2, 3), pool, scene.Places) { Counters = new PipelineCounters() };
        var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        _ = residency.Counters.Track(x, z, 0); // the times land in the last bucket: only the counts are checked here

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));

        Assert.Equal(residency.Stats.Installed, residency.Counters.Count(PipelineStage.Resident));
        Assert.Equal(scene.Grid.ResidentChunkCount, residency.Counters.Count(PipelineStage.Resident));
        Assert.True(residency.Counters.Count(PipelineStage.Generated) >= residency.Counters.Count(PipelineStage.Resident), "a chunk is generated before it is installed");
        Assert.NotNull(residency.Counters.TimeTo(PipelineStage.Resident, 1));
        Assert.Null(pool.Failure);
    }

    [Fact]
    public void Given_AMoveProbe_When_TheWholeViewIsMade_Then_ItSaysSoForThatFrameOnly()
    {
        var probe = new MoveProbe();
        _ = probe.Observe(100, 90, 10, 10, nearReady: true, nearMeshed: true);
        Assert.False(probe.ViewCompleted);
        Assert.NotNull(probe.Observe(200, 190, 10, 10, nearReady: true, nearMeshed: true, viewComplete: true));
        Assert.True(probe.ViewCompleted);
        Assert.Null(probe.Observe(300, 290, 10, 10, nearReady: true, nearMeshed: true, viewComplete: true));
        Assert.False(probe.ViewCompleted);
    }
}
