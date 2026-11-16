using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary><c>game/underground.json</c> (B11): the parse, the shipped numbers and each validator rule.</summary>
public sealed class UndergroundDataTests
{
    private static UndergroundData Shipped => TestGame.Data.Underground!;

    private static IReadOnlyList<string> Problems(UndergroundData data) => UndergroundValidator.Validate(data, TestGame.Data.WorldGen);

    private static bool Names(IReadOnlyList<string> problems, string text) => problems.Any(p => p.Contains(text, StringComparison.Ordinal));

    private static UndergroundData WithOre(string id, Func<OreSpec, OreSpec> change) => Shipped with { Ores = [.. Shipped.Ores.Select(o => o.Id == id ? change(o) : o)] };

    [Fact]
    public void Given_TheShippedFile_When_ItIsLoaded_Then_ItHasThePlansStrataFloorOresCavesAndLava()
    {
        var u = Shipped;

        Assert.Equal(("stone", "slate", 0, 8), (u.Strata.Stone, u.Strata.Deep, u.Strata.DitherFromY, u.Strata.DitherToY));
        Assert.Equal(["granite", "limestone"], u.Strata.Blobs.Select(static b => b.Block));
        Assert.Equal(("bedrock", -64, 1, 4), (u.Floor.Block, u.Floor.BottomY, u.Floor.MinThickness, u.Floor.MaxThickness));
        Assert.Equal((4, 8), (u.Caves.LatticeXz, u.Caves.LatticeY));
        Assert.Equal(("lava", -54), (u.Lava.Block, u.Lava.Level));
        var copper = u.Ores.Single(static o => o.Block == "copper-ore");
        Assert.Equal([new OreBand(-16, 112, 48, copper.Bands[0].Veins)], copper.Bands);
        var iron = u.Ores.Single(static o => o.Block == "iron-ore");
        Assert.Equal([(-24, 56, 16), (80, 320, 160)], iron.Bands.Select(static b => (b.MinY, b.MaxY, b.PeakY)));
        var tin = u.Ores.Single(static o => o.Block == "tin-ore");
        Assert.Equal([(-32, 48, 8)], tin.Bands.Select(static b => (b.MinY, b.MaxY, b.PeakY)));
        Assert.All(u.Ores, static o => Assert.Equal(["stone", "slate", "granite", "limestone"], o.Replaces));
    }

    [Fact]
    public void Given_TheShippedFile_When_ItIsValidated_Then_ThereIsNoProblemAndTheWholeGameDataHasNone()
    {
        Assert.Empty(Problems(Shipped));
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.Contains("underground", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ABundleWithoutTheFile_When_TheGameDataHasNoUnderground_Then_ItValidatesAndHasNone()
    {
        var data = TestGame.Data.WithUnderground(null);

        Assert.Null(data.Underground);
        Assert.DoesNotContain(GameDataValidator.Validate(data), static p => p.Contains("underground", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("not json")]
    public void Given_AFileWrongInShape_When_ItIsParsed_Then_ItIsRefused(string json) => Assert.Throws<InvalidDataException>(() => UndergroundData.Parse(json));

    [Fact]
    public void Given_TheFileText_When_ItIsParsedAgain_Then_ItEqualsTheLoadedOne()
    {
        var text = File.ReadAllText(Repo.Path("content", "game-v1", "game", "underground.json"));

        var parsed = UndergroundData.Parse(text);

        Assert.Equal(Shipped.Strata.Blobs, parsed.Strata.Blobs);
        Assert.Equal(Shipped.Caves, parsed.Caves);
        Assert.Equal(Shipped.Entrances, parsed.Entrances);
        Assert.Equal(Shipped.Ores.Select(static o => (o.Id, o.Block, o.Bands.Count)), parsed.Ores.Select(static o => (o.Id, o.Block, o.Bands.Count)));
    }

    [Fact]
    public void Given_ABlockTheGameLacks_When_ItIsValidated_Then_TheProblemNamesIt()
    {
        var problems = Problems(Shipped with { Strata = Shipped.Strata with { Deep = "basalt" } });

        Assert.True(Names(problems, "basalt"));
    }

    [Fact]
    public void Given_StrataOutOfOrder_When_TheyAreValidated_Then_TheDitherTheBlobsAndTheLatticeAreChecked()
    {
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { DitherFromY = 8, DitherToY = 0 } }), "ditherFromY below ditherToY"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { BlobLattice = 5 } }), "blobLattice"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Deep = "stone" } }), "different blocks"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Blobs = [.. Shipped.Strata.Blobs, Shipped.Strata.Blobs[0]] } }), "used twice"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Blobs = [Shipped.Strata.Blobs[0] with { Threshold = 1500 }] } }), "threshold"));
    }

    [Fact]
    public void Given_ABadFloor_When_ItIsValidated_Then_TheThicknessTheBottomAndTheBlockAreChecked()
    {
        Assert.True(Names(Problems(Shipped with { Floor = Shipped.Floor with { MinThickness = 0 } }), "minThickness"));
        Assert.True(Names(Problems(Shipped with { Floor = Shipped.Floor with { MaxThickness = 9 } }), "maxThickness"));
        Assert.True(Names(Problems(Shipped with { Floor = Shipped.Floor with { BottomY = -48 } }), "bottomY"));
        Assert.True(Names(Problems(Shipped with { Floor = Shipped.Floor with { Block = "stone" } }), "indestructible"));
    }

    [Fact]
    public void Given_BadCaves_When_TheyAreValidated_Then_TheLatticeTheThresholdsAndTheWaterRulesAreChecked()
    {
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { LatticeY = 3 } }), "latticeXz and latticeY"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { SurfaceCover = 0 } }), "surfaceCover"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { UnderRegionCeiling = 4 } }), "underRegionCeiling"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { WaterMargin = 0 } }), "waterMargin"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { BedClearance = 2 } }), "bedClearance"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { Cheese = Shipped.Caves.Cheese with { ShallowThreshold = 100 } } }), "shallowThreshold"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { Cheese = Shipped.Caves.Cheese with { DeepY = 200 } } }), "deepY"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { Spaghetti = Shipped.Caves.Spaghetti with { Thickness = 0 } } }), "thickness"));
        Assert.True(Names(Problems(Shipped with { Caves = Shipped.Caves with { Spaghetti = Shipped.Caves.Spaghetti with { Octaves = 9 } } }), "octaves"));
    }

    [Fact]
    public void Given_BadEntrances_When_TheyAreValidated_Then_TheWaterReachMustCoverTheCavesMargin()
    {
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { WaterReach = Shipped.Caves.WaterMargin - 1 } }), "waterReach"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { ProbeDepth = 0 } }), "probeDepth"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { MinHeightAboveSea = -1 } }), "minHeightAboveSea"));
    }

    [Fact]
    public void Given_BadLava_When_ItIsValidated_Then_TheBlockAndTheLevelAreChecked()
    {
        Assert.True(Names(Problems(Shipped with { Lava = Shipped.Lava with { Block = "stone" } }), "fluid"));
        Assert.True(Names(Problems(Shipped with { Lava = Shipped.Lava with { Level = -63 } }), "lava.level"));
        Assert.True(Names(Problems(Shipped with { Lava = Shipped.Lava with { Level = 4 } }), "lava.level"));
    }

    [Fact]
    public void Given_BadOres_When_TheyAreValidated_Then_EachRuleIsChecked()
    {
        Assert.True(Names(Problems(Shipped with { Ores = [.. Shipped.Ores, Shipped.Ores[0]] }), "used twice"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Id = "Tin" })), "kebab-case"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Replaces = [] })), "replaces no rock"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Replaces = ["stone", "iron-ore"] })), "may not replace"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Replaces = ["stone", "bedrock"] })), "may not replace"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Replaces = ["stone", "water"] })), "water"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { SizeMin = 9, SizeMax = 3 })), "size.min"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Bands = [] })), "no band"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Bands = [new OreBand(-32, 48, 60, 4)] })), "peakY"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Bands = [new OreBand(-100, 48, 8, 4)] })), "inside the world"));
        Assert.True(Names(Problems(WithOre("tin-veins", static o => o with { Bands = [new OreBand(-32, 48, 8, 100)] })), "veins"));
    }

    [Fact]
    public void Given_TheShippedData_When_TheUndergroundWorldIsBuilt_Then_ItReadsTheDataAndAnUnknownBlockIsRefused()
    {
        var world = new UndergroundWorld(Shipped, 42);

        Assert.Equal(-54, world.LavaLevel);
        Assert.Equal(BlockId.Bedrock, world.Bedrock);
        Assert.Equal(BlockId.Lava, world.Lava);
        Assert.Equal(3, world.WaterReach);
        Assert.Throws<ArgumentException>(() => new UndergroundWorld(Shipped with { Lava = Shipped.Lava with { Block = "magma" } }, 42));
    }
}
