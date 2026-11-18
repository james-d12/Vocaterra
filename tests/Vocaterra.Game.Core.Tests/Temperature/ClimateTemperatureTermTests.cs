using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// The world's climate in what the player feels (I06b): the term from the climate temperature at the column and height, the valley and the village as they were, the peaks cool at noon and
/// cold to very cold at night, the warmest clothes enough, determinism and the order of the terms. The seed-42 places are those of the owner's world; the kinds are named here, as the data names them.
/// </summary>
public sealed class ClimateTemperatureTermTests
{
    private const int Noon = 13;
    private const int Midnight = 0;
    private const int WarmestClothes = 100; // +10 degrees (inventory plan 3.13a)

    // The climate temperature is in fixed point: 4 096 is 1 000 thousandths.
    private static readonly ClimateCooling Numbers = new(ReferenceThousandths: -100, CoolingTenthsPerRange: 200, MaxCoolingTenths: 150);

    private static readonly Lazy<Scene> Seed42 = new(static () => WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded));

    private static TemperatureSituation At(IPlaceInfo? places, int x = 0, int y = 40, int z = 0, string kind = "sunny", int hour = Noon) =>
        new(TemperatureRig.Sky(hour, kind), new BlockPos(x, y, z), 0, null, places);

    private static int Term(ClimateCooling numbers, IPlaceInfo? places, int y = 40) => new ClimateTemperatureTerm(numbers).TenthsAt(At(places, y: y));

    private static int Place(int x, int y, int z, string kind, int hour) => TemperatureRig.Place.TenthsAt(At(Seed42.Value.Places, x, y, z, kind, hour));

    private static string Band(int place, int warmth) => TemperatureRig.Data.BandFor(place + warmth).Id;

    [Fact]
    public void Given_NoWorldInTheSituation_When_TheTermIsAsked_Then_ItAddsNothing() => Assert.Equal(0, Term(Numbers, null));

    [Theory]
    [InlineData(0)]
    [InlineData(-410)] // exactly the reference (-100 thousandths)
    [InlineData(3000)]
    public void Given_AClimateAtOrAboveTheReference_When_TheTermIsAsked_Then_ItNeverWarmsOrCools(int climate) =>
        Assert.Equal(0, Term(Numbers, new StubPlaces((_, _, _) => climate)));

    [Theory]
    [InlineData(-410 - 2048, -100)] // half the range below the reference: half of 200
    [InlineData(-410 - 4096, -150)] // a whole range would be 200: the floor is 150
    [InlineData(-410 - 1024, -50)]
    [InlineData(-410 - 40_960, -150)] // far below: the floor
    public void Given_AClimateBelowTheReference_When_TheTermIsAsked_Then_ItCoolsInProportionDownToTheFloor(int climate, int expected) =>
        Assert.Equal(expected, Term(Numbers, new StubPlaces((_, _, _) => climate)));

    [Fact]
    public void Given_ClimateThatFallsWithHeight_When_ThePlayerStandsHigher_Then_TheTermIsColder()
    {
        var places = new StubPlaces((_, y, _) => -(y * 40)); // the lapse, as a stub: 40 units a block

        var low = Term(Numbers, places, y: 40);
        var high = Term(Numbers, places, y: 200);

        Assert.True(high < low, $"{high} < {low}");
    }

    [Fact]
    public void Given_TheColumnAndHeightAreAsked_When_TheTermReadsTheClimate_Then_ItReadsTheFeetsOwnBlock()
    {
        (int, int, int)? asked = null;
        var places = new StubPlaces((x, y, z) =>
        {
            asked = (x, y, z);
            return 0;
        });

        _ = new ClimateTemperatureTerm(Numbers).TenthsAt(At(places, 7, 88, -3));

        Assert.Equal((7, 88, -3), asked);
    }

    [Fact]
    public void Given_TheShippedNumbersAndSeed42_When_ThePlayerStandsAtTheCampOrTheVillage_Then_TheClimateTermIsWithinHalfADegree()
    {
        var places = Seed42.Value.Places!;
        var term = new ClimateTemperatureTerm(TemperatureRig.Data.Climate);

        foreach (var (x, z) in new[] { (176, 186), (292, 218) })
        {
            var y = places.NaturalSurfaceAt(x, z);
            Assert.InRange(term.TenthsAt(At(places, x, y, z)), -5, 0);
        }
    }

    [Fact]
    public void Given_TheValleysFloor_When_TheFeelIsTakenWithAndWithoutTheTerm_Then_ItIsTheSameAtNoonAndAtNight()
    {
        var withoutTerm = new PlaceTemperature(TemperatureTerms.Standard(TemperatureRig.Data).Where(static t => t.Name != "climate"));

        foreach (var (x, z) in new[] { (176, 186), (292, 218), (200, 200) })
        {
            var y = Seed42.Value.Places!.NaturalSurfaceAt(x, z);
            foreach (var hour in new[] { Noon, Midnight })
            {
                var situation = At(Seed42.Value.Places, x, y, z, "sunny", hour);
                Assert.InRange(TemperatureRig.Place.TenthsAt(situation) - withoutTerm.TenthsAt(situation), -5, 0);
            }
        }
    }

    [Fact]
    public void Given_ASnowyPeakOnSeed42_When_ItIsAClearNoon_Then_ItIsCoolAndAClearNightIsColdToVeryCold()
    {
        const int x = -632, y = 296, z = -1144; // the highest ground of seed 42 (jagged peaks)

        var noon = Place(x, y, z, "sunny", Noon);
        var night = Place(x, y, z, "sunny", Midnight);

        Assert.Equal("cool", Band(noon, 0));
        Assert.Equal("very-cold", Band(night, 0));
        Assert.Equal("cool", Band(Place(x, y, z, "sunny", Noon), 0));
    }

    [Fact]
    public void Given_TheWarmestClothes_When_AClearNightOnTheHighestPeakIsFelt_Then_ItIsNoWorseThanCold()
    {
        var night = Place(-632, 296, -1144, "sunny", Midnight);

        Assert.NotEqual("very-cold", Band(night, WarmestClothes));
        Assert.True(night + WarmestClothes >= TemperatureRig.Data.Bands.Single(static b => b.Id == "cold").FromTenths);
    }

    [Fact]
    public void Given_AMidSlopeOnSeed42_When_ItIsFelt_Then_ItIsBetweenTheValleyAndThePeak()
    {
        var valley = Place(176, 42, 186, "sunny", Midnight);
        var slope = Place(-1784, 120, 2952, "sunny", Midnight);
        var peak = Place(-632, 296, -1144, "sunny", Midnight);

        Assert.InRange(slope, peak + 1, valley - 1);
    }

    [Fact]
    public void Given_TheSameSeedAndPlace_When_TheTermIsAskedTwiceAndInAnotherWorld_Then_TheAnswerIsTheSame()
    {
        var other = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);
        var term = new ClimateTemperatureTerm(TemperatureRig.Data.Climate);
        var situation = At(Seed42.Value.Places, -632, 296, -1144);

        Assert.Equal(term.TenthsAt(situation), term.TenthsAt(situation));
        Assert.Equal(term.TenthsAt(situation), term.TenthsAt(situation with { Places = other.Places }));
        Assert.True(term.TenthsAt(situation) < -100);
    }

    [Fact]
    public void Given_TheTermsInAnyOrder_When_TheClimateTermIsAmongThem_Then_TheSumIsTheSame()
    {
        var terms = TemperatureTerms.Standard(TemperatureRig.Data);
        Assert.Contains(terms, static t => t.Name == "climate");
        var situation = At(Seed42.Value.Places, -632, 296, -1144, "rain", Midnight);

        var forward = new PlaceTemperature(terms).TenthsAt(situation);
        var backward = new PlaceTemperature(terms.Reverse()).TenthsAt(situation);

        Assert.Equal(forward, backward);
        Assert.Equal(forward, new PlaceTemperature([.. terms.Skip(3), .. terms.Take(3)]).TenthsAt(situation));
    }

    [Fact]
    public void Given_TheSessionAtItsStart_When_ItsTemperatureIsRead_Then_TheReadingCarriesTheClimateTermAndItIsZeroInTheValley()
    {
        var session = TestGame.NewSession();
        session.Step([]);

        var reading = session.Temperature.Current!.Value;

        Assert.InRange(reading.ClimateTenths, -5, 0);
    }
}
