using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The far view's sections on disk (B07c): what is put is found again by a new store (a restart is instant), many sections of a region sharing one file, and a world whose generator or data changed starts again from nothing.</summary>
public sealed class FileLodPersistenceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-lod-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Given_SectionsPutThroughAStoreOnDisk_When_ANewStoreOpensTheFolder_Then_TheyAreThereAndTheModelDoesNotReplaceTheRealOnes()
    {
        var key = new LodSectionKey(2, -3, 1, 7);
        var section = LodSection.Air();
        section.SetBlock(LodSection.Index(4, 5, 6), BlockId.Planks);
        using (var disk = new FileLodPersistence(_folder, "v1"))
        {
            var store = new LodStore(disk);
            Assert.True(store.Put(key, section, LodOrigin.Real, 0xFF));
            disk.Flush();
        }

        using var again = new FileLodPersistence(_folder, "v1");
        var restarted = new LodStore(again);
        var found = restarted.Get(key);

        Assert.NotNull(found);
        Assert.Equal(BlockId.Planks, found.BlockAt(LodSection.Index(4, 5, 6)));
        Assert.True(restarted.Entry(key)!.IsWhollyReal);
        Assert.False(restarted.Put(key, LodSection.Air(), LodOrigin.Model));
        Assert.Null(restarted.Get(new LodSectionKey(0, 0, 0, 0)));
        Assert.True(File.Exists(again.RegionFile(key)));
        Assert.EndsWith("r.-1.0.lodr", again.RegionFile(key));
    }

    [Fact]
    public void Given_AFolderOfAnotherVersion_When_ItIsOpened_Then_ItsSectionsAreCleared()
    {
        var key = new LodSectionKey(0, 1, 2, 3);
        using (var disk = new FileLodPersistence(_folder, "world-a"))
        {
            new LodStore(disk).Put(key, new LodSection(BlockId.Stone, 0), LodOrigin.Model);
            disk.Flush();
        }

        using var other = new FileLodPersistence(_folder, "world-b");

        Assert.Null(new LodStore(other).Get(key));
        Assert.Equal("world-b", File.ReadAllText(Path.Combine(_folder, "manifest.txt")));
    }

    [Fact]
    public void Given_ManySectionsOfOneRegionAndTheSameSectionPutTwice_When_ANewStoreReadsThem_Then_EveryOneIsThereTheLaterBytesWinAndFewFilesWereMade()
    {
        using (var disk = new FileLodPersistence(_folder, "v1"))
        {
            var store = new LodStore(disk);
            for (var x = 0; x < 8; x++)
            {
                for (var y = -2; y < 6; y++)
                {
                    var section = LodSection.Air();
                    section.SetBlock(LodSection.Index(x, y + 2, 3), BlockId.Stone);
                    _ = store.Put(new LodSectionKey(0, x, y, 5), section, LodOrigin.Real, 0xFF);
                }
            }

            _ = store.Put(new LodSectionKey(0, 3, 0, 5), new LodSection(BlockId.Dirt, 0), LodOrigin.Real, 0xFF);
            disk.Flush();
        }

        using var again = new FileLodPersistence(_folder, "v1");
        var restarted = new LodStore(again);

        Assert.Equal(BlockId.Dirt, restarted.Get(new LodSectionKey(0, 3, 0, 5))!.FillBlock);
        Assert.Equal(BlockId.Stone, restarted.Get(new LodSectionKey(0, 6, 1, 5))!.BlockAt(LodSection.Index(6, 3, 3)));
        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "L0")));
    }
}
