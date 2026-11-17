using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The far store's memory (B07d, the rendering review's H5): real sections are dropped from memory past a cap, least recently used first, once the disk holds them, and are read back when asked for; the player's edits and a store with no disk keep everything; the counts stay exact.</summary>
public sealed class LodStoreCapTests
{
    private sealed class MemoryDisk : ILodPersistence
    {
        public ConcurrentDictionary<LodSectionKey, byte[]> Files { get; } = new();

        public int Loads;

        public byte[]? Load(LodSectionKey key)
        {
            _ = Interlocked.Increment(ref Loads);
            return Files.TryGetValue(key, out var blob) && blob.Length > 0 ? blob : null;
        }

        public void Save(LodSectionKey key, byte[] blob) => Files[key] = blob;
    }

    // A mixed section whose bytes depend on the salt (so each packs to a few kilobytes and no two are alike).
    private static LodSection Salted(int salt)
    {
        var section = LodSection.Air();
        var random = new Random(salt);
        for (var y = 0; y < 32; y++)
        {
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 32; x++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), y < 12 + (random.Next(8) - 4) ? BlockId.Stone : (random.Next(5) == 0 ? BlockId.Dirt : BlockId.Air));
                }
            }
        }

        return section;
    }

    private static LodSectionKey KeyOf(int i) => new(0, i % 40, i / 40, 3);

    private static void AssertSame(LodSection expected, LodSection? actual)
    {
        Assert.NotNull(actual);
        for (var i = 0; i < LodSection.Volume; i += 7)
        {
            Assert.Equal(expected.BlockAt(i), actual.BlockAt(i));
        }
    }

    [Fact]
    public void Given_RealSectionsPastTheCap_When_MoreArePut_Then_MemoryStaysUnderTheCapAndEverySectionIsStillThereFromTheDisk()
    {
        var disk = new MemoryDisk();
        var probe = Salted(1).Pack().Length;
        var cap = probe * 20L; // room for about twenty
        var store = new LodStore(disk, maxRealBytes: cap);
        const int Count = 120;

        for (var i = 0; i < Count; i++)
        {
            Assert.True(store.Put(KeyOf(i), Salted(i), LodOrigin.Real, 0xFF));
            Assert.True(store.RealBytes <= cap + (probe * 2L), $"{store.RealBytes} bytes held over a cap of {cap} after {i + 1} sections"); // a put may overshoot by the section that tipped it, and is trimmed straight after
        }

        Assert.True(store.RealBytes <= cap);
        Assert.True(store.Evicted >= Count - 30, $"{store.Evicted} dropped");
        Assert.True(store.Count < Count / 2);
        Assert.Equal(store.RealBytesRecounted(), store.RealBytes);
        Assert.Equal(Count, disk.Files.Count); // every one was saved as it was put

        for (var i = 0; i < Count; i++)
        {
            AssertSame(Salted(i), store.Get(KeyOf(i))); // dropped or not, it is there
        }

        Assert.True(store.DiskLoads > 50, $"{store.DiskLoads} read back");
        Assert.True(store.RealBytes <= cap);
        Assert.Equal(store.RealBytesRecounted(), store.RealBytes);
        Assert.Equal(store.RealBytes, store.Bytes);
    }

    [Fact]
    public void Given_ASectionKeptInUse_When_OthersArePutPastTheCap_Then_ItStaysInMemoryAndTheOldestUntouchedGoFirst()
    {
        var disk = new MemoryDisk();
        var probe = Salted(1).Pack().Length;
        var store = new LodStore(disk, maxRealBytes: probe * 12L);
        _ = store.Put(KeyOf(0), Salted(0), LodOrigin.Real, 0xFF);
        _ = store.Put(KeyOf(1), Salted(1), LodOrigin.Real, 0xFF);

        for (var i = 2; i < 60; i++)
        {
            _ = store.Put(KeyOf(i), Salted(i), LodOrigin.Real, 0xFF);
            _ = store.Entry(KeyOf(0)); // used all the while
        }

        var loadsBefore = store.DiskLoads;
        Assert.NotNull(store.Entry(KeyOf(0)));
        Assert.Equal(loadsBefore, store.DiskLoads); // still in memory: no read
        Assert.NotNull(store.Entry(KeyOf(1)));
        Assert.Equal(loadsBefore + 1, store.DiskLoads); // the one never touched again was dropped and read back
        Assert.Equal(store.RealBytesRecounted(), store.RealBytes);
    }

    [Fact]
    public void Given_AnEditedSectionAndAStoreWithNoDisk_When_PastTheCap_Then_NothingTheyHoldIsDropped()
    {
        var disk = new MemoryDisk();
        var probe = Salted(1).Pack().Length;
        var store = new LodStore(disk, maxRealBytes: probe * 4L);
        _ = store.Put(KeyOf(0), Salted(0), LodOrigin.Real, 0xFF, editedMask: 0x01);
        for (var i = 1; i < 40; i++)
        {
            _ = store.Put(KeyOf(i), Salted(i), LodOrigin.Real, 0xFF);
        }

        var loads = store.DiskLoads;
        Assert.NotNull(store.Entry(KeyOf(0)));
        Assert.Equal(loads, store.DiskLoads); // the player's edit stays in memory whatever the cap says

        var memoryOnly = new LodStore(null, maxRealBytes: 1);
        for (var i = 0; i < 10; i++)
        {
            _ = memoryOnly.Put(KeyOf(i), Salted(i), LodOrigin.Real, 0xFF);
        }

        Assert.Equal(0, memoryOnly.Evicted); // nothing could be read back
        Assert.Equal(10, memoryOnly.Count);
        AssertSame(Salted(3), memoryOnly.Get(KeyOf(3)));
    }

    [Fact]
    public void Given_ModelSectionsAndRealOnes_When_TheyAreCounted_Then_EachKindHasItsOwnCountAndDropModelTakesOnlyTheModels()
    {
        var disk = new MemoryDisk();
        var store = new LodStore(disk);
        _ = store.Put(KeyOf(0), Salted(0), LodOrigin.Real, 0xFF);
        _ = store.Put(KeyOf(1), Salted(1), LodOrigin.Model);
        _ = store.Put(KeyOf(2), Salted(2), LodOrigin.Model);

        Assert.Equal(store.ModelBytesRecounted(), store.ModelBytes);
        Assert.Equal(store.RealBytesRecounted(), store.RealBytes);
        Assert.True(store.ModelBytes > 0 && store.RealBytes > 0);

        Assert.Equal(2, store.DropModel());
        Assert.Equal(0, store.ModelBytes);
        Assert.Equal(1, store.Count);
        AssertSame(Salted(0), store.Get(KeyOf(0)));
    }

    [Fact]
    public void Given_ManyThreadsPuttingReadingAndDroppingUnderATinyCap_When_TheyAreDone_Then_TheCountsAreExactAndEverySectionHoldsItsBlocks()
    {
        var disk = new MemoryDisk();
        var probe = Salted(1).Pack().Length;
        var store = new LodStore(disk, maxRealBytes: probe * 10L);
        const int Count = 80;
        var sections = Enumerable.Range(0, Count).Select(Salted).ToArray();
        var failures = new ConcurrentBag<string>();

        Parallel.For(0, 6, new ParallelOptions { MaxDegreeOfParallelism = 6 }, thread =>
        {
            var random = new Random(thread);
            for (var n = 0; n < 400; n++)
            {
                var i = random.Next(Count);
                if (thread < 3)
                {
                    _ = store.Put(KeyOf(i), sections[i], LodOrigin.Real, 0xFF);
                }
                else if (store.Get(KeyOf(i)) is { } got && got.BlockAt(LodSection.Index(5, 3, 9)) != sections[i].BlockAt(LodSection.Index(5, 3, 9)))
                {
                    failures.Add($"section {i} read back with other blocks");
                }
            }
        });

        Assert.Empty(failures);
        Assert.Equal(store.RealBytesRecounted(), store.RealBytes);
        Assert.True(store.RealBytes <= probe * 10L + (probe * 8L), $"{store.RealBytes}"); // each racing put may overshoot by its own section before the trim
        foreach (var (key, blob) in disk.Files)
        {
            var i = key.X + (key.Y * 40);
            AssertSame(sections[i], store.Get(key));
            Assert.True(blob.Length > 3);
        }
    }

    [Fact]
    public void Given_ASectionWhollyRealOnDisk_When_AskedWithoutKeepingIt_Then_TheStoreSaysSoAndHoldsNothingNew()
    {
        var disk = new MemoryDisk();
        var writer = new LodStore(disk);
        _ = writer.Put(KeyOf(0), Salted(0), LodOrigin.Real, 0xFF);
        _ = writer.Put(KeyOf(1), Salted(1), LodOrigin.Real, 0x0F);
        _ = writer.Put(KeyOf(2), Salted(2), LodOrigin.RealMip);

        var fresh = new LodStore(disk);

        Assert.True(fresh.IsWhollyReal(KeyOf(0)));
        Assert.False(fresh.IsWhollyReal(KeyOf(1))); // partly real
        Assert.True(fresh.IsWhollyReal(KeyOf(2)));
        Assert.False(fresh.IsWhollyReal(KeyOf(9)));
        Assert.Equal(0, fresh.Count); // asking kept nothing
        Assert.Equal(0, fresh.RealBytes);
    }
}
