using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>Each term of the temperature alone, the sum, and a term added from a test (I06).</summary>
public sealed class TemperatureTermTests
{
    private static AtmosphereSnapshot Channels(double daylight, WeatherChannels channels) => new(12, DayPhase.Day, daylight, channels, "test", null);

    private static readonly WeatherChannels Calm = WeatherChannels.Dry(0, 1, 0, 0);

    [Fact]
    public void TheBaseIsItsNumberWhateverTheSky()
    {
        var term = new BaseTemperatureTerm(140);

        Assert.Equal(140, term.TenthsAt(TemperatureRig.Open(Channels(1, Calm))));
        Assert.Equal(140, term.TenthsAt(TemperatureRig.Open(Channels(0, new WeatherChannels(1, 0, 1, 1, "x", 1)))));
    }

    [Theory]
    [InlineData(1.0, 60)]
    [InlineData(0.5, 0)]
    [InlineData(0.0, -60)]
    [InlineData(0.75, 30)]
    [InlineData(0.25, -30)]
    public void TheDaySwingsBetweenPlusAndMinusTheAmplitudeWithTheDaylight(double daylight, int expected) =>
        Assert.Equal(expected, new DayTemperatureTerm(60).TenthsAt(TemperatureRig.Open(Channels(daylight, Calm))));

    [Fact]
    public void TheWeatherCoolsByEachChannelTimesItsCoefficientAndReadsNoKindId()
    {
        var term = new WeatherTemperatureTerm(cloudTenths: 20, precipitationTenths: 60, windTenths: 50);

        Assert.Equal(0, term.TenthsAt(TemperatureRig.Open(Channels(1, Calm))));
        Assert.Equal(-20, term.TenthsAt(TemperatureRig.Open(Channels(1, WeatherChannels.Dry(1, 1, 0, 0)))));
        Assert.Equal(-50, term.TenthsAt(TemperatureRig.Open(Channels(1, WeatherChannels.Dry(0, 1, 0, 1)))));
        Assert.Equal(-60, term.TenthsAt(TemperatureRig.Open(Channels(1, new WeatherChannels(0, 1, 0, 0, "anything", 1)))));
        Assert.Equal(-(10 + 30 + 25), term.TenthsAt(TemperatureRig.Open(Channels(1, new WeatherChannels(0.5, 1, 0, 0.5, "anything", 0.5)))));
        Assert.Equal(0, term.TenthsAt(TemperatureRig.Open(Channels(1, WeatherChannels.Dry(0, 1, 1, 0)))));
    }

    [Fact]
    public void TheWeatherTermIsTheSameForTwoKindsWithTheSameChannels()
    {
        var term = new WeatherTemperatureTerm(20, 60, 50);
        var one = new AtmosphereSnapshot(12, DayPhase.Day, 1, new WeatherChannels(0.4, 1, 0, 0.3, "rain", 0.2), "first", null);
        var other = one with { KindId = "second" };

        Assert.Equal(term.TenthsAt(TemperatureRig.Open(one)), term.TenthsAt(TemperatureRig.Open(other)));
    }

    [Fact]
    public void AShelterWarmsInProportionToTheCoverAndOnlyInPrecipitationWhenTheDataSays()
    {
        var onlyWet = new ShelterTemperatureTerm(50, onlyInPrecipitation: true);
        var always = new ShelterTemperatureTerm(50, onlyInPrecipitation: false);
        var dry = Channels(1, Calm);
        var wet = Channels(1, new WeatherChannels(1, 0.5, 0, 0, "x", 0.8));

        Assert.Equal(0, onlyWet.TenthsAt(TemperatureRig.Covered(dry)));
        Assert.Equal(50, onlyWet.TenthsAt(TemperatureRig.Covered(wet)));
        Assert.Equal(0, onlyWet.TenthsAt(TemperatureRig.Open(wet)));
        Assert.Equal(50, always.TenthsAt(TemperatureRig.Covered(dry)));
        Assert.Equal(0, always.TenthsAt(TemperatureRig.Open(dry)));
        Assert.Equal(25, always.TenthsAt(TemperatureRig.Open(dry) with { CoveredShare = 5.0 / 10 }));
        Assert.Equal(16, always.TenthsAt(TemperatureRig.Open(dry) with { CoveredShare = 3.0 / 9 }));
    }

    [Fact]
    public void AFireWarmsWithinItsRadiusOfALitRingOnly()
    {
        var term = new FireTemperatureTerm(radiusBlocks: 5, bonusTenths: 150);
        var sky = Channels(1, Calm);
        var lit = TemperatureRig.Ring(lit: true);
        var cold = TemperatureRig.Ring(lit: false);

        Assert.Equal(150, term.TenthsAt(TemperatureRig.Beside(sky, lit, 2)));
        Assert.Equal(150, term.TenthsAt(TemperatureRig.Beside(sky, lit, 5)));
        Assert.Equal(0, term.TenthsAt(TemperatureRig.Beside(sky, lit, 6)));
        Assert.Equal(0, term.TenthsAt(TemperatureRig.Beside(sky, cold, 2)));
        Assert.Equal(0, term.TenthsAt(TemperatureRig.Open(sky))); // a world with no ring at all
    }

    [Fact]
    public void ThePlaceIsTheSumOfItsTermsAndTheBreakdownNamesEach()
    {
        var place = new PlaceTemperature([new BaseTemperatureTerm(140), new DayTemperatureTerm(60), new WeatherTemperatureTerm(20, 60, 50)]);
        var situation = TemperatureRig.Open(Channels(0.5, WeatherChannels.Dry(1, 1, 0, 0)));

        Assert.Equal(140 + 0 - 20, place.TenthsAt(situation));
        Assert.Equal([("base", 140), ("day", 0), ("weather", -20)], place.Breakdown(situation).Select(static c => (c.Term, c.Tenths)).ToArray());
    }

    [Fact]
    public void NoTermsIsZeroAndAnAbsurdSumStaysInsideTheLimit()
    {
        var situation = TemperatureRig.Open(Channels(1, Calm));

        Assert.Equal(0, new PlaceTemperature([]).TenthsAt(situation));
        Assert.Equal(PlaceTemperature.LimitTenths, new PlaceTemperature([new FixedTerm(int.MaxValue), new FixedTerm(int.MaxValue)]).TenthsAt(situation));
        Assert.Equal(-PlaceTemperature.LimitTenths, new PlaceTemperature([new FixedTerm(int.MinValue), new FixedTerm(int.MinValue)]).TenthsAt(situation));
    }

    [Fact]
    public void ANewTermIsOneClassAndOneRegistrationWithNoEditToThePlaceOrTheOtherTerms()
    {
        var sky = Channels(1, Calm);
        var situation = TemperatureRig.Open(sky);
        var standard = TemperatureRig.Place;

        var withMine = standard.With(new AltitudeLapseTerm());

        Assert.Equal(standard.TenthsAt(situation) - (10 * 5), withMine.TenthsAt(situation));
        Assert.Equal(standard.Terms.Count + 1, withMine.Terms.Count);
        Assert.Equal("lapse", withMine.Terms[^1].Name);
        Assert.Equal(standard.TenthsAt(situation), TemperatureRig.Place.TenthsAt(situation)); // the original is untouched
    }

    [Fact]
    public void ARegisteredTermAlsoSumsWhenBuiltFromTheStandardListPlusOne()
    {
        var place = new PlaceTemperature([.. TemperatureTerms.Standard(TemperatureRig.Data), new AltitudeLapseTerm()]);
        var situation = TemperatureRig.Open(TemperatureRig.Sky(13, "sunny"));

        Assert.Equal(TemperatureRig.Place.TenthsAt(situation) - 50, place.TenthsAt(situation));
    }

    // A term a later series would write: the feet's height lowers the temperature (here a degree for each block above y 5).
    private sealed class AltitudeLapseTerm : ITemperatureTerm
    {
        public string Name => "lapse";

        public int TenthsAt(in TemperatureSituation situation) => -(situation.Feet.Y - 5) * 10;
    }

    private sealed class FixedTerm(int tenths) : ITemperatureTerm
    {
        public string Name => "fixed";

        public int TenthsAt(in TemperatureSituation situation) => tenths;
    }
}
