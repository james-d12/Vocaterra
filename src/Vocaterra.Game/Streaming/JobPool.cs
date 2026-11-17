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

    /// <summary>B21: the voxel chunks' meshes (and grass layers), built from a snapshot of the blocks the game loop took: before the residency's jobs, but after every <see cref="JobPool.MeshRun"/> of them one residency job goes first, so the columns the meshes wait for are made too.</summary>
    Mesh = 3,
}

/// <summary>
/// The worker threads of the streamed world (B07, biomes plan section 3.7): at most <see cref="MaxWorkers"/> of them in all (the player's Worker threads setting, B07d: Auto or 1 to 8; a scripted run keeps two), shared by the residency's column jobs, the far view's tile and ring jobs and the chunk meshes (B21). A job is
/// a pure computation over the world's pure source (it never touches the voxel grid, the renderer or the simulation) whose result it hands to a queue the game loop drains. The queue is a priority
/// queue per lane, nearest first (a lower number first), the residency lane before the tile lane, and the voxel chunks' mesh lane (B21: a pure computation over a snapshot of the blocks the game loop took) before both, up to <see cref="MeshRun"/> jobs in a row; a job that is stale by the time a worker reaches it is skipped. With no worker the pool still
/// takes jobs and <see cref="RunInline"/> runs them on the caller's thread within a time budget, which is how the tests and the scripted runs keep a run on one thread.
/// </summary>
public sealed class JobPool : IDisposable
{
    /// <summary>The most worker threads there ever are, the bound of the Worker threads setting (D-game-34 item 42; the game loop's own thread is on top of them). Before B07d it was two.</summary>
    public const int MaxWorkers = 8;

    /// <summary>The worker threads of a scripted run, of the tests and of <c>--prefetch</c> without a count: the small number their results and the machine's limits were made for.</summary>
    public const int ScriptedWorkers = 2;

    private const int RingEvery = 4;

    /// <summary>How many mesh jobs in a row the workers take before a residency job, if one waits, gets its turn (B21).</summary>
    public const int MeshRun = 4;

    private readonly object _gate = new();
    private readonly PriorityQueue<Job, long>[] _queues = [new(), new(), new(), new()];
    private readonly List<Thread> _threads = [];
    private readonly List<bool> _live = [];
    private readonly List<Thread> _retired = [];
    private int _running;
    private volatile int _workers;
    private int _sinceRing;
    private int _meshRun;
    private bool _disposed;
    private long _completed;
    private long _skipped;
    private Exception? _failure;

    /// <summary>Creates a pool and starts its workers.</summary>
    /// <param name="workers">How many worker threads, 0 to <see cref="MaxWorkers"/> (more is clamped): 0 runs jobs only through <see cref="RunInline"/>.</param>
    public JobPool(int workers)
    {
        _workers = Math.Clamp(workers, 0, MaxWorkers);
        lock (_gate)
        {
            StartMissingLocked();
        }
    }

    /// <summary>How many worker threads run (or are to: a thread that is told to stop ends after the job it is on).</summary>
    public int Workers => _workers;

    /// <summary>
    /// Changes the number of worker threads while the game runs (the Worker threads setting): new threads start at once, and surplus ones end after the job each is on, so no queued job is lost and none is run twice. A pool made with no worker (a run on one thread: the tests,
    /// <c>--prefetch 0</c>) stays that way, and a pool with workers keeps at least one.
    /// </summary>
    /// <param name="workers">The number of threads wanted, 1 to <see cref="MaxWorkers"/> (clamped).</param>
    /// <returns>The number the pool now has.</returns>
    public int Resize(int workers)
    {
        lock (_gate)
        {
            if (_disposed || _workers == 0)
            {
                return _workers;
            }

            _workers = Math.Clamp(workers, 1, MaxWorkers);
            StartMissingLocked();
            Monitor.PulseAll(_gate); // an idle thread above the new count wakes and ends
            return _workers;
        }
    }

    // Starts the threads the count asks for (the lock is held): thread i works while i is below the count.
    private void StartMissingLocked()
    {
        for (var i = 0; i < _workers; i++)
        {
            if (i < _threads.Count && _threads[i] is { IsAlive: true } && _live[i])
            {
                continue;
            }

            var index = i;
            var thread = new Thread(() => Work(index)) { IsBackground = true, Name = $"vocaterra-stream-{i}", Priority = ThreadPriority.BelowNormal };
            if (i < _threads.Count)
            {
                _retired.Add(_threads[i]);
                _threads[i] = thread;
            }
            else
            {
                _threads.Add(thread);
                _live.Add(true);
            }

            _live[i] = true;
            thread.Start();
        }
    }

    /// <summary>How many jobs wait or run.</summary>
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _queues[0].Count + _queues[1].Count + _queues[2].Count + _queues[3].Count + _running;
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
            _queues[3].Clear();
            Monitor.PulseAll(_gate);
        }

        List<Thread> all;
        lock (_gate)
        {
            all = [.. _threads, .. _retired];
        }

        foreach (var thread in all)
        {
            _ = thread.Join(2000);
        }
    }

    private void Work(int index)
    {
        while (true)
        {
            Job? job;
            lock (_gate)
            {
                while (true)
                {
                    if (index >= _workers && !_disposed)
                    {
                        _live[index] = false; // told to stop (the setting went down): this thread ends between two jobs
                        return;
                    }

                    if ((job = TakeLocked()) is not null)
                    {
                        break;
                    }

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

    // Takes the next job (the residency lane first, then the tiles'; but after RingEvery tile jobs in a row the ring's gets one, so a stream of tiles never starves it); the lock is held.
    private Job? TakeLocked()
    {
        // A mesh job first (an edit's mesh, the view's fill), but not more than MeshRun in a row while a residency job waits.
        if ((_meshRun < MeshRun || _queues[(int)JobLane.Residency].Count == 0) && _queues[(int)JobLane.Mesh].TryDequeue(out var mesh, out _))
        {
            _meshRun++;
            _running++;
            return mesh;
        }

        _meshRun = 0;
        if (_queues[(int)JobLane.Residency].TryDequeue(out var first, out _))
        {
            _running++;
            return first;
        }

        if (_sinceRing >= RingEvery && _queues[(int)JobLane.Ring].TryDequeue(out var ring, out _))
        {
            _sinceRing = 0;
            _running++;
            return ring;
        }

        for (var lane = 1; lane < (int)JobLane.Mesh; lane++)
        {
            if (_queues[lane].TryDequeue(out var job, out _))
            {
                _sinceRing = lane == (int)JobLane.Ring ? 0 : _sinceRing + 1;
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
