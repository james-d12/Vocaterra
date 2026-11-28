using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Options;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>
/// <c>--terrain</c> and the far store's cache key (Q02): the option parses to the text the bundle's options read, an unknown id or a value off the grid is refused with a message that names the ids or the grid, and the
/// folder of a world's far sections is named by the seed and the terrain options, so one seed with other options never shares far tiles, while a world at the defaults keeps its old name.
/// </summary>
public sealed class TerrainLaunchOptionsTests
{
    private static TerrainOptionData Table => TestGame.Data.TerrainOptions;

    [Fact]
    public void Given_NoOption_When_Parsed_Then_TheTerrainIsLeftToTheDefaultsOrTheSave()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var options, out var error), error);

        Assert.Null(options.Terrain);
        Assert.True(options.CheckTerrain(Table, out var asked, out var message));
        Assert.Null(asked);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void Given_Pairs_When_Parsed_And_Checked_Then_TheOptionsAreTheBundlesAndCanonical()
    {
        Assert.True(LaunchOptions.TryParse(["--terrain", "hills=50,mountains=150", "--seed", "7"], out var options, out var error), error);

        Assert.Equal("hills=50,mountains=150", options.Terrain);
        Assert.True(options.CheckTerrain(Table, out var asked, out _));
        Assert.Equal("hills=50;mountains=150", asked!.Canonical);
        Assert.Equal(7UL, options.Seed);
        Assert.True(LaunchOptions.TryParse(["--terrain", "mountains=100"], out var spelled, out _));
        Assert.True(spelled.CheckTerrain(Table, out var defaults, out _));
        Assert.True(defaults!.IsDefault); // asking for the defaults is asking for them: under --load it must match a save at the defaults
    }

    [Theory]
    [InlineData("hills")]
    [InlineData("hills=high")]
    [InlineData("hills=1.5")]
    [InlineData(",,")]
    public void Given_AMalformedList_When_Parsed_Then_ItIsRefusedAtOnce(string text)
    {
        Assert.False(LaunchOptions.TryParse(["--terrain", text], out _, out var error));

        Assert.StartsWith("--terrain needs <id>=<value>", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnUnknownIdOrAValueOffTheGrid_When_Checked_Then_ItIsRefusedWithAClearMessage()
    {
        Assert.True(LaunchOptions.TryParse(["--terrain", "volcanoes=3"], out var unknown, out _));
        Assert.False(unknown.CheckTerrain(Table, out _, out var unknownMessage));
        Assert.Contains("--terrain: there is no terrain option 'volcanoes'", unknownMessage, StringComparison.Ordinal);
        Assert.Contains("mountains, hills, ocean, continents, rivers", unknownMessage, StringComparison.Ordinal);

        Assert.True(LaunchOptions.TryParse(["--terrain", "mountains=110"], out var off, out _));
        Assert.False(off.CheckTerrain(Table, out _, out var offMessage));
        Assert.Contains("'mountains' takes a value from 25 to 150 in steps of 25", offMessage, StringComparison.Ordinal);
        Assert.Contains("110 is not one", offMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheUsage_When_ItIsRead_Then_TerrainIsDocumented() =>
        Assert.Contains("--terrain <id>=<n>,...", LaunchOptions.Usage, StringComparison.Ordinal);

    [Fact]
    public void Given_TheSameSeedWithOtherOptions_When_TheFarStoreIsNamed_Then_EachWorldHasItsOwnFolderAndTheDefaultsKeepTheOldName()
    {
        var plain = WorldStateHeader.For(TestGame.Bundle, 42);
        var defaults = WorldStateHeader.For(TestGame.Bundle, 42, null, WorldOptions.Default);
        var tall = WorldStateHeader.For(TestGame.Bundle, 42, null, Table.Create([new("mountains", 150)]));
        var wide = WorldStateHeader.For(TestGame.Bundle, 42, null, Table.Create([new("ocean", 3)]));

        var (plainName, tallName, wideName) = (StreamedWorldHost.LodWorldName(plain), StreamedWorldHost.LodWorldName(tall), StreamedWorldHost.LodWorldName(wide));
        Assert.Equal(plainName, StreamedWorldHost.LodWorldName(defaults));
        Assert.Matches("^42-g\\d+-[0-9a-f]{12}$", plainName); // the name a world had before the options: its far sections stay valid
        Assert.EndsWith("-t" + Table.Create([new("mountains", 150)]).Key[..12], tallName, StringComparison.Ordinal);
        Assert.NotEqual(plainName, tallName);
        Assert.NotEqual(tallName, wideName);
        Assert.NotEqual(FileLodPersistence.FolderFor(tallName), FileLodPersistence.FolderFor(wideName));
        Assert.StartsWith("43-", StreamedWorldHost.LodWorldName(WorldStateHeader.For(TestGame.Bundle, 43, null, Table.Create([new("mountains", 150)]))), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TwoWorldsOfOneSeedAndOtherOptions_When_TheirFarSectionsAreSaved_Then_TheyNeverShareATile()
    {
        var root = Path.Combine(Path.GetTempPath(), "vocaterra-q02-" + Guid.NewGuid().ToString("N"));
        try
        {
            var key = new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, 1, 2, 3);
            var tall = Path.Combine(root, StreamedWorldHost.LodWorldName(WorldStateHeader.For(TestGame.Bundle, 42, null, Table.Create([new("mountains", 150)]))));
            var wide = Path.Combine(root, StreamedWorldHost.LodWorldName(WorldStateHeader.For(TestGame.Bundle, 42, null, Table.Create([new("ocean", 3)]))));
            using (var disk = new FileLodPersistence(tall, "v1"))
            {
                disk.Save(key, [1, 2, 3]);
                disk.Flush();
            }

            using var same = new FileLodPersistence(tall, "v1");
            using var other = new FileLodPersistence(wide, "v1");
            Assert.Equal([1, 2, 3], same.Load(key));
            Assert.Null(other.Load(key));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
