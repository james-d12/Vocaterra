using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The workers' nice value (B40): on Linux a thread that asks for it is nicer than the thread that made it, as /proc says, and the other threads are not changed.</summary>
public sealed class WorkerPriorityTests
{
    private static int? NiceOf(string name)
    {
        foreach (var dir in Directory.GetDirectories("/proc/self/task"))
        {
            var stat = File.ReadAllText(Path.Combine(dir, "stat"));
            var open = stat.IndexOf('(', StringComparison.Ordinal);
            var close = stat.LastIndexOf(')');
            if (stat[(open + 1)..close] == name)
            {
                return int.Parse(stat[(close + 2)..].Split(' ')[16], System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    [Fact]
    public void Given_ALinuxWorkerThread_When_ItLowersItsPriority_Then_ItsNiceValueRisesAndAnotherThreadsDoesNot()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Null(WorkerPriority.LowerCurrentThread());
            return;
        }

        int? returned = null;
        using var asked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var plain = new Thread(() => release.Wait()) { Name = "b40-plain", IsBackground = true };
        var nice = new Thread(() =>
        {
            returned = WorkerPriority.LowerCurrentThread();
            asked.Set();
            release.Wait();
        })
        { Name = "b40-nice", IsBackground = true };
        plain.Start();
        nice.Start();
        asked.Wait();
        var (plainNice, niceNice) = (NiceOf("b40-plain"), NiceOf("b40-nice"));
        release.Set();
        plain.Join();
        nice.Join();

        Assert.NotNull(plainNice);
        Assert.Equal(returned, niceNice);
        Assert.Equal(Math.Min(19, plainNice!.Value + WorkerPriority.Raise), niceNice);
    }
}
