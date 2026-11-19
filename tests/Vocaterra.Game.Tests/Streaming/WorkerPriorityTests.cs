using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The workers' nice value (B40): on Linux a thread that asks for it is nicer than the thread that made it, as /proc says, and the other threads are not changed.</summary>
public sealed class WorkerPriorityTests
{
    /// <summary>The nice value of one thread, read from its own /proc entry. Only the two test threads are read (each by its own tid), so a
    /// thread of the test host that exits meanwhile cannot make the read fail (B40b).</summary>
    private static int NiceOf(int tid)
    {
        var stat = File.ReadAllText($"/proc/self/task/{tid}/stat");
        var close = stat.LastIndexOf(')');
        return int.Parse(stat[(close + 2)..].Split(' ')[16], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The calling thread's tid: /proc/thread-self links to "pid/task/tid" (no P/Invoke needed in the test project).</summary>
    private static int CurrentTid() => int.Parse(Path.GetFileName(new FileInfo("/proc/thread-self").LinkTarget!), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Given_ALinuxWorkerThread_When_ItLowersItsPriority_Then_ItsNiceValueRisesAndAnotherThreadsDoesNot()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Null(WorkerPriority.LowerCurrentThread());
            return;
        }

        int? returned = null;
        int plainTid = 0;
        int niceTid = 0;
        using var ready = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        var plain = new Thread(() =>
        {
            plainTid = CurrentTid();
            ready.Signal();
            release.Wait();
        })
        { Name = "b40-plain", IsBackground = true };
        var nice = new Thread(() =>
        {
            returned = WorkerPriority.LowerCurrentThread();
            niceTid = CurrentTid();
            ready.Signal();
            release.Wait();
        })
        { Name = "b40-nice", IsBackground = true };
        plain.Start();
        nice.Start();
        ready.Wait();
        var (plainNice, niceNice) = (NiceOf(plainTid), NiceOf(niceTid));
        release.Set();
        plain.Join();
        nice.Join();

        Assert.Equal(returned, niceNice);
        Assert.Equal(Math.Min(19, plainNice + WorkerPriority.Raise), niceNice);
    }
}
