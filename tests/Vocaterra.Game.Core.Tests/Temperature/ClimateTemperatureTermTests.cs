using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>
/// The climate baseline (I06c, replacing I06b's cooling-only term and the constant base): the generator's climate temperature at the column and height through a curve, so the valley keeps its
/// 14 degrees, high and cold places are cold and warm places warm; the valley and the village as they were, the peaks cool at noon and cold to very cold at night, the warmest clothes
/// enough, determinism and the order of the terms. The seed-42 places are those of the owner's world; the kinds are named here, as the data names them.
/// </summary>
public sealed class ClimateTemperatureTermTests
{
    private const int Noon = 13;
    private const int Midnight = 0;
    private const int WarmestClothes = 100; // +10 degrees (inventory plan 3.13a)

    // The climate temperature is in fixed point: 4 096 is 1 000 thousandths. A curve of the term's own: flat 100 tenths from 0 to 1000, then up to 200 tenths at 3000.
    private static readonly ClimateCurve Curve = new([(-1000, -100), (0, 100), (1000, 100), (3000, 300)]);

    private static readonly Lazy<Scene> Seed42 = new(static () => WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded));

    private static TemperatureContext At(IPlaceInfo? places, int x = 0, int y = 40, int z = 0, string kind = "sunny", int hour = Noon) =>
        TemperatureRig.At(TemperatureRig.Sky(hour, kind), places, x, y, z);

    private static int Term(IPlaceInfo? places, int y = 40, TemperatureEnvironment? environment = null) =>
        new ClimateTemperatureTerm(Curve, 140).TenthsAt(TemperatureRig.At(TemperatureRig.Sky(Noon, "sunny"), places, 0, y, 0, environment ?? TemperatureEnvironment.Plain));

    private static int Place(int x, int y, int z, string kind, int hour) => TemperatureRig.Place.TenthsAt(At(Seed42.Value.Places, x, y, z, kind, hour));

    private static string Band(int place, int warmth) => TemperatureRig.Data.BandFor(place + warmth).Id;

    [Fact]
    public void Given_NoWorldInTheContext_When_TheTermIsAsked_Then_ItGivesTheNoWorldBaseline() => Assert.Equal(140, Term(null));

    [Theory]
    [InlineData(-4096 * 2, -100)] // far below the curve's first point: flat
    [InlineData(0, 100)]
    [InlineData(4096, 100)] // 1 000 thousandths, still on the flat stretch
    [InlineData(4096 * 2, 200)] // halfway up the rising line
    [InlineData(4096 * 9, 300)] // beyond the last point: flat
    public void Given_AClimateTemperature_When_TheTermIsAsked_Then_ItIsThePointsStraightLineAndFlatBeyond(int climate, int expected) =>
        Assert.Equal(expected, Term(new StubPlaces((_, _, _) => climate)));

    [Fact]
    public void Given_ClimateThatFallsWithHeight_When_ThePlayerStandsHigher_Then_TheTermIsColder()
    {
        var places = new StubPlaces((_, y, _) => -(y * 40), surface: (_, _) => 0); // the lapse, as a stub: 40 units a block, over a ground at sea level

        var low = Term(places, y: 40);
        var high = Term(places, y: 300);

        Assert.True(high < low, $"{high} < {low}");
    }

    [Fact]
    public void Given_TheColumnAndHeightAreAsked_When_TheTermReadsTheClimate_Then_ItReadsTheFeetsOwnColumnAtTheFeetsHeightAboveTheGround()
    {
        (int, int, int)? asked = null;
        var places = new StubPlaces((x, y, z) =>
        {
            asked = (x, y, z);
            return 0;
        });

        _ = new ClimateTemperatureTerm(Curve, 0).TenthsAt(TemperatureRig.At(TemperatureRig.Sky(Noon, "sunny"), places, 7, 48, -3));

        Assert.Equal((7, 48, -3), asked);
    }

    [Fact]
    public void Given_TheShippedNumbersAndSeed42_When_ThePlayerStandsAtTheCampOrTheVillage_Then_TheBaselineIsTheValleysFourteenDegrees()
    {
        var places = Seed42.Value.Places!;
        var term = TemperatureTermRegistry.Standard.Create(TemperatureRig.Data.Terms.Single(static t => t.Id == "climate"));

        foreach (var (x, z) in new[] { (176, 186), (292, 218), (200, 200) })
        {
            var y = places.NaturalSurfaceAt(x, z);
            Assert.InRange(term.TenthsAt(TemperatureRig.At(TemperatureRig.Sky(Noon, "sunny"), places, x, y, z)), 135, 145);
        }
    }

    [Fact]
    public void Given_TheValleysFloor_When_TheFeelIsTakenNowAndAsTheOldModelHadIt_Then_ItIsWithinHalfADegreeAtNoonAndAtNight()
    {
        // The old model: a base of 14 degrees, the day's swing and the weather's cooling, and a climate term that was 0 to -0.5 in the valley.
        var old = new PlaceTemperature([new FixedTerm(140), new DayTemperatureTerm(60), new WeatherTemperatureTerm(20, 60, 50)]);

        foreach (var (x, z) in new[] { (176, 186), (292, 218), (200, 200) })
        {
            var y = Seed42.Value.Places!.NaturalSurfaceAt(x, z);
            foreach (var (kind, hour) in new[] { ("sunny", Noon), ("sunny", Midnight), ("rain", Midnight) })
            {
                var sky = TemperatureRig.Sky(hour, kind);
                var now = TemperatureRig.Place.TenthsAt(At(Seed42.Value.Places, x, y + 1, z, kind, hour));
                Assert.InRange(now - old.TenthsAt(TemperatureRig.Open(sky)), -5, 5);
            }
        }
    }

    [Fact]
    public void Given_ASnowyPeakOnSeed42_When_ItIsAClearNoon_Then_ItIsCoolAndAClearNightIsVeryCold()
    {
        const int x = -632, y = 296, z = -1144; // the highest ground of seed 42 (jagged peaks)

        var noon = Place(x, y, z, "sunny", Noon);
        var night = Place(x, y, z, "sunny", Midnight);

        Assert.Equal("cool", Band(noon, 0));
        Assert.Equal("very-cold", Band(night, 0));
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
        var valley = Place(176, 43, 186, "sunny", Midnight);
        var slope = Place(-1784, 121, 2952, "sunny", Midnight);
        var peak = Place(-632, 296, -1144, "sunny", Midnight);

        Assert.InRange(slope, peak + 1, valley - 1);
    }

    [Fact]
    public void Given_TheSameSeedAndPlace_When_TheTermIsAskedTwiceAndInAnotherWorld_Then_TheAnswerIsTheSame()
    {
        var other = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);
        var term = TemperatureTermRegistry.Standard.Create(TemperatureRig.Data.Terms.Single(static t => t.Id == "climate"));
        var here = At(Seed42.Value.Places, -632, 296, -1144);
        var there = At(other.Places, -632, 296, -1144);

        Assert.Equal(term.TenthsAt(here), term.TenthsAt(here));
        Assert.Equal(term.TenthsAt(here), term.TenthsAt(there));
        Assert.True(term.TenthsAt(here) < 50, "the peak's baseline is far below the valley's 14 degrees");
    }

    [Fact]
    public void Given_TheTermsInAnyOrder_When_TheClimateTermIsAmongThem_Then_TheSumIsTheSame()
    {
        var terms = TemperatureTermRegistry.Standard.Build(TemperatureRig.Data.Terms);
        Assert.Contains(terms, static t => t.Name == "climate");
        var situation = At(Seed42.Value.Places, -632, 296, -1144, "rain", Midnight);

        var forward = new PlaceTemperature(terms, TemperatureRig.Data.Environment).TenthsAt(situation);
        var backward = new PlaceTemperature(terms.Reverse(), TemperatureRig.Data.Environment).TenthsAt(situation);

        Assert.Equal(forward, backward);
        Assert.Equal(forward, new PlaceTemperature([.. terms.Skip(3), .. terms.Take(3)], TemperatureRig.Data.Environment).TenthsAt(situation));
    }

    [Fact]
    public void Given_TheSessionAtItsStart_When_ItsTemperatureIsRead_Then_TheReadingCarriesTheClimateBaselineAndItIsTheValleysInTheValley()
    {
        var session = TestGame.NewSession();
        session.Step([]);

        var reading = session.Temperature.Current!.Value;

        Assert.InRange(reading.TermTenths("climate"), 135, 145);
    }

    private sealed class FixedTerm(int tenths) : ITemperatureTerm
    {
        public string Name => "base";

        public int TenthsAt(ITemperatureContext context) => tenths;
    }
}
