using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The worker threads of the streamed world (B07): at most two in all, nearest job first, the residency's lane before the tiles', stale jobs skipped, and an inline mode for a run on one thread.</summary>
public sealed class JobPoolTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(7, 2)]
    [InlineData(-3, 0)]
    public void Given_ARequestedNumberOfWorkers_When_APoolIsMade_Then_ItHasAtMostTwo(int asked, int expected)
    {
        using var pool = new JobPool(asked);

        Assert.Equal(expected, pool.Workers);
        Assert.Equal(2, JobPool.MaxWorkers);
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
}
