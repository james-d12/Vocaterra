using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>The sky's look by weather (E05): the first kind's look is the day cycle's own, a heavier sky is darker and greyer, the look is continuous in the channels, and the sun closes with the cover.</summary>
public sealed class SkyWeatherLookTests
{
    private static readonly DayClock Clock = new(DayData.Default);

    private static IReadOnlyList<WeatherKind> Kinds => TestGame.Data.Weather.Kinds;

    private static SkyLook Look(double hour, WeatherChannels channels)
    {
        var tick = (long)Math.Round(hour / 24.0 * Clock.CycleTicks) - (long)Math.Round(8 / 24.0 * Clock.CycleTicks);
        var input = SkyLookInput.At(new AtmosphereSnapshot(Clock.HourAt(tick), Clock.PhaseAt(tick), Clock.DaylightAt(tick), channels, "x", null));
        return SkyLook.At(input);
    }

    private static double Luminance((byte R, byte G, byte B) c) => (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);

    private static double Saturation((byte R, byte G, byte B) c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));

    private static int Distance((byte R, byte G, byte B) a, (byte R, byte G, byte B) b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    [Fact]
    public void Given_TheFirstKindsChannels_When_TheLookIsRead_Then_EveryValueIsTheDayCyclesOwnAtEveryHour()
    {
        var first = Kinds[0].Channels;
        Assert.Equal(0.42, first.CloudCover);
        Assert.Equal(1.0, first.Sunlight);
        Assert.Equal(0.0, first.Fog);

        for (var hour = 0.0; hour < 24.0; hour += 0.05)
        {
            var tick = (long)Math.Round(hour / 24.0 * Clock.CycleTicks) - (long)Math.Round(8 / 24.0 * Clock.CycleTicks);
            Assert.Equal(SkyLook.At(SkyLookInput.At(Clock, tick)), Look(hour, first));
        }
    }

    [Fact]
    public void Given_NoWeatherInTheInput_When_TheLookIsRead_Then_ItIsTheFairWeatherLook()
    {
        Assert.Equal(SkyLook.At(new SkyLookInput(13, 1, DayPhase.Day)), SkyLook.At(new SkyLookInput(13, 1, DayPhase.Day, 0.42, 1, 0)));
        Assert.Equal(0f, SkyLook.Noon.Fog);
        Assert.Equal(0.42, SkyLook.Noon.CloudCover);
        Assert.Equal((1f, 1f, 1f), SkyLook.Noon.SunLight);
    }

    [Theory]
    [InlineData(13.0)]
    [InlineData(21.0)]
    [InlineData(0.0)]
    public void Given_AHeavierSky_When_TheLookIsRead_Then_ItIsDarkerAndLessSaturatedThanTheFirstKinds(double hour)
    {
        var fair = Look(hour, Kinds[0].Channels);
        var previousLuminance = Luminance(fair.DomeZenith) + Luminance(fair.DomeHorizon);
        var previousSaturation = Saturation(fair.DomeZenith) + Saturation(fair.DomeHorizon);
        foreach (var kind in Kinds.Skip(1).OrderBy(static k => SkyWeather.Gloom(k.Channels.CloudCover, k.Channels.Sunlight)))
        {
            var heavy = Look(hour, kind.Channels);

            var luminance = Luminance(heavy.DomeZenith) + Luminance(heavy.DomeHorizon);
            var saturation = Saturation(heavy.DomeZenith) + Saturation(heavy.DomeHorizon);
            Assert.True(luminance < previousLuminance, $"{kind.Id} at {hour}: darker ({luminance} < {previousLuminance})");
            Assert.True(saturation < previousSaturation, $"{kind.Id} at {hour}: greyer ({saturation} < {previousSaturation})");
            Assert.True(Luminance(heavy.CloudBottom) < Luminance(fair.CloudBottom), $"{kind.Id} at {hour}: the clouds' undersides darken");
            Assert.True(heavy.SunLight.G < fair.SunLight.G, $"{kind.Id} at {hour}: the world's light dims");
            previousLuminance = luminance;
            previousSaturation = saturation;
        }
    }

    [Fact]
    public void Given_AnOvercastNight_When_TheLookIsRead_Then_TheBlueGlowIsGoneAndTheNightIsDarker()
    {
        var clear = Look(0, Kinds[0].Channels);
        var heavy = Look(0, new WeatherChannels(1, 0.5, 0.5, 0.6, "x", 0.8));

        Assert.True(clear.DomeHorizon.B - clear.DomeHorizon.R > 40, "a clear night glows blue");
        Assert.True(heavy.DomeHorizon.B - heavy.DomeHorizon.R < 12, "an overcast night is a grey");
        Assert.True(Luminance(heavy.DomeHorizon) < Luminance(clear.DomeHorizon));
        Assert.Equal(0f, heavy.Stars);
        Assert.Equal(0f, heavy.MoonStrength);
    }

    [Fact]
    public void Given_AFullCoverAtNight_When_TheLookIsRead_Then_TheCloudTopIsAtMostOneAndAHalfTimesTheDomesMiddle()
    {
        foreach (var sunlight in new[] { 0.0, 0.5, 1.0 })
        {
            var look = SkyLook.At(new SkyLookInput(0, 0, DayPhase.Night, 1, sunlight, 0));

            Assert.True(Luminance(look.CloudTop) <= 1.5 * Luminance(look.DomeMiddle), $"sunlight {sunlight}: the cloud top {Luminance(look.CloudTop)} against the dome {Luminance(look.DomeMiddle)}");
        }
    }

    [Fact]
    public void Given_AFullCoverAtDusk_When_TheLookIsRead_Then_TheAmberIsMostlyGone()
    {
        var clear = Look(SkyLook.DuskHour, Kinds[0].Channels);
        var heavy = Look(SkyLook.DuskHour, new WeatherChannels(1, 0.5, 0.5, 0.6, "x", 0.8));

        Assert.True(Saturation(heavy.DomeHorizon) < Saturation(clear.DomeHorizon) * 0.25, $"{Saturation(heavy.DomeHorizon)} against {Saturation(clear.DomeHorizon)}");
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(13.0)]
    [InlineData(16.0)]
    public void Given_AFullCover_When_TheLookIsRead_Then_TheSunsStrengthIsZeroAndAFairSkyShowsIt(double hour)
    {
        Assert.True(Look(hour, Kinds[0].Channels).SunStrength > 0.9f);
        Assert.Equal(0f, Look(hour, new WeatherChannels(1, 1, 0, 0, string.Empty, 0)).SunStrength);
        Assert.Equal(0f, Look(hour, Kinds.Last().Channels).SunStrength);
    }

    [Fact]
    public void Given_TheCoverRising_When_TheSunIsRead_Then_ItFadesWithoutRising()
    {
        var last = float.MaxValue;
        for (var cover = 0.0; cover <= 1.0; cover += 0.01)
        {
            var strength = Look(13, new WeatherChannels(cover, 1, 0, 0, string.Empty, 0)).SunStrength;
            Assert.True(strength <= last + 1e-6f);
            last = strength;
        }
    }

    [Fact]
    public void Given_AHighSunlightOrMore_When_TheLookIsRead_Then_NothingIsBrighterThanFairWeather()
    {
        var fair = Look(13, Kinds[0].Channels);
        var clearer = Look(13, new WeatherChannels(0.1, 1, 0, 0, string.Empty, 0));

        Assert.Equal(fair.DomeZenith, clearer.DomeZenith);
        Assert.Equal(fair.CloudTop, clearer.CloudTop);
    }

    [Theory]
    [InlineData(13.0)]
    [InlineData(19.6)]
    [InlineData(21.0)]
    [InlineData(22.4)]
    [InlineData(0.0)]
    [InlineData(5.5)]
    public void Given_ABlendFromEachKindToEachOther_When_TheLookIsReadAlong_Then_NoValueJumps(double hour)
    {
        const int Steps = 450; // a 45 second blend read about ten times a second
        foreach (var from in Kinds)
        {
            foreach (var to in Kinds)
            {
                var previous = Look(hour, from.Channels);
                for (var i = 1; i <= Steps; i++)
                {
                    var look = Look(hour, WeatherChannels.Blend(from.Channels, to.Channels, i / (double)Steps));
                    var where = $"{from.Id} to {to.Id} at {hour}, step {i}";
                    Assert.True(Distance(look.DomeHorizon, previous.DomeHorizon) <= 1, where + ": horizon");
                    Assert.True(Distance(look.DomeMiddle, previous.DomeMiddle) <= 1, where + ": middle");
                    Assert.True(Distance(look.DomeZenith, previous.DomeZenith) <= 1, where + ": zenith");
                    Assert.True(Distance(look.CloudTop, previous.CloudTop) <= 1, where + ": cloud top");
                    Assert.True(Distance(look.CloudBottom, previous.CloudBottom) <= 1, where + ": cloud bottom");
                    Assert.True(Math.Abs(look.SunStrength - previous.SunStrength) <= 0.02f, where + ": sun");
                    Assert.True(Math.Abs(look.MoonStrength - previous.MoonStrength) <= 0.02f, where + ": moon");
                    Assert.True(Math.Abs(look.Stars - previous.Stars) <= 0.02f, where + ": stars");
                    Assert.True(Math.Abs(look.Fog - previous.Fog) <= 0.01f, where + ": fog");
                    Assert.True(Math.Abs(look.SunLight.R - previous.SunLight.R) <= 0.005f && Math.Abs(look.SunLight.B - previous.SunLight.B) <= 0.005f, where + ": light");
                    Assert.Equal(previous.Band, look.Band);
                    previous = look;
                }
            }
        }
    }

    [Fact]
    public void Given_TheLowestSunlightOfTheData_When_TheTintIsComputed_Then_ItStaysInsideTheRangeTheBandEdgesWereChosenFor()
    {
        var lowest = Kinds.Min(static k => k.Channels.Sunlight);
        double clearLow = double.MaxValue, clearHigh = double.MinValue, heavyLow = double.MaxValue, heavyHigh = double.MinValue;
        for (var hour = 0.0; hour < 24.0; hour += 0.02)
        {
            var clear = Look(hour, Kinds[0].Channels);
            var heavy = Look(hour, new WeatherChannels(1, lowest, 0, 0, string.Empty, 0));
            Assert.Equal(clear.Band, heavy.Band); // the band follows the daylight alone
            foreach (var value in new[] { LightBands.Tint(clear.SunLight, clear.Band) })
            {
                clearLow = Math.Min(clearLow, Math.Min(value.R, Math.Min(value.G, value.B)));
                clearHigh = Math.Max(clearHigh, Math.Max(value.R, Math.Max(value.G, value.B)));
            }

            var tint = LightBands.Tint(heavy.SunLight, heavy.Band);
            heavyLow = Math.Min(heavyLow, Math.Min(tint.R, Math.Min(tint.G, tint.B)));
            heavyHigh = Math.Max(heavyHigh, Math.Max(tint.R, Math.Max(tint.G, tint.B)));
        }

        // Measured: a clear day's tint runs 0.507 to 1.480 over the whole cycle (E02's range, the band edges' own), and at the data's lowest sunlight (0.5) 0.253 to 0.740. The weather
        // only multiplies the light by the channel, so the tint never goes above E02's top and falls to the channel's share of its bottom: a darker vertex colour, never one past white.
        Assert.InRange(clearLow, 0.5, 0.52);
        Assert.InRange(clearHigh, 1.47, 1.49);
        Assert.InRange(heavyLow, 0.24, 0.27);
        Assert.InRange(heavyHigh, 0.7, 0.75);
        Assert.True(heavyHigh <= clearHigh, "weather never lifts the tint past the clear sky's top");
    }
}
