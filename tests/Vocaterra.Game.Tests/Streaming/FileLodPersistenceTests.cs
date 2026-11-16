using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The far view's sections on disk (B07c): what is put is found again by a new store (a restart is instant), and a world whose generator or data changed starts again from nothing.</summary>
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
        Assert.True(File.Exists(Path.Combine(_folder, "L2", "-3", "7", "1.lod")));
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
}
