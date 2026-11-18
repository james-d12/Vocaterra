using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.Tests.World.Biomes;

/// <summary><c>game/biomes.json</c> (B01): the parse, the shipped rows and each validator rule.</summary>
public sealed class BiomeDataTests
{
    private const string Json = """
        { "schemaVersion": 1, "dither": { "temperature": 10, "humidity": 20 }, "biomes": [
          { "id": "wet", "when": { "water": true, "humidity": { "min": 100 }, "height": { "max": 31 }, "river": { "min": 0, "max": 5 } },
            "top": "sand", "filler": "dirt", "fillerDepth": 2, "underwater": "dirt", "snowCap": true, "baseTemperature": 120,
            "trees": { "density": 20, "species": [ { "species": "oak", "weight": 2, "crown": "round" }, { "species": "birch", "weight": 1 } ] },
            "decor": { "density": 100, "blocks": { "fern": 3 } } },
          { "id": "rest", "top": "grass", "filler": "dirt", "fillerDepth": 3 }
        ] }
        """;

    private static BiomeData Shipped => BiomeTestData.Shipped;

    private static IReadOnlyList<string> Problems(BiomeData data) => BiomeValidator.Validate(data, BiomeTestData.World);

    private static bool Names(IReadOnlyList<string> problems, string text) => problems.Any(p => p.Contains(text, StringComparison.Ordinal));

    private static BiomeData WithRow(string id, Func<BiomeRow, BiomeRow> change) => Shipped with { Rows = [.. Shipped.Rows.Select(r => r.Id == id ? change(r) : r)] };

    [Fact]
    public void Given_TheFile_When_ItIsParsed_Then_RowsConditionsTreesAndDecorAreRead()
    {
        var data = BiomeData.Parse(Json);

        Assert.Equal((10, 20), (data.DitherTemperature, data.DitherHumidity));
        var wet = data.Row("wet");
        Assert.Equal(0, wet.Index);
        Assert.Equal(1, data.Row("rest").Index);
        Assert.Equal(true, wet.When.Water);
        Assert.Equal(new IntRange(100, int.MaxValue), wet.When.Humidity);
        Assert.Equal(new IntRange(int.MinValue, 31), wet.When.Height);
        Assert.Equal(new IntRange(0, 5), wet.When.River);
        Assert.True(wet.When.Temperature.IsAny);
        Assert.Equal(("sand", "dirt", 2, "dirt", true, 120), (wet.Top, wet.Filler, wet.FillerDepth, wet.Underwater, wet.SnowCap, wet.BaseTemperature));
        Assert.Equal([new TreeEntry("oak", 2, "round"), new TreeEntry("birch", 1, null)], wet.Trees.Species);
        Assert.Equal(20, wet.Trees.Density);
        Assert.Equal([new DecorEntry("fern", 3)], wet.Decor.Blocks);
        Assert.True(data.Row("rest").When.IsNone);
        Assert.Empty(Problems(data));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "dither": { "temperature": 0, "humidity": 0 }, "biomes": [] }""")]
    [InlineData("""{ "schemaVersion": 1, "biomes": [] }""")]
    [InlineData("""{ "schemaVersion": 1, "dither": { "temperature": 0, "humidity": 0 }, "biomes": [ { "id": "a" } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "dither": { "temperature": 0, "humidity": 0 }, "biomes": [ { "id": "a", "top": "grass", "filler": "dirt", "fillerDepth": "deep" } ] }""")]
    [InlineData("not json")]
    public void Given_AFileWrongInShape_When_ItIsParsed_Then_ItIsRefused(string json) => Assert.Throws<InvalidDataException>(() => BiomeData.Parse(json));

    [Fact]
    public void Given_TheShippedFile_When_ItIsRead_Then_ItHasThePlansRowsInOrderAndHoldsTogether()
    {
        Assert.Empty(Problems(Shipped));
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.Contains("biomes.json", StringComparison.Ordinal) || p.Contains("world.json", StringComparison.Ordinal));
        var ids = Shipped.Rows.Select(static r => r.Id).ToList();
        foreach (var expected in new[] { "ocean", "beach", "river-channel", "lake-basin", "jagged-peaks", "stony-peaks", "snowy-slopes", "grove", "meadow", "taiga", "plains", "woodland", "birchwood", "dense-woodland" })
        {
            Assert.Contains(expected, ids);
        }

        // Water and coast, then the mountains from the highest down, then the lowlands.
        Assert.True(ids.IndexOf("ocean") < ids.IndexOf("jagged-peaks"));
        Assert.True(ids.IndexOf("jagged-peaks") < ids.IndexOf("stony-peaks") && ids.IndexOf("snowy-slopes") < ids.IndexOf("grove") && ids.IndexOf("meadow") < ids.IndexOf("taiga"));
        Assert.True(Shipped.Rows[^1].When.IsNone);
        Assert.Equal(Enumerable.Range(0, Shipped.Rows.Count), Shipped.Rows.Select(static r => r.Index));
    }

    [Fact]
    public void Given_AnUnknownRow_When_ItIsLookedUp_Then_ItThrows() => Assert.Throws<ArgumentException>(() => Shipped.Row("no-such-biome"));

    [Fact]
    public void Given_ADuplicateOrBadId_When_ItIsValidated_Then_ItIsNamed()
    {
        var twice = Shipped with { Rows = [.. Shipped.Rows, Shipped.Rows[0]] };
        var bad = WithRow("plains", static r => r with { Id = "Plains_1" });

        Assert.True(Names(Problems(twice), "used twice"));
        Assert.True(Names(Problems(bad), "kebab-case"));
    }

    [Fact]
    public void Given_NoRowsOrNoCatchAll_When_ItIsValidated_Then_ItIsRefused()
    {
        Assert.True(Names(Problems(Shipped with { Rows = [] }), "at least one biome"));
        var noCatchAll = Shipped with { Rows = [.. Shipped.Rows.Take(Shipped.Rows.Count - 1)] };
        Assert.True(Names(Problems(noCatchAll), "must have no conditions"));
    }

    [Fact]
    public void Given_AnInvertedOrOutOfRangeCondition_When_ItIsValidated_Then_ItIsNamed()
    {
        var inverted = WithRow("plains", static r => r with { When = r.When with { Humidity = new IntRange(500, -500) } });
        var outside = WithRow("plains", static r => r with { When = r.When with { Temperature = new IntRange(-5000, 0) } });
        var ridges = WithRow("plains", static r => r with { When = r.When with { Ridges = new IntRange(-10, 500) } });

        Assert.True(Names(Problems(inverted), "humidity with min 500 above max -500"));
        Assert.True(Names(Problems(outside), "temperature outside"));
        Assert.True(Names(Problems(ridges), "ridges outside"));
    }

    [Fact]
    public void Given_BlocksTheGameDoesNotHaveOrCannotUse_When_ItIsValidated_Then_ItIsNamed()
    {
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Top = "turf" })), "unknown block 'turf'"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Filler = "fern" })), "filler block 'fern'"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Underwater = "water" })), "underwater block 'water'"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Decor = new DecorRule(100, [new DecorEntry("stone", 1)]) })), "undergrowth block 'stone'"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Decor = new DecorRule(100, [new DecorEntry("moss", 1)]) })), "unknown block 'moss'"));
    }

    [Fact]
    public void Given_BadTreesAndDensities_When_ItIsValidated_Then_EachIsNamed()
    {
        static BiomeRow Trees(BiomeRow r, int density, params TreeEntry[] species) => r with { Trees = new TreeRule(density, species) };

        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 4, new TreeEntry("baobab", 1, null)))), "unknown tree species 'baobab'"));
        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 4, new TreeEntry("oak", 1, "mushroom")))), "unknown crown 'mushroom'"));
        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 4, new TreeEntry("oak", 0, null)))), "must be positive"));
        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 4, new TreeEntry("oak", 1, null), new TreeEntry("oak", 2, null)))), "twice"));
        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 101, new TreeEntry("oak", 1, null)))), "from 0 to 100"));
        Assert.True(Names(Problems(WithRow("plains", static r => Trees(r, 4))), "no species"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Decor = new DecorRule(1001, [new DecorEntry("fern", 1)]) })), "from 0 to 1000"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { Decor = new DecorRule(10, []) })), "no blocks"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { FillerDepth = 40 })), "fillerDepth"));
        Assert.True(Names(Problems(WithRow("plains", static r => r with { BaseTemperature = 2000 })), "baseTemperature"));
        Assert.True(Names(Problems(Shipped with { DitherTemperature = 900 }), "dither"));
    }

    [Fact]
    public void Given_ARowWithAnAmbientField_When_ItIsParsed_Then_ItsBedsAndWeightsAreRead()
    {
        var data = BiomeData.Parse(Json.Replace("\"top\": \"sand\"", "\"ambient\": { \"surf\": 800, \"wind\": 250 }, \"top\": \"sand\"", StringComparison.Ordinal));

        Assert.Equal([new BedShare("surf", 800), new BedShare("wind", 250)], data.Row("wet").Ambient);
        Assert.Null(data.Row("rest").Ambient);
        Assert.Empty(Problems(data));
    }

    [Fact]
    public void Given_ABadAmbientField_When_ItIsValidated_Then_EachProblemIsNamed()
    {
        static BiomeRow Beds(BiomeRow r, params BedShare[] beds) => r with { Ambient = beds };

        Assert.True(Names(Problems(WithRow("plains", static r => Beds(r, new BedShare("thunder", 500)))), "unknown ambient bed 'thunder'"));
        Assert.True(Names(Problems(WithRow("plains", static r => Beds(r, new BedShare("cave", 500)))), "unknown ambient bed 'cave'")); // the cave follows the depth, not a biome
        Assert.True(Names(Problems(WithRow("plains", static r => Beds(r, new BedShare("wind", 1001)))), "from 0 to 1000"));
        Assert.True(Names(Problems(WithRow("plains", static r => Beds(r, new BedShare("wind", -1)))), "from 0 to 1000"));
        Assert.True(Names(Problems(WithRow("plains", static r => Beds(r, new BedShare("wind", 5), new BedShare("wind", 6)))), "twice"));
    }

    [Fact]
    public void Given_TheSchema_When_ItsAmbientBedsAreRead_Then_TheyAreTheBedsABiomeMayName()
    {
        using var schema = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Repo.Path("content", "schema", "biomes.schema.json")));
        var names = schema.RootElement.GetProperty("properties").GetProperty("biomes").GetProperty("items").GetProperty("properties").GetProperty("ambient").GetProperty("propertyNames").GetProperty("enum").EnumerateArray().Select(static e => e.GetString()).ToList();

        Assert.Equal(Vocaterra.Game.Core.Sound.AmbientBeds.ByPlace.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
    }
}
