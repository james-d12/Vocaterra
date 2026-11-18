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
        Assert.Equal(["gravel", "sand", "dirt", "granite", "limestone", "granite", "basalt"], u.Strata.Blobs.Select(static b => b.Block));
        Assert.Equal(["slate"], u.Strata.Blobs[^1].In);
        Assert.Equal((170, 12, 210), (u.Strata.Blobs[0].Threshold, -u.Strata.Blobs[0].MinY, u.Strata.Blobs[0].MaxY));
        Assert.Equal((128, 500, 2, 6, 3, 7, 20), (u.Entrances.Cell, u.Entrances.Chance, u.Entrances.WidthMin, u.Entrances.WidthMax, u.Entrances.HeadroomMin, u.Entrances.HeadroomMax, u.Entrances.MinDrop));
        Assert.NotNull(u.Linings);
        Assert.NotNull(u.SurfaceRock);
        Assert.Equal(("bedrock", -64, 1, 4), (u.Floor.Block, u.Floor.BottomY, u.Floor.MinThickness, u.Floor.MaxThickness));
        Assert.Equal((4, 8), (u.Caves.LatticeXz, u.Caves.LatticeY));
        Assert.Equal(("lava", -54), (u.Lava.Block, u.Lava.Level));
        var copper = u.Ores.Single(static o => o.Block == "copper-ore");
        Assert.Equal([new OreBand(-16, 112, 48, copper.Bands[0].Veins)], copper.Bands);
        var iron = u.Ores.Single(static o => o.Block == "iron-ore");
        Assert.Equal([(-24, 56, 16), (80, 320, 160)], iron.Bands.Select(static b => (b.MinY, b.MaxY, b.PeakY)));
        var tin = u.Ores.Single(static o => o.Block == "tin-ore");
        Assert.Equal([(-32, 48, 8)], tin.Bands.Select(static b => (b.MinY, b.MaxY, b.PeakY)));
        Assert.All(u.Ores, static o => Assert.Equal(["stone", "slate", "granite", "limestone", "basalt"], o.Replaces));
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

    private static (string, int, int, int, int, IReadOnlyList<string>?, int, int) Blob(BlobSpec b) => (b.Block, b.Cell, b.CellY, b.Octaves, b.Threshold, b.In, b.MinY, b.MaxY);

    [Fact]
    public void Given_TheFileText_When_ItIsParsedAgain_Then_ItEqualsTheLoadedOne()
    {
        var text = File.ReadAllText(Repo.Path("content", "game-v1", "game", "underground.json"));

        var parsed = UndergroundData.Parse(text);

        Assert.Equal(Shipped.Strata.Blobs.Select(Blob), parsed.Strata.Blobs.Select(Blob));
        Assert.Equal(Shipped.Caves, parsed.Caves);
        Assert.Equal(Shipped.Entrances with { FloorBlocks = [] }, parsed.Entrances with { FloorBlocks = [] });
        Assert.Equal(Shipped.Entrances.FloorBlocks, parsed.Entrances.FloorBlocks);
        Assert.Equal(Shipped.Linings!.Rules.Select(static r => (r.Block, string.Join(',', r.In), r.Face, r.Zone, r.Chance, r.Patch, r.MinY, r.MaxY)), parsed.Linings!.Rules.Select(static r => (r.Block, string.Join(',', r.In), r.Face, r.Zone, r.Chance, r.Patch, r.MinY, r.MaxY)));
        Assert.Equal(Shipped.SurfaceRock!.Cliff.Bands, parsed.SurfaceRock!.Cliff.Bands);
        Assert.Equal(Shipped.SurfaceRock.Scree.Rules, parsed.SurfaceRock.Scree.Rules);
        Assert.Equal(Shipped.Ores.Select(static o => (o.Id, o.Block, o.Bands.Count)), parsed.Ores.Select(static o => (o.Id, o.Block, o.Bands.Count)));
    }

    [Fact]
    public void Given_ABlockTheGameLacks_When_ItIsValidated_Then_TheProblemNamesIt()
    {
        var problems = Problems(Shipped with { Strata = Shipped.Strata with { Deep = "magma" } });

        Assert.True(Names(problems, "magma"));
    }

    [Fact]
    public void Given_StrataOutOfOrder_When_TheyAreValidated_Then_TheDitherTheBlobsAndTheLatticeAreChecked()
    {
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { DitherFromY = 8, DitherToY = 0 } }), "ditherFromY below ditherToY"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { BlobLattice = 5 } }), "blobLattice"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Deep = "stone" } }), "different blocks"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Blobs = [Shipped.Strata.Blobs[0] with { In = ["water"] }] } }), "must be solid"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Blobs = [Shipped.Strata.Blobs[0] with { In = [] }] } }), "replaces no rock"));
        Assert.True(Names(Problems(Shipped with { Strata = Shipped.Strata with { Blobs = [Shipped.Strata.Blobs[0] with { MinY = 50, MaxY = 10 }] } }), "minY not above maxY"));
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
    public void Given_BadEntrances_When_TheyAreValidated_Then_ThePassageMustBeWalkableAndClearOfWater()
    {
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { WaterReach = Shipped.Caves.WaterMargin } }), "waterReach"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { WidthMin = 1 } }), "widthMin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { WidthMax = 1 } }), "widthMin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { RunMax = 7 } }), "no step is over one block"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { RunMax = 0 } }), "no step is over one block"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { HeadroomMin = 2 } }), "headroomMin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { SegmentMin = 1 } }), "segmentMin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Plunge = 40 } }), "plunge"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { BendSegmentMax = 1 } }), "bendSegmentMin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Bend = 40 } }), "bend"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { MaxTurn = 99 } }), "maxTurn"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Roughness = 5000 } }), "roughness"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Ledges = 2000 } }), "ledges"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Funnel = 9000 } }), "funnel"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { BowlSlope = 100 } }), "bowlSlope"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { BowlDepth = 40 } }), "bowlDepth"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { MinDrop = 4 } }), "minDrop"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { MaxLength = 5 } }), "maxLength"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Margin = 70 } }), "margin"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Chance = 2000 } }), "chance"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { Attempts = 0 } }), "attempts"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { MinHeightAboveSea = -1 } }), "minHeightAboveSea"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { FloorBlocks = [] } }), "floorBlocks"));
        Assert.True(Names(Problems(Shipped with { Entrances = Shipped.Entrances with { FloorBlocks = [new BlockWeight("magma", 1)] } }), "magma"));
    }

    [Fact]
    public void Given_BadLinings_When_TheyAreValidated_Then_EachRulesFaceZoneBlockAndBandAreChecked()
    {
        var rule = Shipped.Linings!.Rules[0];
        UndergroundData With(Func<LiningRule, LiningRule> change) => Shipped with { Linings = Shipped.Linings! with { Rules = [change(rule)] } };

        Assert.True(Names(Problems(With(static r => r with { Face = "sideways" })), "face"));
        Assert.True(Names(Problems(With(static r => r with { Zone = "cold" })), "zone"));
        Assert.True(Names(Problems(With(static r => r with { Block = "magma" })), "magma"));
        Assert.True(Names(Problems(With(static r => r with { In = [] })), "replaces no rock"));
        Assert.True(Names(Problems(With(static r => r with { In = ["lava"] })), "must be solid"));
        Assert.True(Names(Problems(With(static r => r with { Chance = 0 })), "chance"));
        Assert.True(Names(Problems(With(static r => r with { MinY = 90, MaxY = 10 })), "minY not above maxY"));
        Assert.True(Names(Problems(With(static r => r with { Patch = new PatchSpec(0, 4, 1, 0) })), "patch"));
        Assert.True(Names(Problems(Shipped with { Linings = Shipped.Linings with { WetReach = 30 } }), "wetReach"));
    }

    [Fact]
    public void Given_BadSurfaceRock_When_ItIsValidated_Then_TheCliffTheScreeAndTheStreambedAreChecked()
    {
        var rock = Shipped.SurfaceRock!;
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Cliff = rock.Cliff with { Drop = 1 } } }), "cliff.drop"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Cliff = rock.Cliff with { Bands = [] } } }), "cliff.bands"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Cliff = rock.Cliff with { Bands = [new BlockWeight("water", 1)] } } }), "must be solid"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Scree = rock.Scree with { SteepDrop = rock.Cliff.Drop } } }), "steepDrop"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Scree = rock.Scree with { Rules = [new ScatterRule("magma", 500, null)] } } }), "magma"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Streambed = rock.Streambed with { MinWeight = 0 } } }), "minWeight"));
        Assert.True(Names(Problems(Shipped with { SurfaceRock = rock with { Streambed = rock.Streambed with { Rules = [new ScatterRule("gravel", 0, null)] } } }), "chance"));
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
        Assert.Equal(Shipped.Linings!.WetReach, world.WaterReach); // the linings' wet reach is wider than the caves' margin of 3
        Assert.Throws<ArgumentException>(() => new UndergroundWorld(Shipped with { Lava = Shipped.Lava with { Block = "magma" } }, 42));
    }
}
