using Vocaterra.Game.Diagnostics;

namespace Vocaterra.Game.Streaming;

/// <summary>Which queue a job waits in: the residency's jobs always go before the far view's.</summary>
public enum JobLane
{
    /// <summary>The bands and the columns the voxel zone needs now.</summary>
    Residency = 0,

    /// <summary>The far view's terrain tiles.</summary>
    Tiles = 1,

    /// <summary>B07c: the real chunks the far view's store takes in, after the tiles (a tile is what is drawn; the ring only sharpens it).</summary>
    Ring = 2,
}

/// <summary>
/// The worker threads of the streamed world (B07, biomes plan section 3.7): at most <see cref="MaxWorkers"/> of them in all, shared by the chunk prefetcher and the far view's tile builds. A job is
/// a pure computation over the world's pure source (it never touches the voxel grid, the renderer or the simulation) whose result it hands to a queue the game loop drains. The queue is a priority
/// queue per lane, nearest first (a lower number first), the residency lane before the tile lane; a job that is stale by the time a worker reaches it is skipped. With no worker the pool still
/// takes jobs and <see cref="RunInline"/> runs them on the caller's thread within a time budget, which is how the tests and the scripted runs keep a run on one thread.
/// </summary>
public sealed class JobPool : IDisposable
{
    /// <summary>The most worker threads there ever are (the owner's limit, with the game loop's own thread on top).</summary>
    public const int MaxWorkers = 2;

    private readonly object _gate = new();
    private readonly PriorityQueue<Job, long>[] _queues = [new(), new(), new()];
    private readonly List<Thread> _threads = [];
    private int _running;
    private bool _disposed;
    private long _completed;
    private long _skipped;
    private Exception? _failure;

    /// <summary>Creates a pool and starts its workers.</summary>
    /// <param name="workers">How many worker threads, 0 to <see cref="MaxWorkers"/> (more is clamped): 0 runs jobs only through <see cref="RunInline"/>.</param>
    public JobPool(int workers)
    {
        Workers = Math.Clamp(workers, 0, MaxWorkers);
        for (var i = 0; i < Workers; i++)
        {
            var thread = new Thread(Work) { IsBackground = true, Name = $"vocaterra-stream-{i}", Priority = ThreadPriority.BelowNormal };
            _threads.Add(thread);
            thread.Start();
        }
    }

    /// <summary>How many worker threads run.</summary>
    public int Workers { get; }

    /// <summary>How many jobs wait or run.</summary>
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _queues[0].Count + _queues[1].Count + _queues[2].Count + _running;
            }
        }
    }

    /// <summary>How many jobs ran to the end.</summary>
    public long Completed => Interlocked.Read(ref _completed);

    /// <summary>How many jobs were skipped as stale.</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>The first exception a job threw (jobs are pure, so none should), or <see langword="null"/>.</summary>
    public Exception? Failure
    {
        get
        {
            lock (_gate)
            {
                return _failure;
            }
        }
    }

    /// <summary>Queues a job.</summary>
    /// <param name="lane">Its queue.</param>
    /// <param name="priority">Its place in the queue: the lowest first (a squared distance from the player).</param>
    /// <param name="isStale">Asked when a worker reaches the job: true skips it (the player moved on). Called on the worker's thread.</param>
    /// <param name="work">The job. It runs on a worker's thread, or on the caller's through <see cref="RunInline"/>.</param>
    public void Submit(JobLane lane, long priority, Func<bool> isStale, Action work)
    {
        ArgumentNullException.ThrowIfNull(isStale);
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _queues[(int)lane].Enqueue(new Job(isStale, work), priority);
            Monitor.Pulse(_gate);
        }
    }

    /// <summary>
    /// Runs queued jobs on the caller's thread until the queue is empty or the budget is spent (always at least one when any waits). Meant for a pool with no workers; with workers it simply
    /// helps them.
    /// </summary>
    /// <param name="budgetMilliseconds">The most time to spend, or <see langword="null"/> to run everything that waits.</param>
    /// <returns>How many jobs it took.</returns>
    public int RunInline(double? budgetMilliseconds)
    {
        var started = MonotonicClock.Milliseconds();
        var ran = 0;
        while (true)
        {
            Job? job;
            lock (_gate)
            {
                job = TakeLocked();
            }

            if (job is null)
            {
                return ran;
            }

            Run(job);
            ran++;
            if (budgetMilliseconds is { } budget && MonotonicClock.Milliseconds() - started >= budget)
            {
                return ran;
            }
        }
    }

    /// <summary>Drops the jobs still queued in a lane (a restart of the far view).</summary>
    /// <param name="lane">The lane.</param>
    public void Clear(JobLane lane)
    {
        lock (_gate)
        {
            _queues[(int)lane].Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _queues[0].Clear();
            _queues[1].Clear();
            _queues[2].Clear();
            Monitor.PulseAll(_gate);
        }

        foreach (var thread in _threads)
        {
            _ = thread.Join(2000);
        }
    }

    private void Work()
    {
        while (true)
        {
            Job? job;
            lock (_gate)
            {
                while ((job = TakeLocked()) is null)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _ = Monitor.Wait(_gate);
                }
            }

            Run(job);
        }
    }

    // Takes the next job (the residency lane first), counting it as running; the lock is held.
    private Job? TakeLocked()
    {
        foreach (var queue in _queues)
        {
            if (queue.TryDequeue(out var job, out _))
            {
                _running++;
                return job;
            }
        }

        return null;
    }

    private void Run(Job job)
    {
        try
        {
            if (job.IsStale())
            {
                _ = Interlocked.Increment(ref _skipped);
            }
            else
            {
                job.Work();
                _ = Interlocked.Increment(ref _completed);
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _failure ??= ex;
            }
        }
        finally
        {
            lock (_gate)
            {
                _running--;
            }
        }
    }

    private sealed record Job(Func<bool> IsStale, Action Work);
}
