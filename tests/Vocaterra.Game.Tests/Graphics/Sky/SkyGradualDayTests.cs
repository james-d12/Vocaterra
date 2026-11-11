using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Sky;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>The gradual day (E11): the look follows the sun's height through the day and its depth through the night, and moves without a step through every hour, in fair and in the heaviest weather.</summary>
public sealed class SkyGradualDayTests
{
    private static readonly DayClock Clock = new(DayData.Default);

    // The smallest differences between 8:00 or 18:00 and the noon that a viewer sees (out of 255 in a dome colour, and in the sun's light as a multiplier).
    private const int VisibleDomeDifference = 20;
    private const float VisibleLightDifference = 0.08f;

    // The most a colour channel of the look (out of 255) may move from one minute of the dial to the next (100 ticks, 1.7 seconds of play). A dusk is 120 minutes and a colour crosses up to
    // about 215 steps of 255 to the twilight in half of it, at the smoothstep's steepest 1.5 times the straight pace: 5.4 a minute at the most, and a step would show as ten or more.
    private const int MinuteBound = 6;

    // The most the sun's light (as a multiplier) or the lanterns' strength may move in a minute: the twilight's light is 0.56 from the night's, at the same pace, 0.014 a minute.
    private const float MinuteLightBound = 0.02f;

    private static long TickAt(double hour) => (long)Math.Round(hour / 24.0 * Clock.CycleTicks) - (long)Math.Round(8 / 24.0 * Clock.CycleTicks);

    private static SkyLook Look(double hour) => SkyLook.At(SkyLookInput.At(Clock, TickAt(hour)));

    private static double Luminance((byte R, byte G, byte B) c) => (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);

    private static double Luminance((float R, float G, float B) c) => (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);

    private static int Distance((byte R, byte G, byte B) a, (byte R, byte G, byte B) b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    private static float LightDistance((float R, float G, float B) a, (float R, float G, float B) b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    private static double WarmthOf((float R, float G, float B) c) => c.R / c.B;

    private static double WarmthOf((byte R, byte G, byte B) c) => c.R / (double)c.B;

    [Fact]
    public void Given_TheHoursFromSixToNoon_When_TheBrightnessIsRead_Then_ItRisesAndFallsAgainToTheEvening()
    {
        for (var hour = 6.0; hour < 13.0; hour += 0.25)
        {
            var now = Look(hour);
            var next = Look(hour + 0.25);
            Assert.True(Luminance(next.SunLight) >= Luminance(now.SunLight) - 1e-6, $"the light rises at {hour}");
            Assert.True(Luminance(next.DomeMiddle) >= Luminance(now.DomeMiddle) - 0.5, $"the dome rises at {hour}");
        }

        for (var hour = 13.0; hour < 20.0; hour += 0.25)
        {
            var now = Look(hour);
            var next = Look(hour + 0.25);
            Assert.True(Luminance(next.SunLight) <= Luminance(now.SunLight) + 1e-6, $"the light falls at {hour}");
            Assert.True(Luminance(next.DomeMiddle) <= Luminance(now.DomeMiddle) + 0.5, $"the dome falls at {hour}");
        }

        Assert.True(Luminance(Look(13).SunLight) > Luminance(Look(7).SunLight) + 0.03);
        Assert.True(Luminance(Look(13).DomeMiddle) > Luminance(Look(6).DomeMiddle) + 2);
        Assert.True(Luminance(Look(13).SunLight) > Luminance(Look(19).SunLight) + 0.03);
        Assert.True(Luminance(Look(13).DomeMiddle) > Luminance(Look(20).DomeMiddle) + 2);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(18.0)]
    public void Given_TheMorningOrTheEvening_When_TheLookIsComparedWithNoon_Then_ItDiffersVisiblyAndIsWarmer(double hour)
    {
        var look = Look(hour);
        var noon = Look(13);

        Assert.True(Distance(look.DomeHorizon, noon.DomeHorizon) >= VisibleDomeDifference, "the horizon differs");
        Assert.True(Distance(look.DomeMiddle, noon.DomeMiddle) >= VisibleDomeDifference / 2, "the dome differs");
        Assert.True(LightDistance(look.SunLight, noon.SunLight) >= VisibleLightDifference, "the light differs");
        Assert.True(WarmthOf(look.SunLight) > WarmthOf(noon.SunLight) + 0.1, "the light is warmer (red over blue)");
        Assert.True(WarmthOf(look.DomeHorizon) > WarmthOf(noon.DomeHorizon) + 0.2, "the horizon is warmer");
        Assert.True(look.SunLight.R <= 1f && look.SunLight.G <= 1f && look.SunLight.B <= 1f, "never above white");
    }

    [Fact]
    public void Given_TheMiddleOfTheDay_When_TheLookIsRead_Then_ItIsTheNoonsOwnFromTenToQuarterPastFour()
    {
        foreach (var hour in new[] { 10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0 })
        {
            var look = Look(hour);
            Assert.Equal(SkyPalette.Horizon, look.DomeHorizon);
            Assert.Equal((1f, 1f, 1f), look.SunLight);
        }

        Assert.Equal(SkyLook.Noon, SkyLook.At(new SkyLookInput(13, 1, DayPhase.Day)));
    }

    [Fact]
    public void Given_TheEarlyMorning_When_TheLookIsRead_Then_ItIsTheGoldenKeyframe()
    {
        var look = Look(6);

        Assert.True(Distance(look.DomeHorizon, SkyKeyframe.Golden.DomeHorizon) <= 1);
        Assert.True(LightDistance(look.SunLight, SkyKeyframe.Golden.SunLight) < 0.01f);
    }

    [Fact]
    public void Given_TheNightHours_When_TheLookIsRead_Then_MidnightIsDarkerThanTheEdgesAndTheDeepestIsRoundOne()
    {
        var midnight = Look(0);
        foreach (var edge in new[] { Look(22), Look(3.5) })
        {
            Assert.True(Luminance(midnight.SunLight) < Luminance(edge.SunLight) * 0.85, "the world's light");
            Assert.True(Luminance(midnight.DomeHorizon) < Luminance(edge.DomeHorizon) * 0.85, "the horizon");
            Assert.True(Luminance(midnight.DomeZenith) < Luminance(edge.DomeZenith) * 0.85, "the zenith");
            Assert.Equal(1f, edge.LanternStrength);
        }

        var darkest = Enumerable.Range(0, 49).Select(static i => 22.0 + (i * 0.25)).Select(static h => (Hour: h, Light: Luminance(Look(h % 24).SunLight))).MinBy(static x => x.Light);
        Assert.InRange(darkest.Hour, 24.5, 25.5);
        Assert.Equal(1f, Look(1).LanternStrength);
    }

    [Fact]
    public void Given_ThePhaseHours_When_TheBlendIsRead_Then_TheWeightsAddUpToOneAndSitWhereTheSunIs()
    {
        for (var hour = 0.0; hour < 24.0; hour += 0.05)
        {
            var input = SkyLookInput.At(Clock, TickAt(hour));
            var sunY = SkyArc.SunAt(input.Hour).Y;
            var blend = SkyBlend.At(input.Phase, input.Daylight, (float)(Math.Asin(sunY) * 180 / Math.PI));
            Assert.Equal(1f, blend.Noon + blend.Golden + blend.Twilight + blend.Night + blend.Deep, 4);
            Assert.True(blend.Noon >= 0 && blend.Golden >= 0 && blend.Twilight >= 0 && blend.Night >= 0 && blend.Deep >= 0, $"hour {hour}");
        }

        var noon = SkyBlend.At(DayPhase.Day, 1, 56);
        Assert.Equal(new SkyBlend(1f, 0f, 0f, 0f, 0f), noon);
        Assert.Equal(new SkyBlend(0f, 1f, 0f, 0f, 0f), SkyBlend.At(DayPhase.Day, 1, SkyBlend.GoldenBelow));
        Assert.Equal(new SkyBlend(0f, 0f, 1f, 0f, 0f), SkyBlend.At(DayPhase.Dusk, 0.5, 0));
        Assert.Equal(new SkyBlend(0f, 0f, 0f, 0f, 1f), SkyBlend.At(DayPhase.Night, 0, -SkyBlend.DeepUntil));
        Assert.Equal(new SkyBlend(0f, 0f, 0f, 1f, 0f), SkyBlend.At(DayPhase.Night, 0, -SkyBlend.DeepFrom));
    }

    [Fact]
    public void Given_EveryMinuteOfTheDial_When_TheLookIsReadInFairWeather_Then_NoColourStepsFromOneMinuteToTheNext()
    {
        AssertContinuous(null);
    }

    [Fact]
    public void Given_EveryMinuteOfTheDial_When_TheLookIsReadInTheHeaviestWeather_Then_NoColourStepsFromOneMinuteToTheNext()
    {
        var heaviest = TestGame.Data.Weather.Kinds.OrderBy(static k => SkyWeather.Gloom(k.Channels.CloudCover, k.Channels.Sunlight)).Last().Channels;

        AssertContinuous(heaviest);
    }

    private static void AssertContinuous(WeatherChannels? channels)
    {
        SkyLook LookAt(int minute)
        {
            var tick = TickAt(minute / 60.0);
            var phase = Clock.PhaseAt(tick);
            var daylight = Clock.DaylightAt(tick);
            return channels is { } c
                ? SkyLook.At(SkyLookInput.At(new AtmosphereSnapshot(Clock.HourAt(tick), phase, daylight, c, "x", null)))
                : SkyLook.At(SkyLookInput.At(Clock, tick));
        }

        var last = LookAt(-1);
        for (var minute = 0; minute <= 1440 + 1; minute++)
        {
            var now = LookAt(minute);
            var at = $"minute {minute} (hour {minute / 60.0:0.000})";
            Assert.True(Distance(last.DomeHorizon, now.DomeHorizon) <= MinuteBound, "horizon stepped at " + at);
            Assert.True(Distance(last.DomeMiddle, now.DomeMiddle) <= MinuteBound, "middle stepped at " + at);
            Assert.True(Distance(last.DomeZenith, now.DomeZenith) <= MinuteBound, "zenith stepped at " + at);
            Assert.True(Distance(last.CloudTop, now.CloudTop) <= MinuteBound, "cloud top stepped at " + at);
            Assert.True(Distance(last.CloudSide, now.CloudSide) <= MinuteBound, "cloud side stepped at " + at);
            Assert.True(Distance(last.CloudBottom, now.CloudBottom) <= MinuteBound, "cloud bottom stepped at " + at);
            Assert.True(Distance(last.SunColour, now.SunColour) <= MinuteBound, "sun colour stepped at " + at);
            Assert.True(Distance(last.FogColour, now.FogColour) <= MinuteBound, "fog stepped at " + at);
            Assert.True(LightDistance(last.SunLight, now.SunLight) <= MinuteLightBound, "the light stepped at " + at);
            Assert.True(Math.Abs(last.LanternStrength - now.LanternStrength) <= MinuteLightBound, "the lanterns stepped at " + at);
            last = now;
        }
    }
}
