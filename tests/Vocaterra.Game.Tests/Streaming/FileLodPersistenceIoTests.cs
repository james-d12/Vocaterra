using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The far store's disk I/O (B07d, the codebase review's P3 and P4): a saved section is answered at once though the writer has not written it, the files are synced on a timer and not whenever the queue runs dry, and compaction rewrites a region file keeping every live section.</summary>
public sealed class FileLodPersistenceIoTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-lod-io-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static byte[] Blob(int salt, int length = 3000)
    {
        var bytes = new byte[length];
        new Random(salt).NextBytes(bytes);
        bytes[0] = (byte)LodOrigin.Real;
        return bytes;
    }

    private static LodSectionKey KeyOf(int i) => new(0, i % 8, (i / 64) - 2, (i / 8) % 8); // 64 columns of a region, then the layers

    [Fact]
    public void Given_ManySectionsSavedBackToBack_When_TheyAreLoadedAtOnce_Then_EachIsAnsweredWhetherOrNotTheWriterHasWrittenItAndTheTableEmptiesAfterTheFlush()
    {
        using var disk = new FileLodPersistence(_folder, "v1");
        var blobs = Enumerable.Range(0, 600).Select(static i => Blob(i, 1000 + (i % 7 * 300))).ToArray();

        for (var i = 0; i < blobs.Length; i++)
        {
            disk.Save(KeyOf(i), blobs[i]);
            Assert.Equal(blobs[i], disk.Load(KeyOf(i))); // answered the moment it is saved
        }

        for (var i = 0; i < blobs.Length; i += 13)
        {
            Assert.Equal(blobs[i], disk.Load(KeyOf(i)));
        }

        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        Assert.Equal(0, disk.Unwritten);
        for (var i = 0; i < blobs.Length; i++)
        {
            Assert.Equal(blobs[i], disk.Load(KeyOf(i))); // now from the files
        }
    }

    [Fact]
    public void Given_ASectionSavedAgainAndADrop_When_TheyAreLoaded_Then_TheNewestBytesAnswerAndADropReadsAsMissing()
    {
        using var disk = new FileLodPersistence(_folder, "v1");
        var key = KeyOf(5);

        disk.Save(key, Blob(1));
        disk.Save(key, Blob(2));
        Assert.Equal(Blob(2), disk.Load(key));
        disk.Save(key, []);
        Assert.Null(disk.Load(key));
        Assert.True(disk.Flush(TimeSpan.FromSeconds(30)));
        Assert.Null(disk.Load(key));
        disk.Save(key, Blob(3));
        Assert.True(disk.Flush(TimeSpan.FromSeconds(30)));
        Assert.Equal(Blob(3), disk.Load(key));
    }

    [Fact]
    public void Given_BurstsOfWritesWithTheQueueRunningDryBetweenThem_When_TheSyncIntervalIsLong_Then_NoSyncHappensUntilShutdownWhichSyncsOnce()
    {
        var disk = new FileLodPersistence(_folder, "v1", syncIntervalMilliseconds: 600_000);
        for (var burst = 0; burst < 6; burst++)
        {
            for (var i = 0; i < 20; i++)
            {
                disk.Save(KeyOf((burst * 20) + i), Blob(i));
            }

            Assert.True(disk.Flush(TimeSpan.FromSeconds(30))); // the queue is empty here: the old writer synced every region file now
        }

        Assert.Equal(0, disk.Syncs);
        Assert.True(disk.Shutdown(TimeSpan.FromSeconds(30)));
        Assert.Equal(1, disk.Syncs); // at shutdown
        using var again = new FileLodPersistence(_folder, "v1");
        Assert.Equal(Blob(7), again.Load(KeyOf(7)));
    }

    [Fact]
    public void Given_AWriteAndAShortSyncInterval_When_NothingMoreIsQueued_Then_TheFilesAreSyncedOnTheTimerOnceAndNotAgainWhileIdle()
    {
        using var disk = new FileLodPersistence(_folder, "v1", syncIntervalMilliseconds: 150);

        disk.Save(KeyOf(1), Blob(1));
        var until = HangGuard.DeadlineAfter(30_000);
        while (disk.Syncs < 1 && !HangGuard.Passed(until))
        {
            Thread.Sleep(10);
        }

        Assert.Equal(1, disk.Syncs);
        Thread.Sleep(600);
        Assert.Equal(1, disk.Syncs); // idle: nothing is dirty, nothing is synced

        disk.Save(KeyOf(2), Blob(2));
        until = HangGuard.DeadlineAfter(30_000);
        while (disk.Syncs < 2 && !HangGuard.Passed(until))
        {
            Thread.Sleep(10);
        }

        Assert.Equal(2, disk.Syncs);
    }

    [Fact]
    public void Given_ARegionWhoseSectionsWereWrittenSeveralTimes_When_ItIsCompacted_Then_EverySectionIsStillThereTheFileIsSmallerAndLaterWritesWork()
    {
        using var disk = new FileLodPersistence(_folder, "v1");
        const int Sections = 400;
        var latest = new Dictionary<int, byte[]>();
        for (var round = 0; round < 5; round++)
        {
            for (var i = 0; i < Sections; i++)
            {
                latest[i] = Blob((round * 1000) + i, 2000 + (i % 5 * 200));
                disk.Save(KeyOf(i), latest[i]);
            }
        }

        // A neighbouring region with nothing to reclaim must be left alone.
        var otherKey = new LodSectionKey(0, 20, 0, 3);
        disk.Save(otherKey, Blob(99));
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        var file = disk.RegionFile(KeyOf(0));
        var other = disk.RegionFile(otherKey);
        var before = new FileInfo(file).Length;
        var otherBefore = new FileInfo(other).Length;

        disk.Compact(minDeadFraction: 0.25, minDeadBytes: 1);
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));

        Assert.Equal(1, disk.CompactedFiles);
        var after = new FileInfo(file).Length;
        Assert.True(after < before / 3, $"{before} bytes before, {after} after");
        Assert.True(disk.ReclaimedBytes >= before - after - 1);
        Assert.Equal(otherBefore, new FileInfo(other).Length);
        for (var i = 0; i < Sections; i++)
        {
            Assert.Equal(latest[i], disk.Load(KeyOf(i)));
        }

        Assert.Equal(Blob(99), disk.Load(otherKey));
        disk.Save(KeyOf(3), Blob(12345));
        disk.Save(KeyOf(Sections + 5), Blob(777));
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        Assert.Equal(Blob(12345), disk.Load(KeyOf(3)));
        Assert.Equal(Blob(777), disk.Load(KeyOf(Sections + 5)));
        Assert.Equal(latest[4], disk.Load(KeyOf(4)));

        disk.Compact(); // nothing dead to speak of now: it leaves the file as it is
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        Assert.Equal(1, disk.CompactedFiles);
        using var reopened = new FileLodPersistence(_folder, "v1");
        Assert.Equal(latest[100], reopened.Load(KeyOf(100))); // a restart reads the compacted file
        Assert.Equal(Blob(12345), reopened.Load(KeyOf(3)));
    }

    [Fact]
    public void Given_LoadsGoingOnWhileARegionIsCompactedAndWrittenTo_When_TheyAreDone_Then_NoLoadSawAMissingOrWrongSection()
    {
        using var disk = new FileLodPersistence(_folder, "v1");
        const int Sections = 300;
        var blobs = Enumerable.Range(0, Sections).Select(static i => Blob(i, 2500)).ToArray();
        for (var round = 0; round < 4; round++)
        {
            for (var i = 0; i < Sections; i++)
            {
                disk.Save(KeyOf(i), round == 3 ? blobs[i] : Blob((round * 1000) + i, 2500)); // the last round's bytes are the ones to find
            }
        }

        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        var wrong = 0;
        var loads = 0;
        var stop = false;
        var readers = Enumerable.Range(0, 3).Select(r => new Thread(() =>
        {
            var random = new Random(r);
            while (!Volatile.Read(ref stop))
            {
                var i = random.Next(Sections);
                if (!blobs[i].AsSpan().SequenceEqual(disk.Load(KeyOf(i)) ?? []))
                {
                    _ = Interlocked.Increment(ref wrong);
                }

                _ = Interlocked.Increment(ref loads);
            }
        })).ToArray();
        foreach (var reader in readers)
        {
            reader.Start();
        }

        disk.Compact(minDeadFraction: 0.1, minDeadBytes: 1);
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        Volatile.Write(ref stop, true);
        foreach (var reader in readers)
        {
            reader.Join();
        }

        Assert.Equal(0, wrong);
        Assert.True(loads > 100, $"{loads} loads");
        Assert.Equal(1, disk.CompactedFiles);
    }

    [Fact]
    public void Given_ATornRegionFile_When_ItIsCompacted_Then_ItIsLeftAloneOrKeepsWhatWasWhole()
    {
        using var disk = new FileLodPersistence(_folder, "v1");
        for (var i = 0; i < 50; i++)
        {
            disk.Save(KeyOf(i), Blob(i));
            disk.Save(KeyOf(i), Blob(i + 1000));
        }

        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));
        var file = disk.RegionFile(KeyOf(0));
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            stream.SetLength(stream.Length - 4000); // the tail of the file is gone: the last sections point past it
        }

        disk.Compact(minDeadFraction: 0.01, minDeadBytes: 1);
        Assert.True(disk.Flush(TimeSpan.FromSeconds(60)));

        var whole = 0;
        for (var i = 0; i < 50; i++)
        {
            if (disk.Load(KeyOf(i)) is { } found)
            {
                Assert.Equal(Blob(i + 1000), found); // never wrong bytes: a section is whole or missing
                whole++;
            }
        }

        Assert.InRange(whole, 40, 49);
    }
}
