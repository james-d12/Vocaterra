using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Biomes;

/// <summary>The biome table (B01): first match wins, a biome is a row, the border is dithered, and snow and trees follow the temperature at altitude.</summary>
public sealed class BiomeTableTests
{
    private static WorldGenData World => BiomeTestData.World;

    private static BiomeTable Shipped(ulong seed = 42) => new(BiomeTestData.Shipped, World, seed);

    private static ClimateSample Climate(int temperature = 0, int humidity = 0, int continentalness = 1000, int erosion = 0, int ridges = 0, int river = 4096) =>
        new(temperature, humidity, continentalness, erosion, ridges, river);

    [Fact]
    public void Given_TwoRowsThatBothMatch_When_AColumnIsSelected_Then_TheFirstWins()
    {
        var table = new BiomeTable(BiomeTestData.Of(BiomeTestData.Row("first", BiomeTestData.Where(humidity: new IntRange(0, int.MaxValue))), BiomeTestData.Row("second", BiomeTestData.Where(humidity: new IntRange(-400, int.MaxValue))), BiomeTestData.Row("rest")), World, 1);

        Assert.Equal("first", table.Select(Climate(humidity: 3000), 50, 0, 0).Id);
        Assert.Equal("rest", table.Select(Climate(humidity: -3000), 50, 0, 0).Id);
    }

    [Fact]
    public void Given_ABiomeDefinedOnlyInTestData_When_AColumnMatchesIt_Then_ItIsSelectedAndValidWithNoCodeChange()
    {
        var swamp = BiomeTestData.Row("test-swamp", BiomeTestData.Where(humidity: new IntRange(900, int.MaxValue), temperature: new IntRange(200, int.MaxValue)), top: "mossy-stone", filler: "cobble") with
        {
            Decor = new DecorRule(300, [new DecorEntry("reeds", 1)]),
            Trees = new TreeRule(10, [new TreeEntry("birch", 1, "narrow")]),
        };
        var data = BiomeTestData.Of([swamp, .. BiomeTestData.Shipped.Rows]);
        var table = new BiomeTable(data, World, 42);

        var swampy = table.Select(Climate(temperature: 3500, humidity: 4096), 40, 5, 5);
        var elsewhere = table.Select(Climate(temperature: 0, humidity: 0), 40, 5, 5);

        Assert.Equal("test-swamp", swampy.Id);
        Assert.NotEqual("test-swamp", elsewhere.Id);
        Assert.Empty(BiomeValidator.Validate(data, World));
        var surface = table.Surface(swampy);
        Assert.Equal((BlockId.MossyStone, BlockId.Cobble, BlockId.MossyStone, 3), (surface.Top, surface.Filler, surface.Underwater, surface.FillerDepth));
    }

    [Fact]
    public void Given_ARowsBlocks_When_ItIsResolved_Then_TheUnderwaterBlockDefaultsToTheTop()
    {
        var table = Shipped();
        var beach = table.Surface(BiomeTestData.Shipped.Row("beach"));
        var plains = table.Surface(BiomeTestData.Shipped.Row("plains"));

        Assert.Equal(BlockId.Sand, beach.Top);
        Assert.Equal(BlockId.Grass, plains.Top);
        Assert.Equal(BlockId.Dirt, plains.Filler);
        Assert.Equal(plains.Top, plains.Underwater);
    }

    [Fact]
    public void Given_ARowNamingAnUnknownBlock_When_TheTableIsMade_Then_ItThrows()
    {
        var data = BiomeTestData.Of(BiomeTestData.Row("rest", top: "turf"));

        Assert.Throws<Vocaterra.Game.Core.World.Layout.SceneLayoutException>(() => new BiomeTable(data, World, 1));
    }

    [Fact]
    public void Given_WaterAndRiverColumns_When_TheyAreSelected_Then_ARiverChannelTheOceanAndALakeBasinAreToldApart()
    {
        var table = Shipped();

        Assert.Equal("ocean", table.Select(Climate(continentalness: -3000, river: 0), 20, 0, 0).Id);
        Assert.Equal("river-channel", table.Select(Climate(continentalness: 800, river: 0), 28, 0, 0).Id);
        Assert.Equal("lake-basin", table.Select(Climate(continentalness: 3000, river: 4000), 29, 0, 0).Id);
        Assert.Equal("river-channel", table.Classify(Climate(continentalness: 3000, river: 4096), 29, 0, 0, true, riverWeight: 3000).Row.Id); // B02c: a river's carve says it is a river, not the climate's river field
        Assert.Equal("lake-basin", table.Classify(Climate(continentalness: 3000, river: 0), 29, 0, 0, true, riverWeight: 500).Row.Id);
        Assert.Equal("beach", table.Select(Climate(continentalness: -400, river: 4000), 33, 0, 0).Id);
    }

    [Fact]
    public void Given_HighGroundOfDifferentTemperature_When_ItIsSelected_Then_TheMountainRowsFollowTheTemperatureAtThatHeight()
    {
        var table = Shipped();

        Assert.Equal("jagged-peaks", table.Select(Climate(temperature: -600, ridges: 3500), 260, 0, 0).Id);
        Assert.Equal("stony-peaks", table.Select(Climate(temperature: 3000, ridges: 3500), 260, 0, 0).Id);
        Assert.Equal("snowy-slopes", table.Select(Climate(temperature: -1600, ridges: 1000), 120, 0, 0).Id);
        Assert.Equal("meadow", table.Select(Climate(temperature: 1500, ridges: 1000), 120, 0, 0).Id);
        Assert.Equal("grove", table.Select(Climate(temperature: 0, ridges: 1000), 160, 0, 0).Id);
    }

    [Fact]
    public void Given_TheLowlands_When_TemperatureAndHumidityVary_Then_TheWhittakerRowsAreChosen()
    {
        var table = Shipped();

        Assert.Equal("taiga", table.Select(Climate(temperature: -2500, humidity: 0), 45, 0, 0).Id);
        Assert.Equal("plains", table.Select(Climate(temperature: 500, humidity: -3000), 45, 0, 0).Id);
        Assert.Equal("woodland", table.Select(Climate(temperature: 500, humidity: 0), 45, 0, 0).Id);
        Assert.Equal("birchwood", table.Select(Climate(temperature: -400, humidity: 3000), 45, 0, 0).Id);
        Assert.Equal("dense-woodland", table.Select(Climate(temperature: 3000, humidity: 3000), 45, 0, 0).Id);
    }

    [Fact]
    public void Given_AColumnOnABorder_When_NeighbouringColumnsAreSelected_Then_TheBorderIsDitheredNotALine()
    {
        var table = Shipped();
        var boundary = FieldScaleOfData("plains"); // the humidity at which plains give way to woodland
        var climate = Climate(humidity: boundary);
        var plains = 0;
        var changes = 0;
        var previous = string.Empty;
        for (var x = 0; x < 400; x++)
        {
            var id = table.Select(climate, 45, x, 17).Id;
            plains += id == "plains" ? 1 : 0;
            changes += previous.Length > 0 && id != previous ? 1 : 0;
            previous = id;
        }

        Assert.InRange(plains, 100, 300); // both biomes on the same climate value
        Assert.True(changes > 30, $"the border changes {changes} times along a line: it is dithered");
    }

    private static int FieldScaleOfData(string id) => BiomeTestData.Shipped.Row(id).When.Humidity.Max * IntMath.One / 1000;

    [Fact]
    public void Given_TheSameColumn_When_ItIsSelectedAgainOrByAnotherTable_Then_TheAnswerIsTheSameAndTheSeedKeysTheDither()
    {
        var a = Shipped(42);
        var b = Shipped(42);
        var other = Shipped(43);
        var climate = Climate(humidity: FieldScaleOfData("plains"));
        var differing = 0;
        for (var x = -200; x < 200; x++)
        {
            Assert.Equal(a.Select(climate, 45, x, 3), b.Select(climate, 45, x, 3));
            differing += a.Select(climate, 45, x, 3).Id != other.Select(climate, 45, x, 3).Id ? 1 : 0;
        }

        Assert.True(differing > 20, "another seed dithers the border another way");
    }

    [Fact]
    public void Given_AColdAndAWarmClimate_When_TheSnowLineIsFound_Then_TheColdRangeIsWhiteLowerDown()
    {
        var table = Shipped();

        int SnowLine(int temperature)
        {
            for (var y = 33; y < 320; y++)
            {
                if (table.Classify(Climate(temperature: temperature, humidity: 0, ridges: 3000), y, 0, 0).Snow)
                {
                    return y;
                }
            }

            return int.MaxValue;
        }

        var cold = SnowLine(-2000);
        var mild = SnowLine(0);
        var hot = SnowLine(2500);
        Assert.True(cold < mild && mild < hot, $"snow starts at {cold}, {mild} and {hot}");
        Assert.True(cold is >= 120 and < 130 && mild > 150, "even a cold range is white only from the slopes up (y 120, B02b); a mild one only high up");
    }

    [Fact]
    public void Given_TheSnowRule_When_ColumnsAreClassified_Then_SnowNeedsARowThatAllowsItAndTheColdAndNeverLiesUnderWater()
    {
        var table = Shipped();
        var cold = Climate(temperature: -3500, humidity: 0, continentalness: 1500);

        Assert.True(table.Classify(cold, 120, 0, 0).Snow); // snowy slopes allow it
        Assert.False(table.Classify(cold, 20, 0, 0).Snow); // under water
        Assert.False(table.Classify(Climate(temperature: 1000, ridges: 3000), 260, 0, 0).Snow); // stony peaks are warm
        var snowy = table.Classify(cold, 120, 0, 0);
        Assert.True(snowy.SurfaceTemperature < FieldScaleOf(World.Altitude.SnowBelowTemperature));
    }

    private static int FieldScaleOf(int thousandths) => thousandths * IntMath.One / 1000;

    [Fact]
    public void Given_TheTreeLine_When_ColumnsAreClassified_Then_TreesStopWhereItIsColdAtThatHeight()
    {
        var table = Shipped();
        var climate = Climate(temperature: 0, humidity: 0);

        Assert.True(table.Classify(climate, 60, 0, 0).TreesGrow);
        var line = Enumerable.Range(33, 280).First(y => !table.Classify(climate, y, 0, 0).TreesGrow);
        Assert.InRange(line, 200, 260);
        Assert.False(table.Classify(climate, 300, 0, 0).TreesGrow);
        Assert.True(table.Classify(Climate(temperature: 3000), 200, 0, 0).TreesGrow); // a hot land grows trees higher
    }

    [Fact]
    public void Given_TheSurfaceTemperature_When_ItIsReadFromTheTableAndTheSampler_Then_TheyAgree()
    {
        var sampler = new ClimateSampler(World, 42);
        var table = Shipped(42);
        var height = new HeightModel(World, 42);
        for (var i = 0; i < 400; i++)
        {
            var x = (i * 331) - 40_000;
            var z = (i * 217) - 30_000;
            var c = sampler.At(x, z);
            var shape = height.Shape(x, z, c);
            var column = table.Classify(c, shape.Height, x, z);
            var jitterless = sampler.TemperatureAt(x, shape.Height, z);
            Assert.InRange(column.SurfaceTemperature - jitterless, -FieldScaleOf(BiomeTestData.Shipped.DitherTemperature), FieldScaleOf(BiomeTestData.Shipped.DitherTemperature));
        }
    }
}
