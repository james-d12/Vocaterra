using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The worker threads of the streamed world (B07): at most two in all, nearest job first, the residency's lane before the tiles', stale jobs skipped, and an inline mode for a run on one thread.</summary>
public sealed class JobPoolTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(7, 7)]
    [InlineData(8, 8)]
    [InlineData(50, 8)]
    [InlineData(-3, 0)]
    public void Given_ARequestedNumberOfWorkers_When_APoolIsMade_Then_ItHasAtMostTheSettingsEight(int asked, int expected)
    {
        using var pool = new JobPool(asked);

        Assert.Equal(expected, pool.Workers);
        Assert.Equal(8, JobPool.MaxWorkers);
    }

    [Fact]
    public void Given_APoolWithWorkers_When_ItIsResized_Then_ThreadsStartAndEndWithoutLosingAJob()
    {
        using var pool = new JobPool(2);
        var ran = 0;
        var threads = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
        using var gate = new ManualResetEventSlim();
        const int Jobs = 400;
        for (var i = 0; i < Jobs; i++)
        {
            pool.Submit(JobLane.Tiles, i, static () => false, () =>
            {
                threads[Environment.CurrentManagedThreadId] = true;
                _ = gate.Wait(60_000);
                _ = Interlocked.Increment(ref ran);
            });
        }

        Assert.Equal(8, pool.Resize(8));
        Assert.Equal(8, pool.Workers);

        // Every worker holds a job at the gate until all eight have started (a wait on the condition, bounded, not a sleep of a fixed length); a worker that has not started yet
        // would otherwise find the queue already drained by the others, and the count below would be taken before the threads exist.
        var started = Environment.TickCount64 + 30_000;
        while (threads.Count < 8 && Environment.TickCount64 < started)
        {
            Thread.Sleep(5);
        }

        Assert.True(threads.Count >= 8, $"only {threads.Count} of the 8 workers started a job within 30 s");
        gate.Set();
        Assert.Equal(1, pool.Resize(1)); // down again, mid-queue
        Assert.Equal(1, pool.Workers);
        var until = Environment.TickCount64 + 30_000;
        while (Volatile.Read(ref ran) < Jobs && Environment.TickCount64 < until)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(Jobs, ran); // every queued job ran once: no loss and no duplicate
        Assert.Equal(Jobs, pool.Completed);
        Assert.Equal(0, pool.Pending);
        Assert.InRange(threads.Count, 2, 8);
        Assert.Equal(8, pool.Resize(99)); // clamped to the bound
        Assert.Equal(1, pool.Resize(-4)); // and a pool with workers keeps one
    }

    [Fact]
    public void Given_APoolWithNoWorker_When_ItIsResized_Then_ItStaysInline()
    {
        using var pool = new JobPool(0);

        Assert.Equal(0, pool.Resize(6));
        Assert.Equal(0, pool.Workers);
    }

    [Fact]
    public void Given_AResizedPool_When_ItIsDisposed_Then_EveryThreadEnds()
    {
        var pool = new JobPool(4);
        _ = pool.Resize(8);
        _ = pool.Resize(2);
        _ = pool.Resize(6);

        pool.Dispose();

        Assert.True(pool.Pending == 0);
    }

    [Fact]
    public void Given_JobsInTwoLanesWithPriorities_When_TheyRunInline_Then_TheResidencyLaneGoesFirstAndEachLaneNearestFirst()
    {
        using var pool = new JobPool(0);
        var order = new List<string>();
        pool.Submit(JobLane.Tiles, 1, static () => false, () => order.Add("tile1"));
        pool.Submit(JobLane.Residency, 9, static () => false, () => order.Add("res9"));
        pool.Submit(JobLane.Residency, 2, static () => false, () => order.Add("res2"));
        pool.Submit(JobLane.Tiles, 0, static () => false, () => order.Add("tile0"));
        pool.Submit(JobLane.Residency, 5, static () => false, () => order.Add("res5"));

        var ran = pool.RunInline(null);

        Assert.Equal(5, ran);
        Assert.Equal(["res2", "res5", "res9", "tile0", "tile1"], order);
        Assert.Equal(0, pool.Pending);
        Assert.Equal(5, pool.Completed);
    }

    [Fact]
    public void Given_AStaleJob_When_ItIsReached_Then_ItIsSkippedAndCounted()
    {
        using var pool = new JobPool(0);
        var ran = 0;
        pool.Submit(JobLane.Tiles, 0, static () => true, () => ran++);
        pool.Submit(JobLane.Tiles, 1, static () => false, () => ran++);

        _ = pool.RunInline(null);

        Assert.Equal(1, ran);
        Assert.Equal(1, pool.Skipped);
        Assert.Equal(1, pool.Completed);
    }

    [Fact]
    public void Given_ABudget_When_JobsRunInline_Then_AtLeastOneRunsAndTheRestWait()
    {
        using var pool = new JobPool(0);
        for (var i = 0; i < 5; i++)
        {
            pool.Submit(JobLane.Residency, i, static () => false, static () => Thread.Sleep(5));
        }

        var ran = pool.RunInline(0.0);

        Assert.Equal(1, ran);
        Assert.Equal(4, pool.Pending);
    }

    [Fact]
    public void Given_TwoWorkers_When_ManyJobsAreSubmitted_Then_TheyRunOnAtMostTwoThreadsOtherThanTheCallers()
    {
        using var pool = new JobPool(2);
        var threads = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
        using var done = new CountdownEvent(60);
        for (var i = 0; i < 60; i++)
        {
            pool.Submit(JobLane.Residency, i, static () => false, () =>
            {
                threads[Environment.CurrentManagedThreadId] = true;
                Thread.Sleep(1);
                _ = done.Signal();
            });
        }

        Assert.True(done.Wait(TimeSpan.FromSeconds(30)));
        Assert.InRange(threads.Count, 1, 2);
        Assert.DoesNotContain(Environment.CurrentManagedThreadId, threads.Keys);
        Assert.Null(pool.Failure);
    }

    [Fact]
    public void Given_AJobThatThrows_When_ItRuns_Then_TheFailureIsKeptAndTheWorkerGoesOn()
    {
        using var pool = new JobPool(0);
        var after = 0;
        pool.Submit(JobLane.Residency, 0, static () => false, static () => throw new InvalidOperationException("pure jobs do not throw"));
        pool.Submit(JobLane.Residency, 1, static () => false, () => after++);

        _ = pool.RunInline(null);

        Assert.IsType<InvalidOperationException>(pool.Failure);
        Assert.Equal(1, after);
    }

    [Fact]
    public void Given_AStreamOfTileJobsAndOneRingJob_When_TheyRun_Then_TheRingJobIsNotLeftToTheEnd()
    {
        using var pool = new JobPool(0);
        var order = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            var n = i;
            pool.Submit(JobLane.Tiles, n, static () => false, () => order.Add("tile" + n));
        }

        pool.Submit(JobLane.Ring, 0, static () => false, () => order.Add("ring"));

        _ = pool.RunInline(null);

        Assert.InRange(order.IndexOf("ring"), 0, 5);
        Assert.Equal(13, order.Count);
    }
}
