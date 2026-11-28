using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Worlds;

namespace Vocaterra.Game.Core.Tests.Worlds;

/// <summary>The worlds on disk (Q01): the folder and metadata round trip, the list's order, the Touch, the Delete, and the worlds that cannot be loaded.</summary>
public sealed class WorldStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-worlds-" + Path.GetRandomFileName());
    private long _now = 1_000_000;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private WorldStore Store() => new(_folder, () => _now);

    [Fact]
    public void Given_AWorldIsCreated_When_ItIsListed_Then_ItsFolderAndMetadataComeBack()
    {
        var store = Store();

        var made = store.Create("  My   First World ", 12345UL, WorldKind.Valley);
        var listed = Assert.Single(store.List());

        Assert.Equal("my-first-world", made.Folder);
        Assert.Equal(Path.Combine(Path.GetFullPath(_folder), "my-first-world"), made.Directory);
        Assert.Equal(made.Folder, listed.Folder);
        Assert.Equal(new WorldMeta(1, "My First World", 12345UL, WorldKind.Valley, 1_000_000, 1_000_000), listed.Meta);
        Assert.False(listed.HasSave);
        Assert.True(listed.Loadable);
        Assert.Equal(Path.Combine(made.Directory, "world.save"), listed.SavePath);
        Assert.True(File.Exists(Path.Combine(made.Directory, "world.json")));
        Assert.Equal(["My First World"], store.Names());
    }

    [Fact]
    public void TheMetadataFileHoldsTheNameSeedTypeTimesAndSchemaAndNothingForTheTerrainYet()
    {
        var store = Store();
        var made = store.Create("W", ulong.MaxValue, WorldKind.Streamed);

        var text = File.ReadAllText(made.MetaPath);

        Assert.Equal("{\"schema\":1,\"name\":\"W\",\"seed\":18446744073709551615,\"world\":\"Streamed\",\"created\":1000000,\"lastPlayed\":1000000}", text);
    }

    [Fact]
    public void Given_TerrainValues_When_AWorldIsMadeAndRead_Then_TheyRoundTripAndAreLeftOutWhenThereAreNone()
    {
        var store = Store();
        var terrain = new Dictionary<string, int> { ["roughness"] = 3, ["seaLevel"] = -2 };

        var made = store.Create("Hilly", 1UL, WorldKind.Streamed, terrain);
        var read = Assert.Single(store.List()).Meta!;

        Assert.Equal(terrain, read.Terrain);
        Assert.Contains("\"terrain\":", File.ReadAllText(made.MetaPath), StringComparison.Ordinal);
        Assert.DoesNotContain("terrain", File.ReadAllText(store.Create("Plain", 2UL, WorldKind.Streamed).MetaPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NamesWhoseSlugsCollide_When_WorldsAreMade_Then_EachGetsItsOwnFolder()
    {
        var store = Store();

        var first = store.Create("A b", 1UL, WorldKind.Streamed);
        var second = store.Create("a-b", 2UL, WorldKind.Streamed);
        var third = store.Create("A.B", 3UL, WorldKind.Streamed);

        Assert.Equal(["a-b", "a-b-2", "a-b-3"], new[] { first.Folder, second.Folder, third.Folder });
        Assert.Equal(3, store.List().Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_ARefusedName_When_AWorldIsMade_Then_NothingIsMade(string name)
    {
        var store = Store();

        _ = Assert.Throws<ArgumentException>(() => store.Create(name, 1UL, WorldKind.Streamed));
        _ = store.Create("Taken", 1UL, WorldKind.Streamed);
        _ = Assert.Throws<ArgumentException>(() => store.Create("TAKEN", 2UL, WorldKind.Streamed));

        Assert.Single(store.List());
    }

    [Fact]
    public void TheListIsNewestPlayedFirstThenNewestMadeThenByName()
    {
        var store = Store();
        _now = 100;
        var oldest = store.Create("Oldest", 1UL, WorldKind.Streamed);
        _now = 200;
        _ = store.Create("Middle", 2UL, WorldKind.Streamed);
        _now = 300;
        _ = store.Create("Newest", 3UL, WorldKind.Streamed);
        Assert.Equal(["Newest", "Middle", "Oldest"], store.List().Select(static w => w.Name));

        _now = 400;
        _ = store.Touch(oldest); // played again: it comes to the top

        Assert.Equal(["Oldest", "Newest", "Middle"], store.List().Select(static w => w.Name));
        Assert.Equal(400, store.List()[0].Meta!.LastPlayed);
        Assert.Equal(100, store.List()[0].Meta!.Created);
    }

    [Fact]
    public void Given_WorldsMadeAtTheSameSecond_When_Listed_Then_TheOrderIsByNameAndSoTheSameEveryTime()
    {
        var store = Store();
        foreach (var name in new[] { "Charlie", "alpha", "Bravo" })
        {
            _ = store.Create(name, 1UL, WorldKind.Streamed);
        }

        Assert.Equal(["alpha", "Bravo", "Charlie"], store.List().Select(static w => w.Name));
        Assert.Equal(store.List().Select(static w => w.Folder), store.List().Select(static w => w.Folder));
    }

    [Fact]
    public void Given_ATouch_When_TheClockHasNotMovedForward_Then_LastPlayedNeverGoesBack()
    {
        var store = Store();
        _now = 500;
        var world = store.Create("W", 1UL, WorldKind.Streamed);
        _now = 100;

        var touched = store.Touch(world);

        Assert.Equal(500, touched.Meta!.LastPlayed);
    }

    [Fact]
    public void Given_ASaveInTheFolder_When_TheWorldIsListedAndTouched_Then_ItHasASave()
    {
        var store = Store();
        var world = store.Create("W", 1UL, WorldKind.Streamed);
        SaveFile.Write(world.SavePath, SaveGame.Save(TestGame.NewSession()));

        Assert.True(Assert.Single(store.List(probeSaves: true)).HasSave);
        Assert.True(store.Touch(world).HasSave);
        Assert.True(Assert.Single(store.List(probeSaves: true)).Loadable);
    }

    [Fact]
    public void Given_ASaveOfAnOlderVersion_When_TheWorldsAreListedWithProbing_Then_TheWorldSaysWhyItCannotBeLoaded()
    {
        var store = Store();
        var old = store.Create("Old", 1UL, WorldKind.Streamed);
        File.WriteAllText(old.SavePath, "{\"format\":\"vocaterra-save\",\"version\":7}");
        var broken = store.Create("Broken", 2UL, WorldKind.Streamed);
        File.WriteAllText(broken.SavePath, "not json at all");
        _ = store.Create("Fine", 3UL, WorldKind.Streamed);

        var plain = store.List();
        var probed = store.List(probeSaves: true).ToDictionary(static w => w.Name);

        Assert.All(plain, static w => Assert.True(w.Loadable)); // the cheap list does not read the saves
        Assert.False(probed["Old"].Loadable);
        Assert.Contains("version 7", probed["Old"].Problem, StringComparison.Ordinal);
        Assert.Contains("older saves are not migrated", probed["Old"].Problem, StringComparison.Ordinal);
        Assert.False(probed["Broken"].Loadable);
        Assert.Contains("not a readable save", probed["Broken"].Problem, StringComparison.Ordinal);
        Assert.True(probed["Fine"].Loadable);
    }

    [Fact]
    public void Given_AFolderWithoutMetadataAndOneWithBrokenMetadata_When_Listed_Then_TheFirstIsLeftOutAndTheSecondComesLastWithItsProblem()
    {
        var store = Store();
        _ = store.Create("Good", 1UL, WorldKind.Streamed);
        Directory.CreateDirectory(Path.Combine(_folder, "just-a-folder"));
        Directory.CreateDirectory(Path.Combine(_folder, "aaa-broken"));
        File.WriteAllText(Path.Combine(_folder, "aaa-broken", "world.json"), "{ nope");
        Directory.CreateDirectory(Path.Combine(_folder, "zzz-future"));
        File.WriteAllText(Path.Combine(_folder, "zzz-future", "world.json"), "{\"schema\":9,\"name\":\"Future\",\"seed\":1,\"world\":\"Streamed\",\"created\":1,\"lastPlayed\":1}");

        var worlds = store.List();

        Assert.Equal(["Good", "aaa-broken", "zzz-future"], worlds.Select(static w => w.Name));
        Assert.True(worlds[0].Loadable);
        Assert.Null(worlds[1].Meta);
        Assert.Contains("not readable", worlds[1].Problem, StringComparison.Ordinal);
        Assert.Contains("schema 9", worlds[2].Problem, StringComparison.Ordinal);
        Assert.False(worlds[1].Loadable);
        Assert.False(worlds[2].Loadable);
    }

    [Fact]
    public void Given_AWorld_When_ItIsDeleted_Then_ItsWholeFolderGoesAndNothingElse()
    {
        var store = Store();
        var gone = store.Create("Gone", 1UL, WorldKind.Streamed);
        var kept = store.Create("Kept", 2UL, WorldKind.Streamed);
        File.WriteAllText(gone.SavePath, "x");
        File.WriteAllText(Path.Combine(gone.Directory, "crash.save"), "x");

        store.Delete(gone);

        Assert.False(Directory.Exists(gone.Directory));
        Assert.True(Directory.Exists(kept.Directory));
        Assert.Equal(["Kept"], store.List().Select(static w => w.Name));
        Assert.Equal(["Gone"], new[] { gone.Name }); // the entry still says what it was
    }

    [Fact]
    public void Given_APathThatIsNoWorldFolderOfTheStore_When_ItIsDeleted_Then_ItIsRefusedAndStays()
    {
        var store = Store();
        var world = store.Create("W", 1UL, WorldKind.Streamed);
        var outside = Path.Combine(Path.GetTempPath(), "vocaterra-outside-" + Path.GetRandomFileName());
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "world.json"), "{}");
        var nested = Path.Combine(world.Directory, "inner");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "world.json"), "{}");
        try
        {
            _ = Assert.Throws<InvalidOperationException>(() => store.Delete(world with { Directory = outside }));
            _ = Assert.Throws<InvalidOperationException>(() => store.Delete(world with { Directory = _folder })); // the saves folder itself
            _ = Assert.Throws<InvalidOperationException>(() => store.Delete(world with { Directory = nested })); // not directly under it
            _ = Assert.Throws<InvalidOperationException>(() => store.Delete(world with { Directory = Path.Combine(_folder, "nothing-here") }));

            Assert.True(Directory.Exists(outside));
            Assert.True(Directory.Exists(world.Directory));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Given_NoSavesFolder_When_Listed_Then_ThereAreNoWorldsAndNothingIsMade()
    {
        var store = Store();

        Assert.Empty(store.List());
        Assert.Empty(store.Names());
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void Find_ReadsTheWorldOfAFolder()
    {
        var store = Store();
        var made = store.Create("Findable", 5UL, WorldKind.Streamed);

        Assert.Equal(5UL, store.Find(made.Folder)!.Meta!.Seed);
        Assert.Null(store.Find("no-such"));
    }
}
