using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World;

/// <summary><c>game/world.json</c> (B01): the parse, the shipped values and each validator rule.</summary>
public sealed class WorldGenDataTests
{
    private static WorldGenData Shipped => TestGame.Data.WorldGen;

    private static IReadOnlyList<string> Problems(WorldGenData world) => WorldGenValidator.Validate(world);

    private static bool Names(IReadOnlyList<string> problems, string text) => problems.Any(p => p.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void Given_TheFile_When_ItIsParsed_Then_EveryPartIsRead()
    {
        var world = WorldGenData.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "world.json")));

        Assert.Equal(Shipped.Vertical, world.Vertical); // the loaded file and the parsed text agree
        Assert.Equal(Shipped.Climate, world.Climate);
        Assert.Equal(Shipped.Altitude, world.Altitude);
        Assert.Equal(Shipped.Height.Base, world.Height.Base);
        Assert.Equal(Shipped.Height.RangeFactor, world.Height.RangeFactor);
        Assert.Equal(Shipped.Height.Peaks, world.Height.Peaks);
        Assert.Equal(Shipped.Height with { Base = [], RangeFactor = [], Peaks = [] }, world.Height with { Base = [], RangeFactor = [], Peaks = [] });
        Assert.Equal(new VerticalRange(-64, 320, 32), world.Vertical);
        Assert.Equal(new NoiseSpec(4096, 3, 4500, 1300), world.Climate.Continentalness);
        Assert.Equal(new ContinentSpec(8000, 800, 2, 1, 700, 120, 1800, 3000, 600, 6000, 23), world.Climate.Continents);
        Assert.Equal(new NoiseSpec(12288, 2, 0, 0), world.Climate.Erosion);
        Assert.Equal(new SplinePoint(-1000, -58), world.Height.Base[0]);
        Assert.Equal(Shipped.Height.Coast, world.Height.Coast);
        Assert.NotNull(world.Height.CoastData); // the coast fades the peaks and calms the hills
        Assert.Equal(4, world.Height.Terraces.Step);
        Assert.Equal(3, world.Height.Rivers.Depth);
        Assert.Equal(4, world.Height.Lakes.Depth);
        Assert.Equal(new WaterSpec(3, 2), world.Height.Water);
    }

    [Fact]
    public void Given_TheShippedFile_When_ItIsValidated_Then_ItHoldsTogetherAndKeepsThePlansNumbers()
    {
        Assert.Empty(Problems(Shipped));
        Assert.Equal(32, Shipped.Vertical.SeaLevel); // the valley keeps its coordinates
        Assert.Equal((-64, 320), (Shipped.Vertical.MinY, Shipped.Vertical.MaxY));
        Assert.Equal((280, 310), (Shipped.Height.SoftCapAbove, Shipped.Height.Clamp));
        Assert.True(Shipped.Height.Rivers.Depth >= 1 && Shipped.Height.Lakes.Depth >= 1 && Shipped.Height.Water.Step >= 1);
        Assert.Equal(4, Shipped.Climate.LatticeBlocks);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 1, "vertical": {} }""")]
    [InlineData("not json")]
    public void Given_AFileWrongInShape_When_ItIsParsed_Then_ItIsRefused(string json) => Assert.Throws<InvalidDataException>(() => WorldGenData.Parse(json));

    [Fact]
    public void Given_ABadVerticalRange_When_ItIsValidated_Then_ItIsNamed()
    {
        Assert.True(Names(Problems(Shipped with { Vertical = new VerticalRange(-60, 320, 32) }), "multiple of 16"));
        Assert.True(Names(Problems(Shipped with { Vertical = new VerticalRange(320, -64, 32) }), "multiple of 16"));
        Assert.True(Names(Problems(Shipped with { Vertical = new VerticalRange(-64, 320, 400) }), "seaLevel"));
    }

    [Fact]
    public void Given_BadNoiseAndLapse_When_ItIsValidated_Then_EachIsNamed()
    {
        static WorldGenData With(WorldGenData w, Func<ClimateData, ClimateData> change) => w with { Climate = change(w.Climate) };

        Assert.True(Names(Problems(With(Shipped, static c => c with { Continentalness = c.Continentalness with { Cell = 0 } })), "continentalness.cell"));
        Assert.True(Names(Problems(With(Shipped, static c => c with { Erosion = c.Erosion with { Octaves = 9 } })), "erosion.octaves"));
        Assert.True(Names(Problems(With(Shipped, static c => c with { Erosion = new NoiseSpec(4, 6, 0, 0) })), "halve the cell"));
        Assert.True(Names(Problems(With(Shipped, static c => c with { Ridges = c.Ridges with { WarpCell = 0 } })), "warpCell"));
        Assert.True(Names(Problems(With(Shipped, static c => c with { LapsePer100Blocks = -1 })), "lapse"));
        Assert.True(Names(Problems(With(Shipped, static c => c with { LatticeBlocks = 0 })), "latticeBlocks"));
    }

    [Fact]
    public void Given_BadSplines_When_ItIsValidated_Then_EachIsNamed()
    {
        static WorldGenData With(WorldGenData w, Func<HeightData, HeightData> change) => w with { Height = change(w.Height) };

        Assert.True(Names(Problems(With(Shipped, static h => h with { Base = [new SplinePoint(0, 40)] })), "at least two control points"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Base = [new SplinePoint(10, 40), new SplinePoint(10, 50)] })), "inputs must rise"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Base = [new SplinePoint(-2000, 40), new SplinePoint(10, 50)] })), "the input -2000"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Base = [new SplinePoint(0, 40), new SplinePoint(10, 900)] })), "the output 900"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { RangeFactor = [new SplinePoint(0, 40), new SplinePoint(10, 1500)] })), "height.rangeFactor"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Peaks = [new SplinePoint(-5, 0), new SplinePoint(10, 100)] })), "height.peaks"));
    }

    [Fact]
    public void Given_BadCapsAndFeatures_When_ItIsValidated_Then_EachIsNamed()
    {
        static WorldGenData With(WorldGenData w, Func<HeightData, HeightData> change) => w with { Height = change(w.Height) };

        Assert.True(Names(Problems(With(Shipped, static h => h with { SoftCapAbove = 320 })), "softCapAbove below clamp"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Clamp = 400 })), "softCapAbove below clamp"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Terraces = h.Terraces with { FromHeight = 200, ToHeight = 100 } })), "height.terraces"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Crags = h.Crags with { HeightFrom = 300, HeightTo = 200 } })), "height.crags"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Rivers = h.Rivers with { Depth = 0 } })), "height.rivers"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Rivers = h.Rivers with { SeaFadeFrom = 0, SeaFadeTo = -5 } })), "seaFadeFrom"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Lakes = h.Lakes with { Depth = 40 } })), "height.lakes"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Lakes = h.Lakes with { InlandFrom = 500, InlandTo = 100 } })), "inlandFrom"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Detail = h.Detail with { HillsOctaves = 0 } })), "height.detail"));
        Assert.True(Names(Problems(Shipped with { Altitude = new AltitudeData(-2000, -300) }), "snowBelowTemperature"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { CoastData = new CoastSpec(200, 100, 0, 100, 500) })), "height.coast.peaksFrom/peaksTo"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { CoastData = new CoastSpec(0, 100, 0, 100, 500, 60, 40) })), "height.coast.flatFrom"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { Rivers = h.Rivers with { Valley = new ValleySpec(10, 2, 150) } })), "height.rivers.valley"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { WaterData = new WaterSpec(0, 2) })), "height.water"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { WaterData = new WaterSpec(3, 40) })), "height.water"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { PeakVariation = new PeakVariationSpec(0, 2, 500) })), "height.peakVariation"));
        Assert.True(Names(Problems(Shipped with { Climate = Shipped.Climate with { Continents = Shipped.Climate.Continents! with { ChannelMin = 0 } } }), "climate.continents"));
        Assert.True(Names(Problems(Shipped with { Climate = Shipped.Climate with { Continents = Shipped.Climate.Continents! with { SeaPerBlock = 4 } } }), "climate.continents"));
        Assert.True(Names(Problems(Shipped with { Climate = Shipped.Climate with { Ridges = Shipped.Climate.Ridges with { Round = 600 } } }), "ridges.round"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { CoastData = new CoastSpec(0, 100, 100, 100, 500) })), "height.coast.hillsFrom/hillsTo"));
        Assert.True(Names(Problems(With(Shipped, static h => h with { CoastData = new CoastSpec(0, 100, 0, 100, 1500) })), "height.coast.hillsFloor"));
    }

    [Fact]
    public void Given_AFileWithNoCoastSection_When_ItIsParsed_Then_ThePeaksAndHillsAreTheSameEverywhere()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "world.json"));
        var cut = json.IndexOf(",\n    \"coast\"", StringComparison.Ordinal);
        Assert.True(cut > 0);
        var end = json.IndexOf('}', cut) + 1;

        var world = WorldGenData.Parse(json[..cut] + json[end..]);

        Assert.Null(world.Height.CoastData);
        Assert.Equal(CoastSpec.None, world.Height.Coast);
        Assert.Empty(Problems(world));
    }

    [Fact]
    public void Given_TheLoadedGameData_When_ItIsRead_Then_TheWorldAndBiomesAreLoadedOnce()
    {
        var data = TestGame.Data;

        Assert.Same(data.WorldGen, data.WorldGen);
        Assert.NotEmpty(data.Biomes.Rows);
        Assert.Equal(data.WorldGen, data.WithBiomes(data.Biomes).WorldGen);
        Assert.Equal(data.Biomes, data.WithWorldGen(data.WorldGen).Biomes);
        Assert.DoesNotContain(GameDataValidator.Validate(data), static p => p.Contains("world.json", StringComparison.Ordinal));
    }
}
