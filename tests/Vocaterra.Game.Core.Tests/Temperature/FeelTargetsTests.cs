using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// The owner's feel targets after the QA passes of 2026-11-19 (I10b, QA-2026-11-19-progression-4: colder nights, hotter warm biomes), on seed 42 in the streamed world: the felt value is the place's plus the warmth of the
/// outfit (the starting clothes give +4, the coat in place of the shirt +8, the warmest clothes +10). The kinds are named here, as the data names them. They replace the tests of I06b and I06c that held the valley as it was.
/// </summary>
public sealed class FeelTargetsTests
{
    private const int Noon = 13;
    private const int Night = 1;
    private const int StartingClothes = 40;
    private const int CoatForShirt = 80;

    private static readonly Lazy<Scene> Seed42 = new(static () => WorldFactory.Create(TestGame.Data, 42, WorldMode.Streamed));

    private static readonly (int X, int Z)[] Valley = [(177, 185), (292, 222)]; // the camp and the village

    private static int Felt(int x, int z, string kind, int hour, int warmth, int? y = null)
    {
        var places = Seed42.Value.Places!;
        var at = y ?? places.NaturalSurfaceAt(x, z) + 1;
        return TemperatureRig.Place.TenthsAt(TemperatureRig.At(TemperatureRig.Sky(hour, kind), places, x, at, z)) + warmth;
    }

    private static string Band(int felt) => TemperatureRig.Data.BandFor(felt).Id;

    [Fact]
    public void Given_ARainyNightInTheValley_When_ThePlayerWearsTheStartingClothes_Then_ItFeelsColdBelowThreeDegrees()
    {
        foreach (var (x, z) in Valley)
        {
            var felt = Felt(x, z, "rain", Night, StartingClothes);

            Assert.True(felt < 30, $"({x},{z}) feels {felt / 10.0}");
            Assert.Equal("cold", Band(felt));
        }

        Assert.InRange(Felt(177, 185, "rain", Night, StartingClothes), 0, 20); // about one degree at the camp
    }

    [Fact]
    public void Given_ARainyNightInTheValley_When_TheCoatTakesTheShirtsPlace_Then_ItFeelsCool()
    {
        foreach (var (x, z) in Valley)
        {
            Assert.Equal("cool", Band(Felt(x, z, "rain", Night, CoatForShirt)));
        }
    }

    [Fact]
    public void Given_AClearNightInTheValley_When_ThePlayerWearsTheStartingClothes_Then_ItIsCoolNotCold()
    {
        foreach (var (x, z) in Valley)
        {
            Assert.Equal("cool", Band(Felt(x, z, "sunny", Night, StartingClothes)));
        }
    }

    [Fact]
    public void Given_AClearNoonInTheValley_When_ThePlayerWearsTheStartingClothes_Then_ItIsWarmNotHot()
    {
        foreach (var (x, z) in Valley)
        {
            Assert.Equal("warm", Band(Felt(x, z, "sunny", Noon, StartingClothes)));
        }
    }

    [Theory]
    [InlineData("beach", 4826, -676)]
    [InlineData("plains", 540, 240)]
    public void Given_AWarmBiomeAtAClearNoon_When_TheCoatIsWorn_Then_ItIsHotAndInTheStartingClothesWarm(string biome, int x, int z)
    {
        Assert.Equal(biome, Seed42.Value.Places!.BiomeAt(x, z).Id);
        Assert.True(Felt(x, z, "sunny", Noon, CoatForShirt) >= 280, biome);
        Assert.Equal("hot", Band(Felt(x, z, "sunny", Noon, CoatForShirt)));
        Assert.Equal("warm", Band(Felt(x, z, "sunny", Noon, StartingClothes)));
    }

    [Fact]
    public void Given_TheHighestPeak_When_ItIsAClearNight_Then_ItIsVeryCold()
    {
        Assert.Equal("very-cold", Band(Felt(-632, -1144, "sunny", Night, 0, y: 296)));
    }
}
